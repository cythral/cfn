using Lambdajection.Core;
using Amazon.S3;
using Cythral.CloudFormation.AwsUtils.CloudFormation;

using Microsoft.Extensions.DependencyInjection;

namespace Cythral.CloudFormation.GithubWebhook
{
    public class Startup : ILambdaStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.UseAwsService<IAmazonS3>();
            services.AddSingleton<RequestValidator>();
            services.AddSingleton<DeployStackFacade>();
            services.AddSingleton<PipelineStarter>();
        }
    }
}