using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;

using static Amazon.CloudFormation.OnFailure;
using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation {
    public class StackDeployer {

        // maybe this could be an extension method for the cloudformation client instead?
        public static async Task Deploy(
            string stackName, 
            string template,
            string roleArn = null,
            IEnumerable<Parameter> parameters = null,
            IEnumerable<string> capabilities = null, 
            IAmazonCloudFormation cloudformationClient = null
        ) {
            capabilities = capabilities ?? new List<string> { "CAPABILITY_IAM", "CAPABILITY_NAMED_IAM" };
            cloudformationClient = cloudformationClient ?? new AmazonCloudFormationClient();

            bool stackExists = false;

            try { // if this throws, assume the stack does not exist.
                var describeStacksRequest = new DescribeStacksRequest { StackName = stackName };
                var describeStacksResponse = await cloudformationClient.DescribeStacksAsync(describeStacksRequest);
                Console.WriteLine($"Got describe stacks response: {Serialize(describeStacksResponse)}");

                stackExists = describeStacksResponse.Stacks.Count() != 0;
            } catch(Exception e) {
                Console.WriteLine($"Describe stacks failure: {e.Message}\n{e.StackTrace}");
            } 

            if(!stackExists) {
                var createStackRequest = new CreateStackRequest {
                    StackName =  stackName,
                    TemplateBody = template,
                    Parameters = (List<Parameter>) parameters,
                    Capabilities = (List<string>) capabilities,
                    RoleARN = roleArn,
                    OnFailure = DELETE
                };

                var createStackResponse = await cloudformationClient.CreateStackAsync(createStackRequest);
                Console.WriteLine($"Got create stack response: {Serialize(createStackResponse)}");
            } else {
                var updateStackRequest = new UpdateStackRequest {
                    StackName = stackName,
                    TemplateBody = template,
                    Parameters = (List<Parameter>) parameters,
                    Capabilities = (List<string>) capabilities,
                    RoleARN = roleArn
                };

                var updateStackResponse = await cloudformationClient.UpdateStackAsync(updateStackRequest);
                Console.WriteLine($"Got update stack response: {Serialize(updateStackResponse)}");
            }
        }
    }
}