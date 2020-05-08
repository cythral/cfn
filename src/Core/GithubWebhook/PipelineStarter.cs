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
                var response = await client.StartExecutionAsync(new StartExecutionRequest
                {
                    Name = $"{payload.Repository.Name}-cicd-pipeline",
                    Input = Serialize(payload)
                });

                Console.WriteLine($"Received start execution response: {Serialize(response)}");
            }
            catch (Exception) { }
        }
    }
}