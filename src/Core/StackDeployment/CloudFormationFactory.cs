using System.Threading.Tasks;

using Amazon.CloudFormation;
using Amazon.SecurityToken.Model;

using Cythral.CloudFormation.Aws;

namespace Cythral.CloudFormation.StackDeployment
{
    public class CloudFormationFactory
    {
        private StsFactory stsFactory = new StsFactory();

        public virtual async Task<IAmazonCloudFormation> Create(string roleArn = null)
        {
            if (roleArn != null)
            {
                var client = await stsFactory.Create();
                var response = await client.AssumeRoleAsync(new AssumeRoleRequest
                {
                    RoleArn = roleArn,
                    RoleSessionName = "cloudformation-deployment-ops"
                });

                return (IAmazonCloudFormation)new AmazonCloudFormationClient(response.Credentials);
            }

            return (IAmazonCloudFormation)new AmazonCloudFormationClient();
        }
    }
}