using System;

using Amazon.Lambda.SNSEvents;

using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.StackDeploymentStatus.Request
{
    public class StackDeploymentStatusRequestFactory
    {
        public virtual StackDeploymentStatusRequest CreateFromSnsEvent(SNSEvent evnt)
        {
            var message = evnt.Records[0].Sns.Message;
            var request = new StackDeploymentStatusRequest();
            request.SourceTopic = evnt.Records[0].Sns.TopicArn;

            foreach (var line in message.Split('\n'))
            {
                var i = line.IndexOf('=');

                if (i <= 0)
                {
                    continue;
                }

                var key = line.Substring(0, i).Trim();
                var value = line.Substring(i + 1).Trim('\'');

                switch (key)
                {
                    case "StackId": request.StackId = value; break;
                    case "StackName": request.StackName = value; break;
                    case "EventId": request.EventId = value; break;
                    case "ClientRequestToken": request.ClientRequestToken = value; break;
                    case "Timestamp": request.Timestamp = DateTime.Parse(value); break;
                    case "LogicalResourceId": request.LogicalResourceId = value; break;
                    case "PhysicalResourceId": request.PhysicalResourceId = value; break;
                    case "Namespace": request.Namespace = value; break;
                    case "PrincipalId": request.PrincipalId = value; break;
                    case "ResourceProperties": request.ResourceProperties = Deserialize<object>(value); break;
                    case "ResourceStatus": request.ResourceStatus = value; break;
                    case "ResourceType": request.ResourceType = value; break;
                }
            }

            return request;
        }
    }
}