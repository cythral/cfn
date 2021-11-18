namespace Cythral.CloudFormation.StackDeployment.Github
{
    public class GithubCommitInfo
    {
        public string GithubOwner { get; set; } = string.Empty;
        public string GithubRepository { get; set; } = string.Empty;
        public string GithubRef { get; set; } = string.Empty;
    }
}