using System;
using System.Text.Json;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.StepFunctions;

using Cythral.CloudFormation.AwsUtils;
using Cythral.CloudFormation.AwsUtils.SimpleStorageService;

using Lambdajection.Attributes;
using Lambdajection.Core;

using Microsoft.Extensions.DependencyInjection;

namespace Cythral.CloudFormation.DeploymentSupersession
{
    public class Startup : ILambdaStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.UseAwsService<IAmazonS3>();
            services.UseAwsService<IAmazonStepFunctions>();
            services.AddSingleton<RequestFactory>();
            services.AddSingleton<S3GetObjectFacade>();
            services.AddSingleton(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
        }
    }
}