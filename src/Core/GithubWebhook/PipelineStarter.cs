using System;
using System.Threading.Tasks;
using Cythral.CloudFormation.Events;
using Cythral.CloudFormation.Aws;
using Amazon.StepFunctions.Model;
using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.GithubWebhook
{
    public class PipelineStarter
    {
        private StepFunctionsClientFactory stepFunctionsClientFactory = new StepFunctionsClientFactory();

        public virtual async Task StartPipelineIfExists(PushEvent payload)
        {
            var client = stepFunctionsClientFactory.Create();

            try
            {
                var accountId = Environment.GetEnvironmentVariable("AWS_ACCOUNT_ID");
                var region = Environment.GetEnvironmentVariable("AWS_REGION");

                var response = await client.StartExecutionAsync(new StartExecutionRequest
                {
                    StateMachineArn = $"arn:aws:states:{region}:{accountId}:stateMachine:{payload.Repository.Name}-cicd-pipeline",
                    Name = payload.HeadCommit.Id,
                    Input = Serialize(payload)
                });

                Console.WriteLine($"Received start execution response: {Serialize(response)}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Got error trying to start pipeline: {e.Message}");
            }
        }
    }
}