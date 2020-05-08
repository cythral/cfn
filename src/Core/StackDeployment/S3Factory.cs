using System.Threading.Tasks;

using Amazon.S3;
using Amazon.SecurityToken.Model;

namespace Cythral.CloudFormation.StackDeployment
{
    public class S3Factory
    {
        private StsFactory stsFactory = new StsFactory();

        public virtual Task<IAmazonS3> Create()
        {
            return Task.FromResult((IAmazonS3)new AmazonS3Client());
        }
    }
}