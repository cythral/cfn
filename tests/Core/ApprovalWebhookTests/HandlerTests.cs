using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Amazon.Lambda.ApplicationLoadBalancerEvents;
using Amazon.S3;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

using Cythral.CloudFormation.ApprovalWebhook;
using Cythral.CloudFormation.AwsUtils;
using Cythral.CloudFormation.AwsUtils.SimpleStorageService;

using NSubstitute;
using NSubstitute.ClearExtensions;

using FluentAssertions;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

using NUnit.Framework;

using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.Tests.ApprovalWebhook
{
    public class HandlerTests
    {
        private const string bucket = "bucket";
        private const string token = "token";
        private const string pipeline = "pipeline";

        private static ApprovalInfo approvalInfo = new ApprovalInfo
        {
            Token = token
        };

        private static IOptions<Config> config = Options.Create(new Config
        {
            StateStore = bucket,
        });

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

            var stepFunctionsClient = Substitute.For<IAmazonStepFunctions>();
            var s3Client = Substitute.For<IAmazonS3>();
            var s3GetObjectFacade = Substitute.For<S3GetObjectFacade>();
            var logger = Substitute.For<ILogger<Handler>>();
            var handler = new Handler(stepFunctionsClient, s3Client, s3GetObjectFacade, config, logger);

            s3GetObjectFacade.GetObject<ApprovalInfo>(Arg.Any<string>(), Arg.Any<string>()).Returns(approvalInfo);

            var response = await handler.Handle(request);
            response.StatusCode.Should().Be(200);
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

            var stepFunctionsClient = Substitute.For<IAmazonStepFunctions>();
            var s3Client = Substitute.For<IAmazonS3>();
            var s3GetObjectFacade = Substitute.For<S3GetObjectFacade>();
            var logger = Substitute.For<ILogger<Handler>>();
            var handler = new Handler(stepFunctionsClient, s3Client, s3GetObjectFacade, config, logger);

            s3GetObjectFacade.GetObject<ApprovalInfo>(Arg.Any<string>(), Arg.Any<string>()).Returns(approvalInfo);

            var response = await handler.Handle(request);
            response.StatusDescription.Should().Be("200 OK");
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

            var stepFunctionsClient = Substitute.For<IAmazonStepFunctions>();
            var s3Client = Substitute.For<IAmazonS3>();
            var s3GetObjectFacade = Substitute.For<S3GetObjectFacade>();
            var logger = Substitute.For<ILogger<Handler>>();
            var handler = new Handler(stepFunctionsClient, s3Client, s3GetObjectFacade, config, logger);

            s3GetObjectFacade.GetObject<ApprovalInfo>(Arg.Any<string>(), Arg.Any<string>()).Returns(approvalInfo);

            await handler.Handle(request);

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

            var stepFunctionsClient = Substitute.For<IAmazonStepFunctions>();
            var s3Client = Substitute.For<IAmazonS3>();
            var s3GetObjectFacade = Substitute.For<S3GetObjectFacade>();
            var logger = Substitute.For<ILogger<Handler>>();
            var handler = new Handler(stepFunctionsClient, s3Client, s3GetObjectFacade, config, logger);

            s3GetObjectFacade.GetObject<ApprovalInfo>(Arg.Any<string>(), Arg.Any<string>()).Returns(approvalInfo);

            await handler.Handle(request);
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

            var stepFunctionsClient = Substitute.For<IAmazonStepFunctions>();
            var s3Client = Substitute.For<IAmazonS3>();
            var s3GetObjectFacade = Substitute.For<S3GetObjectFacade>();
            var logger = Substitute.For<ILogger<Handler>>();
            var handler = new Handler(stepFunctionsClient, s3Client, s3GetObjectFacade, config, logger);

            s3GetObjectFacade.GetObject<ApprovalInfo>(Arg.Any<string>(), Arg.Any<string>()).Returns(approvalInfo);

            await handler.Handle(request);
            await s3Client.Received().DeleteObjectAsync(Arg.Is(bucket), Arg.Is($"{pipeline}/approvals/{tokenHash}"));
        }
    }
}