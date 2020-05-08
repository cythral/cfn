using System;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.Lambda.SNSEvents;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

using Cythral.CloudFormation.Facades;
using Cythral.CloudFormation.StackDeployment.TemplateConfig;

using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.StackDeployment
{
    public class Handler
    {
        private const string notificationArnKey = "NOTIFICATION_ARN";
        private static DeployStackFacade stackDeployer = new DeployStackFacade();
        private static S3GetObjectFacade s3GetObjectFacade = new S3GetObjectFacade();
        private static ParseConfigFileFacade parseConfigFileFacade = new ParseConfigFileFacade();

        public static async Task<Response> Handle(
            Request request,
            ILambdaContext context = null
        )
        {
            var notificationArn = Environment.GetEnvironmentVariable(notificationArnKey);
            var template = await s3GetObjectFacade.GetObject(request.ZipLocation, request.TemplateFileName);
            var config = await GetConfig(request);

            await stackDeployer.Deploy(new DeployStackContext
            {
                StackName = request.StackName,
                Template = template,
                RoleArn = request.RoleArn,
                NotificationArn = notificationArn,
                Parameters = config?.Parameters,
                Tags = config?.Tags,
                StackPolicyBody = config?.StackPolicy?.Value,
                ClientRequestToken = request.Token
            });

            return new Response
            {
                Success = true
            };
        }

        private static async Task<TemplateConfiguration> GetConfig(Request request)
        {
            var fileName = request.TemplateConfigurationFileName;

            if (fileName != null && fileName != "")
            {
                var source = await s3GetObjectFacade.GetObject(request.ZipLocation, fileName);
                return parseConfigFileFacade.Parse(source);
            }

            return null;
        }
    }
}