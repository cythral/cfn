using System.Threading.Tasks;

using Cythral.CloudFormation.GithubWebhook.Github.Entities;

using Microsoft.Extensions.Logging;

using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.GithubWebhook.Github
{
    public class GithubCommitMessageFetcher
    {
        private readonly GithubHttpClient client;
        private readonly ILogger<GithubCommitMessageFetcher> logger;

        public GithubCommitMessageFetcher(GithubHttpClient client, ILogger<GithubCommitMessageFetcher> logger)
        {
            this.client = client;
            this.logger = logger;
        }

        internal GithubCommitMessageFetcher()
        {
            // testing only
        }

        public virtual async Task<string> FetchCommitMessage(GithubEvent @event)
        {
            switch (@event)
            {
                case PushEvent pushEvent: return FetchPushEventCommitMessage(pushEvent);
                case PullRequestEvent pullRequestEvent: return await FetchPullRequestEventCommitMessage(pullRequestEvent);
            }

            return "";
        }


        public string FetchPushEventCommitMessage(PushEvent pushEvent)
        {
            return pushEvent.HeadCommit.Message;
        }

        public async Task<string> FetchPullRequestEventCommitMessage(PullRequestEvent pullRequestEvent)
        {
            var ownerName = pullRequestEvent.Repository.Owner.Login;
            var repoName = pullRequestEvent.Repository.Name;
            var commitSha = pullRequestEvent.PullRequest.Head.Sha;
            var response = await client.GetAsync<RepoCommit>($"https://api.github.com/repos/{ownerName}/{repoName}/commits/{commitSha}");

            logger.LogInformation($"Received get commit response: {Serialize(response)}");
            return response.Commit.Message;
        }
    }
}
