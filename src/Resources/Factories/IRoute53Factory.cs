using System.Threading.Tasks;

using Amazon.Route53;

namespace Cythral.CloudFormation.Resources.Factories
{
    public interface IRoute53Factory
    {
        Task<IAmazonRoute53> Create(string roleArn = null);
    }
}