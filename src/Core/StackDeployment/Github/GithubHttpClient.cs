using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

using Microsoft.Extensions.Options;

namespace Cythral.CloudFormation.StackDeployment.Github
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

        private void Configure()
        {
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("brighid", "v1"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", config.GithubToken);
        }

        public virtual Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            return client.SendAsync(request);
        }
    }
}