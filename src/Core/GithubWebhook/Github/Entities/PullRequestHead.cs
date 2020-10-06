using System.Text.Json.Serialization;

namespace Cythral.CloudFormation.GithubWebhook.Github.Entities
{
    public class PullRequestHead
    {
        [JsonPropertyName("sha")]
        public string Sha { get; set; }

    }
}