using System.Threading.Tasks;

using Amazon.Runtime;
using Amazon.Route53;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;

namespace Cythral.CloudFormation.Resources.Factories
{
    public class Route53Factory : IRoute53Factory
    {

        private IStsFactory _stsFactory { get; set; } = new StsFactory();

        public async Task<IAmazonRoute53> Create(string roleArn = null)
        {
            if (roleArn != null)
            {
                var stsClient = await _stsFactory.Create();
                var response = await stsClient.AssumeRoleAsync(new AssumeRoleRequest
                {
                    RoleArn = roleArn,
                    RoleSessionName = "route-53-operations"
                });

                return (IAmazonRoute53) new AmazonRoute53Client(response.Credentials);
            }

            return (IAmazonRoute53) new AmazonRoute53Client();
        }
    }
}