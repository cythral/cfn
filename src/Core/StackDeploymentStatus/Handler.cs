using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.SNSEvents;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

using Cythral.CloudFormation.StackDeploymentStatus.Github;
using Cythral.CloudFormation.StackDeploymentStatus.Request;

using Lambdajection.Attributes;
using Lambdajection.Core;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cythral.CloudFormation.StackDeploymentStatus
{
    [Lambda(typeof(Startup))]
    public partial class Handler
    {
        private readonly StackDeploymentStatusRequestFactory requestFactory;
        private readonly IAmazonStepFunctions stepFunctionsClient;
        private readonly IAmazonSQS sqsClient;
        private readonly IAwsFactory<IAmazonCloudFormation> cloudformationFactory;
        private readonly GithubStatusNotifier githubStatusNotifier;
        private readonly TokenInfoRepository tokenInfoRepository;
        private readonly Config config;
        private readonly ILogger<Handler> logger;

        public Handler(
            StackDeploymentStatusRequestFactory requestFactory,
            IAmazonStepFunctions stepFunctionsClient,
            IAmazonSQS sqsClient,
            IAwsFactory<IAmazonCloudFormation> cloudformationFactory,
            GithubStatusNotifier githubStatusNotifier,
            TokenInfoRepository tokenInfoRepository,
            IOptions<Config> config,
            ILogger<Handler> logger
        )
        {
            this.requestFactory = requestFactory;
            this.stepFunctionsClient = stepFunctionsClient;
            this.sqsClient = sqsClient;
            this.cloudformationFactory = cloudformationFactory;
            this.tokenInfoRepository = tokenInfoRepository;
            this.githubStatusNotifier = githubStatusNotifier;
            this.config = config.Value;
            this.logger = logger;
        }

        public async Task<Response> Handle(
            SNSEvent snsRequest,
            ILambdaContext context = null
        )
        {
            logger.LogInformation($"Received request: {JsonSerializer.Serialize(snsRequest)}");

            var request = requestFactory.CreateFromSnsEvent(snsRequest);
            var status = request.ResourceStatus;

            if (request.ResourceType == "AWS::CloudFormation::Stack" && request.ClientRequestToken.Length > 0)
            {
                if (status == "DELETE_COMPLETE" || status.EndsWith("ROLLBACK_COMPLETE") || status.EndsWith("FAILED"))
                {
                    await SendFailure(request);
                }

                if (status.EndsWith("COMPLETE"))
                {
                    await SendSuccess(request);
                }

            }

            return new Response { Success = true };
        }

        private async Task SendFailure(StackDeploymentStatusRequest request)
        {
            var tokenInfo = await tokenInfoRepository.FindByRequest(request);

            if (request.SourceTopic != config.GithubTopicArn)
            {
                var response = await stepFunctionsClient.SendTaskFailureAsync(new SendTaskFailureRequest
                {
                    TaskToken = tokenInfo.ClientRequestToken,
                    Cause = request.ResourceStatus
                });

                logger.LogInformation($"Received send task failure response: {JsonSerializer.Serialize(response)}");
                await Dequeue(tokenInfo);
            }

            await githubStatusNotifier.NotifyFailure(
                tokenInfo.GithubOwner,
                tokenInfo.GithubRepo,
                tokenInfo.GithubRef,
                request.StackName,
                tokenInfo.EnvironmentName
            );
        }

        private async Task SendSuccess(StackDeploymentStatusRequest request)
        {
            var tokenInfo = await tokenInfoRepository.FindByRequest(request);
            if (request.SourceTopic != config.GithubTopicArn)
            {
                var outputs = await GetStackOutputs(request.StackId, tokenInfo.RoleArn);
                var response = await stepFunctionsClient.SendTaskSuccessAsync(new SendTaskSuccessRequest
                {
                    TaskToken = tokenInfo.ClientRequestToken,
                    Output = JsonSerializer.Serialize(outputs)
                });

                logger.LogInformation($"Received send task success response: {JsonSerializer.Serialize(response)}");
                await Dequeue(tokenInfo);
            }

            await githubStatusNotifier.NotifySuccess(
                tokenInfo.GithubOwner,
                tokenInfo.GithubRepo,
                tokenInfo.GithubRef,
                request.StackName,
                tokenInfo.EnvironmentName
            );
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

        private async Task Dequeue(TokenInfo tokenInfo)
        {
            var response = await sqsClient.DeleteMessageAsync(new DeleteMessageRequest
            {
                QueueUrl = tokenInfo.QueueUrl,
                ReceiptHandle = tokenInfo.ReceiptHandle,
            });

            logger.LogInformation($"Got delete message response: {JsonSerializer.Serialize(response)}");
        }
    }
}