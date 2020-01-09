using System.Threading.Tasks;

using Amazon.CertificateManager;

namespace Cythral.CloudFormation.Resources.Factories
{
    public interface IAcmFactory
    {
        Task<IAmazonCertificateManager> Create();
    }
}