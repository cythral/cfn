using System.Threading.Tasks;

using Amazon.KeyManagementService;

namespace Cythral.CloudFormation.Aws
{
    public class KmsFactory
    {
        public virtual Task<IAmazonKeyManagementService> Create()
        {
            return Task.FromResult((IAmazonKeyManagementService)new AmazonKeyManagementServiceClient());
        }
    }
}