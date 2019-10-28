using System;
using System.Threading.Tasks;
using System.Net;
using System.Text.Json;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization;
using Amazon.Lambda.Serialization.Json;
using Amazon.Lambda.ApplicationLoadBalancerEvents;
using Cythral.CloudFormation.Cicd.Events;
using Cythral.CloudFormation.Cicd.Exceptions;

using static System.Net.HttpStatusCode;
using static System.Text.Json.JsonSerializer;

using WebhookConfig = Cythral.CloudFormation.Cicd.Config;

[assembly:LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace Cythral.CloudFormation.Cicd {
    public class Webhook {
        public static WebhookConfig Config { get; set; }

        /// <summary>
        /// This function is called on every request to /webhooks/github
        /// </summary>
        /// <param name="request">Request sent by the application load balancer</param>
        /// <param name="context">The lambda context</param>
        /// <returns>A load balancer response object</returns>
        public static async Task<ApplicationLoadBalancerResponse> Handle(ApplicationLoadBalancerRequest request, ILambdaContext context = null) {
            Console.WriteLine($"Got request: {Serialize(request)}");
            PushEvent payload = null;

            // create the config variable if it hasn't been created already (may have been cached from previous request)
            Config = Config ?? await WebhookConfig.Create(new List<(string,bool)> {
                // envvar name              encrypted? 
                ("GITHUB_OWNER",            false),
                ("GITHUB_TOKEN",            true),
                ("GITHUB_SIGNING_SECRET",   true),
            });

            try {
                payload = RequestValidator.Validate(request, expectedOwner: Config["GITHUB_OWNER"], signingKey: Config["GITHUB_SIGNING_SECRET"]);
            } catch(RequestValidationException e) {
                Console.WriteLine(e.Message);
                return CreateResponse(statusCode: e.StatusCode);
            }

            

            return CreateResponse(statusCode: OK);
        }

        private static ApplicationLoadBalancerResponse CreateResponse(HttpStatusCode statusCode, string contentType = "text/plain", string body = "") {
            string CreateStatusString() {
                var result = "";

                foreach(var character in statusCode.ToString()) {
                    if(Char.ToLower(character) == character) {
                        result += character;
                    } else {
                        result += $" {character}";
                    }
                }

                return result;
            }

            return new ApplicationLoadBalancerResponse {
                StatusCode = (int) statusCode,
                StatusDescription = $"{(int) statusCode}{CreateStatusString()}",
                Headers = new Dictionary<string,string> { ["content-type"] = contentType },
                Body = body,
                IsBase64Encoded = false,
            };
        }
    }
}
