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

        public virtual async Task StartPipelineIfExists(GithubEvent payload)
        {
            try
            {
                var accountId = Environment.GetEnvironmentVariable("AWS_ACCOUNT_ID");
                var region = Environment.GetEnvironmentVariable("AWS_REGION");

                var pushEventInput = payload is PushEvent pushEvent ? Serialize(pushEvent) : null;
                var prEventInput = payload is PullRequestEvent pullEvent ? Serialize(pullEvent) : null;
                var input = pushEventInput ?? prEventInput;

                var stateMachineArn = $"arn:aws:states:{region}:{accountId}:stateMachine:{payload.Repository.Name}-cicd-pipeline";
                var request = new StartExecutionRequest { StateMachineArn = stateMachineArn, Input = input };

                if (payload.Ref.StartsWith("refs/heads/"))
                {
                    request.Name = payload.HeadCommitId;
                }

                var response = await stepFunctionsClient.StartExecutionAsync(request);
                logger.LogInformation($"Received start execution response: {Serialize(response)}");
            }
            catch (Exception e)
            {
                logger.LogError($"Got error trying to start pipeline: {e.Message}");
            }
        }
    }
}