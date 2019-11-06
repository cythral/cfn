using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Cythral.CloudFormation.CustomResource;
using Cythral.CloudFormation.CustomResource.Attributes;

using static Cythral.CloudFormation.CustomResource.GranteeType;

namespace Cythral.CloudFormation.Resources {
    [CustomResource(
        ResourcePropertiesType=typeof(Secret.Properties),
        Grantees = new string[] { "cfn-metadata:DevAgentRoleArn", "cfn-metadata:ProdAgentRoleArn" },
        GranteeType = Import
    )]
    public partial class Secret {
        public class Properties {
            public string Ciphertext { get; set; }
        }

        public static Func<IAmazonKeyManagementService> KmsClientFactory { get; set; } = delegate { 
            return (IAmazonKeyManagementService) new AmazonKeyManagementServiceClient();
        };

        public async Task<Response> Create() {
            var plaintext = await Decrypt(Request.ResourceProperties.Ciphertext);

            return new Response {
                PhysicalResourceId = DateTime.Now.ToString(),
                Data = new {
                    Plaintext = plaintext
                }
            };
        }

        public async Task<Response> Update() {
            return await Create();
        }

        public Task<Response> Delete() {
            return Task.FromResult(new Response {});
        }

        private async Task<string> Decrypt(string value) {
            var stream = new MemoryStream();
            var byteArray = Convert.FromBase64String(value);
            await stream.WriteAsync(byteArray);

            var request = new DecryptRequest { CiphertextBlob = stream };
            var response = await KmsClientFactory().DecryptAsync(request);
            var plaintextStream = response.Plaintext;

            using(var reader = new StreamReader(plaintextStream)) {
                return await reader.ReadToEndAsync();
            }
        }
    }
}