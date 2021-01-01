using System;
using System.Threading.Tasks;

using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

using Cythral.CloudFormation.AwsUtils;
using Cythral.CloudFormation.GithubWebhook;
using Cythral.CloudFormation.GithubWebhook.Github;
using Cythral.CloudFormation.GithubWebhook.Github.Entities;

using Microsoft.Extensions.Logging;

using NSubstitute;

using NUnit.Framework;

using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.GithubWebhook.Pipelines.Tests
{
    public class PipelineStarterTests
    {
        [SetUp]
        public void SetupEnvvars()
        {
            Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1");
            Environment.SetEnvironmentVariable("AWS_ACCOUNT_ID", "5");
        }

        [Test]
        public async Task StartExecutionIsCalled_ForPushEvent()
        {
            var repoName = "name";
            var sha = "sha";
            var @ref = "refs/heads/master";
            var payload = new PushEvent
            {
                Ref = @ref,
                HeadCommit = new Commit
                {
                    Id = sha,
                },
                Repository = new Repository
                {
                    Name = repoName
                }
            };
            var serializedPayload = Serialize(payload);
            var stepFunctionsClient = Substitute.For<IAmazonStepFunctions>();
            var logger = Substitute.For<ILogger<PipelineStarter>>();
            var pipelineStarter = new PipelineStarter(stepFunctionsClient, logger);

            await pipelineStarter.StartPipelineIfExists(payload);

            var StateMachineArn = $"arn:aws:states:us-east-1:5:stateMachine:{repoName}-cicd-pipeline";
            await stepFunctionsClient.Received().StartExecutionAsync(Arg.Is<StartExecutionRequest>(req =>
                req.StateMachineArn == StateMachineArn &&
                req.Name == sha &&
                req.Input == serializedPayload
            ));
        }

        [Test]
        public async Task StartExecutionIsCalled_ForTags()
        {
            var repoName = "name";
            var sha = "sha";
            var @ref = "refs/tags/v0.1.0";
            var payload = new PushEvent
            {
                Ref = @ref,
                HeadCommit = new Commit
                {
                    Id = sha,
                },
                Repository = new Repository
                {
                    Name = repoName
                }
            };
            var serializedPayload = Serialize(payload);
            var stepFunctionsClient = Substitute.For<IAmazonStepFunctions>();
            var logger = Substitute.For<ILogger<PipelineStarter>>();
            var pipelineStarter = new PipelineStarter(stepFunctionsClient, logger);

            await pipelineStarter.StartPipelineIfExists(payload);

            var StateMachineArn = $"arn:aws:states:us-east-1:5:stateMachine:{repoName}-cicd-pipeline";
            await stepFunctionsClient.Received().StartExecutionAsync(Arg.Is<StartExecutionRequest>(req =>
                req.StateMachineArn == StateMachineArn &&
                req.Name == null &&
                req.Input == serializedPayload
            ));
        }

        [Test]
        public async Task StartExecutionIsCalled_ForPREvent()
        {
            var repoName = "name";
            var sha = "sha";
            var @ref = "ref";
            var payload = new PullRequestEvent
            {
                PullRequest = new PullRequest
                {
                    Head = new PullRequestHead
                    {
                        Sha = sha,
                        Ref = @ref,
                    }
                },
                Repository = new Repository
                {
                    Name = repoName
                }
            };

            var serializedPayload = Serialize(payload);
            var stepFunctionsClient = Substitute.For<IAmazonStepFunctions>();
            var logger = Substitute.For<ILogger<PipelineStarter>>();
            var pipelineStarter = new PipelineStarter(stepFunctionsClient, logger);

            await pipelineStarter.StartPipelineIfExists(payload);

            var StateMachineArn = $"arn:aws:states:us-east-1:5:stateMachine:{repoName}-cicd-pipeline";
            await stepFunctionsClient.Received().StartExecutionAsync(Arg.Is<StartExecutionRequest>(req =>
                req.StateMachineArn == StateMachineArn &&
                req.Name == sha &&
                req.Input == serializedPayload
            ));
        }

        // TODO: test exceptions are swallowed
    }
}
