using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;

using static System.Text.Json.JsonSerializer;
using static Amazon.CloudFormation.OnFailure;

namespace Cythral.CloudFormation.AwsUtils.CloudFormation
{
    public class DeployStackFacade
    {
        private AmazonClientFactory<IAmazonCloudFormation> cloudformationFactory = new AmazonClientFactory<IAmazonCloudFormation>();

        public virtual async Task Deploy(DeployStackContext context)
        {
            var cloudformationClient = await cloudformationFactory.Create(context.RoleArn);
            var notificationArns = GetNotificationArns(context);
            var stackExists = await DoesStackExist(context, cloudformationClient);
            var parameters = (List<Parameter>)context.Parameters ?? new List<Parameter> { };
            var capabilities = (List<string>)context.Capabilities ?? new List<string> { };
            var tags = (List<Tag>)context.Tags ?? new List<Tag> { };


            if (!stackExists)
            {
                var createStackRequest = new CreateStackRequest
                {
                    StackName = context.StackName,
                    TemplateBody = context.Template,
                    Parameters = parameters,
                    Capabilities = capabilities,
                    Tags = tags,
                    NotificationARNs = notificationArns,
                    RoleARN = context.PassRoleArn,
                    ClientRequestToken = context.ClientRequestToken,
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
                    Parameters = parameters,
                    Capabilities = capabilities,
                    Tags = tags,
                    NotificationARNs = notificationArns,
                    ClientRequestToken = context.ClientRequestToken,
                    RoleARN = context.PassRoleArn
                };

                try
                {
                    var updateStackResponse = await cloudformationClient.UpdateStackAsync(updateStackRequest);
                    Console.WriteLine($"Got update stack response: {Serialize(updateStackResponse)}");
                }
                catch (Exception e)
                {
                    if (e.Message == "No updates are to be performed.")
                    {
                        throw new NoUpdatesException(e.Message);
                    }
                    else
                    {
                        throw e;
                    }
                }
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
            return context.NotificationArn != null ? new List<string> { context.NotificationArn } : new List<string> { };
        }
    }
}