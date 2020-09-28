extern alias CommonAwsUtils;

using System;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.SecurityToken;

using CommonAwsUtils::Cythral.CloudFormation.AwsUtils;

using FluentAssertions;

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
            var factory = new AmazonClientFactory<IAmazonS3>();
            var client = await factory.Create();
        }

        [Test]
        public async Task CreateShouldReturnCorrectImplementation()
        {
            var factory = new AmazonClientFactory<IAmazonS3>();
            var client = await factory.Create();

            client.GetType().Should().Be(typeof(AmazonS3Client));
        }

        [Test]
        public void CreateShouldThrowSecurityTokenServiceExceptionIfGivenRoleArn()
        {
            Assert.ThrowsAsync<AmazonSecurityTokenServiceException>(async () =>
            {
                var factory = new AmazonClientFactory<IAmazonS3>();
                var client = await factory.Create("arn:aws:iam:::role/Admin");
            });
        }
    }
}