using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Cythral.CloudFormation.GithubWebhook.Pipelines
{
    public class Sha256SumComputer
    {
        public Sha256SumComputer()
        {

        }

        public virtual string ComputeSum(string contents)
        {
            using var sha256 = SHA256.Create();
            var fileBytes = Encoding.UTF8.GetBytes(contents);
            var sumBytes = sha256.ComputeHash(fileBytes);
            return string.Join("", sumBytes.Select(@byte => $"{@byte:X2}"));
        }
    }
}