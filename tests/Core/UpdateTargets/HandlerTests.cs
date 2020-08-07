using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Amazon.ElasticLoadBalancingV2;
using Amazon.ElasticLoadBalancingV2.Model;
using Amazon.Lambda.SNSEvents;

using Cythral.CloudFormation.UpdateTargets;
using Cythral.CloudFormation.UpdateTargets.DnsResolver;
using Cythral.CloudFormation.UpdateTargets.Request;

using NSubstitute;

using NUnit.Framework;

using static Amazon.ElasticLoadBalancingV2.TargetHealthStateEnum;

using ElbClientFactory = Cythral.CloudFormation.AwsUtils.AmazonClientFactory<
    Amazon.ElasticLoadBalancingV2.IAmazonElasticLoadBalancingV2,
    Amazon.ElasticLoadBalancingV2.AmazonElasticLoadBalancingV2Client
>;

namespace Cythral.CloudFormation.Tests.UpdateTargets
{
    public class HandlerTests
    {
        private static DnsResolverFactory dnsResolverFactory = Substitute.For<DnsResolverFactory>();
        private static ElbClientFactory elbClientFactory = Substitute.For<ElbClientFactory>();
        private static UpdateTargetsRequestFactory requestFactory = Substitute.For<UpdateTargetsRequestFactory>();

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

            elbClientFactory.Create().Returns(elbClient);
            return elbClient;
        }

        private IDnsResolver CreateDnsResolver()
        {
            var dnsResolver = Substitute.For<IDnsResolver>();
            dnsResolver
            .Resolve(Arg.Any<string>())
            .Returns(new IPHostEntry());

            dnsResolverFactory.Create().Returns(dnsResolver);
            return dnsResolver;
        }

        private UpdateTargetsRequest CreateRequest(string targetGroupArn, string dnsName)
        {
            var request = new UpdateTargetsRequest
            {
                TargetGroupArn = targetGroupArn,
                TargetDnsName = dnsName,
            };

            requestFactory.CreateFromSnsEvent(Arg.Any<SNSEvent>()).Returns(request);
            return request;
        }

        [SetUp]
        public void SetupDnsResolverFactory()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "dnsResolverFactory", dnsResolverFactory);
            dnsResolverFactory.ClearReceivedCalls();
        }

        [SetUp]
        public void SetupElbClientFactory()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "elbClientFactory", elbClientFactory);
            elbClientFactory.ClearReceivedCalls();
        }

        [SetUp]
        public void SetupRequestFactory()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "requestFactory", requestFactory);
            requestFactory.ClearReceivedCalls();
        }

        [Test]
        public async Task HandleCallsResolve()
        {
            var dnsName = "http://example.com";
            var targetGroupArn = "arn:aws:elb:us-east-1:1:targetgroup/test/test";

            var dnsResolver = CreateDnsResolver();
            var elbClient = CreateElbClient(targetGroupArn);
            var request = CreateRequest(targetGroupArn, dnsName);
            var snsEvent = Substitute.For<SNSEvent>();

            await Handler.Handle(snsEvent);

            dnsResolver
            .Received()
            .Resolve(
                Arg.Is<string>(req => req == dnsName)
            );
        }

        [Test]
        public async Task HandleDeregistersUnhealthyTargets()
        {
            var dnsName = "http://example.com";
            var targetGroupArn = "arn:aws:elb:us-east-1:1:targetgroup/test/test";
            var elbClient = CreateElbClient(targetGroupArn);
            var request = CreateRequest(targetGroupArn, dnsName);
            var dnsResolver = CreateDnsResolver();
            var snsRequest = Substitute.For<SNSEvent>();

            await Handler.Handle(snsRequest);

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
            var dnsName = "http://example.com";
            var targetGroupArn = "arn:aws:elb:us-east-1:1:targetgroup/test/test";
            var elbClient = CreateElbClient(targetGroupArn);
            var request = CreateRequest(targetGroupArn, dnsName);
            var snsRequest = Substitute.For<SNSEvent>();

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

            await Handler.Handle(snsRequest);
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
            var dnsName = "http://example.com";
            var targetGroupArn = "arn:aws:elb:us-east-1:1:targetgroup/test/test";
            var elbClient = CreateElbClient(targetGroupArn, targets);
            var request = CreateRequest(targetGroupArn, dnsName);
            var snsRequest = Substitute.For<SNSEvent>();

            dnsResolver
            .Resolve(Arg.Is<string>(hostname => hostname == dnsName))
            .Returns(new IPHostEntry());

            await Handler.Handle(snsRequest);

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
            var dnsName = "http://example.com";
            var targetGroupArn = "arn:aws:elb:us-east-1:1:targetgroup/test/test";
            var elbClient = CreateElbClient(targetGroupArn, targets);
            var request = CreateRequest(targetGroupArn, dnsName);
            var snsRequest = Substitute.For<SNSEvent>();

            dnsResolver
            .Resolve(Arg.Is<string>(hostname => hostname == dnsName))
            .Returns(new IPHostEntry
            {
                AddressList = new IPAddress[] {
                    IPAddress.Parse("10.0.0.1")
                }
            });

            await Handler.Handle(snsRequest);

            await elbClient
            .DidNotReceive()
            .RegisterTargetsAsync(Arg.Any<RegisterTargetsRequest>());
        }
    }
}