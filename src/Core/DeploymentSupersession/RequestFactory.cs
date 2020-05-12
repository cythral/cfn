using static System.Text.Json.JsonSerializer;

using Amazon.Lambda.SQSEvents;

namespace Cythral.CloudFormation.DeploymentSupersession
{
    public class RequestFactory
    {
        public virtual Request CreateFromSqsEvent(SQSEvent sqsEvent)
        {
            var stringContent = sqsEvent.Records[0].Body;
            return Deserialize<Request>(stringContent);
        }
    }
}