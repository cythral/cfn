using Lambdajection.Attributes;
using Lambdajection.Encryption;

namespace Cythral.CloudFormation.StackDeployment
{
    [LambdaOptions(typeof(Handler), "Config")]
    public class Config
    {
        [Encrypted] public string GithubToken { get; set; } = string.Empty;
        public string NotificationArn { get; set; } = string.Empty;
    }
}