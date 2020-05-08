using System.Threading.Tasks;

using Amazon.SecurityToken;

namespace Cythral.CloudFormation.Aws
{
    public class StsFactory
    {
        public virtual Task<IAmazonSecurityTokenService> Create()
        {
            return Task.FromResult((IAmazonSecurityTokenService)new AmazonSecurityTokenServiceClient());
        }
    }
}