using System.Threading.Tasks;

using Amazon.CertificateManager;

namespace Cythral.CloudFormation.Resources.Factories
{
    public class AcmFactory : IAcmFactory
    {
        public Task<IAmazonCertificateManager> Create()
        {
            return Task.FromResult((IAmazonCertificateManager)new AmazonCertificateManagerClient());
        }
    }
}