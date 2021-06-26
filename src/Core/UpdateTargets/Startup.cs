using Amazon.ElasticLoadBalancingV2;

using Cythral.CloudFormation.UpdateTargets.Request;

using Lambdajection.Core;

using Microsoft.Extensions.DependencyInjection;

namespace Cythral.CloudFormation.UpdateTargets
{
    public class Startup : ILambdaStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.UseAwsService<IAmazonElasticLoadBalancingV2>();
            services.AddSingleton<DnsResolver>();
            services.AddSingleton<UpdateTargetsRequestFactory>();
        }
    }
}