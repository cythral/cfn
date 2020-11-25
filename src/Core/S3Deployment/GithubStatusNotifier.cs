using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using static System.Net.Http.HttpMethod;
using static System.Net.HttpStatusCode;

namespace Cythral.CloudFormation.S3Deployment
{

    public class GithubStatusNotifier
    {
        private readonly HttpClient client;
        private readonly Config config;
        private readonly ILogger<GithubStatusNotifier> logger;

        public GithubStatusNotifier(HttpClient client, IOptions<Config> config, ILogger<GithubStatusNotifier> logger)
        {
            this.client = client;
            this.config = config.Value;
            this.logger = logger;
        }

        internal GithubStatusNotifier()
        {
            // Used for testing
        }

        private async Task Notify(
            string bucketName,
            string repoName,
            string sha,
            string state,
            string description,
            string filteringStatus
        )
        {
            var url = $"https://api.github.com/repos/{config.GithubOwner}/{repoName}/statuses/{sha}";
            var destination = $"https://s3.console.aws.amazon.com/s3/buckets/{bucketName}/?region=us-east-1";
            var request = new HttpRequestMessage
            {
                Method = Post,
                RequestUri = new Uri(url),
                Content = JsonContent.Create(new CreateStatusRequest
                {
                    State = state,
                    Description = description,
                    TargetUrl = $"https://sso.brigh.id/start/shared?destination={destination}",
                    Context = $"AWS S3 ({bucketName})",
                })
            };

            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("brighid", "v1"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.GithubToken);

            var response = await client.SendAsync(request);

            if (response.StatusCode != OK)
            {
                logger.LogError($"Got status code {response.StatusCode} back from GitHub when commit status update.");
            }
        }

        public virtual async Task NotifyPending(string bucketName, string repoName, string sha)
        {
            await Notify(
                bucketName: bucketName,
                repoName: repoName,
                sha: sha,
                state: "pending",
                description: $"{repoName} S3 Deployment In Progress",
                filteringStatus: "active"
            );
        }

        public virtual async Task NotifyFailure(string bucketName, string repoName, string sha)
        {
            await Notify(
                bucketName: bucketName,
                repoName: repoName,
                sha: sha,
                state: "failure",
                description: $"{repoName} S3 Deployment Failed",
                filteringStatus: "deleted"
            );
        }

        public virtual async Task NotifySuccess(string bucketName, string repoName, string sha)
        {
            await Notify(
                bucketName: bucketName,
                repoName: repoName,
                sha: sha,
                state: "success",
                description: $"{repoName} S3 Deployment Succeeded",
                filteringStatus: "active"
            );
        }
    }
}