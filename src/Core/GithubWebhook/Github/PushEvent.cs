using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Cythral.CloudFormation.GithubWebhook.Github.Entities;

namespace Cythral.CloudFormation.GithubWebhook.Github
{
    public class PushEvent : GithubEvent
    {
        [JsonPropertyName("ref")]
        public string Ref { get; set; }

        [JsonPropertyName("head")]
        public string Head { get; set; }

        [JsonPropertyName("before")]
        public string Before { get; set; }

        [JsonPropertyName("head_commit")]
        public Commit HeadCommit { get; set; }

        [JsonPropertyName("size")]
        public int Size { get; set; }

        [JsonPropertyName("distinct_size")]
        public int DistinctSize { get; set; }

        [JsonPropertyName("commits")]
        public IEnumerable<Commit> Commits { get; set; }

        [JsonPropertyName("pusher")]
        public Author Pusher { get; set; }

        [JsonPropertyName("sender")]
        public User Sender { get; set; }

        [JsonPropertyName("on_default_branch")]
        public virtual bool OnDefaultBranch => Ref == $"refs/heads/{Repository.DefaultBranch}";

        [JsonPropertyName("head_commit_id")]
        public override string HeadCommitId => HeadCommit?.Id;
    }
}