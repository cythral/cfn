using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Amazon.ElasticLoadBalancingV2;
using Amazon.ElasticLoadBalancingV2.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SNSEvents;

using Cythral.CloudFormation.UpdateTargets.Request;

using Lambdajection.Attributes;

using Microsoft.Extensions.Logging;

namespace Cythral.CloudFormation.UpdateTargets
{
    [Lambda(typeof(Startup))]
    public partial class Handler
    {
        private readonly DnsResolver dnsResolver;
        private readonly IAmazonElasticLoadBalancingV2 elbClient;
        private readonly ILogger<Handler> logger;
        private readonly UpdateTargetsRequestFactory requestFactory;

        public Handler(
            DnsResolver dnsResolver,
            IAmazonElasticLoadBalancingV2 elbClient,
            UpdateTargetsRequestFactory requestFactory,
            ILogger<Handler> logger
        )
        {
            this.dnsResolver = dnsResolver;
            this.elbClient = elbClient;
            this.requestFactory = requestFactory;
            this.logger = logger;
        }

        public async Task<Response> Handle(SNSEvent snsRequest, CancellationToken cancellationToken = default)
        {
            var request = requestFactory.CreateFromSnsEvent(snsRequest);
            logger.LogInformation($"Received transformed request: {JsonSerializer.Serialize(request)}");

            var addresses = dnsResolver.Resolve(request.TargetDnsName).AddressList ?? new IPAddress[] { };
            var targetHealthRequest = new DescribeTargetHealthRequest { TargetGroupArn = request.TargetGroupArn };
            var targetHealthResponse = await elbClient.DescribeTargetHealthAsync(targetHealthRequest);
            logger.LogInformation($"Got target health response: {JsonSerializer.Serialize(targetHealthResponse)}");

            var targetHealthDescriptions = targetHealthResponse.TargetHealthDescriptions;
            var healthyTargets = from target in targetHealthDescriptions where target.TargetHealth.State != TargetHealthStateEnum.Unhealthy select target.Target;
            var unhealthyTargets = from target in targetHealthDescriptions where target.TargetHealth.State == TargetHealthStateEnum.Unhealthy select target.Target;
            var newTargets = from address in addresses
                             where healthyTargets.All(target => !IPAddress.Parse(target.Id).Equals(address))
                             select new TargetDescription
                             {
                                 Id = address.ToString(),
                                 AvailabilityZone = "all",
                                 Port = 80
                             };

            await Task.WhenAll(new Task[]
            {
                DeregisterTargets(request.TargetGroupArn, unhealthyTargets),
                RegisterTargets(request.TargetGroupArn, newTargets),
            });

            return new Response
            {
                Success = true
            };
        }

        private async Task DeregisterTargets(string targetGroupArn, IEnumerable<TargetDescription> targets)
        {
            if (targets.Count() == 0)
            {
                return;
            }

            var deregisterTargetsResponse = await elbClient.DeregisterTargetsAsync(new DeregisterTargetsRequest
            {
                TargetGroupArn = targetGroupArn,
                Targets = targets.ToList()
            });

            logger.LogInformation($"Got deregister targets response: {JsonSerializer.Serialize(deregisterTargetsResponse)}");
        }

        private async Task RegisterTargets(string targetGroupArn, IEnumerable<TargetDescription> targets)
        {
            if (targets.Count() == 0)
            {
                return;
            }

            var registerTargetsResponse = await elbClient.RegisterTargetsAsync(new RegisterTargetsRequest
            {
                TargetGroupArn = targetGroupArn,
                Targets = targets.ToList()
            });

            logger.LogInformation($"Got register targets response: {JsonSerializer.Serialize(registerTargetsResponse)}");
        }
    }
}