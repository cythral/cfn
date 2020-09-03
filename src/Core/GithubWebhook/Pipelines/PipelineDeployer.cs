using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Amazon.CloudFormation.Model;
using Amazon.S3;
using Amazon.S3.Model;

using Cythral.CloudFormation.AwsUtils.CloudFormation;
using Cythral.CloudFormation.GithubWebhook.Github;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.GithubWebhook.Pipelines
{
    public class PipelineDeployer
    {
        private readonly IAmazonS3 s3Client;
        private readonly Sha256SumComputer sumComputer;
        private readonly GithubFileFetcher fileFetcher;
        private readonly GithubStatusNotifier statusNotifier;
        private readonly DeployStackFacade stackDeployer;
        private readonly Config config;
        private readonly ILogger<PipelineDeployer> logger;

        public PipelineDeployer(
            IAmazonS3 s3Client,
            Sha256SumComputer sumComputer,
            GithubFileFetcher fileFetcher,
            GithubStatusNotifier statusNotifier,
            DeployStackFacade stackDeployer,
            IOptions<Config> options,
            ILogger<PipelineDeployer> logger
        )
        {
            this.s3Client = s3Client;
            this.config = options.Value;
            this.sumComputer = sumComputer;
            this.fileFetcher = fileFetcher;
            this.statusNotifier = statusNotifier;
            this.stackDeployer = stackDeployer;
            this.logger = logger;
        }

        internal PipelineDeployer()
        {
            // used for testing
        }

        public virtual async Task Deploy(PushEvent payload)
        {
            var stackName = $"{payload.Repository.Name}-{config.StackSuffix}";
            var contentsUrl = payload.Repository.ContentsUrl;
            var templateContent = await fileFetcher.Fetch(contentsUrl, config.TemplateFilename, payload.Ref);
            var pipelineDefinition = await fileFetcher.Fetch(contentsUrl, config.PipelineDefinitionFilename, payload.Ref);
            var roleArn = config.RoleArn;

            if (templateContent == null)
            {
                logger.LogInformation($"Couldn't find template for {payload.Repository.Name}");
                return;
            }

            var parameters = new List<Parameter> {
                new Parameter { ParameterKey = "GithubToken", ParameterValue = config.GithubToken },
                new Parameter { ParameterKey = "GithubOwner", ParameterValue = config.GithubOwner },
                new Parameter { ParameterKey = "GithubRepo", ParameterValue = payload.Repository.Name },
                new Parameter { ParameterKey = "GithubBranch", ParameterValue = payload.Repository.DefaultBranch }
            };

            if (pipelineDefinition != null)
            {
                var key = await UploadDefinition(payload.Repository.Name, pipelineDefinition);

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
            }

            try
            {
                await stackDeployer.Deploy(new DeployStackContext
                {
                    StackName = stackName,
                    Template = templateContent,
                    NotificationArn = config.StatusNotificationTopicArn,
                    ClientRequestToken = payload.HeadCommit.Id,
                    PassRoleArn = roleArn,
                    Parameters = parameters
                });
            }
            catch (NoUpdatesException)
            {
                await statusNotifier.NotifySuccess(payload.Repository.Name, payload.HeadCommit.Id);
            }
            catch (Exception e)
            {
                logger.LogError($"Failed to create/update stack: {e.Message} {e.StackTrace}");

                await statusNotifier.NotifyFailure(payload.Repository.Name, payload.HeadCommit.Id);
            }
        }

        public virtual async Task<string> UploadDefinition(string repoName, string pipelineDefinition)
        {
            var hash = sumComputer.ComputeSum(pipelineDefinition);
            var key = $"{repoName}/{hash}";

            if (!await IsAlreadyUploaded(key))
            {
                var response = await s3Client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = config.ArtifactStore,
                    Key = key,
                    ContentBody = pipelineDefinition
                });

                logger.LogInformation($"Got response from s3 put object on the pipeline definition: {Serialize(response)}");
            }

            return key;
        }

        private async Task<bool> IsAlreadyUploaded(string key)
        {
            try
            {
                await s3Client.GetObjectMetadataAsync(config.ArtifactStore, key);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}