using Lambdajection.Attributes;
using Lambdajection.Encryption;

namespace Cythral.CloudFormation.StackDeploymentStatus
{
    [LambdaOptions(typeof(Handler), "Config")]
    public class Config
    {
        [Encrypted] public string GithubToken { get; set; }
        public string GithubOwner { get; set; }
        public string GithubTopicArn { get; set; }
        public string StackSuffix { get; set; }
    }
}