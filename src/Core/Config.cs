using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;

namespace Cythral.CloudFormation
{
    public class Config : Dictionary<string, string>
    {
        public static async Task<Config> Create(IEnumerable<(string, bool)> keys, IEnumerable<string> encryptedKeys = null, IAmazonKeyManagementService kmsClient = null)
        {
            kmsClient = kmsClient ?? new AmazonKeyManagementServiceClient();

            var config = new Config();

            foreach (var (key, encrypted) in keys)
            {
                config[key] = Environment.GetEnvironmentVariable(key) ?? "";

                if (encrypted)
                {
                    config[key] = await Decrypt(config[key], kmsClient);
                }
            }

            return config;
        }

        private static async Task<string> Decrypt(string value, IAmazonKeyManagementService kmsClient)
        {
            var stream = new MemoryStream();
            var byteArray = Convert.FromBase64String(value);

            await stream.WriteAsync(byteArray);

            var request = new DecryptRequest { CiphertextBlob = stream };
            var response = await kmsClient.DecryptAsync(request);
            var plaintextStream = response.Plaintext;

            using (var reader = new StreamReader(plaintextStream))
            {
                return await reader.ReadToEndAsync();
            }
        }
    }
}