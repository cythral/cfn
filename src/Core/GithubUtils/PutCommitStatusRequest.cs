using Octokit;

namespace Cythral.CloudFormation.GithubUtils
{
    public class PutCommitStatusRequest
    {
        public string StackName { get; set; }
        public string GithubOwner { get; set; }
        public string GithubRepo { get; set; }
        public string GithubRef { get; set; }
        public CommitState CommitState { get; set; }
        public string EnvironmentName { get; set; }
        public string GoogleClientId { get; set; }
        public string IdentityPoolId { get; set; }
    }
}