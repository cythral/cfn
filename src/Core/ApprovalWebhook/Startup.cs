using Lambdajection.Core;
using Amazon.S3;
using Amazon.StepFunctions;
using Cythral.CloudFormation.AwsUtils;
using Cythral.CloudFormation.AwsUtils.SimpleStorageService;
using Microsoft.Extensions.DependencyInjection;

namespace Cythral.CloudFormation.ApprovalWebhook
{
    public class Startup : ILambdaStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.UseAwsService<IAmazonStepFunctions>();
            services.UseAwsService<IAmazonS3>();
            services.AddSingleton<S3GetObjectFacade>();
        }
    }
}