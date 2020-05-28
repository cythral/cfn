using System.Reflection;
using System.IO;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using Octokit;
namespace Cythral.CloudFormation.Tests.EndToEnd
{
    public static class GithubExtensions
    {
        public static async Task<bool> Exists(this IRepositoriesClient client, string owner, string name)
        {
            try
            {
                await client.Get(owner, name);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static async Task<Reference> CreateOrUpdate(this IReferencesClient client, string repoOwner, string repoName, string refName, string sha, bool force = false)
        {
            try
            {
                return await client.Update(repoOwner, repoName, refName, new ReferenceUpdate(sha, force));
            }
            catch (Exception)
            {
                return await client.Create(repoOwner, repoName, new NewReference(refName, sha));
            }
        }

        public static async Task<Commit> CreateCommit(this IGitDatabaseClient client, string repoOwner, string repoName, string before, string branch, string message, Dictionary<string, string> files, bool force = false)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var tree = new NewTree
            {
                BaseTree = before
            };

            foreach (var entry in files)
            {
                using (var stream = assembly.GetManifestResourceStream($"GithubWebhookEndToEnd.Resources.{entry.Value}"))
                {
                    tree.Tree.Add(new NewTreeItem
                    {
                        Path = entry.Key,
                        Mode = "100644",
                        Content = await stream.ReadAsString()
                    });
                }
            }

            var response = await client.Tree.Create(repoOwner, repoName, tree);

            var commitRequest = (before != null) ? new NewCommit(message, response.Sha, before) : new NewCommit(message, response.Sha);
            var commitResponse = await client.Commit.Create(repoOwner, repoName, commitRequest);
            await client.Reference.CreateOrUpdate(repoOwner, repoName, $"heads/{branch}", commitResponse.Sha, force);

            return commitResponse;
        }
    }
}