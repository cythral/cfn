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
            if (request.GithubOwner == null || request.GithubRepo == null || request.GithubRef == null)
            {
                return;
            }

            var encryptedGithubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            var commitStatusClient = await commitStatusClientFactory.Create(encryptedGithubToken);
            await commitStatusClient.Create(request.GithubOwner, request.GithubRepo, request.GithubRef, new NewCommitStatus
            {
                State = request.CommitState,
                TargetUrl = GetSsoUrl(request),
                Description = $"CloudFormation Deployment ${GetStatusDescription(request.CommitState)}",
                Context = $"AWS CloudFormation ({request.EnvironmentName})"
            });
        }

        private static string GetSsoUrl(PutCommitStatusRequest request)
        {
            var stackUrl = $"https://console.aws.amazon.com/cloudformation/home?region=us-east-1#/stacks/stackinfo?filteringText=&filteringStatus=active&viewNested=true&hideStacks=false&stackId={request.StackName}";

            if (request.IdentityPoolId == null || request.GoogleClientId == null)
            {
                return stackUrl;
            }

            var nonceBytes = new byte[16];
            using (var generator = new RNGCryptoServiceProvider())
            {
                generator.GetBytes(nonceBytes);
            }

            var nonce = string.Join("", nonceBytes.Select(x => $"{x:X2}"));
            var destination = Uri.EscapeDataString(stackUrl);
            var state = Serialize(new { identity_pool_id = request.IdentityPoolId, destination = destination });
            var encodedState = Uri.EscapeDataString(state);
            var redirectUri = Uri.EscapeDataString("https://sso.brigh.id/redirect.html");

            return $"https://accounts.google.com/o/oauth2/v2/auth?redirect_uri={redirectUri}&state={encodedState}&client_id={request.GoogleClientId}&response_type=id_token&scope=profile+openid+email&nonce={nonce}";
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