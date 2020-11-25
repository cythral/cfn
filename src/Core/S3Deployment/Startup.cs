using System.Net.Http;

using Amazon.S3;

using Cythral.CloudFormation.AwsUtils.SimpleStorageService;

using Lambdajection.Core;

using Microsoft.Extensions.DependencyInjection;

namespace Cythral.CloudFormation.S3Deployment
{
    public class Startup : ILambdaStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.UseAwsService<IAmazonS3>();
            services.AddSingleton<S3GetObjectFacade>();
            services.AddSingleton<GithubStatusNotifier>();
            services.AddSingleton<HttpClient>();
        }
    }
}