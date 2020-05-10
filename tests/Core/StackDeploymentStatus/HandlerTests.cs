using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.ElasticLoadBalancingV2;
using Amazon.ElasticLoadBalancingV2.Model;
using Amazon.Lambda.SNSEvents;

using Cythral.CloudFormation.Aws;
using Cythral.CloudFormation.Entities;
using Cythral.CloudFormation.Events;
using Cythral.CloudFormation.StackDeploymentStatus;
using Cythral.CloudFormation.UpdateTargets.DnsResolver;
using Cythral.CloudFormation.StackDeploymentStatus.Request;

using NSubstitute;
using NSubstitute.ClearExtensions;

using NUnit.Framework;

using static Amazon.ElasticLoadBalancingV2.TargetHealthStateEnum;
using static System.Text.Json.JsonSerializer;

using SNSRecord = Amazon.Lambda.SNSEvents.SNSEvent.SNSRecord;
using SNSMessage = Amazon.Lambda.SNSEvents.SNSEvent.SNSMessage;

namespace Cythral.CloudFormation.Tests.StackDeploymentStatus
{
    public class HandlerTests
    {
        private static StackDeploymentStatusRequestFactory requestFactory = Substitute.For<StackDeploymentStatusRequestFactory>();
        private static StepFunctionsClientFactory stepFunctionsClientFactory = Substitute.For<StepFunctionsClientFactory>();
        private static IAmazonStepFunctions stepFunctionsClient = Substitute.For<IAmazonStepFunctions>();
        private static S3GetObjectFacade s3GetObjectFacade = Substitute.For<S3GetObjectFacade>();
        private static SqsFactory sqsFactory = Substitute.For<SqsFactory>();
        private static IAmazonSQS sqsClient = Substitute.For<IAmazonSQS>();
        private const string stackId = "stackId";
        private const string bucket = "bucket";
        private const string key = "key";
        private const string s3Location = "s3://bucket/tokens/key";
        private const string tokenKey = "bucket-key";
        private const string token = "token";
        private const string receiptHandle = "receiptHandle";
        private const string queueUrl = "queueUrl";
        private static string tokenInfo = Serialize(new TokenInfo
        {
            ClientRequestToken = token,
            ReceiptHandle = receiptHandle,
            QueueUrl = queueUrl,
        });


        [SetUp]
        public void SetupRequestFactory()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "requestFactory", requestFactory);
            requestFactory.ClearReceivedCalls();
        }

        [SetUp]
        public void SetupStepFunctionsClientFactory()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "stepFunctionsClientFactory", stepFunctionsClientFactory);
            stepFunctionsClientFactory.ClearReceivedCalls();
        }

        [SetUp]
        public void SetupStepFunctionsClient()
        {
            stepFunctionsClientFactory.Create().Returns(stepFunctionsClient);
            stepFunctionsClient.ClearReceivedCalls();
        }

        [SetUp]
        public void SetupS3GetObjectFacade()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "s3GetObjectFacade", s3GetObjectFacade);
            s3GetObjectFacade.ClearReceivedCalls();
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

        private StackDeploymentStatusRequest CreateRequest(string stackId, string token, string status = "CREATE_COMPLETE", string resourceType = "AWS::CloudFormation::Stack")
        {
            var request = new StackDeploymentStatusRequest
            {
                StackId = stackId,
                ClientRequestToken = token,
                ResourceStatus = status,
                ResourceType = resourceType
            };

            requestFactory.CreateFromSnsEvent(Arg.Any<SNSEvent>()).Returns(request);
            return request;
        }

        [Test]
        public async Task RequestIsParsed()
        {
            var request = CreateRequest(stackId, tokenKey);
            var snsEvent = Substitute.For<SNSEvent>();

            await Handler.Handle(snsEvent);

            requestFactory.Received().CreateFromSnsEvent(Arg.Is(snsEvent));
        }

        [Test]
        public async Task StepFunctionsClientIsCreated()
        {
            var request = CreateRequest(stackId, tokenKey);
            var snsEvent = Substitute.For<SNSEvent>();

            await Handler.Handle(snsEvent);

            stepFunctionsClientFactory.Received().Create();
        }

        [Test]
        public async Task ShouldDoNothingIfResourceTypeIsNotStack()
        {
            var request = CreateRequest(stackId, tokenKey, null, "AWS::S3::Bucket");
            var snsEvent = Substitute.For<SNSEvent>();

            await Handler.Handle(snsEvent);

            await s3GetObjectFacade.DidNotReceiveWithAnyArgs().GetObject(Arg.Any<string>());
            await stepFunctionsClient.DidNotReceiveWithAnyArgs().SendTaskFailureAsync(Arg.Any<SendTaskFailureRequest>());
            await stepFunctionsClient.DidNotReceiveWithAnyArgs().SendTaskSuccessAsync(Arg.Any<SendTaskSuccessRequest>());
        }

        [Test]
        public async Task SendTaskFailureIsCalledIfStatusEndsWithRollbackComplete()
        {
            var status = "UPDATE_ROLLBACK_COMPLETE";
            var request = CreateRequest(stackId, tokenKey, status);
            var snsEvent = Substitute.For<SNSEvent>();

            await Handler.Handle(snsEvent);

            await s3GetObjectFacade.Received().GetObject(Arg.Is(s3Location));
            await stepFunctionsClient
                .Received()
                .SendTaskFailureAsync(Arg.Is<SendTaskFailureRequest>(req => req.TaskToken == token && req.Cause == status));
        }

        [Test]
        public async Task SendTaskFailureIsCalledIfStatusEndsWithFailed()
        {
            var status = "CREATE_FAILED";
            var request = CreateRequest(stackId, tokenKey, status);
            var snsEvent = Substitute.For<SNSEvent>();

            await Handler.Handle(snsEvent);

            await s3GetObjectFacade.Received().GetObject(Arg.Is(s3Location));
            await stepFunctionsClient
                .Received()
                .SendTaskFailureAsync(Arg.Is<SendTaskFailureRequest>(req => req.TaskToken == token && req.Cause == status));

            await sqsFactory.Received().Create();
            await sqsClient
                .Received()
                .DeleteMessageAsync(Arg.Is<DeleteMessageRequest>(req => req.QueueUrl == queueUrl && req.ReceiptHandle == receiptHandle));
        }

        [Test]
        public async Task ShouldNotSendFailureIfTokenNotProvided()
        {
            var status = "CREATE_FAILED";
            var request = CreateRequest(stackId, "", status);
            var serializedRequest = Serialize(request);
            var snsEvent = Substitute.For<SNSEvent>();

            await Handler.Handle(snsEvent);

            await s3GetObjectFacade.DidNotReceive().GetObject(Arg.Is(s3Location));
            await stepFunctionsClient
                .DidNotReceive()
                .SendTaskFailureAsync(Arg.Any<SendTaskFailureRequest>());

            await sqsFactory.DidNotReceive().Create();
            await sqsClient.DidNotReceive().DeleteMessageAsync(Arg.Any<DeleteMessageRequest>());
        }

        [Test]
        public async Task SendTaskSuccessIfStatusEndsWithComplete()
        {
            var status = "CREATE_COMPLETE";
            var request = CreateRequest(stackId, tokenKey, status);
            var serializedRequest = Serialize(request);
            var snsEvent = Substitute.For<SNSEvent>();

            await Handler.Handle(snsEvent);

            await s3GetObjectFacade.Received().GetObject(Arg.Is(s3Location));
            await stepFunctionsClient
                .Received()
                .SendTaskSuccessAsync(Arg.Is<SendTaskSuccessRequest>(req => req.TaskToken == token && req.Output == serializedRequest));

            await sqsFactory.Received().Create();
            await sqsClient
                .Received()
                .DeleteMessageAsync(Arg.Is<DeleteMessageRequest>(req => req.QueueUrl == queueUrl && req.ReceiptHandle == receiptHandle));
        }

        [Test]
        public async Task ShouldNotSendSuccessIfTokenNotProvided()
        {
            var status = "CREATE_COMPLETE";
            var request = CreateRequest(stackId, "", status);
            var serializedRequest = Serialize(request);
            var snsEvent = Substitute.For<SNSEvent>();

            await Handler.Handle(snsEvent);

            await s3GetObjectFacade.DidNotReceive().GetObject(Arg.Is(s3Location));
            await stepFunctionsClient
                .DidNotReceive()
                .SendTaskSuccessAsync(Arg.Any<SendTaskSuccessRequest>());

            await sqsFactory.DidNotReceive().Create();
            await sqsClient.DidNotReceive().DeleteMessageAsync(Arg.Any<DeleteMessageRequest>());
        }
    }
}