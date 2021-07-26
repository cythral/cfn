using System;

using Amazon.ECS;

using Lambdajection.Core;

using Microsoft.Extensions.DependencyInjection;

namespace Cythral.CloudFormation.EcsDeployment
{
    public class Startup : ILambdaStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.UseAwsService<IAmazonECS>();
            services.AddSingleton<IDelayFactory, DefaultDelayFactory>();
        }
    }
}