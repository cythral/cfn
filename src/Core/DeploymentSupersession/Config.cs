
using Lambdajection.Attributes;
using Lambdajection.Core;

namespace Cythral.CloudFormation.DeploymentSupersession
{
    [LambdaOptions(typeof(Handler), "Lambda")]
    public class Config
    {
        public string StateStore { get; set; } = string.Empty;
    }
}