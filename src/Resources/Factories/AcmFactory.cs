using System.Threading.Tasks;

using Amazon.CertificateManager;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;

namespace Cythral.CloudFormation.Resources.Factories
{
    public class AcmFactory : IAcmFactory
    {
        private IStsFactory _stsFactory { get; set; } = new StsFactory();

        public async Task<IAmazonCertificateManager> Create(string roleArn = null)
        {
            if (roleArn != null)
            {
                var stsClient = await _stsFactory.Create();
                var response = await stsClient.AssumeRoleAsync(new AssumeRoleRequest
                {
                    RoleArn = roleArn,
                    RoleSessionName = "acm-operations"
                });

                return new AmazonCertificateManagerClient(response.Credentials);
            }

            return new AmazonCertificateManagerClient();
        }
    }
}