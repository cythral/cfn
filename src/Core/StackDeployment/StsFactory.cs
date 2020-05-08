using System.Threading.Tasks;

using Amazon.SecurityToken;

namespace Cythral.CloudFormation.StackDeployment
{
    public class StsFactory
    {
        public virtual Task<IAmazonSecurityTokenService> Create()
        {
            return Task.FromResult((IAmazonSecurityTokenService)new AmazonSecurityTokenServiceClient());
        }
    }
}