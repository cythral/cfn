extern alias GithubWebhook;

using System;
using System.Threading.Tasks;

using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

using GithubWebhook::Cythral.CloudFormation.AwsUtils;
using GithubWebhook::Cythral.CloudFormation.GithubWebhook;
using GithubWebhook::Cythral.CloudFormation.GithubWebhook.Github;
using GithubWebhook::Cythral.CloudFormation.GithubWebhook.Github.Entities;

using Microsoft.Extensions.Logging;

using NSubstitute;

using NUnit.Framework;

using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.GithubWebhook.Pipelines.Tests
{
    using PipelineStarter = GithubWebhook::Cythral.CloudFormation.GithubWebhook.Pipelines.PipelineStarter;

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
            var payload = new PushEvent
            {
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
        public async Task StartExecutionIsCalled_ForPREvent()
        {
            var repoName = "name";
            var sha = "sha";
            var payload = new PullRequestEvent
            {
                PullRequest = new PullRequest
                {
                    Head = new PullRequestHead
                    {
                        Sha = sha
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
