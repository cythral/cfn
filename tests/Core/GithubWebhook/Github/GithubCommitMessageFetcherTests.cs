extern alias GithubWebhook;

using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using FluentAssertions;

using GithubWebhook::Cythral.CloudFormation.GithubWebhook.Github;
using GithubWebhook::Cythral.CloudFormation.GithubWebhook.Github.Entities;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using NUnit.Framework;

using static System.Net.HttpStatusCode;
using static NSubstitute.Arg;

namespace Cythral.CloudFormation.Tests.GithubWebhook.Github
{
    using GithubCommitMessageFetcher = GithubWebhook::Cythral.CloudFormation.GithubWebhook.Github.GithubCommitMessageFetcher;
    using GithubHttpClient = GithubWebhook::Cythral.CloudFormation.GithubWebhook.GithubHttpClient;

    public class GithubCommitMessageFetcherTests
    {
        private const string repoName = "repoName";
        private const string ownerName = "ownerName";
        private const string commitMessage = "commitMessage";
        private const string headCommitSha = "headCommitSha";

        [Test]
        public async Task FetchCommitMessage_ShouldReturnHeadCommitId_ForPushEvent()
        {
            var client = Substitute.For<GithubHttpClient>();
            var logger = Substitute.For<ILogger<GithubCommitMessageFetcher>>();
            var fetcher = new GithubCommitMessageFetcher(client, logger);

            var message = await fetcher.FetchCommitMessage(new PushEvent
            {
                HeadCommit = new Commit
                {
                    Message = commitMessage
                }
            });

            message.Should().Be(commitMessage);
        }

        [Test]
        public async Task FetchCommitMessage_ShouldFetchMessageFromApi_ForPullRequestEvent()
        {
            var client = Substitute.For<GithubHttpClient>();
            var logger = Substitute.For<ILogger<GithubCommitMessageFetcher>>();
            var fetcher = new GithubCommitMessageFetcher(client, logger);

            client.GetAsync<RepoCommit>(Any<string>()).Returns(new RepoCommit
            {
                Commit = new RepoCommitDetails
                {
                    Message = commitMessage
                }
            });

            var message = await fetcher.FetchCommitMessage(new PullRequestEvent
            {
                PullRequest = new PullRequest
                {
                    Head = new PullRequestHead
                    {
                        Sha = headCommitSha
                    }
                },
                Repository = new Repository
                {
                    Name = repoName,
                    Owner = new User
                    {
                        Login = ownerName,
                    }
                }
            });

            message.Should().Be(commitMessage);
            await client.Received().GetAsync<RepoCommit>(Is<string>($"https://api.github.com/repos/{ownerName}/{repoName}/commits/{headCommitSha}"));
        }
    }
}