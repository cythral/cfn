using System;
using System.Threading.Tasks;

using Amazon.Lambda.SQSEvents;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

using AutoFixture.AutoNSubstitute;
using AutoFixture.NUnit3;

using Cythral.CloudFormation.AwsUtils;
using Cythral.CloudFormation.AwsUtils.SimpleStorageService;
using Cythral.CloudFormation.DeploymentSupersession;

using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ClearExtensions;

using NUnit.Framework;

using static System.Text.Json.JsonSerializer;
using static NSubstitute.Arg;

namespace Cythral.CloudFormation.Tests.DeploymentSupersession
{
    public class HandlerTests
    {

        [Test, Auto]
        public async Task ShouldCreateRequestFromSqsEvent(
            Request request,
            [Substitute] SQSEvent sqsEvent,
            [Frozen, Options] IOptions<Config> options,
            [Frozen, Substitute] RequestFactory requestFactory,
            [Target] Handler handler
        )
        {
            requestFactory.CreateFromSqsEvent(Any<SQSEvent>()).Returns(request);

            await handler.Handle(sqsEvent);

            requestFactory.Received().CreateFromSqsEvent(Is(sqsEvent));
        }

        [Test, Auto]
        public async Task ShouldRetrieveStateFile(
            string bucket,
            string pipeline,
            Request request,
            [Substitute] SQSEvent sqsEvent,
            [Frozen, Options] IOptions<Config> options,
            [Frozen, Substitute] RequestFactory requestFactory,
            [Frozen, Substitute] S3GetObjectFacade s3GetObjectFacade,
            [Target] Handler handler
        )
        {
            options.Value.StateStore = bucket;
            request.Pipeline = pipeline;
            requestFactory.CreateFromSqsEvent(Arg.Any<SQSEvent>()).Returns(request);

            await handler.Handle(sqsEvent);

            await s3GetObjectFacade.Received().TryGetObject<StateInfo>(Is(bucket), Is($"{pipeline}/state.json"));
        }

        [Test, Auto]
        public async Task ShouldSendTaskSuccessWithSupersededTrueIfNewerCommitExists(
            string token,
            Request request,
            [Substitute] SQSEvent sqsEvent,
            [Frozen, Substitute] IAmazonStepFunctions stepFunctionsClient,
            [Frozen, Options] IOptions<Config> options,
            [Frozen, Substitute] RequestFactory requestFactory,
            [Frozen, Substitute] S3GetObjectFacade s3GetObjectFacade,
            [Target] Handler handler
        )
        {
            request.Token = token;
            request.CommitTimestamp = DateTime.Now - TimeSpan.FromHours(1);
            requestFactory.CreateFromSqsEvent(Any<SQSEvent>()).Returns(request);
            s3GetObjectFacade.TryGetObject<StateInfo>(string.Empty, string.Empty).ReturnsForAnyArgs(new StateInfo
            {
                LastCommitTimestamp = DateTime.Now
            });

            await handler.Handle(sqsEvent);

            var expectedOutput = Serialize(new
            {
                Superseded = true
            });

            await stepFunctionsClient.Received().SendTaskSuccessAsync(Is<SendTaskSuccessRequest>(req =>
                req.TaskToken == token &&
                req.Output == expectedOutput
            ));
        }

        [Test, Auto]
        public async Task ShouldSendTaskSuccessWithSupersededFalseIfNewerCommitDoesntExist(
            string token,
            Request request,
            [Substitute] SQSEvent sqsEvent,
            [Frozen, Substitute] IAmazonStepFunctions stepFunctionsClient,
            [Frozen, Options] IOptions<Config> options,
            [Frozen, Substitute] RequestFactory requestFactory,
            [Frozen, Substitute] S3GetObjectFacade s3GetObjectFacade,
            [Target] Handler handler
        )
        {
            request.Token = token;
            request.CommitTimestamp = DateTime.Now - TimeSpan.FromHours(1);
            requestFactory.CreateFromSqsEvent(Any<SQSEvent>()).Returns(request);
            s3GetObjectFacade.TryGetObject<StateInfo>(Arg.Any<string>(), Arg.Any<string>()).Returns(new StateInfo
            {
                LastCommitTimestamp = DateTime.Now - TimeSpan.FromHours(2)
            });

            await handler.Handle(sqsEvent);

            var expectedOutput = Serialize(new
            {
                Superseded = false
            });

            await stepFunctionsClient.Received().SendTaskSuccessAsync(Arg.Is<SendTaskSuccessRequest>(req =>
                req.TaskToken == token &&
                req.Output == expectedOutput
            ));
        }

        [Test, Auto]
        public async Task ShouldPutUpdatedStateInfoIfNewerCommitDoesntExist(
            string bucket,
            string pipeline,
            string token,
            Request request,
            [Substitute] SQSEvent sqsEvent,
            [Frozen, Substitute] IAmazonS3 s3Client,
            [Frozen, Options] IOptions<Config> options,
            [Frozen, Substitute] RequestFactory requestFactory,
            [Frozen, Substitute] S3GetObjectFacade s3GetObjectFacade,
            [Target] Handler handler
        )
        {
            options.Value.StateStore = bucket;
            request.Pipeline = pipeline;
            request.Token = token;
            request.CommitTimestamp = DateTime.Now - TimeSpan.FromHours(1);

            requestFactory.CreateFromSqsEvent(Any<SQSEvent>()).Returns(request);
            s3GetObjectFacade.TryGetObject<StateInfo>(Arg.Any<string>(), Arg.Any<string>()).Returns(new StateInfo
            {
                LastCommitTimestamp = DateTime.Now - TimeSpan.FromHours(2)
            });

            await handler.Handle(sqsEvent);

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
