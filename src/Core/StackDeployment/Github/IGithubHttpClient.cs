using System.Net.Http;
using System.Threading.Tasks;

namespace Cythral.CloudFormation.StackDeployment.Github
{
    public interface IGithubHttpClient
    {
        Task<HttpResponseMessage> SendAsync(HttpRequestMessage request);
    }
}