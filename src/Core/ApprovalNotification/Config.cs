using Lambdajection.Attributes;

namespace Cythral.CloudFormation.ApprovalNotification
{
    [LambdaOptions(typeof(Handler), "Lambda")]
    public class Config
    {
        public string BaseUrl { get; set; }
        public string StateStore { get; set; }
        public string TopicArn { get; set; }
    }
}
