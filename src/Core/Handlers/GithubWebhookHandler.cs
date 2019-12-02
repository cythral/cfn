using System;
using System.Threading.Tasks;
using System.Net;
using System.Text.Json;
using System.Collections.Generic;
using System.Runtime.Serialization;

using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization;
using Amazon.Lambda.Serialization.Json;
using Amazon.Lambda.ApplicationLoadBalancerEvents;

using Cythral.CloudFormation.Events;
using Cythral.CloudFormation.Exceptions;
using Cythral.CloudFormation.Entities;
using Cythral.CloudFormation.Facades;

using static System.Net.HttpStatusCode;
using static System.Text.Json.JsonSerializer;

using WebhookConfig = Cythral.CloudFormation.Config;

namespace Cythral.CloudFormation {
    public class GithubWebhookHandler {
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
                ("TEMPLATE_FILENAME",       false),
                ("STACK_SUFFIX",            false),
                ("ROLE_ARN",                false),
            });

            try {
                payload = RequestValidator.Validate(request, expectedOwner: Config["GITHUB_OWNER"], signingKey: Config["GITHUB_SIGNING_SECRET"]);
            } catch(RequestValidationException e) {
                Console.WriteLine(e.Message);
                return CreateResponse(statusCode: e.StatusCode);
            }

            var stackName = $"{payload.Repository.Name}-{Config["STACK_SUFFIX"]}";
            var contentsUrl = payload.Repository.ContentsUrl;
            var templateContent = await CommittedFile.FromContentsUrl(contentsUrl, Config["TEMPLATE_FILENAME"], Config);
            var roleArn = Config["ROLE_ARN"];

            if(templateContent == null) {
                Console.WriteLine($"Couldn't find template for {payload.Repository.Name}");
                return CreateResponse(statusCode: NotFound);
            }
            
            var parameters = new List<Parameter> {
                new Parameter { ParameterKey = "GithubToken", ParameterValue = Config["GITHUB_TOKEN"] },
                new Parameter { ParameterKey = "GithubOwner", ParameterValue = payload.Repository.Owner.Name },
                new Parameter { ParameterKey = "GithubRepo", ParameterValue = payload.Repository.Name },
                new Parameter { ParameterKey = "GithubBranch", ParameterValue = payload.Repository.DefaultBranch }
            };

            try {
                await StackDeployer.Deploy(stackName, templateContent, roleArn, parameters);
                return CreateResponse(statusCode: OK);
            } catch(Exception e) {
                Console.WriteLine($"Failed to create/update stack: {e.Message}");
            }

            return CreateResponse(statusCode: BadRequest);
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
