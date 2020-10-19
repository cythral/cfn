using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;

using Microsoft.Extensions.Options;

namespace Cythral.CloudFormation.GithubWebhook.Github
{
    public class GithubHttpClient
    {
        private static readonly HttpClient client = new HttpClient();
        private readonly Config config;

        public GithubHttpClient(IOptions<Config> config)
        {
            this.config = config.Value;
            Configure();
        }

        internal GithubHttpClient()
        {
            // for testing only
        }

        private void Configure()
        {
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("brighid", "v1"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", config.GithubToken);
        }

        public virtual Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            return client.SendAsync(request);
        }

        public virtual Task<T> GetAsync<T>(string url)
        {
            return client.GetFromJsonAsync<T>(url);
        }
    }
}