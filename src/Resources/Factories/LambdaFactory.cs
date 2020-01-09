using System.Threading.Tasks;

using Amazon.Lambda;

namespace Cythral.CloudFormation.Resources.Factories
{
    public class LambdaFactory : ILambdaFactory
    {
        public Task<IAmazonLambda> Create()
        {
            return Task.FromResult((IAmazonLambda)new AmazonLambdaClient());
        }
    }
}