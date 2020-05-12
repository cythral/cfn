using System.Text;
using System.Security.Cryptography;
using System.Net;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using static System.Text.Json.JsonSerializer;
using static System.Net.HttpStatusCode;

using Cythral.CloudFormation.Aws;

using Amazon.Lambda.Core;
using Amazon.SimpleNotificationService.Model;
using Amazon.StepFunctions.Model;
using Amazon.Lambda.ApplicationLoadBalancerEvents;
using Amazon.S3.Model;

namespace Cythral.CloudFormation.ApprovalNotification
{
    public class Handler
    {
        private SnsFactory snsFactory = new SnsFactory();
        private S3Factory s3Factory = new S3Factory();
        private S3GetObjectFacade s3GetObjectFacade = new S3GetObjectFacade();
        private StepFunctionsClientFactory stepFunctionsClientFactory = new StepFunctionsClientFactory();

        public async Task<Response> Handle(Request request, ILambdaContext context = null)
        {
            Console.WriteLine($"Recieved request: {Serialize(request)}");

            await CancelPreviousApprovals(request);

            var approvalHash = await CreateApprovalObject(request);
            var client = await snsFactory.Create();
            var baseUrl = Environment.GetEnvironmentVariable("BASE_URL");
            var pipeline = request.Pipeline;
            var approveUrl = $"{baseUrl}?action=approve&pipeline={pipeline}&token={approvalHash}";
            var rejectUrl = $"{baseUrl}?action=reject&pipeline={pipeline}&token={approvalHash}";
            var defaultMessage = $"{request.CustomMessage}.\n\nApprove:\n{approveUrl}\n\nReject:\n{rejectUrl}";


            var response = await client.PublishAsync(new PublishRequest
            {
                TopicArn = Environment.GetEnvironmentVariable("TOPIC_ARN"),
                MessageStructure = "json",
                Message = Serialize(new Dictionary<string, string>
                {
                    ["default"] = defaultMessage,
                    ["email"] = defaultMessage,
                    ["email - json "] = Serialize(new
                    {
                        Pipeline = request.Pipeline,
                        Message = request.CustomMessage,
                        ApprovalUrl = approveUrl,
                        RejectionUrl = rejectUrl
                    })
                })
            });

            return new Response
            {
                Success = true
            };
        }

        private async Task<string> CreateApprovalObject(Request request)
        {
            using (var sha256 = SHA256.Create())
            using (var client = await s3Factory.Create())
            {
                var tokenBytes = Encoding.UTF8.GetBytes(request.Token);
                var hashBytes = sha256.ComputeHash(tokenBytes);
                var hash = string.Join("", hashBytes.Select(byt => $"{byt:X2}"));
                var bucket = Environment.GetEnvironmentVariable("STATE_STORE");
                var pipeline = request.Pipeline;

                var response = await client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = bucket,
                    Key = $"{pipeline}/approvals/{hash}",
                    ContentBody = Serialize(new ApprovalInfo
                    {
                        Token = request.Token
                    })
                });

                Console.WriteLine($"Put object response: {Serialize(response)}");
                return hash;
            }
        }

        private async Task CancelPreviousApprovals(Request request)
        {
            using (var client = await s3Factory.Create())
            {
                var bucket = Environment.GetEnvironmentVariable("STATE_STORE");
                var pipeline = request.Pipeline;
                var approvals = await client.ListObjectsV2Async(new ListObjectsV2Request
                {
                    BucketName = bucket,
                    Prefix = $"{pipeline}/approvals"
                });

                var tasks = approvals.S3Objects.Select(location => CancelPreviousApproval(request, location));
                Task.WaitAll(tasks.ToArray());
            }
        }

        private async Task CancelPreviousApproval(Request request, S3Object location)
        {
            using (var stepFunctionsClient = stepFunctionsClientFactory.Create())
            using (var s3Client = await s3Factory.Create())
            {
                var approvalInfo = await s3GetObjectFacade.GetObject<ApprovalInfo>(location.BucketName, location.Key);

                try
                {
                    var sendTaskCancelResponse = await stepFunctionsClient.SendTaskSuccessAsync(new SendTaskSuccessRequest
                    {
                        TaskToken = approvalInfo.Token,
                        Output = Serialize(new
                        {
                            Action = "reject"
                        })
                    });

                    Console.WriteLine($"Cancellation response: {Serialize(sendTaskCancelResponse)}");
                }
                catch (TaskTimedOutException) { }
                catch (TaskDoesNotExistException) { }


                var deleteResponse = await s3Client.DeleteObjectAsync(location.BucketName, location.Key);
                Console.WriteLine($"Delete approval object response: {Serialize(deleteResponse)}");
            }
        }
    }
}