using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System;
using System.Threading.Tasks;
using NUnit.Framework;

using static System.Text.Json.JsonSerializer;
using Octokit;

using Cythral.CloudFormation.AwsUtils.KeyManagementService;

using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;

namespace Cythral.CloudFormation.Tests.EndToEnd.GithubWebhook
{
    public class EndToEndTests
    {
        private static KmsDecryptFacade kmsDecryptFacade = new KmsDecryptFacade();

        private const string repoOwner = "cythral";
        private const string repoName = "cfn-test-repo";
        private const string stackName = "cfn-test-repo-cicd";
        private const string cicdFileName = "cicd.template.yml";

        private GitHubClient github;
        private IAmazonCloudFormation cloudformation;
        private string baseTree;

        [SetUp]
        public async Task SetupGithubRepository()
        {
            var encryptedToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            var token = await kmsDecryptFacade.Decrypt(encryptedToken);
            var headerValue = new ProductHeaderValue("Brighid");

            github = new GitHubClient(headerValue);
            github.Credentials = new Credentials(token);

            if (await github.Repository.Exists(repoOwner, repoName))
            {
                await github.Repository.Delete(repoOwner, repoName);
            }

            await github.Repository.Create(repoOwner, new NewRepository(repoName)
            {
                AutoInit = true
            });

            var response = await github.Git.Tree.Get(repoOwner, repoName, "refs/heads/master");
            baseTree = response.Sha;
        }

        [SetUp]
        public async Task SetupCloudFormation()
        {
            cloudformation = new AmazonCloudFormationClient();

            if (await cloudformation.StackExists(stackName))
            {
                await cloudformation.DeleteStackAsync(new DeleteStackRequest
                {
                    StackName = stackName
                });

                await cloudformation.WaitUntilStackDoesNotExist(stackName);
            }
        }

        [Test(Description = "Pushing to master creates a simple CICD Stack")]
        public async Task PushToMasterCreatesSimpleCicdStack()
        {
            #region Create Commit on Master

            var assembly = Assembly.GetExecutingAssembly();
            TreeResponse response;

            using (var stream = assembly.GetManifestResourceStream("EndToEnd.Resources.bucket-only.template.yml"))
            using (var reader = new StreamReader(stream))
            {
                var tree = new NewTree
                {
                    BaseTree = baseTree
                };

                tree.Tree.Add(new NewTreeItem
                {
                    Path = cicdFileName,
                    Mode = "100644",
                    Content = await reader.ReadToEndAsync()
                });

                response = await github.Git.Tree.Create(repoOwner, repoName, tree);
            }

            Console.WriteLine(Serialize(response));

            var commit = new NewCommit("Create pipeline", response.Sha, baseTree);
            var commitResponse = await github.Git.Commit.Create(repoOwner, repoName, commit);

            await github.Git.Reference.Update(repoOwner, repoName, "heads/master", new ReferenceUpdate(commitResponse.Sha));

            #endregion

            await cloudformation.WaitUntilStackHasStatus(stackName, "CREATE_COMPLETE", 120);
        }

        [Test(Description = "Pushing to a non default branch does not create a cicd stack")]
        public async Task PushToNonDefaultBranchDoesntCreateCicdStack()
        {
            #region Create Commit on Test Branch

            var assembly = Assembly.GetExecutingAssembly();
            TreeResponse response;

            using (var stream = assembly.GetManifestResourceStream("EndToEnd.Resources.bucket-only.template.yml"))
            using (var reader = new StreamReader(stream))
            {
                var tree = new NewTree
                {
                    BaseTree = baseTree
                };

                tree.Tree.Add(new NewTreeItem
                {
                    Path = cicdFileName,
                    Mode = "100644",
                    Content = await reader.ReadToEndAsync()
                });

                response = await github.Git.Tree.Create(repoOwner, repoName, tree);
            }

            Console.WriteLine(Serialize(response));

            var commit = new NewCommit("Create pipeline", response.Sha, baseTree);
            var commitResponse = await github.Git.Commit.Create(repoOwner, repoName, commit);

            await github.Git.Reference.Create(repoOwner, repoName, new NewReference("heads/test", commitResponse.Sha));

            #endregion

            try
            {
                await cloudformation.WaitUntilStackExists(stackName, 15);
                await cloudformation.WaitUntilStackHasStatus(stackName, "CREATE_COMPLETE");
                Assert.Fail();
            }
            catch (Exception)
            {
                Assert.Pass();
            }
        }
    }
}