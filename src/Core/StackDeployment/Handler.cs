using System.Text;
using System.Security.Cryptography;
using System;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

using Cythral.CloudFormation.Aws;
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
        private static TokenGenerator tokenGenerator = new TokenGenerator();
        private static RequestFactory requestFactory = new RequestFactory();
        private static StepFunctionsClientFactory stepFunctionsClientFactory = new StepFunctionsClientFactory();

        public static async Task<Response> Handle(
            SQSEvent sqsEvent,
            ILambdaContext context = null
        )
        {
            var request = requestFactory.CreateFromSqsEvent(sqsEvent);

            try
            {
                var notificationArn = Environment.GetEnvironmentVariable(notificationArnKey);
                var template = await s3GetObjectFacade.GetZipEntryInObject(request.ZipLocation, request.TemplateFileName);
                var config = await GetConfig(request);
                var token = await tokenGenerator.Generate(sqsEvent, request);

                await stackDeployer.Deploy(new DeployStackContext
                {
                    StackName = request.StackName,
                    Template = template,
                    RoleArn = request.RoleArn,
                    NotificationArn = notificationArn,
                    Parameters = config?.Parameters,
                    Tags = config?.Tags,
                    StackPolicyBody = config?.StackPolicy?.Value,
                    ClientRequestToken = token
                });
            }
            catch (Exception e)
            {
                var client = stepFunctionsClientFactory.Create();
                var response = await client.SendTaskFailureAsync(new SendTaskFailureRequest
                {
                    TaskToken = request.Token,
                    Cause = e.Message
                });

                return new Response
                {
                    Success = true
                };
            }

            throw new Exception();
        }

        private static async Task<TemplateConfiguration> GetConfig(Request request)
        {
            var fileName = request.TemplateConfigurationFileName;

            if (fileName != null && fileName != "")
            {
                var source = await s3GetObjectFacade.GetZipEntryInObject(request.ZipLocation, fileName);
                return parseConfigFileFacade.Parse(source);
            }

            return null;
        }
    }
}