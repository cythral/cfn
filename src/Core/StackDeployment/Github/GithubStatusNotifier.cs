using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

using static System.Net.Http.HttpMethod;
using static System.Net.HttpStatusCode;

namespace Cythral.CloudFormation.StackDeployment.Github
{
    public class GithubStatusNotifier
    {
        private const string BaseDetailsUrl = "https://console.aws.amazon.com/cloudformation/home?region=us-east-1%2523/stacks/stackinfo%253FfilteringText=%2526filteringStatus=active%2526viewNested=true%2526hideStacks=false%2526stackId=";
        private readonly IGithubHttpClient httpClient;

        public GithubStatusNotifier(IGithubHttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        internal GithubStatusNotifier()
        {
            // for testing only
            httpClient = null!;
        }

        private async Task Notify(string state, string description, string owner, string repo, string sha, string stackName, string environmentName)
        {
            var destination = BaseDetailsUrl + Uri.EscapeDataString(stackName);
            await httpClient.SendAsync(new HttpRequestMessage
            {
                Method = Post,
                RequestUri = new Uri($"https://api.github.com/repos/{owner}/{repo}/statuses/{sha}"),
                Content = JsonContent.Create(new CreateStatusRequest
                {
                    State = state,
                    Description = description,
                    TargetUrl = $"https://sso.brigh.id/start/{environmentName}?destination={destination}",
                    Context = $"AWS CloudFormation - {environmentName} ({stackName})",
                })
            });
        }

        public virtual async Task NotifyFailure(string owner, string repo, string sha, string stackName, string environmentName)
        {
            await Notify("failure", "Failed", owner, repo, sha, stackName, environmentName);
        }

        public virtual async Task NotifyPending(string owner, string repo, string sha, string stackName, string environmentName)
        {
            await Notify("pending", "In Progress", owner, repo, sha, stackName, environmentName);
        }

        public virtual async Task NotifySuccess(string owner, string repo, string sha, string stackName, string environmentName)
        {
            await Notify("success", "Succeeded", owner, repo, sha, stackName, environmentName);
        }
    }
}
