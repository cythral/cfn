using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

using AutoFixture.AutoNSubstitute;
using AutoFixture.NUnit3;

using Cythral.CloudFormation.StackDeploymentStatus;
using Cythral.CloudFormation.StackDeploymentStatus.Github;

using Lambdajection.Core;
using Lambdajection.Sns;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ClearExtensions;

using NUnit.Framework;

using static System.Text.Json.JsonSerializer;

using static NSubstitute.Arg;

namespace Cythral.CloudFormation.Tests.StackDeploymentStatus
{
    using TokenInfo = Cythral.CloudFormation.StackDeploymentStatus.TokenInfo;

    [TestFixture]
    public class HandlerTests
    {
        [Test, Auto]
        public async Task ShouldDoNothingIfClientRequestTokenIsEmpty(
            SnsMessage<CloudFormationStackEvent> message,
            [Frozen, Substitute] IAmazonStepFunctions stepFunctions,
            [Target] Handler handler
        )
        {
            message.Message.ClientRequestToken = string.Empty;

            await handler.Handle(message);

            await stepFunctions.DidNotReceiveWithAnyArgs().SendTaskFailureAsync(Any<SendTaskFailureRequest>());
            await stepFunctions.DidNotReceiveWithAnyArgs().SendTaskSuccessAsync(Any<SendTaskSuccessRequest>());
        }

        [Test, Auto]
        public async Task ShouldDoNothingIfStackIdAndPhysicalResourceIdDontMatch(
            SnsMessage<CloudFormationStackEvent> message,
            [Frozen, Substitute] IAmazonStepFunctions stepFunctions,
            [Target] Handler handler
        )
        {
            await handler.Handle(message);

            await stepFunctions.DidNotReceiveWithAnyArgs().SendTaskFailureAsync(Any<SendTaskFailureRequest>());
            await stepFunctions.DidNotReceiveWithAnyArgs().SendTaskSuccessAsync(Any<SendTaskSuccessRequest>());
        }

        [Test, Auto]
        public async Task AllSourcesNotified_OfFailure_WhenSourceTopicIsNotGithub_DeleteComplete(
            string stackId,
            SnsMessage<CloudFormationStackEvent> message,
            [Frozen] TokenInfo tokenInfo,
            [Frozen, Substitute] TokenInfoRepository tokenInfoRepository,
            [Frozen, Substitute] GithubStatusNotifier statusNotifier,
            [Frozen, Substitute] IAmazonSQS sqs,
            [Frozen, Substitute] IAmazonStepFunctions stepFunctions,
            [Target] Handler handler
        )
        {
            var status = "DELETE_COMPLETE";
            message.Message.ResourceStatus = status;
            message.Message.StackId = stackId;
            message.Message.PhysicalResourceId = stackId;

            await handler.Handle(message);

            await tokenInfoRepository
                .Received()
                .FindByRequest(Is(message.Message));

            await stepFunctions
                .Received()
                .SendTaskFailureAsync(Is<SendTaskFailureRequest>(req => req.TaskToken == tokenInfo.ClientRequestToken && req.Cause == status));

            await sqs
                .Received()
                .DeleteMessageAsync(Is<DeleteMessageRequest>(req => req.QueueUrl == tokenInfo.QueueUrl && req.ReceiptHandle == tokenInfo.ReceiptHandle));

            await statusNotifier
                .Received()
                .NotifyFailure(Is(tokenInfo.GithubOwner), Is(tokenInfo.GithubRepo), Is(tokenInfo.GithubRef), Is(message.Message.StackName), Is(tokenInfo.EnvironmentName));
        }

        [Test, Auto]
        public async Task AllSourcesNotified_OfFailure_WhenSourceTopicIsNotGithub_UpdateRollbackComplete(
            string stackId,
            SnsMessage<CloudFormationStackEvent> message,
            [Frozen] TokenInfo tokenInfo,
            [Frozen, Substitute] TokenInfoRepository tokenInfoRepository,
            [Frozen, Substitute] GithubStatusNotifier statusNotifier,
            [Frozen, Substitute] IAmazonSQS sqs,
            [Frozen, Substitute] IAmazonStepFunctions stepFunctions,
            [Target] Handler handler
        )
        {
            var status = "UPDATE_ROLLBACK_COMPLETE";
            message.Message.ResourceStatus = status;
            message.Message.StackId = stackId;
            message.Message.PhysicalResourceId = stackId;

            await handler.Handle(message);

            await tokenInfoRepository
                .Received()
                .FindByRequest(Is(message.Message));

            await stepFunctions
                .Received()
                .SendTaskFailureAsync(Is<SendTaskFailureRequest>(req => req.TaskToken == tokenInfo.ClientRequestToken && req.Cause == status));

            await sqs
                .Received()
                .DeleteMessageAsync(Is<DeleteMessageRequest>(req => req.QueueUrl == tokenInfo.QueueUrl && req.ReceiptHandle == tokenInfo.ReceiptHandle));

            await statusNotifier
                .Received()
                .NotifyFailure(Is(tokenInfo.GithubOwner), Is(tokenInfo.GithubRepo), Is(tokenInfo.GithubRef), Is(message.Message.StackName), Is(tokenInfo.EnvironmentName));
        }

        [Test, Auto]
        public async Task AllSourcesNotified_OfFailure_WhenSourceTopicIsNotGithub_UpdateRollbackFailed(
            string stackId,
            SnsMessage<CloudFormationStackEvent> message,
            [Frozen] TokenInfo tokenInfo,
            [Frozen, Substitute] TokenInfoRepository tokenInfoRepository,
            [Frozen, Substitute] GithubStatusNotifier statusNotifier,
            [Frozen, Substitute] IAmazonSQS sqs,
            [Frozen, Substitute] IAmazonStepFunctions stepFunctions,
            [Target] Handler handler
        )
        {
            var status = "UPDATE_ROLLBACK_FAILED";
            message.Message.ResourceStatus = status;
            message.Message.StackId = stackId;
            message.Message.PhysicalResourceId = stackId;

            await handler.Handle(message);

            await tokenInfoRepository
                .Received()
                .FindByRequest(Is(message.Message));

            await stepFunctions
                .Received()
                .SendTaskFailureAsync(Is<SendTaskFailureRequest>(req => req.TaskToken == tokenInfo.ClientRequestToken && req.Cause == status));

            await sqs
                .Received()
                .DeleteMessageAsync(Is<DeleteMessageRequest>(req => req.QueueUrl == tokenInfo.QueueUrl && req.ReceiptHandle == tokenInfo.ReceiptHandle));

            await statusNotifier
                .Received()
                .NotifyFailure(Is(tokenInfo.GithubOwner), Is(tokenInfo.GithubRepo), Is(tokenInfo.GithubRef), Is(message.Message.StackName), Is(tokenInfo.EnvironmentName));
        }

        [Test, Auto]
        public async Task AllSourcesNotified_OfFailure_WhenSourceTopicIsNotGithub_CreateFailed(
            string stackId,
            SnsMessage<CloudFormationStackEvent> message,
            [Frozen] TokenInfo tokenInfo,
            [Frozen, Substitute] TokenInfoRepository tokenInfoRepository,
            [Frozen, Substitute] GithubStatusNotifier statusNotifier,
            [Frozen, Substitute] IAmazonSQS sqs,
            [Frozen, Substitute] IAmazonStepFunctions stepFunctions,
            [Target] Handler handler
        )
        {
            var status = "CREATE_FAILED";
            message.Message.ResourceStatus = status;
            message.Message.StackId = stackId;
            message.Message.PhysicalResourceId = stackId;

            await handler.Handle(message);

            await tokenInfoRepository
                .Received()
                .FindByRequest(Is(message.Message));

            await stepFunctions
                .Received()
                .SendTaskFailureAsync(Is<SendTaskFailureRequest>(req => req.TaskToken == tokenInfo.ClientRequestToken && req.Cause == status));

            await sqs
                .Received()
                .DeleteMessageAsync(Is<DeleteMessageRequest>(req => req.QueueUrl == tokenInfo.QueueUrl && req.ReceiptHandle == tokenInfo.ReceiptHandle));

            await statusNotifier
                .Received()
                .NotifyFailure(Is(tokenInfo.GithubOwner), Is(tokenInfo.GithubRepo), Is(tokenInfo.GithubRef), Is(message.Message.StackName), Is(tokenInfo.EnvironmentName));
        }

        [Test, Auto]
        public async Task OnlyGithubNotified_OfFailure_WhenSourceTopicIsGithub_DeleteComplete(
            string githubTopicArn,
            string stackId,
            SnsMessage<CloudFormationStackEvent> message,
            [Frozen] Config config,
            [Frozen] TokenInfo tokenInfo,
            [Frozen, Substitute] TokenInfoRepository tokenInfoRepository,
            [Frozen, Substitute] GithubStatusNotifier statusNotifier,
            [Frozen, Substitute] IAmazonSQS sqs,
            [Frozen, Substitute] IAmazonStepFunctions stepFunctions,
            [Target] Handler handler
        )
        {
            var status = "DELETE_COMPLETE";
            config.GithubTopicArn = githubTopicArn;
            message.Message.ResourceStatus = status;
            message.Message.SourceTopic = githubTopicArn;
            message.Message.StackId = stackId;
            message.Message.PhysicalResourceId = stackId;

            await handler.Handle(message);

            await tokenInfoRepository
                .Received()
                .FindByRequest(Is(message.Message));

            await stepFunctions
                .DidNotReceive()
                .SendTaskFailureAsync(Any<SendTaskFailureRequest>());

            await sqs
                .DidNotReceive()
                .DeleteMessageAsync(Any<DeleteMessageRequest>());

            await statusNotifier
                .Received()
                .NotifyFailure(Is(tokenInfo.GithubOwner), Is(tokenInfo.GithubRepo), Is(tokenInfo.GithubRef), Is(message.Message.StackName), Is(tokenInfo.EnvironmentName));
        }

        [Test, Auto]
        public async Task OnlyGithubNotified_OfFailure_WhenSourceTopicIsGithub_UpdateRollbackComplete(
            string githubTopicArn,
            string stackId,
            SnsMessage<CloudFormationStackEvent> message,
            [Frozen] Config config,
            [Frozen] TokenInfo tokenInfo,
            [Frozen, Substitute] TokenInfoRepository tokenInfoRepository,
            [Frozen, Substitute] GithubStatusNotifier statusNotifier,
            [Frozen, Substitute] IAmazonSQS sqs,
            [Frozen, Substitute] IAmazonStepFunctions stepFunctions,
            [Target] Handler handler
        )
        {
            var status = "UPDATE_ROLLBACK_COMPLETE";
            config.GithubTopicArn = githubTopicArn;
            message.Message.ResourceStatus = status;
            message.Message.SourceTopic = githubTopicArn;
            message.Message.StackId = stackId;
            message.Message.PhysicalResourceId = stackId;

            await handler.Handle(message);

            await tokenInfoRepository
                .Received()
                .FindByRequest(Is(message.Message));

            await stepFunctions
                .DidNotReceive()
                .SendTaskFailureAsync(Any<SendTaskFailureRequest>());

            await sqs
                .DidNotReceive()
                .DeleteMessageAsync(Any<DeleteMessageRequest>());

            await statusNotifier
                .Received()
                .NotifyFailure(Is(tokenInfo.GithubOwner), Is(tokenInfo.GithubRepo), Is(tokenInfo.GithubRef), Is(message.Message.StackName), Is(tokenInfo.EnvironmentName));
        }

        [Test, Auto]
        public async Task OnlyGithubNotified_OfFailure_WhenSourceTopicIsGithub_UpdateRollbackFailed(
            string githubTopicArn,
            string stackId,
            SnsMessage<CloudFormationStackEvent> message,
            [Frozen] Config config,
            [Frozen] TokenInfo tokenInfo,
            [Frozen, Substitute] TokenInfoRepository tokenInfoRepository,
            [Frozen, Substitute] GithubStatusNotifier statusNotifier,
            [Frozen, Substitute] IAmazonSQS sqs,
            [Frozen, Substitute] IAmazonStepFunctions stepFunctions,
            [Target] Handler handler
        )
        {
            var status = "UPDATE_ROLLBACK_FAILED";
            config.GithubTopicArn = githubTopicArn;
            message.Message.ResourceStatus = status;
            message.Message.SourceTopic = githubTopicArn;
            message.Message.StackId = stackId;
            message.Message.PhysicalResourceId = stackId;

            await handler.Handle(message);

            await tokenInfoRepository
                .Received()
                .FindByRequest(Is(message.Message));

            await stepFunctions
                .DidNotReceive()
                .SendTaskFailureAsync(Any<SendTaskFailureRequest>());

            await sqs
                .DidNotReceive()
                .DeleteMessageAsync(Any<DeleteMessageRequest>());

            await statusNotifier
                .Received()
                .NotifyFailure(Is(tokenInfo.GithubOwner), Is(tokenInfo.GithubRepo), Is(tokenInfo.GithubRef), Is(message.Message.StackName), Is(tokenInfo.EnvironmentName));
        }

        [Test, Auto]
        public async Task OnlyGithubNotified_OfFailure_WhenSourceTopicIsGithub_CreateFailed(
            string githubTopicArn,
            string stackId,
            SnsMessage<CloudFormationStackEvent> message,
            [Frozen] Config config,
            [Frozen] TokenInfo tokenInfo,
            [Frozen, Substitute] TokenInfoRepository tokenInfoRepository,
            [Frozen, Substitute] GithubStatusNotifier statusNotifier,
            [Frozen, Substitute] IAmazonSQS sqs,
            [Frozen, Substitute] IAmazonStepFunctions stepFunctions,
            [Target] Handler handler
        )
        {
            var status = "CREATE_FAILED";
            config.GithubTopicArn = githubTopicArn;
            message.Message.ResourceStatus = status;
            message.Message.SourceTopic = githubTopicArn;
            message.Message.StackId = stackId;
            message.Message.PhysicalResourceId = stackId;

            await handler.Handle(message);

            await tokenInfoRepository
                .Received()
                .FindByRequest(Is(message.Message));

            await stepFunctions
                .DidNotReceive()
                .SendTaskFailureAsync(Any<SendTaskFailureRequest>());

            await sqs
                .DidNotReceive()
                .DeleteMessageAsync(Any<DeleteMessageRequest>());

            await statusNotifier
                .Received()
                .NotifyFailure(Is(tokenInfo.GithubOwner), Is(tokenInfo.GithubRepo), Is(tokenInfo.GithubRef), Is(message.Message.StackName), Is(tokenInfo.EnvironmentName));
        }

        [Test, Auto]
        public async Task AllSourcesNotified_OfSuccess_WhenSourceTopicIsNotGithub_UpdateComplete(
            string stackId,
            SnsMessage<CloudFormationStackEvent> message,
            [Frozen] TokenInfo tokenInfo,
            [Frozen] DescribeStacksResponse describeStacksResponse,
            [Frozen, Substitute] TokenInfoRepository tokenInfoRepository,
            [Frozen, Substitute] GithubStatusNotifier statusNotifier,
            [Frozen, Substitute] IAwsFactory<IAmazonCloudFormation> cloudformationFactory,
            [Frozen, Substitute] IAmazonCloudFormation cloudformationClient,
            [Frozen, Substitute] IAmazonSQS sqs,
            [Frozen, Substitute] IAmazonStepFunctions stepFunctions,
            [Target] Handler handler
        )
        {
            var status = "UPDATE_COMPLETE";
            message.Message.ResourceStatus = status;
            message.Message.StackId = stackId;
            message.Message.PhysicalResourceId = stackId;
            describeStacksResponse.Stacks[0] = new Stack
            {
                Outputs = new List<Output>()
                {
                    new() { OutputKey = "A", OutputValue = "B"},
                    new() { OutputKey = "Foo", OutputValue = "Bar"},
                },
            };

            var serializedOutputs = @"{""A"":""B"",""Foo"":""Bar""}";

            await handler.Handle(message);

            await tokenInfoRepository
                .Received()
                .FindByRequest(Is(message.Message));

            await cloudformationFactory
                .Received()
                .Create(Is(tokenInfo.RoleArn));

            await cloudformationClient
                .Received()
                .DescribeStacksAsync(Is<DescribeStacksRequest>(req => req.StackName == stackId));

            await stepFunctions
                .Received()
                .SendTaskSuccessAsync(Is<SendTaskSuccessRequest>(req => req.TaskToken == tokenInfo.ClientRequestToken && req.Output == serializedOutputs));

            await sqs
                .Received()
                .DeleteMessageAsync(Is<DeleteMessageRequest>(req => req.QueueUrl == tokenInfo.QueueUrl && req.ReceiptHandle == tokenInfo.ReceiptHandle));

            await statusNotifier
                .Received()
                .NotifySuccess(Is(tokenInfo.GithubOwner), Is(tokenInfo.GithubRepo), Is(tokenInfo.GithubRef), Is(message.Message.StackName), Is(tokenInfo.EnvironmentName));
        }

        [Test, Auto]
        public async Task AllSourcesNotified_OfSuccess_WhenSourceTopicIsNotGithub_CreateComplete(
            string stackId,
            SnsMessage<CloudFormationStackEvent> message,
            [Frozen] TokenInfo tokenInfo,
            [Frozen] DescribeStacksResponse describeStacksResponse,
            [Frozen, Substitute] TokenInfoRepository tokenInfoRepository,
            [Frozen, Substitute] GithubStatusNotifier statusNotifier,
            [Frozen, Substitute] IAwsFactory<IAmazonCloudFormation> cloudformationFactory,
            [Frozen, Substitute] IAmazonCloudFormation cloudformationClient,
            [Frozen, Substitute] IAmazonSQS sqs,
            [Frozen, Substitute] IAmazonStepFunctions stepFunctions,
            [Target] Handler handler
        )
        {
            var status = "CREATE_COMPLETE";
            message.Message.ResourceStatus = status;
            message.Message.StackId = stackId;
            message.Message.PhysicalResourceId = stackId;
            describeStacksResponse.Stacks[0] = new Stack
            {
                Outputs = new List<Output>()
                {
                    new() { OutputKey = "A", OutputValue = "B"},
                    new() { OutputKey = "Foo", OutputValue = "Bar"},
                },
            };

            var serializedOutputs = @"{""A"":""B"",""Foo"":""Bar""}";

            await handler.Handle(message);

            await tokenInfoRepository
                .Received()
                .FindByRequest(Is(message.Message));

            await cloudformationFactory
                .Received()
                .Create(Is(tokenInfo.RoleArn));

            await cloudformationClient
                .Received()
                .DescribeStacksAsync(Is<DescribeStacksRequest>(req => req.StackName == stackId));

            await stepFunctions
                .Received()
                .SendTaskSuccessAsync(Is<SendTaskSuccessRequest>(req => req.TaskToken == tokenInfo.ClientRequestToken && req.Output == serializedOutputs));

            await sqs
                .Received()
                .DeleteMessageAsync(Is<DeleteMessageRequest>(req => req.QueueUrl == tokenInfo.QueueUrl && req.ReceiptHandle == tokenInfo.ReceiptHandle));

            await statusNotifier
                .Received()
                .NotifySuccess(Is(tokenInfo.GithubOwner), Is(tokenInfo.GithubRepo), Is(tokenInfo.GithubRef), Is(message.Message.StackName), Is(tokenInfo.EnvironmentName));
        }

        [Test, Auto]
        public async Task OnlyGithubNotified_OfSuccess_WhenSourceTopicIsGithub_UpdateComplete(
            string stackId,
            string githubTopicArn,
            SnsMessage<CloudFormationStackEvent> message,
            [Frozen] Config config,
            [Frozen] TokenInfo tokenInfo,
            [Frozen] DescribeStacksResponse describeStacksResponse,
            [Frozen, Substitute] TokenInfoRepository tokenInfoRepository,
            [Frozen, Substitute] GithubStatusNotifier statusNotifier,
            [Frozen, Substitute] IAwsFactory<IAmazonCloudFormation> cloudformationFactory,
            [Frozen, Substitute] IAmazonCloudFormation cloudformationClient,
            [Frozen, Substitute] IAmazonSQS sqs,
            [Frozen, Substitute] IAmazonStepFunctions stepFunctions,
            [Target] Handler handler
        )
        {
            var status = "UPDATE_COMPLETE";
            config.GithubTopicArn = githubTopicArn;
            message.Message.SourceTopic = githubTopicArn;
            message.Message.ResourceStatus = status;
            message.Message.SourceTopic = githubTopicArn;
            message.Message.StackId = stackId;
            message.Message.PhysicalResourceId = stackId;

            describeStacksResponse.Stacks[0] = new Stack
            {
                Outputs = new List<Output>()
                {
                    new() { OutputKey = "A", OutputValue = "B"},
                    new() { OutputKey = "Foo", OutputValue = "Bar"},
                },
            };

            var serializedOutputs = @"{""A"":""B"",""Foo"":""Bar""}";

            await handler.Handle(message);

            await tokenInfoRepository
                .Received()
                .FindByRequest(Is(message.Message));

            await cloudformationFactory
                .DidNotReceive()
                .Create(Is(tokenInfo.RoleArn));

            await cloudformationClient
                .DidNotReceive()
                .DescribeStacksAsync(Is<DescribeStacksRequest>(req => req.StackName == stackId));

            await stepFunctions
                .DidNotReceive()
                .SendTaskSuccessAsync(Is<SendTaskSuccessRequest>(req => req.TaskToken == tokenInfo.ClientRequestToken && req.Output == serializedOutputs));

            await sqs
                .DidNotReceive()
                .DeleteMessageAsync(Is<DeleteMessageRequest>(req => req.QueueUrl == tokenInfo.QueueUrl && req.ReceiptHandle == tokenInfo.ReceiptHandle));

            await statusNotifier
                .Received()
                .NotifySuccess(Is(tokenInfo.GithubOwner), Is(tokenInfo.GithubRepo), Is(tokenInfo.GithubRef), Is(message.Message.StackName), Is(tokenInfo.EnvironmentName));
        }

        [Test, Auto]
        public async Task OnlyGithubNotified_OfSuccess_WhenSourceTopicIsGithub_CreateComplete(
            string stackId,
            string githubTopicArn,
            SnsMessage<CloudFormationStackEvent> message,
            [Frozen] Config config,
            [Frozen] TokenInfo tokenInfo,
            [Frozen] DescribeStacksResponse describeStacksResponse,
            [Frozen, Substitute] TokenInfoRepository tokenInfoRepository,
            [Frozen, Substitute] GithubStatusNotifier statusNotifier,
            [Frozen, Substitute] IAwsFactory<IAmazonCloudFormation> cloudformationFactory,
            [Frozen, Substitute] IAmazonCloudFormation cloudformationClient,
            [Frozen, Substitute] IAmazonSQS sqs,
            [Frozen, Substitute] IAmazonStepFunctions stepFunctions,
            [Target] Handler handler
        )
        {
            var status = "CREATE_COMPLETE";
            config.GithubTopicArn = githubTopicArn;
            message.Message.SourceTopic = githubTopicArn;
            message.Message.ResourceStatus = status;
            message.Message.SourceTopic = githubTopicArn;
            message.Message.StackId = stackId;
            message.Message.PhysicalResourceId = stackId;

            describeStacksResponse.Stacks[0] = new Stack
            {
                Outputs = new List<Output>()
                {
                    new() { OutputKey = "A", OutputValue = "B"},
                    new() { OutputKey = "Foo", OutputValue = "Bar"},
                },
            };

            var serializedOutputs = @"{""A"":""B"",""Foo"":""Bar""}";

            await handler.Handle(message);

            await tokenInfoRepository
                .Received()
                .FindByRequest(Is(message.Message));

            await cloudformationFactory
                .DidNotReceive()
                .Create(Is(tokenInfo.RoleArn));

            await cloudformationClient
                .DidNotReceive()
                .DescribeStacksAsync(Is<DescribeStacksRequest>(req => req.StackName == stackId));

            await stepFunctions
                .DidNotReceive()
                .SendTaskSuccessAsync(Is<SendTaskSuccessRequest>(req => req.TaskToken == tokenInfo.ClientRequestToken && req.Output == serializedOutputs));

            await sqs
                .DidNotReceive()
                .DeleteMessageAsync(Is<DeleteMessageRequest>(req => req.QueueUrl == tokenInfo.QueueUrl && req.ReceiptHandle == tokenInfo.ReceiptHandle));

            await statusNotifier
                .Received()
                .NotifySuccess(Is(tokenInfo.GithubOwner), Is(tokenInfo.GithubRepo), Is(tokenInfo.GithubRef), Is(message.Message.StackName), Is(tokenInfo.EnvironmentName));
        }
    }
}