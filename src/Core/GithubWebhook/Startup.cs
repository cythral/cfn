using System.Text.Json;

using Amazon.CloudFormation;
using Amazon.S3;
using Amazon.StepFunctions;

using Cythral.CloudFormation.GithubWebhook.Github;
using Cythral.CloudFormation.GithubWebhook.Pipelines;
using Cythral.CloudFormation.GithubWebhook.StackDeployment;

using Lambdajection.Core;

using Microsoft.Extensions.DependencyInjection;

namespace Cythral.CloudFormation.GithubWebhook
{
    public class Startup : ILambdaStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<GithubHttpClient>();
            services.AddSingleton<Sha256SumComputer>();
            services.AddSingleton<GithubFileFetcher>();
            services.AddSingleton<GithubStatusNotifier>();
            services.AddSingleton<RequestValidator>();
            services.AddSingleton<DeployStackFacade>();
            services.AddSingleton<PipelineDeployer>();
            services.AddSingleton<PipelineStarter>();
            services.AddSingleton<GithubCommitMessageFetcher>();

            services.UseAwsService<IAmazonS3>();
            services.UseAwsService<IAmazonStepFunctions>();
            services.UseAwsService<IAmazonCloudFormation>();
            services.AddSingleton(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });
        }
    }
}