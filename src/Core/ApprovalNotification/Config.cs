using Brighid.Identity.Client;

using Lambdajection.Attributes;
using Lambdajection.Encryption;

namespace Cythral.CloudFormation.ApprovalNotification
{
    [LambdaOptions(typeof(Handler), "Lambda")]
    public class Config : IdentityConfig
    {
        public string BaseUrl { get; set; }

        public string StateStore { get; set; }

        public string TopicArn { get; set; }

        public override string ClientId { get; set; }

        [Encrypted]
        public override string ClientSecret { get; set; }
    }
}
