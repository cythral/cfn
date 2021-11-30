using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

using Cythral.CloudFormation.StackDeploymentStatus.Github;

using Lambdajection.Attributes;
using Lambdajection.Core;
using Lambdajection.Sns;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cythral.CloudFormation.StackDeploymentStatus
{
    [SnsEventHandler(typeof(Startup))]
    public partial class Handler
    {
        private readonly IAmazonStepFunctions stepFunctionsClient;
        private readonly IAmazonSQS sqsClient;
        private readonly IAwsFactory<IAmazonCloudFormation> cloudformationFactory;
        private readonly GithubStatusNotifier githubStatusNotifier;
        private readonly TokenInfoRepository tokenInfoRepository;
        private readonly Config config;
        private readonly ILogger<Handler> logger;

        public Handler(
            IAmazonStepFunctions stepFunctionsClient,
            IAmazonSQS sqsClient,
            IAwsFactory<IAmazonCloudFormation> cloudformationFactory,
            GithubStatusNotifier githubStatusNotifier,
            TokenInfoRepository tokenInfoRepository,
            IOptions<Config> config,
            ILogger<Handler> logger
        )
        {
            this.stepFunctionsClient = stepFunctionsClient;
            this.sqsClient = sqsClient;
            this.cloudformationFactory = cloudformationFactory;
            this.tokenInfoRepository = tokenInfoRepository;
            this.githubStatusNotifier = githubStatusNotifier;
            this.config = config.Value;
            this.logger = logger;
        }

        public async Task<Response> Handle(SnsMessage<CloudFormationStackEvent> request, CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Received request: {@request}", JsonSerializer.Serialize(request));
            var message = request.Message;
            var status = message.ResourceStatus;

            if (message.ClientRequestToken.Length > 0 && message.StackId == message.PhysicalResourceId)
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

        private async Task SendFailure(SnsMessage<CloudFormationStackEvent> request)
        {
            var tokenInfo = await tokenInfoRepository.FindByRequest(request.Message);

            if (request.Message.SourceTopic != config.GithubTopicArn)
            {
                var response = await stepFunctionsClient.SendTaskFailureAsync(new SendTaskFailureRequest
                {
                    TaskToken = tokenInfo.ClientRequestToken,
                    Cause = request.Message.ResourceStatus
                });

                logger.LogInformation($"Received send task failure response: {JsonSerializer.Serialize(response)}");
                await Dequeue(tokenInfo);
            }

            await githubStatusNotifier.NotifyFailure(
                tokenInfo.GithubOwner,
                tokenInfo.GithubRepo,
                tokenInfo.GithubRef,
                request.Message.StackName,
                tokenInfo.EnvironmentName
            );
        }

        private async Task SendSuccess(SnsMessage<CloudFormationStackEvent> request)
        {
            var tokenInfo = await tokenInfoRepository.FindByRequest(request.Message);
            if (request.Message.SourceTopic != config.GithubTopicArn)
            {
                var outputs = await GetStackOutputs(request.Message.StackId, tokenInfo.RoleArn);
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
                request.Message.StackName,
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