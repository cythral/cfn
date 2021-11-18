using System.Text.Json;

using Amazon.CloudFormation;
using Amazon.S3;
using Amazon.StepFunctions;

using Cythral.CloudFormation.StackDeployment.Github;
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
            services.AddSingleton<S3Util>();
            services.AddSingleton<ParseConfigFileFacade>();
            services.AddSingleton<TokenGenerator>();
            services.AddSingleton<RequestFactory>();
            services.AddSingleton<IGithubHttpClient, GithubHttpClient>();
            services.AddSingleton<GithubStatusNotifier>();

            services.UseAwsService<IAmazonStepFunctions>();
            services.UseAwsService<IAmazonCloudFormation>();
            services.UseAwsService<IAmazonS3>();
            services.AddSingleton(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
        }
    }
}