using System.Threading.Tasks;

using Amazon.S3;
using Amazon.SecurityToken.Model;

namespace Cythral.CloudFormation.Aws
{
    public class S3Factory
    {
        private StsFactory stsFactory = new StsFactory();

        public virtual async Task<IAmazonS3> Create(string roleArn = null)
        {
            if (roleArn != null)
            {
                var client = await stsFactory.Create();
                var response = await client.AssumeRoleAsync(new AssumeRoleRequest
                {
                    RoleArn = roleArn,
                    RoleSessionName = "s3-deployment-ops"
                });

                return (IAmazonS3)new AmazonS3Client(response.Credentials);
            }

            return (IAmazonS3)new AmazonS3Client();
        }
    }
}