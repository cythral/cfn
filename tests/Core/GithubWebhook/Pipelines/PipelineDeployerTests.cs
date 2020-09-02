using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.CloudFormation.Model;
using Amazon.S3;
using Amazon.S3.Model;

using Cythral.CloudFormation.AwsUtils.CloudFormation;
using Cythral.CloudFormation.GithubWebhook.Github;
using Cythral.CloudFormation.GithubWebhook.Github.Entities;

using FluentAssertions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using NUnit.Framework;

using static NSubstitute.Arg;

namespace Cythral.CloudFormation.GithubWebhook.Pipelines.Tests
{
    public class PipelineDeployerTests
    {
        private const string githubToken = "githubToken";
        private const string githubOwner = "githubOwner";
        private const string githubRepo = "githubRepo";
        private const string githubBranch = "githubBranch";
        private const string stackSuffix = "cicd";
        private const string roleArn = "roleArn";
        private const string gitRef = "gitRef";
        private const string contentsUrl = "contentsUrl";
        private const string templateFileName = "templateFileName";
        private const string definitionFileName = "definitionFileName";
        private const string template = "template";
        private const string definition = "definition";
        private const string sum = "sum";
        private const string bucketName = "bucketName";
        private const string expectedKey = "githubRepo/sum";

        class Deploy
        {
            private IOptions<Config> config = Options.Create(new Config
            {
                TemplateFilename = templateFileName,
                PipelineDefinitionFilename = definitionFileName,
                GithubToken = githubToken,
                GithubOwner = githubOwner,
                RoleArn = roleArn,
                StackSuffix = stackSuffix,
                ArtifactStore = bucketName,
            });


            [Test]
            public async Task Deploy_FetchesCicdTemplate()
            {
                var logger = Substitute.For<ILogger<PipelineDeployer>>();
                var fileFetcher = Substitute.For<GithubFileFetcher>();
                var deployer = Substitute.For<DeployStackFacade>();
                var sumComputer = Substitute.For<Sha256SumComputer>();
                var s3Client = Substitute.For<IAmazonS3>();
                var pipelineDeployer = new PipelineDeployer(s3Client, sumComputer, fileFetcher, deployer, config, logger);

                await pipelineDeployer.Deploy(new PushEvent
                {
                    Ref = gitRef,
                    Repository = new Repository
                    {
                        ContentsUrl = contentsUrl
                    }
                });

                await fileFetcher.Received().Fetch(Is(contentsUrl), Is(templateFileName), Is(gitRef));
            }

            [Test]
            public async Task Deploy_FetchesPipelineDefinition()
            {
                var logger = Substitute.For<ILogger<PipelineDeployer>>();
                var fileFetcher = Substitute.For<GithubFileFetcher>();
                var deployer = Substitute.For<DeployStackFacade>();
                var sumComputer = Substitute.For<Sha256SumComputer>();
                var s3Client = Substitute.For<IAmazonS3>();
                var pipelineDeployer = new PipelineDeployer(s3Client, sumComputer, fileFetcher, deployer, config, logger);

                await pipelineDeployer.Deploy(new PushEvent
                {
                    Ref = gitRef,
                    Repository = new Repository
                    {
                        ContentsUrl = contentsUrl
                    }
                });

                await fileFetcher.Received().Fetch(Is(contentsUrl), Is(definitionFileName), Is(gitRef));
            }

            [Test]
            public async Task Deploy_ShouldNotDeployStack_IfTemplateWasNotFound()
            {
                var logger = Substitute.For<ILogger<PipelineDeployer>>();
                var fileFetcher = Substitute.For<GithubFileFetcher>();
                var deployer = Substitute.For<DeployStackFacade>();
                var sumComputer = Substitute.For<Sha256SumComputer>();
                var s3Client = Substitute.For<IAmazonS3>();
                var pipelineDeployer = new PipelineDeployer(s3Client, sumComputer, fileFetcher, deployer, config, logger);

                fileFetcher.Fetch(Any<string>(), Is(templateFileName), Any<string>()).Returns((string)null);

                await pipelineDeployer.Deploy(new PushEvent
                {
                    Ref = gitRef,
                    Repository = new Repository
                    {
                        ContentsUrl = contentsUrl
                    }
                });

                await deployer.DidNotReceiveWithAnyArgs().Deploy(null!);
            }

            [Test]
            public async Task Deploy_ShouldDeployStack_IfTemplateWasFound()
            {
                var logger = Substitute.For<ILogger<PipelineDeployer>>();
                var fileFetcher = Substitute.For<GithubFileFetcher>();
                var deployer = Substitute.For<DeployStackFacade>();
                var sumComputer = Substitute.For<Sha256SumComputer>();
                var s3Client = Substitute.For<IAmazonS3>();
                var pipelineDeployer = new PipelineDeployer(s3Client, sumComputer, fileFetcher, deployer, config, logger);

                fileFetcher.Fetch(Any<string>(), Is(templateFileName), Any<string>()).Returns(template);
                fileFetcher.Fetch(Any<string>(), Is(definitionFileName), Any<string>()).Returns((string)null);

                await pipelineDeployer.Deploy(new PushEvent
                {
                    Ref = gitRef,
                    Repository = new Repository
                    {
                        Name = githubRepo,
                        ContentsUrl = contentsUrl,
                        DefaultBranch = githubBranch
                    }
                });

                await deployer.Received().Deploy(Is<DeployStackContext>(req =>
                    req.PassRoleArn == roleArn &&
                    req.Template == template &&
                    req.StackName == $"{githubRepo}-{stackSuffix}" &&
                    req.Parameters.Count() == 4 &&
                    req.Parameters.Any(parameter => parameter.ParameterKey == "GithubToken" && parameter.ParameterValue == githubToken) &&
                    req.Parameters.Any(parameter => parameter.ParameterKey == "GithubOwner" && parameter.ParameterValue == githubOwner) &&
                    req.Parameters.Any(parameter => parameter.ParameterKey == "GithubRepo" && parameter.ParameterValue == githubRepo) &&
                    req.Parameters.Any(parameter => parameter.ParameterKey == "GithubBranch" && parameter.ParameterValue == githubBranch)
                ));
            }

            [Test]
            public async Task Deploy_ShouldDeployStackWithPipelineDefinition_IfPipelineDefinitionWasFound()
            {
                var logger = Substitute.For<ILogger<PipelineDeployer>>();
                var fileFetcher = Substitute.For<GithubFileFetcher>();
                var deployer = Substitute.For<DeployStackFacade>();
                var sumComputer = Substitute.For<Sha256SumComputer>();
                var s3Client = Substitute.For<IAmazonS3>();
                var pipelineDeployer = new PipelineDeployer(s3Client, sumComputer, fileFetcher, deployer, config, logger);

                fileFetcher.Fetch(Any<string>(), Is(templateFileName), Any<string>()).Returns(template);
                fileFetcher.Fetch(Any<string>(), Is(definitionFileName), Any<string>()).Returns(definition);
                sumComputer.ComputeSum(Any<string>()).Returns(sum);

                await pipelineDeployer.Deploy(new PushEvent
                {
                    Ref = gitRef,
                    Repository = new Repository
                    {
                        Name = githubRepo,
                        ContentsUrl = contentsUrl,
                        DefaultBranch = githubBranch
                    }
                });

                await deployer.Received().Deploy(Is<DeployStackContext>(req =>
                    req.PassRoleArn == roleArn &&
                    req.Template == template &&
                    req.StackName == $"{githubRepo}-{stackSuffix}" &&
                    req.Parameters.Count() == 6 &&
                    req.Parameters.Any(parameter => parameter.ParameterKey == "PipelineDefinitionBucket" && parameter.ParameterValue == bucketName) &&
                    req.Parameters.Any(parameter => parameter.ParameterKey == "PipelineDefinitionKey" && parameter.ParameterValue == expectedKey) &&
                    req.Parameters.Any(parameter => parameter.ParameterKey == "GithubToken" && parameter.ParameterValue == githubToken) &&
                    req.Parameters.Any(parameter => parameter.ParameterKey == "GithubOwner" && parameter.ParameterValue == githubOwner) &&
                    req.Parameters.Any(parameter => parameter.ParameterKey == "GithubRepo" && parameter.ParameterValue == githubRepo) &&
                    req.Parameters.Any(parameter => parameter.ParameterKey == "GithubBranch" && parameter.ParameterValue == githubBranch)
                ));
            }
        }


        class UploadDefinition
        {
            [Test]
            public async Task UploadDefinition_UploadsDefinition_IfObjectDoesntExist()
            {
                var logger = Substitute.For<ILogger<PipelineDeployer>>();
                var fileFetcher = Substitute.For<GithubFileFetcher>();
                var deployer = Substitute.For<DeployStackFacade>();
                var sumComputer = Substitute.For<Sha256SumComputer>();
                sumComputer.ComputeSum(Any<string>()).Returns(sum);

                var s3Client = Substitute.For<IAmazonS3>();
                s3Client
                .When(client => client.GetObjectMetadataAsync(Any<string>(), Any<string>()))
                .Do(client => throw new Exception());

                var config = Options.Create(new Config { ArtifactStore = bucketName });
                var uploader = new PipelineDeployer(s3Client, sumComputer, fileFetcher, deployer, config, logger);

                var result = await uploader.UploadDefinition(githubRepo, definition);

                result.Should().BeEquivalentTo(expectedKey);
                await s3Client.Received().GetObjectMetadataAsync(Is(bucketName), Is(expectedKey));
                await s3Client.Received().PutObjectAsync(Is<PutObjectRequest>(req =>
                    req.BucketName == bucketName &&
                    req.Key == expectedKey &&
                    req.ContentBody == definition
                ));
            }

            [Test]
            public async Task UploadDefinition_DoesNotUploadDefinition_IfAlreadyExists()
            {
                var logger = Substitute.For<ILogger<PipelineDeployer>>();
                var fileFetcher = Substitute.For<GithubFileFetcher>();
                var deployer = Substitute.For<DeployStackFacade>();
                var s3Client = Substitute.For<IAmazonS3>();
                var sumComputer = Substitute.For<Sha256SumComputer>();
                sumComputer.ComputeSum(Any<string>()).Returns(sum);

                var config = Options.Create(new Config { ArtifactStore = bucketName });
                var uploader = new PipelineDeployer(s3Client, sumComputer, fileFetcher, deployer, config, logger);

                var result = await uploader.UploadDefinition(githubRepo, definition);

                result.Should().BeEquivalentTo(expectedKey);
                await s3Client.Received().GetObjectMetadataAsync(Is(bucketName), Is(expectedKey));
                await s3Client.DidNotReceiveWithAnyArgs().PutObjectAsync(null!);
            }
        }
    }
}