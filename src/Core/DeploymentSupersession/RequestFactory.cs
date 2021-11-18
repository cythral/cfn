using System;

using Amazon.Lambda.SQSEvents;

using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.DeploymentSupersession
{
    public class RequestFactory
    {
        public virtual Request CreateFromSqsEvent(SQSEvent sqsEvent)
        {
            var stringContent = sqsEvent.Records[0].Body;
            return Deserialize<Request>(stringContent) ?? throw new Exception("Invalid request");
        }
    }
}