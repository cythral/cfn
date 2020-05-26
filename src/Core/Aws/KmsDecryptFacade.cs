using System.Threading.Tasks;
using System;
using System.IO;
using Amazon.KeyManagementService.Model;

using KmsFactory = Cythral.CloudFormation.Aws.AmazonClientFactory<
    Amazon.KeyManagementService.IAmazonKeyManagementService,
    Amazon.KeyManagementService.AmazonKeyManagementServiceClient
>;

namespace Cythral.CloudFormation.Aws
{
    public class KmsDecryptFacade
    {
        private KmsFactory kmsFactory = new KmsFactory();

        public virtual async Task<string> Decrypt(string value)
        {
            var client = await kmsFactory.Create();
            var stream = new MemoryStream();

            var byteArray = Convert.FromBase64String(value);
            await stream.WriteAsync(byteArray);

            var response = await client.DecryptAsync(new DecryptRequest
            {
                CiphertextBlob = stream
            });

            var plaintextStream = response.Plaintext;
            using (var reader = new StreamReader(plaintextStream))
            {
                return await reader.ReadToEndAsync();
            }
        }
    }
}