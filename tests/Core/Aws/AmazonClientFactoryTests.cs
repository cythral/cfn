using System;
using NUnit.Framework;
using System.Threading.Tasks;
using Cythral.CloudFormation.Aws;

using Amazon.S3;
using Amazon.SecurityToken;

namespace Cythral.CloudFormation.Tests.Aws
{
    public class AmazonClientFactoryTests
    {
        [Test]
        public async Task CreateShouldNotThrow()
        {
            Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1");
            var factory = new AmazonClientFactory<IAmazonS3, AmazonS3Client>();
            var client = await factory.Create();
        }

        [Test]
        public void CreateShouldThrowSecurityTokenServiceExceptionIfGivenRoleArn()
        {
            Assert.ThrowsAsync<AmazonSecurityTokenServiceException>(async () =>
            {
                Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1");
                var factory = new AmazonClientFactory<IAmazonS3, AmazonS3Client>();
                var client = await factory.Create("arn:aws:iam:::role/Admin");
            });
        }
    }
}