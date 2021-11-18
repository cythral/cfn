using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Cythral.CloudFormation.GithubWebhook.Github.Entities;

namespace Cythral.CloudFormation.GithubWebhook.Github
{
    public class PullRequestEvent : GithubEvent
    {
        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;

        [JsonPropertyName("pull_request")]
        public PullRequest PullRequest { get; set; } = new();

        [JsonPropertyName("head_commit_id")]
        public override string HeadCommitId => PullRequest?.Head?.Sha ?? string.Empty;

        [JsonPropertyName("ref")]
        public override string Ref
        {
            get => $"refs/heads/{PullRequest?.Head?.Ref}";
            set => _ = value;
        }
    }
}