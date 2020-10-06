using System.Text.Json.Serialization;

namespace Cythral.CloudFormation.GithubWebhook.Github.Entities
{
    public class PullRequest
    {
        [JsonPropertyName("head")]
        public PullRequestHead Head { get; set; }
    }
}