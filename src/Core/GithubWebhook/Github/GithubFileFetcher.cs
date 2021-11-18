using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using static System.Net.Http.HttpMethod;
using static System.Net.HttpStatusCode;

namespace Cythral.CloudFormation.GithubWebhook.Github
{
    public class GithubFileFetcher
    {
        private readonly GithubHttpClient httpClient;
        private readonly Config config;
        private readonly ILogger<GithubFileFetcher> logger;

        public GithubFileFetcher(GithubHttpClient httpClient, IOptions<Config> config, ILogger<GithubFileFetcher> logger)
        {
            this.httpClient = httpClient;
            this.config = config.Value;
            this.logger = logger;
        }

        internal GithubFileFetcher()
        {
            // Used for testing
            httpClient = null!;
            config = null!;
            logger = null!;
        }

        public virtual async Task<string?> Fetch(string contentsUrl, string filename, string? gitRef = null)
        {
            var url = contentsUrl.Replace("{+path}", filename);
            url += gitRef != null ? $"?ref={gitRef}" : "";

            var request = new HttpRequestMessage { Method = Get, RequestUri = new Uri(url) };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3.raw"));

            var response = await httpClient.SendAsync(request);
            if (response.StatusCode != OK)
            {
                logger.LogError($"Got status code {response.StatusCode} back from GitHub when requesting {url}");
                return null;
            }

            return await response.Content.ReadAsStringAsync();
        }
    }
}