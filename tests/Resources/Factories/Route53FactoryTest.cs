using System;
using System.Threading.Tasks;

using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;

using Cythral.CloudFormation.Resources.Tests;
using static Cythral.CloudFormation.Resources.Tests.TestUtils;

using NSubstitute;

using NUnit.Framework;

namespace Cythral.CloudFormation.Resources.Factories.Tests
{
    public class Route53FactoryTest
    {

        private static IStsFactory StsFactory = Substitute.For<IStsFactory>();
        private static IAmazonSecurityTokenService StsClient = Substitute.For<IAmazonSecurityTokenService>();

        private static Route53Factory CreateInstance()
        {
            var instance = new Route53Factory();
            SetPrivateProperty(instance, "_stsFactory", StsFactory);
            return instance;
        }

        [SetUp]
        public void SetUpStsFactory()
        {
            StsFactory.ClearReceivedCalls();
            StsFactory.Create().Returns(StsClient);
        }

        [SetUp]
        public void SetUpStsClient()
        {
            StsClient.ClearReceivedCalls();
            StsClient.AssumeRoleAsync(Arg.Any<AssumeRoleRequest>()).Returns(new AssumeRoleResponse());
        }

        [Test]
        public async Task CreateWithNoArgs_DoesNotReturnNull()
        {
            var instance = CreateInstance();
            var result = await instance.Create();

            Assert.That(result, Is.Not.EqualTo(null));
        }

        [Test]
        public async Task CreateWithNoArgs_DoesAssumeRole()
        {
            var instance = CreateInstance();
            await instance.Create();

            await StsFactory.DidNotReceive().Create();
        }

        [Test]
        public async Task CreateWithRoleArn_CreatesClientWithCredentials()
        {
            var roleArn = "test arn";
            var instance = CreateInstance();
            var credentials = new SessionAWSCredentials("key", "secret", "token");

            StsClient.AssumeRoleAsync(Arg.Any<AssumeRoleRequest>()).Returns(new AssumeRoleResponse
            {
                Credentials = new Credentials
                {
                    AccessKeyId = credentials.GetCredentials().AccessKey,
                    SecretAccessKey = credentials.GetCredentials().SecretKey,
                    SessionToken = credentials.GetCredentials().Token,
                }
            });

            var result = await instance.Create(roleArn);
            await StsFactory.Received().Create();
            await StsClient.Received().AssumeRoleAsync(
                Arg.Is<AssumeRoleRequest>(req => req.RoleArn == roleArn && req.RoleSessionName != null)
            );

            TestUtils.AssertClientHasCredentials((AmazonServiceClient)result, credentials);
        }
    }
}