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
    }
}