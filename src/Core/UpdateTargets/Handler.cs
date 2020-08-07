using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Amazon.ElasticLoadBalancingV2;
using Amazon.ElasticLoadBalancingV2.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SNSEvents;

using Cythral.CloudFormation.AwsUtils;
using Cythral.CloudFormation.UpdateTargets.DnsResolver;
using Cythral.CloudFormation.UpdateTargets.Request;

using static System.Text.Json.JsonSerializer;
using static Amazon.ElasticLoadBalancingV2.TargetHealthStateEnum;

namespace Cythral.CloudFormation.UpdateTargets
{
    public class Handler
    {
        private static DnsResolverFactory dnsResolverFactory = new DnsResolverFactory();
        private static AmazonClientFactory<IAmazonElasticLoadBalancingV2> elbClientFactory = new AmazonClientFactory<IAmazonElasticLoadBalancingV2>();
        private static UpdateTargetsRequestFactory requestFactory = new UpdateTargetsRequestFactory();

        [LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]
        public static async Task<Response> Handle(
            SNSEvent snsRequest,
            ILambdaContext context = null
        )
        {
            var resolver = dnsResolverFactory.Create();
            var elbClient = await elbClientFactory.Create();
            var request = requestFactory.CreateFromSnsEvent(snsRequest);

            Console.WriteLine($"Received transformed request: {Serialize(request)}");

            var addresses = resolver.Resolve(request.TargetDnsName).AddressList ?? new IPAddress[] { };
            var targetHealthRequest = new DescribeTargetHealthRequest { TargetGroupArn = request.TargetGroupArn };
            var targetHealthResponse = await elbClient.DescribeTargetHealthAsync(targetHealthRequest);
            Console.WriteLine($"Got target health response: {Serialize(targetHealthResponse)}");

            var targetHealthDescriptions = targetHealthResponse.TargetHealthDescriptions;
            var healthyTargets = from target in targetHealthDescriptions where target.TargetHealth.State != Unhealthy select target.Target;
            var unhealthyTargets = from target in targetHealthDescriptions where target.TargetHealth.State == Unhealthy select target.Target;

            if (unhealthyTargets.Count() > 0)
            {
                var deregisterTargetsResponse = await elbClient.DeregisterTargetsAsync(new DeregisterTargetsRequest
                {
                    TargetGroupArn = request.TargetGroupArn,
                    Targets = unhealthyTargets.ToList()
                });

                Console.WriteLine($"Got deregister targets response: {Serialize(deregisterTargetsResponse)}");
            }

            var newTargets = from address in addresses
                             where healthyTargets.All(target => !IPAddress.Parse(target.Id).Equals(address))
                             select new TargetDescription
                             {
                                 Id = address.ToString(),
                                 AvailabilityZone = "all",
                                 Port = 80
                             };

            if (newTargets.Count() > 0)
            {
                var registerTargetsResponse = await elbClient.RegisterTargetsAsync(new RegisterTargetsRequest
                {
                    TargetGroupArn = request.TargetGroupArn,
                    Targets = newTargets.ToList()
                });

                Console.WriteLine($"Got register targets response: {Serialize(registerTargetsResponse)}");
            }

            return new Response
            {
                Success = true
            };
        }
    }
}