using System.Collections.Generic;

using Amazon.Lambda.SNSEvents;

using Cythral.CloudFormation.UpdateTargets;
using Cythral.CloudFormation.UpdateTargets.Request;

using NUnit.Framework;

using static System.Text.Json.JsonSerializer;

using SNSMessage = Amazon.Lambda.SNSEvents.SNSEvent.SNSMessage;
using SNSRecord = Amazon.Lambda.SNSEvents.SNSEvent.SNSRecord;

namespace Cythral.CloudFormation.Tests.UpdateTargets.Request
{
    public class UpdateTargetsRequestFactoryTests
    {
        [Test]
        public void FromSnsEventReturnsRequest()
        {
            var targetGroupArn = "arn:aws:elb:us-east-1:1:targetgroup/test/test";
            var dnsName = "http://example.com";
            var alarm = new AlarmEvent
            {
                Trigger = new Trigger
                {
                    Metrics = new List<MetricDataQuery> {
                        new MetricDataQuery {
                            Id = "healthy",
                        },
                        new MetricDataQuery {
                            Id = "customdata",
                            MetricStat = new MetricStat {
                                Metric = new Metric {
                                    Dimensions = new List<Dimension> {
                                        new Dimension {
                                            Name = "TargetGroupArn",
                                            Value = targetGroupArn
                                        },
                                        new Dimension {
                                            Name ="TargetDnsName",
                                            Value = dnsName
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            var evnt = new SNSEvent
            {
                Records = new List<SNSRecord> {
                    new SNSRecord {
                        Sns = new SNSMessage {
                            Message = Serialize(alarm)
                        }
                    }
                }
            };

            var factory = new UpdateTargetsRequestFactory();
            var request = factory.CreateFromSnsEvent(evnt);
            Assert.That(request.TargetGroupArn, Is.EqualTo(targetGroupArn));
            Assert.That(request.TargetDnsName, Is.EqualTo(dnsName));
        }
    }
}