using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Amazon.Lambda.ApplicationLoadBalancerEvents;
using Amazon.S3;

using Cythral.CloudFormation.AwsUtils;
using Cythral.CloudFormation.AwsUtils.CloudFormation;
using Cythral.CloudFormation.GithubWebhook.Exceptions;
using Cythral.CloudFormation.GithubWebhook.Github;
using Cythral.CloudFormation.GithubWebhook.Github.Entities;
using Cythral.CloudFormation.GithubWebhook.Pipelines;
using Cythral.CloudFormation.StackDeployment;

using FluentAssertions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using NUnit.Framework;

using RichardSzalay.MockHttp;

using static System.Text.Json.JsonSerializer;
using static NSubstitute.Arg;

using Handler = Cythral.CloudFormation.GithubWebhook.Handler;

namespace Cythral.CloudFormation.GithubWebhook.Tests
{

    public class HandlerTests
    {
        public class ValidationException : RequestValidationException
        {
            public override HttpStatusCode StatusCode => HttpStatusCode.BadRequest;

            public ValidationException() : base("Validation Exception") { }
        }

        private readonly IOptions<Config> config = Options.Create(new Config
        {

        });


        [Test]
        public async Task Handle_ReturnsBadRequest_IfValidationFails()
        {
            var requestValidator = Substitute.For<RequestValidator>();
            var starter = Substitute.For<PipelineStarter>();
            var deployer = Substitute.For<PipelineDeployer>();
            var logger = Substitute.For<ILogger<Handler>>();
            var handler = new Handler(requestValidator, starter, deployer, config, logger);

            requestValidator
            .When(validator => validator.Validate(Any<ApplicationLoadBalancerRequest>()))
            .Do(x => throw new ValidationException());

            var response = await handler.Handle(new ApplicationLoadBalancerRequest { });

            response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        }

        [Test]
        public async Task Handle_ShouldDeployPipeline_IfOnDefaultBranch_AndCommitMessageDoesntContainSkip()
        {
            var requestValidator = Substitute.For<RequestValidator>();
            var starter = Substitute.For<PipelineStarter>();
            var deployer = Substitute.For<PipelineDeployer>();
            var logger = Substitute.For<ILogger<Handler>>();
            var handler = new Handler(requestValidator, starter, deployer, config, logger);

            var pushEvent = new PushEvent
            {
                Ref = "refs/heads/master",
                Repository = new Repository { DefaultBranch = "master" },
                HeadCommit = new Commit { Message = "" }
            };

            requestValidator.Validate(Any<ApplicationLoadBalancerRequest>()).Returns(pushEvent);

            var response = await handler.Handle(new ApplicationLoadBalancerRequest { });
            await deployer.Received().Deploy(Is(pushEvent));
        }

        [Test]
        public async Task Handle_ShouldNotDeployPipeline_IfOnDefaultBranch_AndCommitMessageContainsSkip()
        {
            var requestValidator = Substitute.For<RequestValidator>();
            var starter = Substitute.For<PipelineStarter>();
            var deployer = Substitute.For<PipelineDeployer>();
            var logger = Substitute.For<ILogger<Handler>>();
            var handler = new Handler(requestValidator, starter, deployer, config, logger);

            var pushEvent = new PushEvent
            {
                Ref = "refs/heads/master",
                Repository = new Repository { DefaultBranch = "master" },
                HeadCommit = new Commit { Message = "[skip meta-ci]" }
            };

            requestValidator.Validate(Any<ApplicationLoadBalancerRequest>()).Returns(pushEvent);

            var response = await handler.Handle(new ApplicationLoadBalancerRequest { });
            await deployer.DidNotReceiveWithAnyArgs().Deploy(null!);
        }

        [Test]
        public async Task Handle_ShouldNotDeployPipeline_IfNotOnDefaultBranch()
        {
            var requestValidator = Substitute.For<RequestValidator>();
            var starter = Substitute.For<PipelineStarter>();
            var deployer = Substitute.For<PipelineDeployer>();
            var logger = Substitute.For<ILogger<Handler>>();
            var handler = new Handler(requestValidator, starter, deployer, config, logger);

            var pushEvent = new PushEvent
            {
                Ref = "refs/heads/master",
                Repository = new Repository { DefaultBranch = "develop" },
                HeadCommit = new Commit { Message = "" }
            };

            requestValidator.Validate(Any<ApplicationLoadBalancerRequest>()).Returns(pushEvent);

            var response = await handler.Handle(new ApplicationLoadBalancerRequest { });
            await deployer.DidNotReceiveWithAnyArgs().Deploy(null!);
        }

        [Test]
        public async Task Handle_ShouldStartPipeline()
        {
            var requestValidator = Substitute.For<RequestValidator>();
            var starter = Substitute.For<PipelineStarter>();
            var deployer = Substitute.For<PipelineDeployer>();
            var logger = Substitute.For<ILogger<Handler>>();
            var handler = new Handler(requestValidator, starter, deployer, config, logger);

            var pushEvent = new PushEvent
            {
                Ref = "refs/heads/master",
                Repository = new Repository { DefaultBranch = "develop" },
                HeadCommit = new Commit { Message = "" }
            };

            requestValidator.Validate(Any<ApplicationLoadBalancerRequest>()).Returns(pushEvent);

            var response = await handler.Handle(new ApplicationLoadBalancerRequest { });
            await starter.Received().StartPipelineIfExists(Is(pushEvent));
        }

        [Test]
        public async Task Handle_ShouldNotStartPipeline_IfCommitMessageContainsSkip()
        {
            var requestValidator = Substitute.For<RequestValidator>();
            var starter = Substitute.For<PipelineStarter>();
            var deployer = Substitute.For<PipelineDeployer>();
            var logger = Substitute.For<ILogger<Handler>>();
            var handler = new Handler(requestValidator, starter, deployer, config, logger);

            var pushEvent = new PushEvent
            {
                Ref = "refs/heads/master",
                Repository = new Repository { DefaultBranch = "develop" },
                HeadCommit = new Commit { Message = "[skip ci]" }
            };

            requestValidator.Validate(Any<ApplicationLoadBalancerRequest>()).Returns(pushEvent);

            var response = await handler.Handle(new ApplicationLoadBalancerRequest { });
            await starter.DidNotReceiveWithAnyArgs().StartPipelineIfExists(null!);
        }
    }
}