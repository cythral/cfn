using System.Threading.Tasks;

using Octokit;

using Cythral.CloudFormation.AwsUtils.KeyManagementService;

namespace Cythral.CloudFormation.GithubUtils
{
    public class CommitStatusClientFactory
    {
        private static ICommitStatusClient instance = null;
        private KmsDecryptFacade kmsDecryptFacade = new KmsDecryptFacade();

        public virtual async Task<ICommitStatusClient> Create(string encryptedToken)
        {
            if (instance == null)
            {
                var token = await kmsDecryptFacade.Decrypt(encryptedToken);
                var github = new GitHubClient(new ProductHeaderValue("Brighid"));
                github.Credentials = new Credentials(token);
                instance = github.Repository.Status;
            }

            return instance;
        }
    }
}