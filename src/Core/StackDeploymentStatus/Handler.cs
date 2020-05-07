using System;
using System.Threading.Tasks;

using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.SNSEvents;

using Cythral.CloudFormation.StackDeploymentStatus.Request;

using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.StackDeploymentStatus
{
    public class Handler
    {
        private static StackDeploymentStatusRequestFactory requestFactory = new StackDeploymentStatusRequestFactory();
        private static StepFunctionsClientFactory stepFunctionsClientFactory = new StepFunctionsClientFactory();

        public static async Task<Response> Handle(
            SNSEvent snsRequest,
            ILambdaContext context = null
        ) {
            Console.WriteLine($"Received request: {Serialize(snsRequest)}");

            var client = stepFunctionsClientFactory.Create();
            var request = requestFactory.CreateFromSnsEvent(snsRequest);
            var status = request.ResourceStatus;

            if (request.ClientRequestToken.Length > 0) {
                var task = (status.Contains("ROLLBACK") || status.EndsWith("FAILED")) ? SendTaskFailure(request, client) : SendTaskSuccess(request, client);
                await task;
            }

            return new Response { Success = true };
        }

        private static async Task SendTaskFailure(StackDeploymentStatusRequest request, IAmazonStepFunctions client)
        {
            var response = await client.SendTaskFailureAsync(new SendTaskFailureRequest
            {
                TaskToken = request.ClientRequestToken,
                Cause = request.ResourceStatus
            });

            Console.WriteLine($"Received send task failure response: {Serialize(response)}");
        }

        private static async Task SendTaskSuccess(StackDeploymentStatusRequest request, IAmazonStepFunctions client)
        {
            var response = await client.SendTaskSuccessAsync(new SendTaskSuccessRequest
            {
                TaskToken = request.ClientRequestToken,
                Output = Serialize(request)
            });

            Console.WriteLine($"Received send task failure response: {Serialize(response)}");
        }
    }
}