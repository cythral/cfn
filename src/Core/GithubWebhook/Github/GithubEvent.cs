using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Cythral.CloudFormation.GithubWebhook.Github.Entities;

namespace Cythral.CloudFormation.GithubWebhook.Github
{
    public abstract class GithubEvent
    {
        [JsonPropertyName("repository")]
        public Repository Repository { get; set; } = new();

        [JsonPropertyName("head_commit_message")]
        public string HeadCommitMessage { get; set; } = string.Empty;

        [JsonPropertyName("head_commit_id")]
        public abstract string HeadCommitId { get; }

        [JsonPropertyName("ref")]
        public abstract string Ref { get; set; }
    }
}