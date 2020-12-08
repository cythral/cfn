using Lambdajection.Attributes;

namespace Cythral.CloudFormation.ApprovalWebhook
{
    [LambdaOptions(typeof(Handler), "Lambda")]
    public class Config
    {
        public string StateStore { get; set; }
    }
}