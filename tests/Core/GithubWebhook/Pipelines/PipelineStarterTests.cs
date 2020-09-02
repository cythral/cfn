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
        public async Task StartExecutionIsCalled()
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

            await stepFunctionsClient.Received().StartExecutionAsync(Arg.Is<StartExecutionRequest>(req =>
                req.StateMachineArn == $"arn:aws:states:us-east-1:5:stateMachine:{repoName}-cicd-pipeline" &&
                req.Name == sha &&
                req.Input == serializedPayload
            ));
        }

        // TODO: test exceptions are swallowed
    }
}
