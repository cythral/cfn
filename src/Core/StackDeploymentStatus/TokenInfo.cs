namespace Cythral.CloudFormation.StackDeploymentStatus
{
    public class TokenInfo
    {
        public string ClientRequestToken { get; set; } = string.Empty;
        public string QueueUrl { get; set; } = string.Empty;
        public string ReceiptHandle { get; set; } = string.Empty;
        public string RoleArn { get; set; } = string.Empty;
        public string GithubOwner { get; set; } = string.Empty;
        public string GithubRepo { get; set; } = string.Empty;
        public string GithubRef { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = string.Empty;
    }
}