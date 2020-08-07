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
using Cythral.CloudFormation.GithubWebhook.Entities;
using Cythral.CloudFormation.GithubWebhook.Exceptions;

using static System.Net.HttpStatusCode;
using static System.Text.Json.JsonSerializer;

using S3Factory = Cythral.CloudFormation.AwsUtils.AmazonClientFactory<
    Amazon.S3.IAmazonS3,
    Amazon.S3.AmazonS3Client
>;
using WebhookConfig = Cythral.CloudFormation.GithubWebhook.Config;

namespace Cythral.CloudFormation.GithubWebhook
{
    public class Handler
    {
        public static WebhookConfig Config { get; set; }
        private static RequestValidator requestValidator = new RequestValidator();
        private static DeployStackFacade stackDeployer = new DeployStackFacade();
        private static PipelineStarter pipelineStarter = new PipelineStarter();
        private static S3Factory s3Factory = new S3Factory();

        /// <summary>
        /// This function is called on every request to /webhooks/github
        /// </summary>
        /// <param name="request">Request sent by the application load balancer</param>
        /// <param name="context">The lambda context</param>
        /// <returns>A load balancer response object</returns>
        [LambdaSerializer(typeof(CamelCaseLambdaJsonSerializer))]
        public static async Task<ApplicationLoadBalancerResponse> Handle(ApplicationLoadBalancerRequest request, ILambdaContext context = null)
        {
            PushEvent payload = null;

            // create the config variable if it hasn't been created already (may have been cached from previous request)
            Config = Config ?? await WebhookConfig.Create(new List<(string, bool)> {
                // envvar name                      encrypted? 
                ("GITHUB_OWNER",                    false),
                ("GITHUB_TOKEN",                    true),
                ("GITHUB_SIGNING_SECRET",           true),
                ("TEMPLATE_FILENAME",               false),
                ("PIPELINE_DEFINITION_FILENAME",    false),
                ("ARTIFACT_STORE",                  false),
                ("STACK_SUFFIX",                    false),
                ("ROLE_ARN",                        false),
            });

            try
            {
                payload = requestValidator.Validate(request, Config["GITHUB_OWNER"], true, Config["GITHUB_SIGNING_SECRET"]);
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

            Task.WaitAll(tasks.ToArray());
            return CreateResponse(statusCode: OK);
        }

        private static async Task DeployCicdStack(PushEvent payload)
        {
            var stackName = $"{payload.Repository.Name}-{Config["STACK_SUFFIX"]}";
            var contentsUrl = payload.Repository.ContentsUrl;
            var templateContent = await CommittedFile.FromContentsUrl(contentsUrl, Config["TEMPLATE_FILENAME"], Config, payload.Ref);
            var pipelineDefinition = await CommittedFile.FromContentsUrl(contentsUrl, Config["PIPELINE_DEFINITION_FILENAME"], Config, payload.Ref);
            var roleArn = Config["ROLE_ARN"];

            if (templateContent == null)
            {
                Console.WriteLine($"Couldn't find template for {payload.Repository.Name}");
                return;
            }

            var parameters = new List<Parameter> {
                new Parameter { ParameterKey = "GithubToken", ParameterValue = Config["GITHUB_TOKEN"] },
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
                    ParameterValue = Config["ARTIFACT_STORE"]
                });

                parameters.Add(new Parameter
                {
                    ParameterKey = "PipelineDefinitionKey",
                    ParameterValue = key
                });

                if (!await IsFileAlreadyUploaded(Config["ARTIFACT_STORE"], key))
                {
                    var s3Client = await s3Factory.Create();
                    var response = await s3Client.PutObjectAsync(new PutObjectRequest
                    {
                        BucketName = Config["ARTIFACT_STORE"],
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

        private static async Task<bool> IsFileAlreadyUploaded(string bucket, string key)
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
