using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

using Cythral.CloudFormation.GithubWebhook.Tests;

using FluentAssertions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using NUnit.Framework;

using static System.Net.HttpStatusCode;
using static NSubstitute.Arg;

namespace Cythral.CloudFormation.GithubWebhook.Github.Tests
{
    public class GithubStatusNotifierTests
    {
        class NotifyPending
        {
            private const string repoName = "repoName";
            private const string owner = "owner";
            private const string sha = "sha";
            private const string stackSuffix = "cicd";
            private const string expectedUrl = "https://api.github.com/repos/owner/repoName/statuses/sha";
            private const string expectedState = "pending";
            private const string expectedContext = "CloudFormation - shared (repoName-cicd)";
            private const string expectedTargetUrl = "https://sso.brigh.id/start/shared?destination=https://console.aws.amazon.com/cloudformation/home?region=us-east-1#/stacks/stackinfo?filteringText=&filteringStatus=active&viewNested=true&hideStacks=false&stackId=repoName-cicd";
            private const string expectedPendingDescription = "repoName Meta CICD Stack Deployment In Progress";

            private IOptions<Config> options = Options.Create(new Config
            {
                GithubOwner = owner,
                StackSuffix = stackSuffix,
            });
            [Test]
            public async Task NotifyPending_ShouldSendAPostRequest()
            {
                var logger = Substitute.For<ILogger<GithubStatusNotifier>>();
                var httpClient = Substitute.For<GithubHttpClient>(options);
                var fetcher = new GithubStatusNotifier(httpClient, options, logger);

                httpClient.SendAsync(Any<HttpRequestMessage>()).Returns(new HttpResponseMessage { StatusCode = OK });
                await fetcher.NotifyPending(repoName, sha);

                await httpClient.Received().SendAsync(Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post
                ));
            }

            [Test]
            public async Task NotifyPending_ShouldSendARequest_ToTheCorrectUrl()
            {
                var logger = Substitute.For<ILogger<GithubStatusNotifier>>();
                var httpClient = Substitute.For<GithubHttpClient>(options);
                var fetcher = new GithubStatusNotifier(httpClient, options, logger);

                httpClient.SendAsync(Any<HttpRequestMessage>()).Returns(new HttpResponseMessage { StatusCode = OK });
                await fetcher.NotifyPending(repoName, sha);

                await httpClient.Received().SendAsync(Is<HttpRequestMessage>(req =>
                    req.RequestUri == new Uri(expectedUrl)
                ));
            }

            [Test]
            public async Task NotifyPending_ShouldSendARequest_WithPendingState()
            {
                var logger = Substitute.For<ILogger<GithubStatusNotifier>>();
                var httpClient = Substitute.For<GithubHttpClient>(options);
                var fetcher = new GithubStatusNotifier(httpClient, options, logger);

                httpClient.SendAsync(Any<HttpRequestMessage>()).Returns(new HttpResponseMessage { StatusCode = OK });
                await fetcher.NotifyPending(repoName, sha);

                await httpClient.Received().SendAsync(Is<HttpRequestMessage>(req =>
                    req.Json<CreateStatusRequest>().State == expectedState
                ));
            }

            [Test]
            public async Task NotifyPending_ShouldSendARequest_WithPendingDescription()
            {
                var logger = Substitute.For<ILogger<GithubStatusNotifier>>();
                var httpClient = Substitute.For<GithubHttpClient>(options);
                var fetcher = new GithubStatusNotifier(httpClient, options, logger);

                httpClient.SendAsync(Any<HttpRequestMessage>()).Returns(new HttpResponseMessage { StatusCode = OK });
                await fetcher.NotifyPending(repoName, sha);

                await httpClient.Received().SendAsync(Is<HttpRequestMessage>(req =>
                    req.Json<CreateStatusRequest>().Description == expectedPendingDescription
                ));
            }

            [Test]
            public async Task NotifyPending_ShouldSendARequest_WithCorrectTargeturl()
            {
                var logger = Substitute.For<ILogger<GithubStatusNotifier>>();
                var httpClient = Substitute.For<GithubHttpClient>(options);
                var fetcher = new GithubStatusNotifier(httpClient, options, logger);

                httpClient.SendAsync(Any<HttpRequestMessage>()).Returns(new HttpResponseMessage { StatusCode = OK });
                await fetcher.NotifyPending(repoName, sha);

                await httpClient.Received().SendAsync(Is<HttpRequestMessage>(req =>
                    req.Json<CreateStatusRequest>().TargetUrl == expectedTargetUrl
                ));
            }

            [Test]
            public async Task NotifyPending_ShouldSendARequest_WithCorrectContext()
            {
                var logger = Substitute.For<ILogger<GithubStatusNotifier>>();
                var httpClient = Substitute.For<GithubHttpClient>(options);
                var fetcher = new GithubStatusNotifier(httpClient, options, logger);

                httpClient.SendAsync(Any<HttpRequestMessage>()).Returns(new HttpResponseMessage { StatusCode = OK });
                await fetcher.NotifyPending(repoName, sha);

                await httpClient.Received().SendAsync(Is<HttpRequestMessage>(req =>
                    req.Json<CreateStatusRequest>().Context == expectedContext
                ));
            }
        }

        class NotifyFailure
        {
            private const string repoName = "repoName";
            private const string owner = "owner";
            private const string sha = "sha";
            private const string stackSuffix = "cicd";
            private const string expectedUrl = "https://api.github.com/repos/owner/repoName/statuses/sha";
            private const string expectedState = "failure";
            private const string expectedContext = "CloudFormation - shared (repoName-cicd)";
            private const string expectedTargetUrl = "https://sso.brigh.id/start/shared?destination=https://console.aws.amazon.com/cloudformation/home?region=us-east-1#/stacks/stackinfo?filteringText=&filteringStatus=deleted&viewNested=true&hideStacks=false&stackId=repoName-cicd";
            private const string expectedPendingDescription = "repoName Meta CICD Stack Deployment Failed";

            private IOptions<Config> options = Options.Create(new Config
            {
                GithubOwner = owner,
                StackSuffix = stackSuffix,
            });

            [Test]
            public async Task NotifyFailure_ShouldSendAPostRequest()
            {
                var logger = Substitute.For<ILogger<GithubStatusNotifier>>();
                var httpClient = Substitute.For<GithubHttpClient>(options);
                var fetcher = new GithubStatusNotifier(httpClient, options, logger);

                httpClient.SendAsync(Any<HttpRequestMessage>()).Returns(new HttpResponseMessage { StatusCode = OK });
                await fetcher.NotifyFailure(repoName, sha);

                await httpClient.Received().SendAsync(Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post
                ));
            }

            [Test]
            public async Task NotifyPending_ShouldSendARequest_ToTheCorrectUrl()
            {
                var logger = Substitute.For<ILogger<GithubStatusNotifier>>();
                var httpClient = Substitute.For<GithubHttpClient>(options);
                var fetcher = new GithubStatusNotifier(httpClient, options, logger);

                httpClient.SendAsync(Any<HttpRequestMessage>()).Returns(new HttpResponseMessage { StatusCode = OK });
                await fetcher.NotifyFailure(repoName, sha);

                await httpClient.Received().SendAsync(Is<HttpRequestMessage>(req =>
                    req.RequestUri == new Uri(expectedUrl)
                ));
            }

            [Test]
            public async Task NotifyFailure_ShouldSendARequest_WithPendingState()
            {
                var logger = Substitute.For<ILogger<GithubStatusNotifier>>();
                var httpClient = Substitute.For<GithubHttpClient>(options);
                var fetcher = new GithubStatusNotifier(httpClient, options, logger);

                httpClient.SendAsync(Any<HttpRequestMessage>()).Returns(new HttpResponseMessage { StatusCode = OK });
                await fetcher.NotifyFailure(repoName, sha);

                await httpClient.Received().SendAsync(Is<HttpRequestMessage>(req =>
                    req.Json<CreateStatusRequest>().State == expectedState
                ));
            }

            [Test]
            public async Task NotifyFailure_ShouldSendARequest_WithPendingDescription()
            {
                var logger = Substitute.For<ILogger<GithubStatusNotifier>>();
                var httpClient = Substitute.For<GithubHttpClient>(options);
                var fetcher = new GithubStatusNotifier(httpClient, options, logger);

                httpClient.SendAsync(Any<HttpRequestMessage>()).Returns(new HttpResponseMessage { StatusCode = OK });
                await fetcher.NotifyFailure(repoName, sha);

                await httpClient.Received().SendAsync(Is<HttpRequestMessage>(req =>
                    req.Json<CreateStatusRequest>().Description == expectedPendingDescription
                ));
            }

            [Test]
            public async Task NotifyFailure_ShouldSendARequest_WithCorrectTargeturl()
            {
                var logger = Substitute.For<ILogger<GithubStatusNotifier>>();
                var httpClient = Substitute.For<GithubHttpClient>(options);
                var fetcher = new GithubStatusNotifier(httpClient, options, logger);

                httpClient.SendAsync(Any<HttpRequestMessage>()).Returns(new HttpResponseMessage { StatusCode = OK });
                await fetcher.NotifyFailure(repoName, sha);

                await httpClient.Received().SendAsync(Is<HttpRequestMessage>(req =>
                    req.Json<CreateStatusRequest>().TargetUrl == expectedTargetUrl
                ));
            }

            [Test]
            public async Task NotifyFailure_ShouldSendARequest_WithCorrectContext()
            {
                var logger = Substitute.For<ILogger<GithubStatusNotifier>>();
                var httpClient = Substitute.For<GithubHttpClient>(options);
                var fetcher = new GithubStatusNotifier(httpClient, options, logger);

                httpClient.SendAsync(Any<HttpRequestMessage>()).Returns(new HttpResponseMessage { StatusCode = OK });
                await fetcher.NotifyFailure(repoName, sha);

                await httpClient.Received().SendAsync(Is<HttpRequestMessage>(req =>
                    req.Json<CreateStatusRequest>().Context == expectedContext
                ));
            }
        }
    }
}