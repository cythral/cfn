using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using Amazon.CertificateManager;
using Amazon.CertificateManager.Model;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Amazon.Lambda;

using Amazon.Lambda.Core;
using Amazon.Lambda.Model;
using Amazon.Route53;
using Amazon.Route53.Model;

using Cythral.CloudFormation.CustomResource.Core;
using Cythral.CloudFormation.CustomResource.Attributes;
using Cythral.CloudFormation.Resources;

using FluentAssertions;

using NSubstitute;

using NUnit.Framework;

using RichardSzalay.MockHttp;

namespace Cythral.CloudFormation.Tests.Resources
{
    public class SecretTest
    {

        private IAmazonKeyManagementService CreateKmsClient(string value)
        {
            var client = Substitute.For<IAmazonKeyManagementService>();
            var stream = new MemoryStream(Convert.FromBase64String(value));
            stream.Seek(0, SeekOrigin.Begin);

            client
            .DecryptAsync(Arg.Any<DecryptRequest>())
            .Returns(new DecryptResponse
            {
                Plaintext = stream
            });

            return client;
        }

        public string ReadStream(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        [Test]
        public async Task HandleCallsDecrypt()
        {
            var ciphertext = "dGVzdAo=";
            var client = CreateKmsClient(ciphertext);
            Secret.KmsClientFactory = () => client;

            var request = new Request<Secret.Properties>
            {
                RequestType = RequestType.Create,
                ResourceProperties = new Secret.Properties
                {
                    Ciphertext = ciphertext
                }
            };

            await Secret.Handle(request.ToStream());
            await client
            .Received()
            .DecryptAsync(
                Arg.Is<DecryptRequest>(req => ReadStream(req.CiphertextBlob) == "test\n")
            );
        }
    }
}