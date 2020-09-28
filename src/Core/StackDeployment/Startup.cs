using Amazon.CloudFormation;
using Amazon.S3;
using Amazon.StepFunctions;

using Cythral.CloudFormation.AwsUtils.CloudFormation;
using Cythral.CloudFormation.AwsUtils.SimpleStorageService;
using Cythral.CloudFormation.GithubUtils;
using Cythral.CloudFormation.StackDeployment.TemplateConfig;

using Lambdajection.Core;

using Microsoft.Extensions.DependencyInjection;

namespace Cythral.CloudFormation.StackDeployment
{
    public class Startup : ILambdaStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<DeployStackFacade>();
            services.AddSingleton<S3GetObjectFacade>();
            services.AddSingleton<ParseConfigFileFacade>();
            services.AddSingleton<TokenGenerator>();
            services.AddSingleton<RequestFactory>();
            services.AddSingleton<PutCommitStatusFacade>();

            services.UseAwsService<IAmazonStepFunctions>();
            services.UseAwsService<IAmazonCloudFormation>();
            services.UseAwsService<IAmazonS3>();
        }
    }
}