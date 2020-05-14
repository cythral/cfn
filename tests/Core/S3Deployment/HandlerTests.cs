using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;

using Cythral.CloudFormation.Aws;
using Cythral.CloudFormation.S3Deployment;

using NSubstitute;
using NSubstitute.ClearExtensions;

using NUnit.Framework;

namespace Cythral.CloudFormation.Tests.S3Deployment
{
    public class HandlerTests
    {
        private static S3Factory s3Factory = Substitute.For<S3Factory>();
        private static S3GetObjectFacade s3GetObjectFacade = Substitute.For<S3GetObjectFacade>();
        private static IAmazonS3 s3Client = Substitute.For<IAmazonS3>();

        private const string zipLocation = "zipLocation";
        private const string destinationBucket = "destinationBucket";
        private const string roleArn = "roleArn";

        [SetUp]
        public void SetupS3()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "s3Factory", s3Factory);
            TestUtils.SetPrivateStaticField(typeof(Handler), "s3GetObjectFacade", s3GetObjectFacade);

            s3Factory.ClearSubstitute();
            s3Factory.Create(Arg.Any<string>()).Returns(s3Client);

            s3GetObjectFacade.GetObjectStream(Arg.Any<string>()).Returns(TestZipFile.Stream);
        }

        private Request CreateRequest()
        {
            return new Request
            {
                ZipLocation = zipLocation,
                DestinationBucket = destinationBucket,
                RoleArn = roleArn
            };
        }


        [Test]
        public void TestStreamIsGood()
        {
            using (var stream = TestZipFile.Stream)
            using (var archive = new ZipArchive(stream))
            {
                Assert.That(archive.Entries.Any(entry => entry.FullName == "README.txt"), Is.EqualTo(true));
                Assert.That(archive.Entries.Any(entry => entry.FullName == "LICENSE.txt"), Is.EqualTo(true));
            }
        }

        [Test]
        public async Task ShouldGetObjectStream()
        {
            var request = CreateRequest();
            await Handler.Handle(request);

            await s3GetObjectFacade.Received().GetObjectStream(Arg.Is(zipLocation));
        }

        [Test]
        public async Task ShouldUploadReadme()
        {
            string streamContents = null;

            s3Client
            .PutObjectAsync(Arg.Is<PutObjectRequest>(req => req.Key == "README.txt"))
            .Returns(x =>
            {
                using (var stream = x.ArgAt<PutObjectRequest>(0).InputStream)
                using (var reader = new StreamReader(stream))
                {
                    streamContents = reader.ReadToEnd();
                }

                return (PutObjectResponse)null;
            });

            var request = CreateRequest();
            await Handler.Handle(request);

            await s3Client.PutObjectAsync(Arg.Is<PutObjectRequest>(req =>
                req.BucketName == destinationBucket &&
                req.Key == "README.txt" &&
                req.Headers.ContentLength == 3
            ));

            Assert.That(streamContents, Is.EqualTo("hi\n"));
        }

        [Test]
        public async Task ShouldUploadLicense()
        {
            string streamContents = null;

            s3Client
            .PutObjectAsync(Arg.Is<PutObjectRequest>(req => req.Key == "LICENSE.txt"))
            .Returns(x =>
            {
                using (var stream = x.ArgAt<PutObjectRequest>(0).InputStream)
                using (var reader = new StreamReader(stream))
                {
                    streamContents = reader.ReadToEnd();
                }

                return (PutObjectResponse)null;
            });

            var request = CreateRequest();
            await Handler.Handle(request);

            await s3Client.PutObjectAsync(Arg.Is<PutObjectRequest>(req =>
                req.BucketName == destinationBucket &&
                req.Key == "LICENSE.txt" &&
                req.Headers.ContentLength == 5
            ));

            Assert.That(streamContents, Is.EqualTo("test\n"));
        }
    }
}
