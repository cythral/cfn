using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using RichardSzalay.MockHttp;

using NUnit.Framework;

using Cythral.CloudFormation;
using Cythral.CloudFormation.Events;
using Cythral.CloudFormation.Entities;
using Cythral.CloudFormation.Exceptions;
using Cythral.CloudFormation.Handlers;

using Amazon;
using Amazon.ElasticLoadBalancingV2;
using Amazon.ElasticLoadBalancingV2.Model;

using static System.Net.HttpStatusCode;
using static Amazon.ElasticLoadBalancingV2.TargetHealthStateEnum;
using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.Tests.Handlers {
    public class UpdateTargetsHandlerTest {
        private IAmazonElasticLoadBalancingV2 CreateElbClient(string targetGroupArn) {
            var elbClient = Substitute.For<IAmazonElasticLoadBalancingV2>();

            elbClient
            .DescribeTargetHealthAsync(
                Arg.Is<DescribeTargetHealthRequest>(req =>
                    req.TargetGroupArn == targetGroupArn
                )
            )
            .Returns(new DescribeTargetHealthResponse {
                TargetHealthDescriptions = new List<TargetHealthDescription> {
                    new TargetHealthDescription {
                        Target = new TargetDescription { Id = "10.0.0.1" },
                        TargetHealth = new TargetHealth { State = Unhealthy }
                    },
                    new TargetHealthDescription {
                        Target = new TargetDescription { Id = "10.0.0.2" },
                        TargetHealth = new TargetHealth { State = Healthy }
                    }
                }
            });

            return elbClient;
        }

        [Test] 
        public async Task HandleCallsResolve() {
            var dnsResolver = Substitute.For<IDnsResolver>();
            var dnsName = "http://example.com";
            var targetGroupArn = "arn:aws:elb:us-east-1:1:targetgroup/test/test";
            var elbClient = CreateElbClient(targetGroupArn);
            var request = new UpdateTargetsHandler.Request {
                TargetGroupArn = targetGroupArn,
                TargetDnsName = dnsName,
            };

            dnsResolver
            .Resolve(Arg.Any<string>())
            .Returns(new IPHostEntry());

            await UpdateTargetsHandler.Handle(
                request:        request,
                resolver:       dnsResolver,
                elbClient:      elbClient
            );

            dnsResolver
            .Received()
            .Resolve(
                Arg.Is<string>(req => req == dnsName)
            );
        }

        [Test]
        public async Task HandleDeregistersUnhealthyTargets() {
            var dnsResolver = Substitute.For<IDnsResolver>();
            var dnsName = "http://example.com";
            var targetGroupArn = "arn:aws:elb:us-east-1:1:targetgroup/test/test";
            var elbClient = CreateElbClient(targetGroupArn);
            var request = new UpdateTargetsHandler.Request {
                TargetGroupArn = targetGroupArn,
                TargetDnsName = dnsName,
            };

            dnsResolver
            .Resolve(Arg.Is<string>(hostname => hostname == dnsName))
            .Returns(new IPHostEntry());

            await UpdateTargetsHandler.Handle(
                request:        request,
                resolver:       dnsResolver,
                elbClient:      elbClient
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
        public async Task HandleRegistersNewTargets() {
            var dnsResolver = Substitute.For<IDnsResolver>();
            var dnsName = "http://example.com";
            var targetGroupArn = "arn:aws:elb:us-east-1:1:targetgroup/test/test";
            var elbClient = CreateElbClient(targetGroupArn);
            var request = new UpdateTargetsHandler.Request {
                TargetGroupArn = targetGroupArn,
                TargetDnsName = dnsName,
            };

            dnsResolver
            .Resolve(Arg.Is<string>(hostname => hostname == dnsName))
            .Returns(new IPHostEntry {
                AddressList = new IPAddress[] {
                    IPAddress.Parse("10.0.0.2"),
                    IPAddress.Parse("10.0.0.3"),
                    IPAddress.Parse("10.0.0.4")
                }
            });

            await UpdateTargetsHandler.Handle(
                request:        request,
                resolver:       dnsResolver,
                elbClient:      elbClient
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
    }
}