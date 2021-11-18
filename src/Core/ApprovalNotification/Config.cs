using Brighid.Identity.Client;

using Lambdajection.Attributes;
using Lambdajection.Encryption;

namespace Cythral.CloudFormation.ApprovalNotification
{
    [LambdaOptions(typeof(Handler), "Lambda")]
    public class Config : IdentityConfig
    {
        public string BaseUrl { get; set; } = string.Empty;

        public string StateStore { get; set; } = string.Empty;

        public string TopicArn { get; set; } = string.Empty;

        public override string ClientId { get; set; } = string.Empty;

        [Encrypted]
        public override string ClientSecret { get; set; } = string.Empty;
    }
}
