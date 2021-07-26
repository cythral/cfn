using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Amazon.ECS;
using Amazon.ECS.Model;

using AutoFixture.AutoNSubstitute;
using AutoFixture.NUnit3;

using FluentAssertions.Common;

using Lambdajection.Core;

using NSubstitute;

using NUnit.Framework;

using static NSubstitute.Arg;

using Task = System.Threading.Tasks.Task;

namespace Cythral.CloudFormation.EcsDeployment
{
    public class HandlerTests
    {
        [Test, Auto, Timeout(2000)]
        public async Task ShouldAssumeGivenRoleArn(
            DeploymentProperties request,
            Service service,
            [Frozen, Substitute] IAwsFactory<IAmazonECS> ecsFactory,
            [Frozen, Substitute] IAmazonECS ecsClient,
            [Target] Handler handler,
            CancellationToken cancellationToken
        )
        {
            service.RunningCount = 1;
            ecsFactory.Create(Any<string>(), Any<CancellationToken>()).Returns(ecsClient);
            ecsClient.DescribeServicesAsync(Any<DescribeServicesRequest>(), Any<CancellationToken>()).Returns(new DescribeServicesResponse
            {
                Services = new List<Service> { service }
            });

            await handler.Handle(request, cancellationToken);

            await ecsFactory.Received().Create(Is(request.RoleArn), Is(cancellationToken));
        }

        [Test, Auto, Timeout(2000)]
        public async Task ShouldSetDesiredCountTo1(
            DeploymentProperties request,
            Service service,
            [Frozen, Substitute] IAwsFactory<IAmazonECS> ecsFactory,
            [Frozen, Substitute] IAmazonECS ecsClient,
            [Target] Handler handler,
            CancellationToken cancellationToken
        )
        {
            service.RunningCount = 1;
            ecsFactory.Create(Any<string>(), Any<CancellationToken>()).Returns(ecsClient);
            ecsClient.DescribeServicesAsync(Any<DescribeServicesRequest>(), Any<CancellationToken>()).Returns(new DescribeServicesResponse
            {
                Services = new List<Service> { service }
            });

            await handler.Handle(request, cancellationToken);

            await ecsClient.Received().UpdateServiceAsync(
                Is<UpdateServiceRequest>(req =>
                    req.DesiredCount == 1 &&
                    req.Cluster == request.ClusterName &&
                    req.Service == request.ServiceName
                ),
                Is(cancellationToken)
            );
        }

        [Test, Auto, Timeout(2000)]
        public async Task ShouldWaitUntilServiceRunningCountEquals1(
            DeploymentProperties request,
            Service service,
            [Frozen, Substitute] IAwsFactory<IAmazonECS> ecsFactory,
            [Frozen, Substitute] IAmazonECS ecsClient,
            [Frozen, Substitute] IDelayFactory delayFactory,
            [Target] Handler handler,
            CancellationToken cancellationToken
        )
        {
            var tries = 0;
            service.RunningCount = 0;

            ecsFactory.Create(Any<string>(), Any<CancellationToken>()).Returns(ecsClient);
            ecsClient.DescribeServicesAsync(Any<DescribeServicesRequest>(), Any<CancellationToken>()).Returns(new DescribeServicesResponse
            {
                Services = new List<Service> { service }
            });

            delayFactory.CreateDelay(Any<int>(), Any<CancellationToken>()).Returns(x =>
            {
                if (++tries == 2)
                {
                    service.RunningCount = 1;
                }

                return Task.CompletedTask;
            });

            await handler.Handle(request, cancellationToken);

            await delayFactory.Received(2).CreateDelay(Is(2000), Is(cancellationToken));
            await ecsClient.Received(3).DescribeServicesAsync(
                Is<DescribeServicesRequest>(req =>
                    req.Cluster == request.ClusterName &&
                    req.Services.ElementAt(0) == request.ServiceName
                ),
                Is(cancellationToken)
            );
        }
    }
}