using Octokit;

namespace Cythral.CloudFormation.GithubUtils
{
    public class PutCommitStatusRequest
    {
        public string ServiceName { get; set; }
        public string ProjectName { get; set; }
        public string GithubOwner { get; set; }
        public string GithubRepo { get; set; }
        public string GithubRef { get; set; }
        public string DetailsUrl { get; set; }
        public CommitState CommitState { get; set; }
        public string EnvironmentName { get; set; }
    }
}