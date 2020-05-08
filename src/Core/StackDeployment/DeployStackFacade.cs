using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;

using static Amazon.CloudFormation.OnFailure;
using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.StackDeployment
{
    public class DeployStackFacade
    {
        private CloudFormationFactory cloudFormationFactory = new CloudFormationFactory();

        public virtual async Task Deploy(DeployStackContext context)
        {
            var cloudformationClient = await cloudFormationFactory.Create(context.RoleArn);
            var notificationArns = GetNotificationArns(context);
            var stackExists = await DoesStackExist(context, cloudformationClient);

            if (!stackExists)
            {
                var createStackRequest = new CreateStackRequest
                {
                    StackName = context.StackName,
                    TemplateBody = context.Template,
                    Parameters = (List<Parameter>)context.Parameters,
                    Capabilities = (List<string>)context.Capabilities,
                    Tags = (List<Tag>)context.Tags,
                    NotificationARNs = notificationArns,
                    RoleARN = context.PassRoleArn,
                    OnFailure = DELETE
                };

                var createStackResponse = await cloudformationClient.CreateStackAsync(createStackRequest);
                Console.WriteLine($"Got create stack response: {Serialize(createStackResponse)}");
            }
            else
            {
                var updateStackRequest = new UpdateStackRequest
                {
                    StackName = context.StackName,
                    TemplateBody = context.Template,
                    Parameters = (List<Parameter>)context.Parameters,
                    Capabilities = (List<string>)context.Capabilities,
                    Tags = (List<Tag>)context.Tags,
                    NotificationARNs = notificationArns,
                    RoleARN = context.PassRoleArn
                };

                var updateStackResponse = await cloudformationClient.UpdateStackAsync(updateStackRequest);
                Console.WriteLine($"Got update stack response: {Serialize(updateStackResponse)}");
            }
        }

        private async Task<bool> DoesStackExist(DeployStackContext context, IAmazonCloudFormation cloudformationClient)
        {
            try
            { // if this throws, assume the stack does not exist.
                var describeStacksRequest = new DescribeStacksRequest { StackName = context.StackName };
                var describeStacksResponse = await cloudformationClient.DescribeStacksAsync(describeStacksRequest);
                Console.WriteLine($"Got describe stacks response: {Serialize(describeStacksResponse)}");

                return describeStacksResponse.Stacks.Count() != 0;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Describe stacks failure: {e.Message}\n{e.StackTrace}");
            }

            return false;
        }

        private List<string> GetNotificationArns(DeployStackContext context)
        {
            return context.NotificationArn != null ? new List<string> { context.NotificationArn } : null;
        }
    }
}