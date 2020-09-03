using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using static System.Net.Http.HttpMethod;
using static System.Net.HttpStatusCode;

namespace Cythral.CloudFormation.GithubWebhook.Github
{
    public class GithubStatusNotifier
    {
        private readonly GithubHttpClient client;
        private readonly Config config;
        private readonly ILogger<GithubFileFetcher> logger;

        public GithubStatusNotifier(GithubHttpClient client, IOptions<Config> config, ILogger<GithubFileFetcher> logger)
        {
            this.client = client;
            this.config = config.Value;
            this.logger = logger;
        }

        internal GithubStatusNotifier()
        {
            // Used for testing
        }

        public virtual async Task Notify(string repoName, string sha)
        {
            var url = $"https://api.github.com/repos/{config.GithubOwner}/{repoName}/statuses/{sha}";
            var destination = $"https://console.aws.amazon.com/cloudformation/home?region=us-east-1#/stacks/stackinfo?filteringText=&filteringStatus=active&viewNested=true&hideStacks=false&stackId={repoName}-{config.StackSuffix}";
            var response = await client.SendAsync(new HttpRequestMessage
            {
                Method = Post,
                RequestUri = new Uri(url),
                Content = JsonContent.Create(new CreateStatusRequest
                {
                    State = "pending",
                    Description = $"{repoName} Meta CICD Stack Deployment In Progress",
                    TargetUrl = $"https://sso.brigh.id/start/shared?destination={destination}",
                    Context = $"CloudFormation - shared ({repoName}-{config.StackSuffix})",
                })
            });

            if (response.StatusCode != OK)
            {
                logger.LogError($"Got status code {response.StatusCode} back from GitHub when sending meta-ci commit status update.");
            }
        }
    }
}