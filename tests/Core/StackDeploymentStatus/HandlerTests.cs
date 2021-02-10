using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Lambda.SNSEvents;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

using Cythral.CloudFormation.StackDeploymentStatus;
using Cythral.CloudFormation.StackDeploymentStatus.Github;
using Cythral.CloudFormation.StackDeploymentStatus.Request;

using Lambdajection.Core;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ClearExtensions;

using NUnit.Framework;

using Octokit;

using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.Tests.StackDeploymentStatus
{
    using TokenInfo = Cythral.CloudFormation.StackDeploymentStatus.TokenInfo;

    [TestFixture]
    public class HandlerTests
    {
        private const string stackId = "stackId";
        private const string bucket = "bucket";
        private const string key = "key";
        private const string s3Location = "s3://bucket/tokens/key";
        private const string tokenKey = "bucket-key";
        private const string token = "token";
        private const string receiptHandle = "receiptHandle";
        private const string queueUrl = "queueUrl";
        private const string accountId = "1";
        private const string roleArn = "roleArn";
        private const string githubOwner = "githubOwner";
        private const string githubRepo = "githubRepo";
        private const string githubToken = "githubToken";
        private const string githubRef = "githubRef";
        private const string githubTopicArn = "githubTopicArn";
        private const string googleClientId = "googleClientId";
        private const string identityPoolId = "identityPoolId";
        private const string environmentName = "environmentName";
        private const string stackName = "stackName";
        private static Dictionary<string, string> outputs = new Dictionary<string, string>
        {
            ["A"] = "B"
        };

        private static string serializedOutputs = Serialize(outputs);

        private static TokenInfo tokenInfo = new TokenInfo
        {
            ClientRequestToken = token,
            ReceiptHandle = receiptHandle,
            QueueUrl = queueUrl,
            RoleArn = roleArn,
            EnvironmentName = environmentName,
            GithubOwner = githubOwner,
            GithubRepo = githubRepo,
            GithubRef = githubRef,
        };

        public StackDeploymentStatusRequestFactory CreateRequestFactory(StackDeploymentStatusRequest request)
        {
            var factory = Substitute.For<StackDeploymentStatusRequestFactory>();
            factory.CreateFromSnsEvent(Arg.Any<SNSEvent>()).Returns(request);
            return factory;
        }

        public IAmazonStepFunctions CreateStepFunctions()
        {
            var client = Substitute.For<IAmazonStepFunctions>();
            return client;
        }

        public IAmazonSQS CreateSQS()
        {
            var client = Substitute.For<IAmazonSQS>();
            return client;
        }

        public IAmazonCloudFormation CreateCloudFormation()
        {
            var client = Substitute.For<IAmazonCloudFormation>();

            client.DescribeStacksAsync(Arg.Any<DescribeStacksRequest>()).Returns(new DescribeStacksResponse
            {
                Stacks = new List<Stack>
                {
                    new Stack
                    {
                        Outputs = outputs
                                    .Select(entry => new Output { OutputKey = entry.Key, OutputValue = entry.Value })
                                    .ToList()
                    }
                }
            });

            return client;
        }

        public IAwsFactory<IAmazonCloudFormation> CreateCloudFormationFactory(IAmazonCloudFormation client)
        {
            var factory = Substitute.For<IAwsFactory<IAmazonCloudFormation>>();
            factory.Create(Arg.Any<string>()).Returns(client);
            return factory;
        }

        public GithubStatusNotifier CreateStatusNotifier()
        {
            var client = Substitute.For<GithubStatusNotifier>();
            return client;
        }

        public TokenInfoRepository CreateTokenInfoRepository()
        {
            var repository = Substitute.For<TokenInfoRepository>();
            repository.FindByRequest(Arg.Any<StackDeploymentStatusRequest>()).Returns(tokenInfo);
            return repository;
        }

        public IOptions<Config> CreateConfig()
        {
            return Options.Create(new Config
            {
                GithubOwner = githubOwner,
                GithubToken = githubToken,
                GithubTopicArn = githubTopicArn,
            });
        }

        public StackDeploymentStatusRequest CreateRequest(string stackId, string token, string status = "CREATE_COMPLETE", string resourceType = "AWS::CloudFormation::Stack")
        {
            return new StackDeploymentStatusRequest
            {
                StackId = stackId,
                PhysicalResourceId = stackId,
                StackName = stackName,
                ClientRequestToken = token,
                ResourceStatus = status,
                ResourceType = resourceType,
                Namespace = accountId,
            };
        }

        [Test]
        public async Task ShouldDoNothingIfClientRequestTokenIsEmpty()
        {
            var request = CreateRequest(stackId, "", null, "AWS::CloudFormation::Stack");
            var requestFactory = CreateRequestFactory(request);
            var stepFunctions = CreateStepFunctions();
            var sqs = CreateSQS();
            var cloudformationClient = CreateCloudFormation();
            var cloudformationFactory = CreateCloudFormationFactory(cloudformationClient);
            var statusNotifier = CreateStatusNotifier();
            var tokenInfoRepository = CreateTokenInfoRepository();
            var snsEvent = Substitute.For<SNSEvent>();
            var config = CreateConfig();
            var logger = Substitute.For<ILogger<Handler>>();
            var handler = new Handler(requestFactory, stepFunctions, sqs, cloudformationFactory, statusNotifier, tokenInfoRepository, config, logger);

            await handler.Handle(snsEvent);

            await stepFunctions.DidNotReceiveWithAnyArgs().SendTaskFailureAsync(Arg.Any<SendTaskFailureRequest>());
            await stepFunctions.DidNotReceiveWithAnyArgs().SendTaskSuccessAsync(Arg.Any<SendTaskSuccessRequest>());
        }

        [Test]
        public async Task ShouldDoNothingIfStackIdAndPhysicalResourceIdDontMatch()
        {
            var request = CreateRequest(stackId, "", "clientRequestToken", "AWS::CloudFormation::Stack");
            var requestFactory = CreateRequestFactory(request);
            var stepFunctions = CreateStepFunctions();
            var sqs = CreateSQS();
            var cloudformationClient = CreateCloudFormation();
            var cloudformationFactory = CreateCloudFormationFactory(cloudformationClient);
            var statusNotifier = CreateStatusNotifier();
            var tokenInfoRepository = CreateTokenInfoRepository();
            var snsEvent = Substitute.For<SNSEvent>();
            var config = CreateConfig();
            var logger = Substitute.For<ILogger<Handler>>();
            var handler = new Handler(requestFactory, stepFunctions, sqs, cloudformationFactory, statusNotifier, tokenInfoRepository, config, logger);

            request.PhysicalResourceId = Guid.NewGuid().ToString();

            await handler.Handle(snsEvent);

            await stepFunctions.DidNotReceiveWithAnyArgs().SendTaskFailureAsync(Arg.Any<SendTaskFailureRequest>());
            await stepFunctions.DidNotReceiveWithAnyArgs().SendTaskSuccessAsync(Arg.Any<SendTaskSuccessRequest>());
        }

        [Test]
        [TestCase("DELETE_COMPLETE")]
        [TestCase("UPDATE_ROLLBACK_COMPLETE")]
        [TestCase("UPDATE_ROLLBACK_FAILED")]
        [TestCase("CREATE_FAILED")]
        public async Task AllSourcesNotified_OfFailure_WhenSourceTopicIsNotGithub(string status)
        {
            var request = CreateRequest(stackId, tokenKey, status);
            var requestFactory = CreateRequestFactory(request);
            var stepFunctions = CreateStepFunctions();
            var sqs = CreateSQS();
            var cloudformationClient = CreateCloudFormation();
            var cloudformationFactory = CreateCloudFormationFactory(cloudformationClient);
            var statusNotifier = CreateStatusNotifier();
            var tokenInfoRepository = CreateTokenInfoRepository();
            var snsEvent = Substitute.For<SNSEvent>();
            var config = CreateConfig();
            var logger = Substitute.For<ILogger<Handler>>();
            var handler = new Handler(requestFactory, stepFunctions, sqs, cloudformationFactory, statusNotifier, tokenInfoRepository, config, logger);

            await handler.Handle(snsEvent);

            await tokenInfoRepository
                .Received()
                .FindByRequest(Arg.Is(request));

            await stepFunctions
                .Received()
                .SendTaskFailureAsync(Arg.Is<SendTaskFailureRequest>(req => req.TaskToken == token && req.Cause == status));

            await sqs
                .Received()
                .DeleteMessageAsync(Arg.Is<DeleteMessageRequest>(req => req.QueueUrl == queueUrl && req.ReceiptHandle == receiptHandle));

            await statusNotifier
                .Received()
                .NotifyFailure(Arg.Is(githubOwner), Arg.Is(githubRepo), Arg.Is(githubRef), Arg.Is(stackName), Arg.Is(environmentName));
        }

        [Test]
        [TestCase("DELETE_COMPLETE")]
        [TestCase("UPDATE_ROLLBACK_COMPLETE")]
        [TestCase("UPDATE_ROLLBACK_FAILED")]
        [TestCase("CREATE_FAILED")]
        public async Task OnlyGithubNotified_OfFailure_WhenSourceTopicIsGithub(string status)
        {
            var request = CreateRequest(stackId, tokenKey, status);
            var requestFactory = CreateRequestFactory(request);
            var stepFunctions = CreateStepFunctions();
            var sqs = CreateSQS();
            var cloudformationClient = CreateCloudFormation();
            var cloudformationFactory = CreateCloudFormationFactory(cloudformationClient);
            var statusNotifier = CreateStatusNotifier();
            var tokenInfoRepository = CreateTokenInfoRepository();
            var snsEvent = Substitute.For<SNSEvent>();
            var config = CreateConfig();
            var logger = Substitute.For<ILogger<Handler>>();
            var handler = new Handler(requestFactory, stepFunctions, sqs, cloudformationFactory, statusNotifier, tokenInfoRepository, config, logger);

            request.SourceTopic = githubTopicArn;

            await handler.Handle(snsEvent);

            await tokenInfoRepository
                .Received()
                .FindByRequest(Arg.Is(request));

            await stepFunctions
                .DidNotReceive()
                .SendTaskFailureAsync(Arg.Any<SendTaskFailureRequest>());

            await sqs
                .DidNotReceive()
                .DeleteMessageAsync(Arg.Any<DeleteMessageRequest>());

            await statusNotifier
                .Received()
                .NotifyFailure(Arg.Is(githubOwner), Arg.Is(githubRepo), Arg.Is(githubRef), Arg.Is(stackName), Arg.Is(environmentName));
        }

        [Test]
        [TestCase("UPDATE_COMPLETE")]
        [TestCase("CREATE_COMPLETE")]
        public async Task AllSourcesNotified_OfSuccess_WhenSourceTopicIsNotGithub(string status)
        {
            var request = CreateRequest(stackId, tokenKey, status);
            var requestFactory = CreateRequestFactory(request);
            var stepFunctions = CreateStepFunctions();
            var sqs = CreateSQS();
            var cloudformationClient = CreateCloudFormation();
            var cloudformationFactory = CreateCloudFormationFactory(cloudformationClient);
            var statusNotifier = CreateStatusNotifier();
            var tokenInfoRepository = CreateTokenInfoRepository();
            var snsEvent = Substitute.For<SNSEvent>();
            var config = CreateConfig();
            var logger = Substitute.For<ILogger<Handler>>();
            var handler = new Handler(requestFactory, stepFunctions, sqs, cloudformationFactory, statusNotifier, tokenInfoRepository, config, logger);

            await handler.Handle(snsEvent);

            await tokenInfoRepository
                .Received()
                .FindByRequest(Arg.Is(request));

            await cloudformationFactory
                .Received()
                .Create(Arg.Is(roleArn));

            await cloudformationClient
                .Received()
                .DescribeStacksAsync(Arg.Is<DescribeStacksRequest>(req => req.StackName == stackId));

            await stepFunctions
                .Received()
                .SendTaskSuccessAsync(Arg.Is<SendTaskSuccessRequest>(req => req.TaskToken == token && req.Output == serializedOutputs));

            await sqs
                .Received()
                .DeleteMessageAsync(Arg.Is<DeleteMessageRequest>(req => req.QueueUrl == queueUrl && req.ReceiptHandle == receiptHandle));

            await statusNotifier
                .Received()
                .NotifySuccess(Arg.Is(githubOwner), Arg.Is(githubRepo), Arg.Is(githubRef), Arg.Is(stackName), Arg.Is(environmentName));
        }

        [Test]
        [TestCase("UPDATE_COMPLETE")]
        [TestCase("CREATE_COMPLETE")]
        public async Task OnlyGithubNotified_OfSuccess_WhenSourceTopicIsGithub(string status)
        {
            var request = CreateRequest(stackId, tokenKey, status);
            var requestFactory = CreateRequestFactory(request);
            var stepFunctions = CreateStepFunctions();
            var sqs = CreateSQS();
            var cloudformationClient = CreateCloudFormation();
            var cloudformationFactory = CreateCloudFormationFactory(cloudformationClient);
            var statusNotifier = CreateStatusNotifier();
            var tokenInfoRepository = CreateTokenInfoRepository();
            var snsEvent = Substitute.For<SNSEvent>();
            var config = CreateConfig();
            var logger = Substitute.For<ILogger<Handler>>();
            var handler = new Handler(requestFactory, stepFunctions, sqs, cloudformationFactory, statusNotifier, tokenInfoRepository, config, logger);

            request.SourceTopic = githubTopicArn;

            await handler.Handle(snsEvent);

            await tokenInfoRepository
                .Received()
                .FindByRequest(Arg.Is(request));

            await cloudformationFactory
                .DidNotReceive()
                .Create(Arg.Is(roleArn));

            await cloudformationClient
                .DidNotReceive()
                .DescribeStacksAsync(Arg.Is<DescribeStacksRequest>(req => req.StackName == stackId));

            await stepFunctions
                .DidNotReceive()
                .SendTaskSuccessAsync(Arg.Is<SendTaskSuccessRequest>(req => req.TaskToken == token && req.Output == serializedOutputs));

            await sqs
                .DidNotReceive()
                .DeleteMessageAsync(Arg.Is<DeleteMessageRequest>(req => req.QueueUrl == queueUrl && req.ReceiptHandle == receiptHandle));

            await statusNotifier
                .Received()
                .NotifySuccess(Arg.Is(githubOwner), Arg.Is(githubRepo), Arg.Is(githubRef), Arg.Is(stackName), Arg.Is(environmentName));
        }
    }
}