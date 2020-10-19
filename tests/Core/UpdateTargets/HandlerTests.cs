using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Amazon.ElasticLoadBalancingV2;
using Amazon.ElasticLoadBalancingV2.Model;
using Amazon.Lambda.SNSEvents;

using Cythral.CloudFormation.AwsUtils;

using Cythral.CloudFormation.UpdateTargets;
using Cythral.CloudFormation.UpdateTargets.Request;

using Microsoft.Extensions.Logging;

using NSubstitute;

using NUnit.Framework;

using static Amazon.ElasticLoadBalancingV2.TargetHealthStateEnum;

namespace Cythral.CloudFormation.Tests.UpdateTargets
{
    public class HandlerTests
    {
        private const string targetGroupArn = "targetGroupArn";
        private const string dnsName = "dnsName";

        private UpdateTargetsRequest request = new UpdateTargetsRequest
        {
            TargetGroupArn = targetGroupArn,
            TargetDnsName = dnsName,
        };

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

        private DnsResolver CreateDnsResolver()
        {
            var dnsResolver = Substitute.For<DnsResolver>();
            dnsResolver
            .Resolve(Arg.Any<string>())
            .Returns(new IPHostEntry());

            return dnsResolver;
        }

        private UpdateTargetsRequestFactory CreateRequestFactory()
        {
            var requestFactory = Substitute.For<UpdateTargetsRequestFactory>();
            requestFactory.CreateFromSnsEvent(Arg.Any<SNSEvent>()).Returns(request);
            return requestFactory;
        }

        [Test]
        public async Task HandleCallsResolve()
        {
            var dnsResolver = CreateDnsResolver();
            var elbClient = CreateElbClient(targetGroupArn);
            var requestFactory = CreateRequestFactory();
            var snsEvent = Substitute.For<SNSEvent>();
            var logger = Substitute.For<ILogger<Handler>>();
            var handler = new Handler(dnsResolver, elbClient, requestFactory, logger);

            await handler.Handle(snsEvent);

            dnsResolver
            .Received()
            .Resolve(
                Arg.Is<string>(req => req == dnsName)
            );
        }

        [Test]
        public async Task HandleDeregistersUnhealthyTargets()
        {
            var elbClient = CreateElbClient(targetGroupArn);
            var requestFactory = CreateRequestFactory();
            var dnsResolver = CreateDnsResolver();
            var snsRequest = Substitute.For<SNSEvent>();
            var logger = Substitute.For<ILogger<Handler>>();
            var handler = new Handler(dnsResolver, elbClient, requestFactory, logger);

            await handler.Handle(snsRequest);

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
            var dnsResolver = CreateDnsResolver();
            var elbClient = CreateElbClient(targetGroupArn);
            var requestFactory = CreateRequestFactory();
            var snsRequest = Substitute.For<SNSEvent>();
            var logger = Substitute.For<ILogger<Handler>>();
            var handler = new Handler(dnsResolver, elbClient, requestFactory, logger);

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

            await handler.Handle(snsRequest);
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

            var dnsResolver = CreateDnsResolver();
            var elbClient = CreateElbClient(targetGroupArn, targets);
            var requestFactory = CreateRequestFactory();
            var snsRequest = Substitute.For<SNSEvent>();
            var logger = Substitute.For<ILogger<Handler>>();
            var handler = new Handler(dnsResolver, elbClient, requestFactory, logger);

            dnsResolver
            .Resolve(Arg.Is<string>(hostname => hostname == dnsName))
            .Returns(new IPHostEntry());

            await handler.Handle(snsRequest);

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

            var dnsResolver = CreateDnsResolver();
            var elbClient = CreateElbClient(targetGroupArn, targets);
            var requestFactory = CreateRequestFactory();
            var snsRequest = Substitute.For<SNSEvent>();
            var logger = Substitute.For<ILogger<Handler>>();
            var handler = new Handler(dnsResolver, elbClient, requestFactory, logger);

            dnsResolver
            .Resolve(Arg.Is<string>(hostname => hostname == dnsName))
            .Returns(new IPHostEntry
            {
                AddressList = new IPAddress[] {
                    IPAddress.Parse("10.0.0.1")
                }
            });

            await handler.Handle(snsRequest);

            await elbClient
            .DidNotReceive()
            .RegisterTargetsAsync(Arg.Any<RegisterTargetsRequest>());
        }
    }
}