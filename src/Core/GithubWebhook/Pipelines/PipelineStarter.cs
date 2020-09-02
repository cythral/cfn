using System;
using System.Threading.Tasks;

using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

using Cythral.CloudFormation.GithubWebhook.Github;

using Microsoft.Extensions.Logging;

using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.GithubWebhook.Pipelines
{
    public class PipelineStarter
    {
        private readonly IAmazonStepFunctions stepFunctionsClient;
        private readonly ILogger<PipelineStarter> logger;

        public PipelineStarter(IAmazonStepFunctions stepFunctionsClient, ILogger<PipelineStarter> logger)
        {
            this.stepFunctionsClient = stepFunctionsClient;
            this.logger = logger;
        }

        internal PipelineStarter()
        {
            // used for testing
        }

        public virtual async Task StartPipelineIfExists(PushEvent payload)
        {
            try
            {
                var accountId = Environment.GetEnvironmentVariable("AWS_ACCOUNT_ID");
                var region = Environment.GetEnvironmentVariable("AWS_REGION");

                var response = await stepFunctionsClient.StartExecutionAsync(new StartExecutionRequest
                {
                    StateMachineArn = $"arn:aws:states:{region}:{accountId}:stateMachine:{payload.Repository.Name}-cicd-pipeline",
                    Name = payload.HeadCommit.Id,
                    Input = Serialize(payload)
                });

                logger.LogInformation($"Received start execution response: {Serialize(response)}");
            }
            catch (Exception e)
            {
                logger.LogError($"Got error trying to start pipeline: {e.Message}");
            }
        }
    }
}