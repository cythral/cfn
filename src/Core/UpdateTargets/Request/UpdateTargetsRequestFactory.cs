using Cythral.CloudFormation.Events;
using Amazon.Lambda.SNSEvents;
using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.UpdateTargets.Request
{
    public class UpdateTargetsRequestFactory
    {
        public virtual UpdateTargetsRequest CreateFromSnsEvent(SNSEvent evnt)
        {
            var message = Deserialize<AlarmEvent>(evnt.Records[0].Sns.Message);
            var request = new UpdateTargetsRequest();

            foreach (var metric in message.Trigger.Metrics)
            {
                if (metric.Id != "customdata") continue;

                foreach (var dimension in metric.MetricStat.Metric.Dimensions)
                {
                    switch (dimension.Name)
                    {
                        case "TargetGroupArn": request.TargetGroupArn = dimension.Value; break;
                        case "TargetDnsName": request.TargetDnsName = dimension.Value; break;
                        default: break;
                    }
                }
            }

            return request;
        }
    }
}