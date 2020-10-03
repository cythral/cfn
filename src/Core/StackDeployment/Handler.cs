using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SQSEvents;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

using Cythral.CloudFormation.StackDeployment.Github;
using Cythral.CloudFormation.StackDeployment.TemplateConfig;

using Lambdajection.Attributes;
using Lambdajection.Core;

using Microsoft.Extensions.Options;

using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.StackDeployment
{
    [Lambda(typeof(Startup))]
    public partial class Handler
    {
        private const string notificationArnKey = "NOTIFICATION_ARN";
        private readonly DeployStackFacade stackDeployer;
        private readonly S3Util s3Util;
        private readonly ParseConfigFileFacade parseConfigFileFacade;
        private readonly TokenGenerator tokenGenerator;
        private readonly RequestFactory requestFactory;
        private readonly IAmazonStepFunctions stepFunctionsClient;
        private readonly IAwsFactory<IAmazonCloudFormation> cloudformationFactory;
        private readonly GithubStatusNotifier statusNotifier;
        private readonly Config config;

        public Handler(
            DeployStackFacade stackDeployer,
            S3Util s3Util,
            ParseConfigFileFacade parseConfigFileFacade,
            TokenGenerator tokenGenerator,
            RequestFactory requestFactory,
            IAmazonStepFunctions stepFunctionsClient,
            IAwsFactory<IAmazonCloudFormation> cloudformationFactory,
            GithubStatusNotifier statusNotifier,
            IOptions<Config> config
        )
        {
            this.stackDeployer = stackDeployer;
            this.s3Util = s3Util;
            this.parseConfigFileFacade = parseConfigFileFacade;
            this.tokenGenerator = tokenGenerator;
            this.requestFactory = requestFactory;
            this.stepFunctionsClient = stepFunctionsClient;
            this.cloudformationFactory = cloudformationFactory;
            this.statusNotifier = statusNotifier;
            this.config = config.Value;
        }

        public async Task<Response> Handle(
            SQSEvent sqsEvent,
            ILambdaContext context = null
        )
        {
            var request = requestFactory.CreateFromSqsEvent(sqsEvent);
            var owner = request.CommitInfo.GithubOwner;
            var repository = request.CommitInfo.GithubRepository;
            var sha = request.CommitInfo.GithubRef;
            var stackName = request.StackName;
            var environmentName = request.EnvironmentName;

            try
            {
                var notificationArn = Environment.GetEnvironmentVariable(notificationArnKey);
                var template = await s3Util.GetZipEntryInObject(request.ZipLocation, request.TemplateFileName);
                var stackConfig = await GetConfig(request);
                var token = await tokenGenerator.Generate(sqsEvent, request);

                await statusNotifier.NotifyPending(owner, repository, sha, stackName, environmentName);
                await stackDeployer.Deploy(new DeployStackContext
                {
                    StackName = request.StackName,
                    Template = template,
                    RoleArn = request.RoleArn,
                    NotificationArn = config.NotificationArn,
                    Parameters = MergeParameters(stackConfig?.Parameters, request.ParameterOverrides),
                    Tags = stackConfig?.Tags,
                    StackPolicyBody = stackConfig?.StackPolicy?.Value,
                    ClientRequestToken = token,
                    Capabilities = request.Capabilities,
                });
            }
            catch (NoUpdatesException)
            {
                var outputs = await GetStackOutputs(request.StackName, request.RoleArn);
                var response = await stepFunctionsClient.SendTaskSuccessAsync(new SendTaskSuccessRequest
                {
                    TaskToken = request.Token,
                    Output = Serialize(outputs)
                });

                await statusNotifier.NotifySuccess(owner, repository, sha, stackName, environmentName);

                return new Response
                {
                    Success = true
                };
            }
            catch (Exception e)
            {
                var response = await stepFunctionsClient.SendTaskFailureAsync(new SendTaskFailureRequest
                {
                    TaskToken = request.Token,
                    Cause = e.Message
                });

                await statusNotifier.NotifyFailure(owner, repository, sha, stackName, environmentName);

                return new Response
                {
                    Success = true
                };
            }

            throw new Exception();
        }

        private async Task<TemplateConfiguration> GetConfig(Request request)
        {
            var fileName = request.TemplateConfigurationFileName;

            if (fileName != null && fileName != "")
            {
                var source = await s3Util.GetZipEntryInObject(request.ZipLocation, fileName);
                return parseConfigFileFacade.Parse(source);
            }

            return null;
        }

        private static List<Parameter> MergeParameters(List<Parameter> parameters, Dictionary<string, string> overrides)
        {
            var result = parameters?.ToDictionary(param => param.ParameterKey, param => param.ParameterValue) ?? new Dictionary<string, string>();
            overrides = overrides ?? new Dictionary<string, string>();

            foreach (var entry in overrides)
            {
                result[entry.Key] = entry.Value;
            }

            return result.Select(entry => new Parameter { ParameterKey = entry.Key, ParameterValue = entry.Value }).ToList();
        }

        private async Task<Dictionary<string, string>> GetStackOutputs(string stackId, string roleArn)
        {
            var client = await cloudformationFactory.Create(roleArn);
            var response = await client.DescribeStacksAsync(new DescribeStacksRequest
            {
                StackName = stackId
            });

            return response.Stacks[0].Outputs.ToDictionary(entry => entry.OutputKey, entry => entry.OutputValue);
        }
    }
}