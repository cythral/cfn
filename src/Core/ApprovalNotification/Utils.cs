using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Cythral.CloudFormation.ApprovalNotification
{
    public static class Utils
    {
        public static string Hash(string plaintext)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(plaintext);
            var hashBytes = sha256.ComputeHash(bytes);
            return string.Join("", hashBytes.Select(byt => $"{byt:X2}"));
        }
    }
}