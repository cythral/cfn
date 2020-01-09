using System.Threading.Tasks;

using Amazon.SecurityToken;

namespace Cythral.CloudFormation.Resources.Factories
{
    public interface IStsFactory
    {
        Task<IAmazonSecurityTokenService> Create();
    }
}