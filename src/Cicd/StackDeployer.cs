using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;

using static Amazon.CloudFormation.OnFailure;
using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.Cicd {
    public class StackDeployer {

        // maybe this could be an extension method for the cloudformation client instead?
        public static async Task Deploy(
            string stackName, 
            string template,
            IEnumerable<string> capabilities = null, 
            IAmazonCloudFormation cloudformationClient = null
        ) {
            capabilities = capabilities ?? new List<string> { "CAPABILITY_IAM", "CAPABILITY_NAMED_IAM" };
            cloudformationClient = cloudformationClient ?? new AmazonCloudFormationClient();

            var describeStacksRequest = new DescribeStacksRequest { StackName = stackName };
            var describeStacksResponse = await cloudformationClient.DescribeStacksAsync(describeStacksRequest);
            Console.WriteLine($"Got describe stacks response: {Serialize(describeStacksResponse)}");

            if(describeStacksResponse.Stacks.Count() == 0) {
                var createStackRequest = new CreateStackRequest {
                    StackName =  stackName,
                    TemplateBody = template,
                    Capabilities = (List<string>) capabilities,
                    OnFailure = DELETE
                };

                var createStackResponse = await cloudformationClient.CreateStackAsync(createStackRequest);
                Console.WriteLine($"Got create stack response: {createStackResponse}");
            } else {
                var updateStackRequest = new UpdateStackRequest {
                    StackName = stackName,
                    TemplateBody = template,
                    Capabilities = (List<string>) capabilities
                };

                var updateStackResponse = await cloudformationClient.UpdateStackAsync(updateStackRequest);
                Console.WriteLine($"Got update stack response: {updateStackResponse}");
            }
        }
    }
}