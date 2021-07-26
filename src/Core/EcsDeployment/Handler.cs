using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Amazon.ECS;
using Amazon.ECS.Model;

using Lambdajection.Attributes;
using Lambdajection.Core;

namespace Cythral.CloudFormation.EcsDeployment
{
    [Lambda(typeof(Startup))]
    public partial class Handler
    {
        private readonly IAwsFactory<IAmazonECS> ecsFactory;
        private readonly IDelayFactory delayFactory;

        public Handler(
            IAwsFactory<IAmazonECS> ecsFactory,
            IDelayFactory delayFactory
        )
        {
            this.ecsFactory = ecsFactory;
            this.delayFactory = delayFactory;
        }

        public async Task<object> Handle(DeploymentProperties request, CancellationToken cancellationToken = default)
        {
            var ecsClient = await ecsFactory.Create(request.RoleArn, cancellationToken);
            var updateServiceRequest = new UpdateServiceRequest
            {
                Cluster = request.ClusterName,
                Service = request.ServiceName,
                DesiredCount = 1,
            };

            await ecsClient.UpdateServiceAsync(updateServiceRequest, cancellationToken);
            while (await GetCurrentCount(ecsClient, request, cancellationToken) == 0)
            {
                await delayFactory.CreateDelay(2000, cancellationToken);
            }

            return new { };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task<int> GetCurrentCount(IAmazonECS ecsClient, DeploymentProperties request, CancellationToken cancellationToken)
        {
            var describeServicesRequest = new DescribeServicesRequest
            {
                Cluster = request.ClusterName,
                Services = new List<string> { request.ServiceName }
            };

            var describeServicesResponse = await ecsClient.DescribeServicesAsync(describeServicesRequest, cancellationToken);
            return describeServicesResponse.Services.ElementAt(0).RunningCount;
        }
    }
}