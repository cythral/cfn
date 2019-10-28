using System.Text;
using System.IO;
using System.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Cythral.CloudFormation.Cicd;
using Cythral.CloudFormation.Cicd.Events;
using Cythral.CloudFormation.Cicd.Entities;
using Cythral.CloudFormation.Cicd.Exceptions;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using FluentAssertions;
using NSubstitute;

using static System.Net.HttpStatusCode;
using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.Tests.Cicd {
    public class ConfigTest {
        [Test]
        public async Task CreateAddsKeysForEnvironmentVariables() {
            Environment.SetEnvironmentVariable("TEST_KEY", "test value");
            
            var kmsClient = Substitute.For<IAmazonKeyManagementService>();
            var config = await Config.Create(new List<(string,bool)> { ("TEST_KEY", false) }, kmsClient: kmsClient);

            Assert.That(config["TEST_KEY"], Is.EqualTo("test value"));
        }

        [Test]
        public async Task CreateDecryptsEncryptedVariables() {
            var encryptedValue = Encoding.ASCII.GetBytes("encrypted variable");
            var expectedValue = Encoding.ASCII.GetBytes("decrypted variable");
            var returnStream = new MemoryStream();

            await returnStream.WriteAsync(expectedValue);
            returnStream.Seek(0, SeekOrigin.Begin);

            Environment.SetEnvironmentVariable("TEST_KEY", Convert.ToBase64String(encryptedValue));

            var kmsClient = Substitute.For<IAmazonKeyManagementService>();
            kmsClient
            .DecryptAsync(Arg.Any<DecryptRequest>())
            .Returns(new DecryptResponse {
                Plaintext = returnStream
            });

            var config = await Config.Create(new List<(string,bool)> { ("TEST_KEY", true) }, kmsClient: kmsClient);
            Assert.That(config["TEST_KEY"], Is.EqualTo("decrypted variable"));
        }
    }
}