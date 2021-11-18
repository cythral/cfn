using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Amazon.Lambda.ApplicationLoadBalancerEvents;
using Amazon.S3;

using Cythral.CloudFormation.AwsUtils;
using Cythral.CloudFormation.GithubWebhook.Exceptions;
using Cythral.CloudFormation.GithubWebhook.Github;
using Cythral.CloudFormation.GithubWebhook.Github.Entities;
using Cythral.CloudFormation.GithubWebhook.Pipelines;

using FluentAssertions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using NUnit.Framework;

using RichardSzalay.MockHttp;

using static System.Text.Json.JsonSerializer;
using static NSubstitute.Arg;

namespace Cythral.CloudFormation.GithubWebhook.Tests
{
    public class HandlerTests
    {

        private const string repoName = "test";
        private const string sha = "sha";

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
            var statusNotifier = Substitute.For<GithubStatusNotifier>();
            var commitMessageFetcher = Substitute.For<GithubCommitMessageFetcher>();
            var logger = Substitute.For<ILogger<Handler>>();
            var handler = new Handler(requestValidator, starter, deployer, statusNotifier, commitMessageFetcher, config, logger);

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
            var statusNotifier = Substitute.For<GithubStatusNotifier>();
            var commitMessageFetcher = Substitute.For<GithubCommitMessageFetcher>();
            var logger = Substitute.For<ILogger<Handler>>();
            var handler = new Handler(requestValidator, starter, deployer, statusNotifier, commitMessageFetcher, config, logger);

            var pushEvent = new PushEvent
            {
                Ref = "refs/heads/master",
                Repository = new Repository { Name = repoName, DefaultBranch = "master" },
                HeadCommit = new Commit { Id = sha, Message = "" }
            };

            requestValidator.Validate(Any<ApplicationLoadBalancerRequest>()).Returns(pushEvent);

            var response = await handler.Handle(new ApplicationLoadBalancerRequest { });
            await deployer.Received().Deploy(Is(pushEvent));
        }

        [Test]
        public async Task Handle_ShouldDeployPipeline_IfOnTag()
        {
            var requestValidator = Substitute.For<RequestValidator>();
            var starter = Substitute.For<PipelineStarter>();
            var deployer = Substitute.For<PipelineDeployer>();
            var statusNotifier = Substitute.For<GithubStatusNotifier>();
            var commitMessageFetcher = Substitute.For<GithubCommitMessageFetcher>();
            var logger = Substitute.For<ILogger<Handler>>();
            var handler = new Handler(requestValidator, starter, deployer, statusNotifier, commitMessageFetcher, config, logger);

            var message = "Release v0.1.0";
            commitMessageFetcher.FetchCommitMessage(Any<GithubEvent>()).Returns(message);

            var pushEvent = new PushEvent
            {
                Ref = "refs/tags/v0.1.0",
                Repository = new Repository { Name = repoName, DefaultBranch = "master" },
                HeadCommit = new Commit { Id = sha }
            };

            requestValidator.Validate(Any<ApplicationLoadBalancerRequest>()).Returns(pushEvent);

            var response = await handler.Handle(new ApplicationLoadBalancerRequest { });
            await deployer.Received().Deploy(Is(pushEvent));
            await commitMessageFetcher.Received().FetchCommitMessage(Is(pushEvent));
        }

        [Test]
        public async Task Handle_ShouldNotDeployPipeline_IfOnDefaultBranch_AndCommitMessageContainsSkip()
        {
            var requestValidator = Substitute.For<RequestValidator>();
            var starter = Substitute.For<PipelineStarter>();
            var deployer = Substitute.For<PipelineDeployer>();
            var statusNotifier = Substitute.For<GithubStatusNotifier>();
            var commitMessageFetcher = Substitute.For<GithubCommitMessageFetcher>();
            var logger = Substitute.For<ILogger<Handler>>();
            var handler = new Handler(requestValidator, starter, deployer, statusNotifier, commitMessageFetcher, config, logger);

            var message = "[skip meta-ci]";
            commitMessageFetcher.FetchCommitMessage(Any<GithubEvent>()).Returns(message);

            var pushEvent = new PushEvent
            {
                Ref = "refs/heads/master",
                Repository = new Repository { Name = repoName, DefaultBranch = "master" },
                HeadCommit = new Commit { Id = sha }
            };

            requestValidator.Validate(Any<ApplicationLoadBalancerRequest>()).Returns(pushEvent);

            var response = await handler.Handle(new ApplicationLoadBalancerRequest { });
            await deployer.DidNotReceiveWithAnyArgs().Deploy(null!);
            await commitMessageFetcher.Received().FetchCommitMessage(Is(pushEvent));
        }

        [Test]
        public async Task Handle_ShouldNotDeployPipeline_IfNotOnDefaultBranch()
        {
            var requestValidator = Substitute.For<RequestValidator>();
            var starter = Substitute.For<PipelineStarter>();
            var deployer = Substitute.For<PipelineDeployer>();
            var statusNotifier = Substitute.For<GithubStatusNotifier>();
            var commitMessageFetcher = Substitute.For<GithubCommitMessageFetcher>();
            var logger = Substitute.For<ILogger<Handler>>();
            var handler = new Handler(requestValidator, starter, deployer, statusNotifier, commitMessageFetcher, config, logger);

            var pushEvent = new PushEvent
            {
                Ref = "refs/heads/master",
                Repository = new Repository { Name = repoName, DefaultBranch = "develop" },
                HeadCommit = new Commit { Id = sha, Message = "" }
            };

            requestValidator.Validate(Any<ApplicationLoadBalancerRequest>()).Returns(pushEvent);

            var response = await handler.Handle(new ApplicationLoadBalancerRequest { });
            await deployer.DidNotReceiveWithAnyArgs().Deploy(null!);
        }

        [Test]
        public async Task Handle_ShouldStartPipeline_OnDefaultBranch()
        {
            var requestValidator = Substitute.For<RequestValidator>();
            var starter = Substitute.For<PipelineStarter>();
            var deployer = Substitute.For<PipelineDeployer>();
            var statusNotifier = Substitute.For<GithubStatusNotifier>();
            var commitMessageFetcher = Substitute.For<GithubCommitMessageFetcher>();
            var logger = Substitute.For<ILogger<Handler>>();
            var handler = new Handler(requestValidator, starter, deployer, statusNotifier, commitMessageFetcher, config, logger);

            var pushEvent = new PushEvent
            {
                Ref = "refs/heads/master",
                Repository = new Repository { Name = repoName, DefaultBranch = "master" },
                HeadCommit = new Commit { Id = sha, Message = "" }
            };

            requestValidator.Validate(Any<ApplicationLoadBalancerRequest>()).Returns(pushEvent);

            var response = await handler.Handle(new ApplicationLoadBalancerRequest { });
            await starter.Received().StartPipelineIfExists(Is(pushEvent));
        }

        [Test]
        public async Task Handle_ShouldStartPipeline_ForAllPrEvents()
        {
            var requestValidator = Substitute.For<RequestValidator>();
            var starter = Substitute.For<PipelineStarter>();
            var deployer = Substitute.For<PipelineDeployer>();
            var statusNotifier = Substitute.For<GithubStatusNotifier>();
            var commitMessageFetcher = Substitute.For<GithubCommitMessageFetcher>();
            var logger = Substitute.For<ILogger<Handler>>();
            var handler = new Handler(requestValidator, starter, deployer, statusNotifier, commitMessageFetcher, config, logger);

            var pushEvent = new PullRequestEvent
            {
                Repository = new Repository { Name = repoName, DefaultBranch = "develop" },
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
            var statusNotifier = Substitute.For<GithubStatusNotifier>();
            var commitMessageFetcher = Substitute.For<GithubCommitMessageFetcher>();
            var logger = Substitute.For<ILogger<Handler>>();
            var handler = new Handler(requestValidator, starter, deployer, statusNotifier, commitMessageFetcher, config, logger);

            var message = "[skip ci]";
            commitMessageFetcher.FetchCommitMessage(Any<GithubEvent>()).Returns(message);

            var pushEvent = new PushEvent
            {
                Ref = "refs/heads/master",
                Repository = new Repository { Name = repoName, DefaultBranch = "develop" },
                HeadCommit = new Commit { Id = sha }
            };

            requestValidator.Validate(Any<ApplicationLoadBalancerRequest>()).Returns(pushEvent);

            var response = await handler.Handle(new ApplicationLoadBalancerRequest { });
            await starter.DidNotReceiveWithAnyArgs().StartPipelineIfExists(null!);
            await commitMessageFetcher.Received().FetchCommitMessage(Is(pushEvent));
        }
    }
}