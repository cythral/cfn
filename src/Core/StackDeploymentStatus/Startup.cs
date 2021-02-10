using System.Threading.Tasks;

using Amazon.CloudFormation;
using Amazon.S3;
using Amazon.SQS;
using Amazon.StepFunctions;

using Cythral.CloudFormation.StackDeploymentStatus.Github;
using Cythral.CloudFormation.StackDeploymentStatus.Request;

using Lambdajection.Core;

using Microsoft.Extensions.DependencyInjection;

namespace Cythral.CloudFormation.StackDeploymentStatus
{
    public class Startup : ILambdaStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.UseAwsService<IAmazonStepFunctions>();
            services.UseAwsService<IAmazonCloudFormation>();
            services.UseAwsService<IAmazonSQS>();
            services.UseAwsService<IAmazonS3>();

            services.AddSingleton<TokenInfoRepository>();
            services.AddSingleton<GithubHttpClient>();
            services.AddSingleton<GithubStatusNotifier>();
            services.AddSingleton<TokenInfoRepository>();
            services.AddSingleton<StackDeploymentStatusRequestFactory>();
            services.AddSingleton<Wait>(Task.Delay);
        }
    }
}