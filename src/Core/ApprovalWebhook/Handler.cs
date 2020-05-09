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

        public static async Task<ApplicationLoadBalancerResponse> Handle(ApplicationLoadBalancerRequest request, ILambdaContext context = null)
        {
            var client = stepFunctionsClientFactory.Create();
            var action = request.QueryStringParameters["action"];
            var token = request.QueryStringParameters["token"];
            var response = await client.SendTaskSuccessAsync(new SendTaskSuccessRequest
            {
                TaskToken = WebUtility.UrlDecode(token),
                Output = Serialize(new
                {
                    Action = action,
                })
            });

            Console.WriteLine($"Send task success response: {Serialize(response)}");

            var body = action == "approve" ? "approved" : "rejected";
            return CreateResponse(OK, body: body);
        }

        private static ApplicationLoadBalancerResponse CreateResponse(HttpStatusCode statusCode, string contentType = "text/plain", string body = "")
        {
            string CreateStatusString()
            {
                var result = "";

                foreach (var character in statusCode.ToString())
                {
                    if (Char.ToLower(character) == character)
                    {
                        result += character;
                    }
                    else
                    {
                        result += $" {character}";
                    }
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