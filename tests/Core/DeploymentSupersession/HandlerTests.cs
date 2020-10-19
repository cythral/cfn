using System;
using System.Threading.Tasks;

using Amazon.Lambda.SQSEvents;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

using Cythral.CloudFormation.AwsUtils;
using Cythral.CloudFormation.AwsUtils.SimpleStorageService;
using Cythral.CloudFormation.DeploymentSupersession;

using NSubstitute;
using NSubstitute.ClearExtensions;

using NUnit.Framework;

using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.Tests.DeploymentSupersession
{
    public class HandlerTests
    {
        private static S3GetObjectFacade s3GetObjectFacade = Substitute.For<S3GetObjectFacade>();
        private static RequestFactory requestFactory = Substitute.For<RequestFactory>();
        private static AmazonClientFactory<IAmazonStepFunctions> stepFunctionsClientFactory = Substitute.For<AmazonClientFactory<IAmazonStepFunctions>>();
        private static IAmazonStepFunctions stepFunctionsClient = Substitute.For<IAmazonStepFunctions>();
        private static AmazonClientFactory<IAmazonS3> s3Factory = Substitute.For<AmazonClientFactory<IAmazonS3>>();
        private static IAmazonS3 s3Client = Substitute.For<IAmazonS3>();

        private static string pipeline = "pipeline";
        private static string bucket = "bucket";
        private static string token = "token";
        private static DateTime commitTimestamp = DateTime.Now - TimeSpan.FromHours(1);

        [SetUp]
        public void SetupRequestFactory()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "requestFactory", requestFactory);
            requestFactory.ClearSubstitute();
        }

        [SetUp]
        public void SetupS3()
        {
            Environment.SetEnvironmentVariable("STATE_STORE", bucket);
            TestUtils.SetPrivateStaticField(typeof(Handler), "s3GetObjectFacade", s3GetObjectFacade);
            TestUtils.SetPrivateStaticField(typeof(Handler), "s3Factory", s3Factory);

            s3Factory.Create().Returns(s3Client);
        }

        [SetUp]
        public void SetupStepFunctions()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "stepFunctionsClientFactory", stepFunctionsClientFactory);
            stepFunctionsClientFactory.ClearSubstitute();
            stepFunctionsClientFactory.Create().Returns(stepFunctionsClient);
        }

        private Request CreateRequest()
        {
            var request = new Request
            {
                Pipeline = pipeline,
                Token = token,
                CommitTimestamp = commitTimestamp
            };

            requestFactory.CreateFromSqsEvent(Arg.Any<SQSEvent>()).Returns(request);
            return request;
        }

        [Test]
        public async Task ShouldCreateRequestFromSqsEvent()
        {
            var request = CreateRequest();
            var sqsEvent = Substitute.For<SQSEvent>();

            await Handler.Handle(sqsEvent);

            requestFactory.Received().CreateFromSqsEvent(Arg.Is(sqsEvent));
        }

        [Test]
        public async Task ShouldRetrieveStateFile()
        {
            var request = CreateRequest();
            var sqsEvent = Substitute.For<SQSEvent>();

            await Handler.Handle(sqsEvent);

            await s3GetObjectFacade.Received().TryGetObject<StateInfo>(Arg.Is(bucket), Arg.Is($"{pipeline}/state.json"));
        }

        [Test]
        public async Task ShouldCreateStepFunctionsClient()
        {
            var request = CreateRequest();
            var sqsEvent = Substitute.For<SQSEvent>();

            await Handler.Handle(sqsEvent);

            await stepFunctionsClientFactory.Received().Create();
        }

        [Test]
        public async Task ShouldSendTaskSuccessWithSupersededTrueIfNewerCommitExists()
        {
            var request = CreateRequest();
            var sqsEvent = Substitute.For<SQSEvent>();

            s3GetObjectFacade.TryGetObject<StateInfo>(Arg.Any<string>(), Arg.Any<string>()).Returns(new StateInfo
            {
                LastCommitTimestamp = DateTime.Now
            });

            await Handler.Handle(sqsEvent);

            var expectedOutput = Serialize(new
            {
                Superseded = true
            });

            await stepFunctionsClient.Received().SendTaskSuccessAsync(Arg.Is<SendTaskSuccessRequest>(req =>
                req.TaskToken == token &&
                req.Output == expectedOutput
            ));
        }

        [Test]
        public async Task ShouldSendTaskSuccessWithSupersededFalseIfNewerCommitDoesntExist()
        {
            var request = CreateRequest();
            var sqsEvent = Substitute.For<SQSEvent>();

            s3GetObjectFacade.TryGetObject<StateInfo>(Arg.Any<string>(), Arg.Any<string>()).Returns(new StateInfo
            {
                LastCommitTimestamp = DateTime.Now - TimeSpan.FromHours(2)
            });

            await Handler.Handle(sqsEvent);

            var expectedOutput = Serialize(new
            {
                Superseded = false
            });

            await stepFunctionsClient.Received().SendTaskSuccessAsync(Arg.Is<SendTaskSuccessRequest>(req =>
                req.TaskToken == token &&
                req.Output == expectedOutput
            ));
        }

        [Test]
        public async Task ShouldPutUpdatedStateInfoIfNewerCommitDoesntExist()
        {
            var request = CreateRequest();
            var sqsEvent = Substitute.For<SQSEvent>();

            s3GetObjectFacade.TryGetObject<StateInfo>(Arg.Any<string>(), Arg.Any<string>()).Returns(new StateInfo
            {
                LastCommitTimestamp = DateTime.Now - TimeSpan.FromHours(2)
            });

            await Handler.Handle(sqsEvent);

            var expectedBody = Serialize(new StateInfo
            {
                LastCommitTimestamp = request.CommitTimestamp
            });

            await s3Client.Received().PutObjectAsync(Arg.Is<PutObjectRequest>(req =>
                req.BucketName == bucket &&
                req.Key == $"{pipeline}/state.json" &&
                req.ContentBody == expectedBody
            ));
        }
    }
}
