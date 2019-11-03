using System;
using System.Net;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Amazon;
using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.ElasticLoadBalancingV2;
using Amazon.ElasticLoadBalancingV2.Model;

using Cythral;
using Cythral.CloudFormation;
using Cythral.CloudFormation.Facades;

using static System.Text.Json.JsonSerializer;
using static Amazon.ElasticLoadBalancingV2.TargetHealthStateEnum;

namespace Cythral.CloudFormation.Handlers {
    public class UpdateTargetsHandler {
        public class Request {
            public string TargetGroupArn { get; set; }
            public string TargetDnsName { get; set; }
        }

        public class Response {
            public bool Success { get; set; }
        }

        public static async Task<Response> Handle(
            Request request,
            ILambdaContext context
        ) {
            var resolver = new DnsResolver();   
            var elbClient = new AmazonElasticLoadBalancingV2Client();
            return await Handle(request, resolver, elbClient, context);
        }

        public static async Task<Response> Handle(
            Request request, 
            IDnsResolver resolver,
            IAmazonElasticLoadBalancingV2 elbClient,
            ILambdaContext context = null
        ) {
            resolver = resolver ?? new DnsResolver();
            elbClient = elbClient ?? new AmazonElasticLoadBalancingV2Client();
            
            var addresses = resolver.Resolve(request.TargetDnsName).AddressList ?? new IPAddress[] {};
            var targetHealthRequest = new DescribeTargetHealthRequest { TargetGroupArn = request.TargetGroupArn };
            var targetHealthResponse = await elbClient.DescribeTargetHealthAsync(targetHealthRequest);
            Console.WriteLine($"Got target health response: {Serialize(targetHealthResponse)}");
            
            var targetHealthDescriptions = targetHealthResponse.TargetHealthDescriptions;
            var healthyTargets = from target in targetHealthDescriptions where target.TargetHealth.State != Unhealthy select target.Target;
            var unhealthyTargets = from target in targetHealthDescriptions where target.TargetHealth.State == Unhealthy select target.Target;
            
            if(unhealthyTargets.Count() > 0) {
                var deregisterTargetsResponse = await elbClient.DeregisterTargetsAsync(new DeregisterTargetsRequest {
                    TargetGroupArn = request.TargetGroupArn,
                    Targets = unhealthyTargets.ToList()
                });

                Console.WriteLine($"Got deregister targets response: {Serialize(deregisterTargetsResponse)}");
            }

            var newTargets = from address in addresses 
                where healthyTargets.All(target => !IPAddress.Parse(target.Id).Equals(address))
                select new TargetDescription {
                    Id = address.ToString(),
                    AvailabilityZone = "all",
                    Port = 80
                };
            
            if(newTargets.Count() > 0) {
                var registerTargetsResponse = await elbClient.RegisterTargetsAsync(new RegisterTargetsRequest {
                    TargetGroupArn = request.TargetGroupArn,
                    Targets = newTargets.ToList()
                });
            
                Console.WriteLine($"Got register targets response: {Serialize(registerTargetsResponse)}");
            }

            return new Response {
                Success = true
            };
        }
    }
}