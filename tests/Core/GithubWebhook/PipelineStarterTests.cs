using System;
using System.Threading.Tasks;

using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

using Cythral.CloudFormation.Aws;
using Cythral.CloudFormation.Entities;
using Cythral.CloudFormation.Events;
using Cythral.CloudFormation.GithubWebhook;

using NSubstitute;

using NUnit.Framework;
using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.Tests.GithubWebhook
{
    public class PipelineStarterTests
    {
        private static StepFunctionsClientFactory stepFunctionsClientFactory = Substitute.For<StepFunctionsClientFactory>();
        private static IAmazonStepFunctions stepFunctionsClient = Substitute.For<IAmazonStepFunctions>();
        private static PipelineStarter pipelineStarter = new PipelineStarter();

        [SetUp]
        public void SetupStepFunctions()
        {
            TestUtils.SetPrivateField(pipelineStarter, "stepFunctionsClientFactory", stepFunctionsClientFactory);
            stepFunctionsClientFactory.ClearReceivedCalls();
            stepFunctionsClientFactory.Create().Returns(stepFunctionsClient);
        }

        [SetUp]
        public void SetupEnvvars()
        {
            Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1");
            Environment.SetEnvironmentVariable("AWS_ACCOUNT_ID", "5");
        }

        [Test]
        public async Task StepFunctionsClientIsCreated()
        {
            var sha = "sha";
            var repoName = "name";
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
            await pipelineStarter.StartPipelineIfExists(payload);

            stepFunctionsClientFactory.Received().Create();
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

            await pipelineStarter.StartPipelineIfExists(payload);

            await stepFunctionsClient.Received().StartExecutionAsync(Arg.Is<StartExecutionRequest>(req =>
                req.StateMachineArn == $"arn:aws:states:us-east-1:5:stateMachine:{repoName}-cicd-pipeline" &&
                req.Name == sha &&
                req.Input == serializedPayload
            ));
        }
    }
}
