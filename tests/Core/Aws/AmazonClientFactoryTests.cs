using System;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.SecurityToken;

using Cythral.CloudFormation.AwsUtils;

using NUnit.Framework;

namespace Cythral.CloudFormation.Tests.Aws
{
    public class AmazonClientFactoryTests
    {

        [SetUp]
        public void Setup()
        {
            Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1");
            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", "none");
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", "none");
        }

        [Test]
        public async Task CreateShouldNotThrow()
        {
            var factory = new AmazonClientFactory<IAmazonS3, AmazonS3Client>();
            var client = await factory.Create();
        }

        [Test]
        public void CreateShouldThrowSecurityTokenServiceExceptionIfGivenRoleArn()
        {
            Assert.ThrowsAsync<AmazonSecurityTokenServiceException>(async () =>
            {
                var factory = new AmazonClientFactory<IAmazonS3, AmazonS3Client>();
                var client = await factory.Create("arn:aws:iam:::role/Admin");
            });
        }
    }
}