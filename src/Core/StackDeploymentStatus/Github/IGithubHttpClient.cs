using System.Net.Http;
using System.Threading.Tasks;

namespace Cythral.CloudFormation.StackDeploymentStatus.Github
{
    public interface IGithubHttpClient
    {
        Task<HttpResponseMessage> SendAsync(HttpRequestMessage request);
    }
}