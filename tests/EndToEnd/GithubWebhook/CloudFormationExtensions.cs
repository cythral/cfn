using System;
using System.Threading;
using System.Threading.Tasks;

using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;

namespace Cythral.CloudFormation.Tests.EndToEnd
{
    public static class CloudFormationExtensions
    {
        public static async Task<bool> StackExists(this IAmazonCloudFormation client, string stackName)
        {
            try
            {
                var response = await client.DescribeStacksAsync(new DescribeStacksRequest
                {
                    StackName = stackName
                });

                return response.Stacks.Count > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static async Task<bool> StackHasStatus(this IAmazonCloudFormation client, string stackName, string status)
        {
            try
            {
                var response = await client.DescribeStacksAsync(new DescribeStacksRequest
                {
                    StackName = stackName
                });

                return response.Stacks.Count > 0 ? response.Stacks[0].StackStatus == status : false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static async Task WaitUntilStackExists(this IAmazonCloudFormation client, string stackName, int timeout = 500)
        {
            int tries = 0;

            while (!await client.StackExists(stackName))
            {
                if (tries > timeout)
                {
                    throw new Exception("Timed out waiting for stack to finish creating");
                }

                await Task.Delay(1000);
                tries++;
            }
        }

        public static async Task WaitUntilStackHasStatus(this IAmazonCloudFormation client, string stackName, string status, int timeout = 500)
        {
            int tries = 0;

            while (!await client.StackHasStatus(stackName, status))
            {
                if (tries > timeout)
                {
                    throw new Exception("Timed out waiting for stack");
                }

                await Task.Delay(1000);
                tries++;
            }
        }

        public static async Task WaitUntilStackDoesNotExist(this IAmazonCloudFormation client, string stackName, int timeout = 500)
        {
            int tries = 0;

            while (await client.StackExists(stackName))
            {
                if (tries > timeout)
                {
                    throw new Exception("Timed out waiting for stack to finish deleting");
                }

                await Task.Delay(1000);
                tries++;
            }
        }
    }
}