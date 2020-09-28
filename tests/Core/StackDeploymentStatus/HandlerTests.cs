extern alias CommonAwsUtils;
extern alias CommonUtils;
extern alias GithubUtils;
extern alias S3AwsUtils;

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

using CommonAwsUtils::Cythral.CloudFormation.AwsUtils;

using Cythral.CloudFormation.StackDeploymentStatus;
using Cythral.CloudFormation.StackDeploymentStatus.Request;

using GithubUtils::Cythral.CloudFormation.GithubUtils;

using NSubstitute;
using NSubstitute.ClearExtensions;

using NUnit.Framework;

using Octokit;

using S3AwsUtils::Cythral.CloudFormation.AwsUtils.SimpleStorageService;

using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.Tests.StackDeploymentStatus
{
    using TokenInfo = CommonUtils::Cythral.CloudFormation.TokenInfo;
    
    [TestFixture]
    public class HandlerTests
    {
        private static StackDeploymentStatusRequestFactory requestFactory = Substitute.For<StackDeploymentStatusRequestFactory>();
        private static AmazonClientFactory<IAmazonStepFunctions> stepFunctionsClientFactory = Substitute.For<AmazonClientFactory<IAmazonStepFunctions>>();
        private static IAmazonStepFunctions stepFunctionsClient = Substitute.For<IAmazonStepFunctions>();
        private static S3GetObjectFacade s3GetObjectFacade = Substitute.For<S3GetObjectFacade>();
        private static AmazonClientFactory<IAmazonSQS> sqsFactory = Substitute.For<AmazonClientFactory<IAmazonSQS>>();
        private static IAmazonSQS sqsClient = Substitute.For<IAmazonSQS>();
        private static AmazonClientFactory<IAmazonCloudFormation> cloudformationFactory = Substitute.For<AmazonClientFactory<IAmazonCloudFormation>>();
        private static IAmazonCloudFormation cloudFormationClient = Substitute.For<IAmazonCloudFormation>();
        private static PutCommitStatusFacade putCommitStatusFacade = Substitute.For<PutCommitStatusFacade>();
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
        private const string githubRef = "githubRef";
        private const string googleClientId = "googleClientId";
        private const string identityPoolId = "identityPoolId";
        private const string environmentName = "environmentName";
        private const string stackName = "stackName";
        private static Dictionary<string, string> outputs = new Dictionary<string, string>
        {
            ["A"] = "B"
        };
        private static string tokenInfo = Serialize(new TokenInfo
        {
            ClientRequestToken = token,
            ReceiptHandle = receiptHandle,
            QueueUrl = queueUrl,
            RoleArn = roleArn,
            EnvironmentName = environmentName,
            GithubOwner = githubOwner,
            GithubRepo = githubRepo,
            GithubRef = githubRef,
        });

        [SetUp]
        public void SetupRequestFactory()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "requestFactory", requestFactory);
            requestFactory.ClearSubstitute();
        }

        [SetUp]
        public void SetupStepFunctionsClientFactory()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "stepFunctionsClientFactory", stepFunctionsClientFactory);
            stepFunctionsClientFactory.ClearSubstitute();
        }

        [SetUp]
        public void SetupStepFunctionsClient()
        {
            stepFunctionsClient.ClearSubstitute();
            stepFunctionsClientFactory.Create().Returns(stepFunctionsClient);
        }

        [SetUp]
        public void SetupS3GetObjectFacade()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "s3GetObjectFacade", s3GetObjectFacade);
            s3GetObjectFacade.ClearSubstitute();
            s3GetObjectFacade.GetObject(Arg.Any<string>()).Returns(tokenInfo);
        }

        [SetUp]
        public void SetupSQS()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "sqsFactory", sqsFactory);
            sqsFactory.ClearSubstitute();
            sqsFactory.Create().Returns(sqsClient);

            sqsClient.ClearSubstitute();
        }

        [SetUp]
        public void SetupCloudFormation()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "cloudformationFactory", cloudformationFactory);
            cloudFormationClient.ClearSubstitute();
            cloudformationFactory.ClearSubstitute();
            cloudformationFactory.Create(Arg.Any<string>()).Returns(cloudFormationClient);

            cloudFormationClient.DescribeStacksAsync(Arg.Any<DescribeStacksRequest>()).Returns(new DescribeStacksResponse
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
        }

        [SetUp]
        public void SetupPutCommitStatusFacade()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "putCommitStatusFacade", putCommitStatusFacade);
            putCommitStatusFacade.ClearSubstitute();
        }

        [SetUp]
        public void SetupEnvironment()
        {
            Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1");
        }

        private static StackDeploymentStatusRequest CreateRequest(string stackId, string token, string status = "CREATE_COMPLETE", string resourceType = "AWS::CloudFormation::Stack")
        {
            var request = new StackDeploymentStatusRequest
            {
                StackId = stackId,
                StackName = stackName,
                ClientRequestToken = token,
                ResourceStatus = status,
                ResourceType = resourceType,
                Namespace = accountId,
            };

            requestFactory.CreateFromSnsEvent(Arg.Any<SNSEvent>()).Returns(request);
            return request;
        }

        [Test]
        public async Task ShouldDoNothingIfResourceIsNotAStack()
        {
            var request = CreateRequest(stackId, tokenKey, null, "AWS::S3::Bucket");
            var snsEvent = Substitute.For<SNSEvent>();

            await Handler.Handle(snsEvent);

            await s3GetObjectFacade.DidNotReceiveWithAnyArgs().GetObject(Arg.Any<string>());
            await stepFunctionsClient.DidNotReceiveWithAnyArgs().SendTaskFailureAsync(Arg.Any<SendTaskFailureRequest>());
            await stepFunctionsClient.DidNotReceiveWithAnyArgs().SendTaskSuccessAsync(Arg.Any<SendTaskSuccessRequest>());
        }

        public class StatusIsDeleteComplete
        {
            private string status = "DELETE_COMPLETE";
            private StackDeploymentStatusRequest request;
            private SNSEvent snsEvent;

            [SetUp]
            public void SetupRequest()
            {
                s3GetObjectFacade.ClearReceivedCalls();
                stepFunctionsClient.ClearReceivedCalls();
                sqsFactory.ClearReceivedCalls();
                sqsClient.ClearReceivedCalls();
                putCommitStatusFacade.ClearReceivedCalls();
                cloudFormationClient.ClearReceivedCalls();
                cloudformationFactory.ClearReceivedCalls();

                request = CreateRequest(stackId, tokenKey, status);
                snsEvent = Substitute.For<SNSEvent>();
            }

            [Test]
            public async Task FailureIsSent()
            {
                await Handler.Handle(snsEvent);

                await s3GetObjectFacade.Received().GetObject(Arg.Is(s3Location));
                await stepFunctionsClient
                    .Received()
                    .SendTaskFailureAsync(Arg.Is<SendTaskFailureRequest>(req => req.TaskToken == token && req.Cause == status));
            }
        }


        public class StatusEndsWithRollbackComplete
        {
            private string status = "UPDATE_ROLLBACK_COMPLETE";
            private StackDeploymentStatusRequest request;
            private SNSEvent snsEvent;

            [SetUp]
            public void SetupRequest()
            {
                s3GetObjectFacade.ClearReceivedCalls();
                stepFunctionsClient.ClearReceivedCalls();
                sqsFactory.ClearReceivedCalls();
                sqsClient.ClearReceivedCalls();
                putCommitStatusFacade.ClearReceivedCalls();
                cloudFormationClient.ClearReceivedCalls();
                cloudformationFactory.ClearReceivedCalls();

                request = CreateRequest(stackId, tokenKey, status);
                snsEvent = Substitute.For<SNSEvent>();
            }

            [Test]
            public async Task FailureIsSent()
            {
                await Handler.Handle(snsEvent);

                await s3GetObjectFacade.Received().GetObject(Arg.Is(s3Location));
                await stepFunctionsClient
                    .Received()
                    .SendTaskFailureAsync(Arg.Is<SendTaskFailureRequest>(req => req.TaskToken == token && req.Cause == status));
            }
        }

        public class StatusEndsWithFailedAndTokenNotProvided
        {
            private string status = "CREATE_FAILED";
            private StackDeploymentStatusRequest request;
            private SNSEvent snsEvent;

            [SetUp]
            public void SetupRequest()
            {
                s3GetObjectFacade.ClearReceivedCalls();
                stepFunctionsClient.ClearReceivedCalls();
                sqsFactory.ClearReceivedCalls();
                sqsClient.ClearReceivedCalls();
                putCommitStatusFacade.ClearReceivedCalls();
                cloudFormationClient.ClearReceivedCalls();
                cloudformationFactory.ClearReceivedCalls();

                request = CreateRequest(stackId, "", status);
                snsEvent = Substitute.For<SNSEvent>();
            }

            [Test]
            public async Task SendsFailure()
            {
                await Handler.Handle(snsEvent);

                await s3GetObjectFacade.DidNotReceive().GetObject(Arg.Is(s3Location));
                await stepFunctionsClient
                    .DidNotReceive()
                    .SendTaskFailureAsync(Arg.Is<SendTaskFailureRequest>(req => req.TaskToken == token && req.Cause == status));
            }

            [Test]
            public async Task DeletesMessageFromQueue()
            {
                await Handler.Handle(snsEvent);

                await sqsFactory.DidNotReceive().Create();
                await sqsClient
                    .DidNotReceive()
                    .DeleteMessageAsync(Arg.Is<DeleteMessageRequest>(req => req.QueueUrl == queueUrl && req.ReceiptHandle == receiptHandle));
            }
        }

        public class StatusEndsWithFailedAndTokenProvided
        {
            private string status = "CREATE_FAILED";
            private StackDeploymentStatusRequest request;
            private SNSEvent snsEvent;

            [SetUp]
            public void Setup()
            {
                s3GetObjectFacade.ClearReceivedCalls();
                stepFunctionsClient.ClearReceivedCalls();
                sqsFactory.ClearReceivedCalls();
                sqsClient.ClearReceivedCalls();
                putCommitStatusFacade.ClearReceivedCalls();

                request = CreateRequest(stackId, tokenKey, status);
                snsEvent = Substitute.For<SNSEvent>();
            }

            [Test]
            public async Task SendsFailure()
            {
                await Handler.Handle(snsEvent);

                await s3GetObjectFacade.Received().GetObject(Arg.Is(s3Location));
                await stepFunctionsClient
                    .Received()
                    .SendTaskFailureAsync(Arg.Is<SendTaskFailureRequest>(req => req.TaskToken == token && req.Cause == status));
            }

            [Test]
            public async Task DeletesMessageFromQueue()
            {
                await Handler.Handle(snsEvent);

                await sqsFactory.Received().Create();
                await sqsClient
                    .Received()
                    .DeleteMessageAsync(Arg.Is<DeleteMessageRequest>(req => req.QueueUrl == queueUrl && req.ReceiptHandle == receiptHandle));
            }

            [Test]
            public async Task PutsFailureCommitStatus()
            {
                await Handler.Handle(snsEvent);

                await putCommitStatusFacade.Received().PutCommitStatus(Arg.Is<PutCommitStatusRequest>(req =>
                    req.CommitState == CommitState.Failure &&
                    req.ServiceName == "AWS CloudFormation" &&
                    req.ProjectName == stackName &&
                    req.DetailsUrl == $"https://console.aws.amazon.com/cloudformation/home?region=us-east-1#/stacks/stackinfo?filteringText=&filteringStatus=active&viewNested=true&hideStacks=false&stackId={stackName}" &&
                    req.EnvironmentName == environmentName &&
                    req.GithubOwner == githubOwner &&
                    req.GithubRepo == githubRepo &&
                    req.GithubRef == githubRef
                ));
            }
        }

        public class StatusEndsWithComplete
        {
            private string status = "CREATE_COMPLETE";
            private StackDeploymentStatusRequest request;
            private string serializedRequest;
            private string serializedOutput;
            private SNSEvent snsEvent;

            [SetUp]
            public void SetupRequest()
            {
                s3GetObjectFacade.ClearReceivedCalls();
                stepFunctionsClient.ClearReceivedCalls();
                sqsFactory.ClearReceivedCalls();
                sqsClient.ClearReceivedCalls();
                putCommitStatusFacade.ClearReceivedCalls();
                cloudFormationClient.ClearReceivedCalls();
                cloudformationFactory.ClearReceivedCalls();

                request = CreateRequest(stackId, tokenKey, status);
                serializedRequest = Serialize(request);
                snsEvent = Substitute.For<SNSEvent>();
                serializedOutput = Serialize(outputs);
            }

            [Test]
            public async Task StacksAreDescribed()
            {
                await Handler.Handle(snsEvent);

                await cloudformationFactory.Received().Create(Arg.Is(roleArn));
                await cloudFormationClient.Received().DescribeStacksAsync(Arg.Is<DescribeStacksRequest>(req =>
                    req.StackName == stackId
                ));
            }

            [Test]
            public async Task SendTaskSuccessIsCalled()
            {
                await Handler.Handle(snsEvent);

                await s3GetObjectFacade.Received().GetObject(Arg.Is(s3Location));
                await stepFunctionsClient
                    .Received()
                    .SendTaskSuccessAsync(Arg.Is<SendTaskSuccessRequest>(req => req.TaskToken == token && req.Output == serializedOutput));
            }

            [Test]
            public async Task MessageIsDeletedFromQueue()
            {
                await Handler.Handle(snsEvent);

                await sqsFactory.Received().Create();
                await sqsClient
                    .Received()
                    .DeleteMessageAsync(Arg.Is<DeleteMessageRequest>(req => req.QueueUrl == queueUrl && req.ReceiptHandle == receiptHandle));
            }

        }

        public class StatusEndsWithCompleteAndTokenNotProvided
        {
            private string status = "CREATE_COMPLETE";
            private StackDeploymentStatusRequest request;
            private string serializedRequest;
            private string serializedOutput;
            private SNSEvent snsEvent;

            [SetUp]
            public void SetupRequest()
            {
                s3GetObjectFacade.ClearReceivedCalls();
                stepFunctionsClient.ClearReceivedCalls();
                sqsFactory.ClearReceivedCalls();
                sqsClient.ClearReceivedCalls();
                putCommitStatusFacade.ClearReceivedCalls();
                cloudFormationClient.ClearReceivedCalls();
                cloudformationFactory.ClearReceivedCalls();

                request = CreateRequest(stackId, "", status);
                serializedRequest = Serialize(request);
                snsEvent = Substitute.For<SNSEvent>();
                serializedOutput = Serialize(outputs);
            }



            [Test]
            public async Task SendTaskSuccessIsNotCalled()
            {
                await Handler.Handle(snsEvent);

                await s3GetObjectFacade.DidNotReceive().GetObject(Arg.Is(s3Location));
                await stepFunctionsClient
                    .DidNotReceive()
                    .SendTaskSuccessAsync(Arg.Is<SendTaskSuccessRequest>(req => req.TaskToken == token && req.Output == serializedOutput));
            }

            [Test]
            public async Task MessageIsDeletedFromQueue()
            {
                await Handler.Handle(snsEvent);

                await sqsFactory.DidNotReceive().Create();
                await sqsClient
                    .DidNotReceive()
                    .DeleteMessageAsync(Arg.Is<DeleteMessageRequest>(req => req.QueueUrl == queueUrl && req.ReceiptHandle == receiptHandle));
            }
        }
    }
}