using System.Threading.Tasks;

using Amazon.Lambda;

namespace Cythral.CloudFormation.Resources.Factories
{
    public interface ILambdaFactory
    {
        Task<IAmazonLambda> Create();
    }
}