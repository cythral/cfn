using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Amazon.ElasticLoadBalancingV2;
using Amazon.ElasticLoadBalancingV2.Model;
using Amazon.Lambda.SNSEvents;

using Cythral.CloudFormation.Entities;
using Cythral.CloudFormation.Events;
using Cythral.CloudFormation.Handlers;

using NSubstitute;

using NUnit.Framework;

using static Amazon.ElasticLoadBalancingV2.TargetHealthStateEnum;
using static System.Text.Json.JsonSerializer;

using SNSRecord = Amazon.Lambda.SNSEvents.SNSEvent.SNSRecord;
using SNSMessage = Amazon.Lambda.SNSEvents.SNSEvent.SNSMessage;

namespace Cythral.CloudFormation.Tests.Handlers
{
    public class UpdateTargetsHandlerTest
    {
        private IAmazonElasticLoadBalancingV2 CreateElbClient(string targetGroupArn, List<TargetHealthDescription> targets = null)
        {
            var elbClient = Substitute.For<IAmazonElasticLoadBalancingV2>();
            targets = targets ?? new List<TargetHealthDescription> {
                new TargetHealthDescription {
                    Target = new TargetDescription { Id = "10.0.0.1" },
                    TargetHealth = new TargetHealth { State = Unhealthy }
                },
                new TargetHealthDescription {
                    Target = new TargetDescription { Id = "10.0.0.2" },
                    TargetHealth = new TargetHealth { State = Healthy }
                }
            };

            elbClient
            .DescribeTargetHealthAsync(
                Arg.Is<DescribeTargetHealthRequest>(req =>
                    req.TargetGroupArn == targetGroupArn
                )
            )
            .Returns(new DescribeTargetHealthResponse
            {
                TargetHealthDescriptions = targets
            });

            return elbClient;
        }

        [Test]
        public async Task HandleCallsResolve()
        {
            var dnsResolver = Substitute.For<IDnsResolver>();
            var dnsName = "http://example.com";
            var targetGroupArn = "arn:aws:elb:us-east-1:1:targetgroup/test/test";
            var elbClient = CreateElbClient(targetGroupArn);
            var request = new UpdateTargetsHandler.Request
            {
                TargetGroupArn = targetGroupArn,
                TargetDnsName = dnsName,
            };

            dnsResolver
            .Resolve(Arg.Any<string>())
            .Returns(new IPHostEntry());

            await UpdateTargetsHandler.Handle(
                request: request,
                resolver: dnsResolver,
                elbClient: elbClient
            );

            dnsResolver
            .Received()
            .Resolve(
                Arg.Is<string>(req => req == dnsName)
            );
        }

        [Test]
        public async Task HandleDeregistersUnhealthyTargets()
        {
            var dnsResolver = Substitute.For<IDnsResolver>();
            var dnsName = "http://example.com";
            var targetGroupArn = "arn:aws:elb:us-east-1:1:targetgroup/test/test";
            var elbClient = CreateElbClient(targetGroupArn);
            var request = new UpdateTargetsHandler.Request
            {
                TargetGroupArn = targetGroupArn,
                TargetDnsName = dnsName,
            };

            dnsResolver
            .Resolve(Arg.Is<string>(hostname => hostname == dnsName))
            .Returns(new IPHostEntry());

            await UpdateTargetsHandler.Handle(
                request: request,
                resolver: dnsResolver,
                elbClient: elbClient
            );

            await elbClient
            .Received()
            .DeregisterTargetsAsync(
                Arg.Is<DeregisterTargetsRequest>(req =>
                    req.TargetGroupArn == targetGroupArn &&
                    req.Targets.Any(target => target.Id == "10.0.0.1") &&
                    req.Targets.All(target => target.Id != "10.0.0.2")
                )
            );
        }

        [Test]
        public async Task HandleRegistersNewTargets()
        {
            var dnsResolver = Substitute.For<IDnsResolver>();
            var dnsName = "http://example.com";
            var targetGroupArn = "arn:aws:elb:us-east-1:1:targetgroup/test/test";
            var elbClient = CreateElbClient(targetGroupArn);
            var request = new UpdateTargetsHandler.Request
            {
                TargetGroupArn = targetGroupArn,
                TargetDnsName = dnsName,
            };

            dnsResolver
            .Resolve(Arg.Is<string>(hostname => hostname == dnsName))
            .Returns(new IPHostEntry
            {
                AddressList = new IPAddress[] {
                    IPAddress.Parse("10.0.0.2"),
                    IPAddress.Parse("10.0.0.3"),
                    IPAddress.Parse("10.0.0.4")
                }
            });

            await UpdateTargetsHandler.Handle(
                request: request,
                resolver: dnsResolver,
                elbClient: elbClient
            );

            await elbClient
            .Received()
            .RegisterTargetsAsync(
                Arg.Is<RegisterTargetsRequest>(req =>
                    req.TargetGroupArn == targetGroupArn &&
                    req.Targets.All(target =>
                        target.Id != "10.0.0.2"
                    ) &&
                    req.Targets.Any(target =>
                        target.Id == "10.0.0.3" &&
                        target.AvailabilityZone == "all" &&
                        target.Port == 80
                    ) &&
                    req.Targets.Any(target =>
                        target.Id == "10.0.0.4" &&
                        target.AvailabilityZone == "all" &&
                        target.Port == 80
                    )
                )
            );
        }

        [Test]
        public async Task HandleDoesntCallDeregisterWhenAllTargetsHealthy()
        {
            var targets = new List<TargetHealthDescription> {
                new TargetHealthDescription {
                    TargetHealth = new TargetHealth { State = Healthy },
                    Target = new TargetDescription { Id = "10.0.0.1" }
                }
            };

            var dnsResolver = Substitute.For<IDnsResolver>();
            var dnsName = "http://example.com";
            var targetGroupArn = "arn:aws:elb:us-east-1:1:targetgroup/test/test";
            var elbClient = CreateElbClient(targetGroupArn, targets);
            var request = new UpdateTargetsHandler.Request
            {
                TargetGroupArn = targetGroupArn,
                TargetDnsName = dnsName,
            };

            dnsResolver
            .Resolve(Arg.Is<string>(hostname => hostname == dnsName))
            .Returns(new IPHostEntry());

            await UpdateTargetsHandler.Handle(
                request: request,
                resolver: dnsResolver,
                elbClient: elbClient
            );

            await elbClient
            .DidNotReceive()
            .DeregisterTargetsAsync(Arg.Any<DeregisterTargetsRequest>());
        }

        [Test]
        public async Task HandleDoesntCallRegisterWhenNoNewTargets()
        {
            var targets = new List<TargetHealthDescription> {
                new TargetHealthDescription {
                    TargetHealth = new TargetHealth { State = Healthy },
                    Target = new TargetDescription { Id = "10.0.0.1" }
                }
            };

            var dnsResolver = Substitute.For<IDnsResolver>();
            var dnsName = "http://example.com";
            var targetGroupArn = "arn:aws:elb:us-east-1:1:targetgroup/test/test";
            var elbClient = CreateElbClient(targetGroupArn, targets);
            var request = new UpdateTargetsHandler.Request
            {
                TargetGroupArn = targetGroupArn,
                TargetDnsName = dnsName,
            };

            dnsResolver
            .Resolve(Arg.Is<string>(hostname => hostname == dnsName))
            .Returns(new IPHostEntry
            {
                AddressList = new IPAddress[] {
                    IPAddress.Parse("10.0.0.1")
                }
            });

            await UpdateTargetsHandler.Handle(
                request: request,
                resolver: dnsResolver,
                elbClient: elbClient
            );

            await elbClient
            .DidNotReceive()
            .RegisterTargetsAsync(Arg.Any<RegisterTargetsRequest>());
        }

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

            var request = UpdateTargetsHandler.Request.FromSnsEvent(evnt);
            Assert.That(request.TargetGroupArn, Is.EqualTo(targetGroupArn));
            Assert.That(request.TargetDnsName, Is.EqualTo(dnsName));
        }
    }
}