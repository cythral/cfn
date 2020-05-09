using System;
using System.Threading.Tasks;
using static System.Text.Json.JsonSerializer;

using Cythral.CloudFormation.Aws;

using Amazon.Lambda.Core;
using Amazon.StepFunctions.Model;
using Amazon.Lambda.ApplicationLoadBalancerEvents;

namespace Cythral.CloudFormation.ApprovalWebhook
{
    public class Handler
    {
        private static StepFunctionsClientFactory stepFunctionsClientFactory = new StepFunctionsClientFactory();

        public static async Task<ApplicationLoadBalancerResponse> Handle(ApplicationLoadBalancerRequest request, ILambdaContext context = null)
        {
            var client = stepFunctionsClientFactory.Create();
            var response = await client.SendTaskSuccessAsync(new SendTaskSuccessRequest
            {
                TaskToken = request.QueryStringParameters["token"],
                Output = Serialize(new
                {
                    Action = request.QueryStringParameters["action"]
                })
            });

            Console.WriteLine($"Send task success response: {Serialize(response)}");

            return new ApplicationLoadBalancerResponse
            {
                Body = Serialize(new
                {
                    Status = "OK"
                })
            };
        }
    }
}