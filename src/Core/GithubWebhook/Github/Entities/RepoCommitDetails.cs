using System;
using System.Text.Json.Serialization;

namespace Cythral.CloudFormation.GithubWebhook.Github.Entities
{
    public class RepoCommitDetails
    {
        [JsonPropertyName("message")]
        public string Message { get; set; }
    }
}