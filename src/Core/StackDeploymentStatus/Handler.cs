using System;
using System.Threading.Tasks;

using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.SNSEvents;
using Amazon.SQS.Model;

using Cythral.CloudFormation.Aws;
using Cythral.CloudFormation.StackDeploymentStatus.Request;

using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.StackDeploymentStatus
{
    public class Handler
    {
        private static StackDeploymentStatusRequestFactory requestFactory = new StackDeploymentStatusRequestFactory();
        private static StepFunctionsClientFactory stepFunctionsClientFactory = new StepFunctionsClientFactory();
        private static S3GetObjectFacade s3GetObjectFacade = new S3GetObjectFacade();
        private static SqsFactory sqsFactory = new SqsFactory();

        public static async Task<Response> Handle(
            SNSEvent snsRequest,
            ILambdaContext context = null
        )
        {
            Console.WriteLine($"Received request: {Serialize(snsRequest)}");

            var client = stepFunctionsClientFactory.Create();
            var request = requestFactory.CreateFromSnsEvent(snsRequest);
            var status = request.ResourceStatus;

            if (request.ResourceType == "AWS::CloudFormation::Stack" && request.ClientRequestToken.Length > 0)
            {
                if (status.EndsWith("ROLLBACK_COMPLETE") || status.EndsWith("FAILED"))
                {
                    await SendFailure(request, client);
                }

                if (status.EndsWith("COMPLETE"))
                {
                    await SendSuccess(request, client);
                }

            }

            return new Response { Success = true };
        }

        private static string TranslateTokenToS3Location(string clientRequestToken)
        {
            var index = clientRequestToken.LastIndexOf("-");
            var bucket = clientRequestToken[0..index];
            var key = clientRequestToken[(index + 1)..];

            return $"s3://{bucket}/tokens/{key}";
        }

        private static async Task<TokenInfo> GetTokenInfoFromRequest(StackDeploymentStatusRequest request)
        {
            var location = TranslateTokenToS3Location(request.ClientRequestToken);
            var sourceString = await s3GetObjectFacade.GetObject(location);
            return Deserialize<TokenInfo>(sourceString);
        }

        private static async Task SendFailure(StackDeploymentStatusRequest request, IAmazonStepFunctions client)
        {
            var tokenInfo = await GetTokenInfoFromRequest(request);
            var response = await client.SendTaskFailureAsync(new SendTaskFailureRequest
            {
                TaskToken = tokenInfo.ClientRequestToken,
                Cause = request.ResourceStatus
            });

            Console.WriteLine($"Received send task failure response: {Serialize(response)}");

            await Dequeue(tokenInfo);
        }

        private static async Task SendSuccess(StackDeploymentStatusRequest request, IAmazonStepFunctions client)
        {
            var tokenInfo = await GetTokenInfoFromRequest(request);
            var response = await client.SendTaskSuccessAsync(new SendTaskSuccessRequest
            {
                TaskToken = tokenInfo.ClientRequestToken,
                Output = Serialize(request)
            });

            Console.WriteLine($"Received send task failure response: {Serialize(response)}");

            await Dequeue(tokenInfo);
        }

        private static async Task Dequeue(TokenInfo tokenInfo)
        {
            var client = await sqsFactory.Create();
            var response = await client.DeleteMessageAsync(new DeleteMessageRequest
            {
                QueueUrl = tokenInfo.QueueUrl,
                ReceiptHandle = tokenInfo.ReceiptHandle,
            });

            Console.WriteLine($"Got delete message response: {Serialize(response)}");
        }
    }
}