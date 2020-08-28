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

using Lambdajection.Attributes;
using Lambdajection.Core;

using Cythral.CloudFormation.AwsUtils.CloudFormation;
using Cythral.CloudFormation.GithubWebhook.Entities;
using Cythral.CloudFormation.GithubWebhook.Exceptions;

using Microsoft.Extensions.Options;

using static System.Net.HttpStatusCode;
using static System.Text.Json.JsonSerializer;

using WebhookConfig = Cythral.CloudFormation.GithubWebhook.Config;

namespace Cythral.CloudFormation.GithubWebhook
{

    [Lambda(Startup = typeof(Startup))]
    public partial class Handler
    {

        private RequestValidator requestValidator = new RequestValidator();
        private DeployStackFacade stackDeployer = new DeployStackFacade();
        private PipelineStarter pipelineStarter = new PipelineStarter();
        private IAwsFactory<IAmazonS3> s3Factory;
        private Config config;

        public Handler(IAwsFactory<IAmazonS3> s3Factory, RequestValidator requestValidator, DeployStackFacade stackDeployer, PipelineStarter pipelineStarter, IOptions<Config> config)
        {
            this.s3Factory = s3Factory;
            this.requestValidator = requestValidator;
            this.stackDeployer = stackDeployer;
            this.pipelineStarter = pipelineStarter;
            this.config = config.Value;
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
                payload = requestValidator.Validate(request, config.GithubOwner, true, config.GithubSigningSecret);
            }
            catch (RequestValidationException e)
            {
                Console.WriteLine(e.Message);
                return CreateResponse(statusCode: e.StatusCode);
            }

            var tasks = new List<Task>();

            if (payload.OnDefaultBranch && !payload.HeadCommit.Message.Contains("[skip meta-ci]"))
            {
                tasks.Add(DeployCicdStack(payload));
            }

            if (!payload.HeadCommit.Message.Contains("[skip ci]"))
            {
                tasks.Add(pipelineStarter.StartPipelineIfExists(payload));
            }

            await Task.WhenAll(tasks);
            return CreateResponse(statusCode: OK);
        }

        private async Task DeployCicdStack(PushEvent payload)
        {
            var stackName = $"{payload.Repository.Name}-{config.StackSuffix}";
            var contentsUrl = payload.Repository.ContentsUrl;
            var templateContent = await CommittedFile.FromContentsUrl(contentsUrl, config.TemplateFilename, config, payload.Ref);
            var pipelineDefinition = await CommittedFile.FromContentsUrl(contentsUrl, config.PipelineDefinitionFilename, config, payload.Ref);
            var roleArn = config.RoleArn;

            if (templateContent == null)
            {
                Console.WriteLine($"Couldn't find template for {payload.Repository.Name}");
                return;
            }

            var parameters = new List<Parameter> {
                new Parameter { ParameterKey = "GithubToken", ParameterValue = config.GithubToken },
                new Parameter { ParameterKey = "GithubOwner", ParameterValue = payload.Repository.Owner.Name },
                new Parameter { ParameterKey = "GithubRepo", ParameterValue = payload.Repository.Name },
                new Parameter { ParameterKey = "GithubBranch", ParameterValue = payload.Repository.DefaultBranch }
            };

            if (pipelineDefinition != null)
            {
                var hash = GetSum(pipelineDefinition);
                var key = $"{payload.Repository.Name}/{hash}";

                parameters.Add(new Parameter
                {
                    ParameterKey = "PipelineDefinitionBucket",
                    ParameterValue = config.ArtifactStore
                });

                parameters.Add(new Parameter
                {
                    ParameterKey = "PipelineDefinitionKey",
                    ParameterValue = key
                });

                if (!await IsFileAlreadyUploaded(config.ArtifactStore, key))
                {
                    var s3Client = await s3Factory.Create();
                    var response = await s3Client.PutObjectAsync(new PutObjectRequest
                    {
                        BucketName = config.ArtifactStore,
                        Key = key,
                        ContentBody = pipelineDefinition
                    });

                    Console.WriteLine($"Got response from s3 put object on the pipeline definition: {Serialize(response)}");
                }
            }

            try
            {
                await stackDeployer.Deploy(new DeployStackContext
                {
                    StackName = stackName,
                    Template = templateContent,
                    PassRoleArn = roleArn,
                    Parameters = parameters
                });
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to create/update stack: {e.Message} {e.StackTrace}");
            }
        }

        private static string GetSum(string contents)
        {
            using (var sha256 = SHA256.Create())
            {
                var fileBytes = Encoding.UTF8.GetBytes(contents);
                var sumBytes = sha256.ComputeHash(fileBytes);
                return string.Join("", sumBytes.Select(byt => $"{byt:X2}"));
            }
        }

        private async Task<bool> IsFileAlreadyUploaded(string bucket, string key)
        {
            var s3Client = await s3Factory.Create();

            try
            {
                await s3Client.GetObjectMetadataAsync(bucket, key);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
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
