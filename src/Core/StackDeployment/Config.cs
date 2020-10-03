using Lambdajection.Attributes;
using Lambdajection.Encryption;

namespace Cythral.CloudFormation.StackDeployment
{
    [LambdaOptions(typeof(Handler), "Config")]
    public class Config
    {
        [Encrypted] public string GithubToken { get; set; }
        public string NotificationArn { get; set; }
    }
}