namespace Cythral.CloudFormation
{
    public class TokenInfo
    {
        public string ClientRequestToken { get; set; }
        public string QueueUrl { get; set; }
        public string ReceiptHandle { get; set; }
        public string RoleArn { get; set; }
        public string GithubOwner { get; set; }
        public string GithubRepo { get; set; }
        public string GithubRef { get; set; }
        public string EnvironmentName { get; set; }
    }
}