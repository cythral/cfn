using System.Net;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using static System.Text.Json.JsonSerializer;
using static System.Net.HttpStatusCode;

using Cythral.CloudFormation.Aws;

using Amazon.Lambda.Core;
using Amazon.SimpleNotificationService.Model;
using Amazon.Lambda.ApplicationLoadBalancerEvents;

namespace Cythral.CloudFormation.ApprovalNotification
{
    public class Handler
    {
        private SnsFactory snsFactory = new SnsFactory();

        public async Task<Response> Handle(Request request, ILambdaContext context = null)
        {
            Console.WriteLine($"Recieved request: {Serialize(request)}");
            var client = await snsFactory.Create();
            var baseUrl = Environment.GetEnvironmentVariable("BASE_URL");
            var encodedToken = WebUtility.UrlEncode(request.Token);
            var approveUrl = $"{baseUrl}?action=approve&token={encodedToken}";
            var rejectUrl = $"{baseUrl}?action=reject&token={encodedToken}";
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

            return await Task.FromResult((Response)null);
        }
    }
}