using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;
using System;
using System.Threading.Tasks;
using NUnit.Framework;

using static System.Text.Json.JsonSerializer;
using Octokit;

using Cythral.CloudFormation.AwsUtils.KeyManagementService;

using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

namespace Cythral.CloudFormation.Tests.EndToEnd.GithubWebhook
{
    [SingleThreaded]
    public class EndToEndTests
    {
        private static KmsDecryptFacade kmsDecryptFacade = new KmsDecryptFacade();

        private const string repoOwner = "cythral";
        private const string repoName = "cfn-test-repo";
        private const string stackName = "cfn-test-repo-cicd";
        private const string cicdFileName = "cicd.template.yml";
        private const string pipelineFileName = "pipeline.asl.json";

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
        public async Task PushToMaster()
        {
            #region Create Commit on Master

            var assembly = Assembly.GetExecutingAssembly();
            TreeResponse response;

            using (var stream = assembly.GetManifestResourceStream("GithubWebhookEndToEnd.Resources.bucket-only.template.yml"))
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

            var commit = new NewCommit("Create pipeline", response.Sha, baseTree);
            var commitResponse = await github.Git.Commit.Create(repoOwner, repoName, commit);

            await github.Git.Reference.Update(repoOwner, repoName, "heads/master", new ReferenceUpdate(commitResponse.Sha));

            #endregion

            await cloudformation.WaitUntilStackHasStatus(stackName, "CREATE_COMPLETE", 120);
        }

        [Test(Description = "Pushing to a non default branch does not create a cicd stack")]
        public async Task PushToNonDefaultBranch()
        {
            #region Create Commit on Test Branch

            var assembly = Assembly.GetExecutingAssembly();
            TreeResponse response;

            using (var stream = assembly.GetManifestResourceStream("GithubWebhookEndToEnd.Resources.bucket-only.template.yml"))
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

        [Test(Description = "Pushing to the default branch with a pipeline.asl.json uploads the file to S3 and its bucket/key are passed into the cicd template")]
        public async Task PushToMasterWithPipeline()
        {
            var stepFunctionsClient = new AmazonStepFunctionsClient();
            var assembly = Assembly.GetExecutingAssembly();
            var accountId = Environment.GetEnvironmentVariable("AWS_ACCOUNT_ID");
            var region = Environment.GetEnvironmentVariable("AWS_REGION");

            Commit commit1, commit2, commit3;

            string pipelineDefinition = null;

            TreeResponse response;

            #region Create Commit on Master
            {
                using (var cicdFileStream = assembly.GetManifestResourceStream("GithubWebhookEndToEnd.Resources.state-machine.template.yml"))
                using (var cicdFileReader = new StreamReader(cicdFileStream))
                using (var pipelineFileStream = assembly.GetManifestResourceStream("GithubWebhookEndToEnd.Resources.pipeline.asl.json"))
                using (var pipelineFileReader = new StreamReader(pipelineFileStream))
                {
                    pipelineDefinition = await pipelineFileReader.ReadToEndAsync();

                    var tree1 = new NewTree
                    {
                        BaseTree = baseTree
                    };

                    tree1.Tree.Add(new NewTreeItem
                    {
                        Path = cicdFileName,
                        Mode = "100644",
                        Content = await cicdFileReader.ReadToEndAsync()
                    });

                    tree1.Tree.Add(new NewTreeItem
                    {
                        Path = pipelineFileName,
                        Mode = "100644",
                        Content = pipelineDefinition
                    });

                    response = await github.Git.Tree.Create(repoOwner, repoName, tree1);
                }

                var commit = new NewCommit("Create pipeline", response.Sha, baseTree);
                commit1 = await github.Git.Commit.Create(repoOwner, repoName, commit);

                await github.Git.Reference.Update(repoOwner, repoName, "heads/master", new ReferenceUpdate(commit1.Sha));
            }
            #endregion

            #region Assert State Machine was Created
            {
                await cloudformation.WaitUntilStackHasStatus(stackName, "CREATE_COMPLETE", 30);

                var stateMachineResponse = await stepFunctionsClient.DescribeStateMachineAsync(new DescribeStateMachineRequest
                {
                    StateMachineArn = $"arn:aws:states:{region}:{accountId}:stateMachine:cfn-test-repo-cicd-pipeline",
                });

                Assert.That(stateMachineResponse.Definition, Is.EqualTo(pipelineDefinition));
            }
            #endregion

            #region Create Another Commit 
            {
                var updatedTree = new NewTree
                {
                    BaseTree = commit1.Sha
                };

                updatedTree.Tree.Add(new NewTreeItem
                {
                    Path = "README.md",
                    Mode = "100644",
                    Content = "Poke"
                });

                var updatedTreeResponse = await github.Git.Tree.Create(repoOwner, repoName, updatedTree);
                var commit = new NewCommit("Poke", updatedTreeResponse.Sha, commit1.Sha);

                commit2 = await github.Git.Commit.Create(repoOwner, repoName, commit);
                await github.Git.Reference.Update(repoOwner, repoName, "heads/master", new ReferenceUpdate(commit2.Sha));
            }
            #endregion

            #region Assert Execution was Created
            {
                await Task.Delay(3000);

                var executionResponse = await stepFunctionsClient.ListExecutionsAsync(new ListExecutionsRequest
                {
                    StateMachineArn = $"arn:aws:states:{region}:{accountId}:stateMachine:cfn-test-repo-cicd-pipeline",
                });

                Assert.True(executionResponse.Executions.Any(execution => execution.Name == commit2.Sha));
            }
            #endregion

            #region Create Commit with [no ci] in the message
            {
                var updatedTree = new NewTree
                {
                    BaseTree = commit1.Sha
                };

                updatedTree.Tree.Add(new NewTreeItem
                {
                    Path = "README.md",
                    Mode = "100644",
                    Content = "Poke"
                });

                var updatedTreeResponse = await github.Git.Tree.Create(repoOwner, repoName, updatedTree);
                var commit = new NewCommit("Poke [no ci]", updatedTreeResponse.Sha, commit1.Sha);

                commit3 = await github.Git.Commit.Create(repoOwner, repoName, commit);
                await github.Git.Reference.Update(repoOwner, repoName, "heads/master", new ReferenceUpdate(commit2.Sha));
            }
            #endregion

            #region Assert No Execution was Created
            {
                await Task.Delay(3000);

                var executionResponse = await stepFunctionsClient.ListExecutionsAsync(new ListExecutionsRequest
                {
                    StateMachineArn = $"arn:aws:states:{region}:{accountId}:stateMachine:cfn-test-repo-cicd-pipeline",
                });

                Assert.False(executionResponse.Executions.Any(execution => execution.Name == commit3.Sha));
            }
            #endregion
        }
    }
}