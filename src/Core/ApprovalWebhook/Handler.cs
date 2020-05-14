using System.Net;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using static System.Text.Json.JsonSerializer;
using static System.Net.HttpStatusCode;

using Cythral.CloudFormation.Aws;

using Amazon.Lambda.Core;
using Amazon.StepFunctions.Model;
using Amazon.Lambda.ApplicationLoadBalancerEvents;

namespace Cythral.CloudFormation.ApprovalWebhook
{
    public class Handler
    {
        private static StepFunctionsClientFactory stepFunctionsClientFactory = new StepFunctionsClientFactory();
        private static S3Factory s3Factory = new S3Factory();
        private static S3GetObjectFacade s3GetObjectFacade = new S3GetObjectFacade();

        public static async Task<ApplicationLoadBalancerResponse> Handle(ApplicationLoadBalancerRequest request, ILambdaContext context = null)
        {
            string body = null;

            using (var stepFunctionsClient = stepFunctionsClientFactory.Create())
            using (var s3Client = await s3Factory.Create())
            {
                var action = request.QueryStringParameters["action"];
                var pipeline = request.QueryStringParameters["pipeline"];
                var tokenHash = request.QueryStringParameters["token"];
                var key = $"{pipeline}/approvals/{tokenHash}";
                var bucket = Environment.GetEnvironmentVariable("STATE_STORE");
                var approvalInfo = await s3GetObjectFacade.GetObject<ApprovalInfo>(bucket, key);

                var sendTaskResponse = await stepFunctionsClient.SendTaskSuccessAsync(new SendTaskSuccessRequest
                {
                    TaskToken = approvalInfo.Token,
                    Output = Serialize(new
                    {
                        Action = action,
                    })
                });

                Console.WriteLine($"Send task success response: {Serialize(sendTaskResponse)}");

                var deleteResponse = await s3Client.DeleteObjectAsync(bucket, key);
                Console.WriteLine($"Received delete response: {Serialize(deleteResponse)}");

                body = action == "approve" ? "approved" : "rejected";
            }

            return CreateResponse(OK, body: body);
        }

        private static ApplicationLoadBalancerResponse CreateResponse(HttpStatusCode statusCode, string contentType = "text/plain", string body = "")
        {
            string CreateStatusString()
            {
                var result = "";

                foreach (var character in statusCode.ToString())
                {
                    result += (Char.ToLower(character) == character) ? $"{character}" : $" {character}";
                }

                return result;
            }

            return new ApplicationLoadBalancerResponse
            {
                StatusCode = (int)statusCode,
                StatusDescription = $"{(int)statusCode}{CreateStatusString()}",
                Headers = new Dictionary<string, string> { ["content-type"] = contentType },
                Body = body,
                IsBase64Encoded = false,
            };
        }
    }
}