extern alias CommonAwsUtils;
extern alias CommonUtils;
extern alias S3AwsUtils;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Amazon.Lambda.ApplicationLoadBalancerEvents;
using Amazon.S3;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

using CommonAwsUtils::Cythral.CloudFormation.AwsUtils;

using NSubstitute;
using NSubstitute.ClearExtensions;

using NUnit.Framework;

using S3AwsUtils::Cythral.CloudFormation.AwsUtils.SimpleStorageService;

using static System.Text.Json.JsonSerializer;

using Handler = Cythral.CloudFormation.ApprovalWebhook.Handler;

namespace Cythral.CloudFormation.Tests.ApprovalWebhook
{
    using ApprovalInfo = CommonUtils::Cythral.CloudFormation.ApprovalInfo;

    public class HandlerTests
    {
        private static AmazonClientFactory<IAmazonStepFunctions> stepFunctionsClientFactory = Substitute.For<AmazonClientFactory<IAmazonStepFunctions>>();
        private static IAmazonStepFunctions stepFunctionsClient = Substitute.For<IAmazonStepFunctions>();
        private static S3GetObjectFacade s3GetObjectFacade = Substitute.For<S3GetObjectFacade>();
        private static AmazonClientFactory<IAmazonS3> s3Factory = Substitute.For<AmazonClientFactory<IAmazonS3>>();
        private static IAmazonS3 s3Client = Substitute.For<IAmazonS3>();

        private const string bucket = "bucket";
        private const string token = "token";
        private const string pipeline = "pipeline";

        private static ApprovalInfo approvalInfo = new ApprovalInfo
        {
            Token = token
        };

        [SetUp]
        public void SetupStepFunctions()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "stepFunctionsClientFactory", stepFunctionsClientFactory);
            stepFunctionsClientFactory.ClearSubstitute();
            stepFunctionsClient.ClearSubstitute();

            stepFunctionsClientFactory.Create().Returns(stepFunctionsClient);
            stepFunctionsClient.SendTaskSuccessAsync(Arg.Any<SendTaskSuccessRequest>()).Returns(new SendTaskSuccessResponse { });
        }

        [SetUp]
        public void SetupS3()
        {
            Environment.SetEnvironmentVariable("STATE_STORE", bucket);
            TestUtils.SetPrivateStaticField(typeof(Handler), "s3Factory", s3Factory);
            TestUtils.SetPrivateStaticField(typeof(Handler), "s3GetObjectFacade", s3GetObjectFacade);
            s3GetObjectFacade.ClearSubstitute();
            s3Client.ClearSubstitute();
            s3Factory.ClearSubstitute();

            s3Factory.Create().Returns(s3Client);
            s3GetObjectFacade.GetObject<ApprovalInfo>(Arg.Any<string>(), Arg.Any<string>()).Returns(approvalInfo);
        }

        [Test]
        public async Task ShouldReturnCorrectStatusCode()
        {
            var tokenHash = "tokenHash";
            var action = "approve";
            var request = new ApplicationLoadBalancerRequest
            {
                QueryStringParameters = new Dictionary<string, string>
                {
                    ["token"] = tokenHash,
                    ["action"] = action,
                    ["pipeline"] = pipeline,
                }
            };

            var response = await Handler.Handle(request);

            Assert.That(response.StatusCode, Is.EqualTo(200));
        }

        [Test]
        public async Task ShouldReturnCorrectStatusDescription()
        {
            var tokenHash = "tokenHash";
            var action = "approve";
            var request = new ApplicationLoadBalancerRequest
            {
                QueryStringParameters = new Dictionary<string, string>
                {
                    ["token"] = tokenHash,
                    ["action"] = action,
                    ["pipeline"] = pipeline,
                }
            };

            var response = await Handler.Handle(request);

            Assert.That(response.StatusDescription, Is.EqualTo("200 OK"));
        }

        [Test]
        public async Task ShouldCreateStepFunctionsClient()
        {
            var tokenHash = "tokenHash";
            var action = "approve";
            var request = new ApplicationLoadBalancerRequest
            {
                QueryStringParameters = new Dictionary<string, string>
                {
                    ["token"] = tokenHash,
                    ["action"] = action,
                    ["pipeline"] = pipeline,
                }
            };

            await Handler.Handle(request);

            await stepFunctionsClientFactory.Received().Create();
        }

        [Test]
        public async Task ShouldCreateS3Client()
        {
            var tokenHash = "tokenHash";
            var action = "approve";
            var request = new ApplicationLoadBalancerRequest
            {
                QueryStringParameters = new Dictionary<string, string>
                {
                    ["token"] = tokenHash,
                    ["action"] = action,
                    ["pipeline"] = pipeline,
                }
            };

            await Handler.Handle(request);

            await s3Factory.Received().Create();
        }

        [Test]
        public async Task ShouldGetApprovalInfo()
        {
            var tokenHash = "tokenHash";
            var action = "approve";
            var serializedOutput = Serialize(new { Action = action });
            var request = new ApplicationLoadBalancerRequest
            {
                QueryStringParameters = new Dictionary<string, string>
                {
                    ["token"] = tokenHash,
                    ["action"] = action,
                    ["pipeline"] = pipeline,
                }
            };

            await Handler.Handle(request);

            await s3GetObjectFacade.Received().GetObject<ApprovalInfo>(Arg.Is(bucket), Arg.Is($"{pipeline}/approvals/{tokenHash}"));
        }

        [Test]
        public async Task ShouldCallSendTaskSuccess()
        {
            var tokenHash = "tokenHash";
            var action = "approve";
            var serializedOutput = Serialize(new { Action = action });
            var request = new ApplicationLoadBalancerRequest
            {
                QueryStringParameters = new Dictionary<string, string>
                {
                    ["token"] = tokenHash,
                    ["action"] = action,
                    ["pipeline"] = pipeline,
                }
            };

            await Handler.Handle(request);

            await stepFunctionsClient.Received().SendTaskSuccessAsync(Arg.Is<SendTaskSuccessRequest>(req =>
                req.TaskToken == token &&
                req.Output == serializedOutput
            ));
        }

        [Test]
        public async Task ShouldDeleteApprovalInfo()
        {
            var tokenHash = "tokenHash";
            var action = "approve";
            var serializedOutput = Serialize(new { Action = action });
            var request = new ApplicationLoadBalancerRequest
            {
                QueryStringParameters = new Dictionary<string, string>
                {
                    ["token"] = tokenHash,
                    ["action"] = action,
                    ["pipeline"] = pipeline,
                }
            };

            await Handler.Handle(request);

            await s3Client.Received().DeleteObjectAsync(Arg.Is(bucket), Arg.Is($"{pipeline}/approvals/{tokenHash}"));
        }
    }
}