using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

using static System.Text.Json.JsonSerializer;

using Octokit;

namespace Cythral.CloudFormation.GithubUtils
{
    public class PutCommitStatusFacade
    {
        private CommitStatusClientFactory commitStatusClientFactory = new CommitStatusClientFactory();

        public virtual async Task PutCommitStatus(PutCommitStatusRequest request)
        {
            if (request.ServiceName == null ||
                request.EnvironmentName == null ||
                request.GithubOwner == null ||
                request.GithubRepo == null ||
                request.GithubRef == null)
            {
                return;
            }

            var encryptedGithubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            var commitStatusClient = await commitStatusClientFactory.Create(encryptedGithubToken);
            await commitStatusClient.Create(request.GithubOwner, request.GithubRepo, request.GithubRef, new NewCommitStatus
            {
                State = request.CommitState,
                TargetUrl = GetSsoUrl(request),
                Description = $"{request.ServiceName} Deployment {GetStatusDescription(request.CommitState)}",
                Context = $"{request.ServiceName} - {request.EnvironmentName} ({request.ProjectName})"
            });
        }

        private static string GetSsoUrl(PutCommitStatusRequest request)
        {
            var destination = Uri.EscapeDataString(request.DetailsUrl);
            return $"https://sso.brigh.id/start/{request.EnvironmentName.ToLower()}?destination={destination}";
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