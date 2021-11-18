using Lambdajection.Attributes;
using Lambdajection.Encryption;

namespace Cythral.CloudFormation.StackDeploymentStatus
{
    [LambdaOptions(typeof(Handler), "Config")]
    public class Config
    {
        [Encrypted] public string GithubToken { get; set; } = string.Empty;
        public string GithubOwner { get; set; } = string.Empty;
        public string GithubTopicArn { get; set; } = string.Empty;
        public string StackSuffix { get; set; } = string.Empty;
    }
}