using System;
using System.Threading.Tasks;

using Octokit;

namespace Cythral.CloudFormation.GithubUtils
{
    public class PutCommitStatusFacade
    {
        private CommitStatusClientFactory commitStatusClientFactory = new CommitStatusClientFactory();

        public virtual async Task PutCommitStatus(PutCommitStatusRequest request)
        {
            if (request.GithubOwner == null || request.GithubRepo == null || request.GithubRef == null)
            {
                return;
            }

            var encryptedGithubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            var commitStatusClient = await commitStatusClientFactory.Create(encryptedGithubToken);
            await commitStatusClient.Create(request.GithubOwner, request.GithubRepo, request.GithubRef, new NewCommitStatus
            {
                State = request.CommitState,
                TargetUrl = $"https://console.aws.amazon.com/cloudformation/home?region=us-east-1#/stacks/stackinfo?filteringText=&filteringStatus=active&viewNested=true&hideStacks=false&stackId={request.StackName}",
                Description = $"CloudFormation Deployment ${GetStatusDescription(request.CommitState)}",
                Context = $"AWS CloudFormation ({request.EnvironmentName})"
            });

        }

        private static string GetStatusDescription(CommitState state)
        {
            switch (state)
            {
                case CommitState.Error: return "Errored";
                case CommitState.Failure: return "Failed";
                case CommitState.Pending: return "In Progress";
                case CommitState.Success: return "Succeeded";
                default: return "Unknown";
            }
        }
    }
}