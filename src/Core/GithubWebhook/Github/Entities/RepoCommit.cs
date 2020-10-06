using System;
using System.Text.Json.Serialization;

namespace Cythral.CloudFormation.GithubWebhook.Github.Entities
{
    public class RepoCommit
    {
        [JsonPropertyName("commit")]
        public RepoCommitDetails Commit { get; set; }
    }
}