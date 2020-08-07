using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

using Cythral.CloudFormation.AwsUtils.KeyManagementService;
using Cythral.CloudFormation.GithubWebhook;
using Cythral.CloudFormation.GithubWebhook.Entities;

using NUnit.Framework;

using Octokit;

using static System.Text.Json.JsonSerializer;

using Commit = Cythral.CloudFormation.GithubWebhook.Entities.Commit;
using Repository = Cythral.CloudFormation.GithubWebhook.Entities.Repository;
using User = Cythral.CloudFormation.GithubWebhook.Entities.User;

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
        private const string webhookUrl = "https://brigh.id/webhooks/github";

        private GitHubClient github;
        private IAmazonCloudFormation cloudformation;
        private string baseTree;
        private string signingKey;

        #region Setup / Teardown

        [SetUp]
        public async Task SetupGithubRepository()
        {
            var encryptedToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            var token = await kmsDecryptFacade.Decrypt(encryptedToken);
            var encryptedSigningKey = Environment.GetEnvironmentVariable("GITHUB_SIGNING_SECRET");
            signingKey = await kmsDecryptFacade.Decrypt(encryptedSigningKey);

            var headerValue = new ProductHeaderValue("Brighid");

            github = new GitHubClient(headerValue);
            github.Credentials = new Credentials(token);

            if (!await github.Repository.Exists(repoOwner, repoName))
            {
                await github.Repository.Create(repoOwner, new NewRepository(repoName)
                {
                    AutoInit = true,
                    Private = true
                });

                await Task.Delay(1000);
            }

            try
            {
                await github.Git.Reference.Delete(repoOwner, repoName, "heads/test");
            }
            catch (Exception) { }

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

        [OneTimeTearDown]
        public async Task TeardownGithub()
        {
            try
            {
                await github.Repository.Delete(repoOwner, repoName);
            }
            catch (Exception) { }
        }

        [OneTimeTearDown]
        public async Task TeardownCloudFormation()
        {
            try
            {
                await cloudformation.DeleteStackAsync(new DeleteStackRequest
                {
                    StackName = stackName
                });

                await cloudformation.WaitUntilStackDoesNotExist(stackName);
            }
            catch (Exception) { }
        }

        #endregion

        #region Github to AWS Tests

        [Test(Description = "Pushing to master creates a simple CICD Stack")]
        public async Task PushToMasterSimple()
        {
            #region Create Commit on Master

            var commitResponse = await github.Git.CreateCommit(
                repoOwner: repoOwner,
                repoName: repoName,
                before: null,
                branch: "master",
                message: "Create pipeline",
                files: new Dictionary<string, string>
                {
                    [cicdFileName] = "bucket-only.template.yml"
                },
                force: true
            );

            #endregion

            await cloudformation.WaitUntilStackHasStatus(stackName, "CREATE_COMPLETE", 120);
        }

        [Test(Description = "Pushing to a non default branch does not create a cicd stack")]
        public async Task PushToNonDefaultBranch()
        {
            #region Create Commit on Test Branch

            var commitResponse = await github.Git.CreateCommit(
                repoOwner: repoOwner,
                repoName: repoName,
                before: null,
                branch: "test",
                message: "Create pipeline",
                files: new Dictionary<string, string>
                {
                    [cicdFileName] = "bucket-only.template.yml"
                },
                force: true
            );

            #endregion

            await Task.Delay(2000);

            try
            {
                await cloudformation.WaitUntilStackExists(stackName);
                await cloudformation.WaitUntilStackHasStatus(stackName, "CREATE_COMPLETE", 30);
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

            Octokit.Commit commit1, commit2, commit3;
            string pipelineDefinition = null;

            #region Get the Pipeline Definition
            using (var stream = assembly.GetManifestResourceStream($"GithubWebhookEndToEnd.Resources.pipeline.asl.json"))
            {
                pipelineDefinition = await stream.ReadAsString();
            }
            #endregion

            #region Create Commit on Master

            commit1 = await github.Git.CreateCommit(
                repoOwner: repoOwner,
                repoName: repoName,
                before: null,
                branch: "master",
                message: "Create pipeline",
                files: new Dictionary<string, string>
                {
                    [cicdFileName] = "state-machine.template.yml",
                    [pipelineFileName] = "pipeline.asl.json"
                },
                force: true
            );

            #endregion

            #region Assert State Machine was Created

            await cloudformation.WaitUntilStackHasStatus(stackName, "CREATE_COMPLETE");

            var stateMachineResponse = await stepFunctionsClient.DescribeStateMachineAsync(new DescribeStateMachineRequest
            {
                StateMachineArn = $"arn:aws:states:{region}:{accountId}:stateMachine:cfn-test-repo-cicd-pipeline",
            });

            Assert.That(stateMachineResponse.Definition, Is.EqualTo(pipelineDefinition));

            #endregion

            #region Create Another Commit 

            commit2 = await github.Git.CreateCommit(
                repoOwner: repoOwner,
                repoName: repoName,
                before: commit1.Sha,
                branch: "master",
                message: "Add README",
                files: new Dictionary<string, string>
                {
                    ["README.md"] = "README.md.1",
                },
                force: true
            );

            #endregion

            #region Assert Execution was Created
            {
                await Task.Delay(4000);

                var executionResponse = await stepFunctionsClient.ListExecutionsAsync(new ListExecutionsRequest
                {
                    StateMachineArn = $"arn:aws:states:{region}:{accountId}:stateMachine:cfn-test-repo-cicd-pipeline",
                });

                Assert.True(executionResponse.Executions.Any(execution => execution.Name == commit2.Sha));
            }
            #endregion

            #region Create Commit with [skip ci] in the message

            commit3 = await github.Git.CreateCommit(
                repoOwner: repoOwner,
                repoName: repoName,
                before: commit2.Sha,
                branch: "master",
                message: "Add README [skip ci]",
                files: new Dictionary<string, string>
                {
                    ["README.md"] = "README.md.2",
                },
                force: true
            );

            #endregion

            #region Assert No Execution was Created
            {
                await Task.Delay(4000);

                var executionResponse = await stepFunctionsClient.ListExecutionsAsync(new ListExecutionsRequest
                {
                    StateMachineArn = $"arn:aws:states:{region}:{accountId}:stateMachine:cfn-test-repo-cicd-pipeline",
                });

                Assert.False(executionResponse.Executions.Any(execution => execution.Name == commit3.Sha));
            }
            #endregion
        }

        #endregion

        #region HTTP Method Response Tests

        [Test(Description = "Direct get requests to the webhook results in a bad response code")]
        public async Task DirectGetRequest()
        {
            var client = new HttpClient();
            var response = await client.GetAsync(webhookUrl);

            Assert.That(response.IsSuccessStatusCode, Is.False);
        }

        [Test(Description = "Direct patch requests to the webhook results in a bad response code")]
        public async Task DirectPatchRequest()
        {
            var client = new HttpClient();
            var response = await client.PatchAsync(webhookUrl, new StringContent("test"));

            Assert.That(response.IsSuccessStatusCode, Is.False);
        }

        [Test(Description = "Direct delete requests to the webhook results in a bad response code")]
        public async Task DirectDeleteRequest()
        {
            var client = new HttpClient();
            var response = await client.DeleteAsync(webhookUrl);

            Assert.That(response.IsSuccessStatusCode, Is.False);
        }

        #endregion

        #region Direct POST Request Tests

        [Test(Description = "Signed post requests with a bad repository owner should result in a bad request response code")]
        public async Task SignedPostRequestWithBadOwner()
        {
            var client = new HttpClient();
            var response = await client.PostWithSignature(webhookUrl, signingKey, new PushEvent
            {
                HeadCommit = new Commit
                {
                    Id = baseTree
                },
                Repository = new Repository
                {
                    ContentsUrl = $"https://api.github.com/repos/{repoOwner}/{repoName}/contents/{{+path}}",
                    Owner = new User
                    {
                        Name = "Codertocat"
                    }
                }
            });

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test(Description = "Signed post requests with a bad contents url should result in a bad request response code")]
        public async Task SignedPostRequestWithBadContentsUrl()
        {
            var client = new HttpClient();
            var response = await client.PostWithSignature(webhookUrl, signingKey, new PushEvent
            {
                HeadCommit = new Commit
                {
                    Id = baseTree
                },
                Repository = new Repository
                {
                    ContentsUrl = $"https://api.github.com/repos/Codertocat/{repoName}/contents/{{+path}}",
                    Owner = new User
                    {
                        Name = repoOwner
                    }
                }
            });

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test(Description = "Post requests with a bad signature should result in a bad request response code")]
        public async Task PostRequestWithBadSignature()
        {
            var client = new HttpClient();
            var response = await client.PostWithSignature(webhookUrl, "bad signing key", new PushEvent
            {
                HeadCommit = new Commit
                {
                    Id = baseTree
                },
                Repository = new Repository
                {
                    ContentsUrl = $"https://api.github.com/repos/{repoOwner}/{repoName}/contents/{{+path}}",
                    Owner = new User
                    {
                        Name = repoOwner
                    }
                }
            });

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        #endregion
    }
}