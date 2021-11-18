using Amazon.Lambda.SQSEvents;

using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.StackDeployment
{
    public class RequestFactory
    {
        public virtual Request CreateFromSqsEvent(SQSEvent evnt)
        {
            return Deserialize<Request>(evnt.Records[0].Body) ?? throw new System.Exception("Could not deserialize request.");
        }
    }
}
