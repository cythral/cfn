using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Amazon.CloudFormation.Model;
using Amazon.Lambda.ApplicationLoadBalancerEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.S3;
using Amazon.S3.Model;

using Cythral.CloudFormation.AwsUtils.CloudFormation;
using Cythral.CloudFormation.GithubWebhook.Exceptions;
using Cythral.CloudFormation.GithubWebhook.Github;
using Cythral.CloudFormation.GithubWebhook.Pipelines;

using Lambdajection.Attributes;
using Lambdajection.Core;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using static System.Net.HttpStatusCode;
using static System.Text.Json.JsonSerializer;

[assembly: LambdaSerializer(typeof(CamelCaseLambdaJsonSerializer))]

namespace Cythral.CloudFormation.GithubWebhook
{

    [Lambda(Startup = typeof(Startup))]
    public partial class Handler
    {

        private readonly RequestValidator requestValidator;
        private readonly PipelineStarter pipelineStarter;
        private readonly PipelineDeployer pipelineDeployer;
        private readonly Config config;
        private readonly ILogger<Handler> logger;

        public Handler(
            RequestValidator requestValidator,
            PipelineStarter pipelineStarter,
            PipelineDeployer pipelineDeployer,
            IOptions<Config> config,
            ILogger<Handler> logger
        )
        {
            this.requestValidator = requestValidator;
            this.pipelineStarter = pipelineStarter;
            this.pipelineDeployer = pipelineDeployer;
            this.config = config.Value;
            this.logger = logger;
        }

        /// <summary>
        /// This function is called on every request to /webhooks/github
        /// </summary>
        /// <param name="request">Request sent by the application load balancer</param>
        /// <param name="context">The lambda context</param>
        /// <returns>A load balancer response object</returns>

        public async Task<ApplicationLoadBalancerResponse> Handle(ApplicationLoadBalancerRequest request, ILambdaContext context = null)
        {
            PushEvent payload = null;

            try
            {
                payload = requestValidator.Validate(request);
            }
            catch (RequestValidationException e)
            {
                logger.LogError(e.Message);
                return CreateResponse(statusCode: e.StatusCode);
            }

            var tasks = new List<Task>();

            if (payload.OnDefaultBranch && !payload.HeadCommit.Message.Contains("[skip meta-ci]"))
            {
                var task = pipelineDeployer.Deploy(payload);
                tasks.Add(task);
            }

            if (!payload.HeadCommit.Message.Contains("[skip ci]"))
            {
                var task = pipelineStarter.StartPipelineIfExists(payload);
                tasks.Add(task);
            }

            await Task.WhenAll(tasks);

            return CreateResponse(statusCode: OK);
        }

        private static ApplicationLoadBalancerResponse CreateResponse(HttpStatusCode statusCode, string contentType = "text/plain", string body = "")
        {
            string CreateStatusString()
            {
                var result = "";
                var previous = ' ';

                foreach (var character in statusCode.ToString())
                {
                    var previousWasUppercase = Char.ToLower(previous) != previous;
                    var currentIsUppercase = Char.ToLower(character) != character;

                    result += (currentIsUppercase && !previousWasUppercase) ? $" {character}" : $"{character}";
                    previous = character;
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
