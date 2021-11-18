using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using Cythral.CloudFormation.AwsUtils;
using Cythral.CloudFormation.AwsUtils.SimpleStorageService;
using Cythral.CloudFormation.S3Deployment;

using FluentAssertions;

using Lambdajection.Core;

using Microsoft.Extensions.Logging;

using NSubstitute;
using NSubstitute.ClearExtensions;

using NUnit.Framework;

using static NSubstitute.Arg;

namespace Cythral.CloudFormation.Tests.S3Deployment
{
    public class HandlerTests
    {
        private static IAwsFactory<IAmazonS3> s3Factory = Substitute.For<IAwsFactory<IAmazonS3>>();
        private static S3GetObjectFacade s3GetObjectFacade = Substitute.For<S3GetObjectFacade>();
        private static IAmazonS3 s3Client = Substitute.For<IAmazonS3>();
        private static GithubStatusNotifier statusNotifier = Substitute.For<GithubStatusNotifier>();
        private static ILogger<Handler> logger = Substitute.For<ILogger<Handler>>();

        private const string zipLocation = "zipLocation";
        private const string destinationBucket = "destinationBucket";
        private const string roleArn = "roleArn";
        private const string githubOwner = "githubOwner";
        private const string githubRepo = "githubRepo";
        private const string githubRef = "githubRef";
        private const string googleClientId = "googleClientId";
        private const string identityPoolId = "identityPoolId";
        private const string environmentName = "environmentName";
        private const string projectName = "projectName";

        private const string existingObject1Key = "object1";
        private const string existingObject2Key = "object2";

        private static List<string> existingObjectKeys = new List<string> {
            existingObject1Key,
            existingObject2Key
        };

        private static List<S3Object> objects = new List<S3Object>
        {
            new S3Object { Key = existingObject1Key },
            new S3Object { Key = existingObject2Key }
        };

        [SetUp]
        public void SetupS3()
        {
            s3Factory.ClearSubstitute();
            s3Client.ClearSubstitute();
            s3GetObjectFacade.ClearSubstitute();

            s3Factory.Create(Arg.Is(roleArn)).Returns(s3Client);

            s3GetObjectFacade.GetObjectStream(Arg.Any<string>()).Returns(TestZipFile.Stream);
            s3Client.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>()).Returns(new ListObjectsV2Response
            {
                S3Objects = objects
            });
        }

        [SetUp]
        public void SetupStatusNotifier()
        {
            statusNotifier.ClearSubstitute();
        }

        private Request CreateRequest()
        {
            return new Request
            {
                ZipLocation = zipLocation,
                DestinationBucket = destinationBucket,
                RoleArn = roleArn,
                EnvironmentName = environmentName,
                ProjectName = projectName,
                CommitInfo = new CommitInfo
                {
                    GithubOwner = githubOwner,
                    GithubRepository = githubRepo,
                    GithubRef = githubRef,
                }
            };
        }


        [Test]
        public void TestStreamIsGood()
        {
            using var stream = TestZipFile.Stream;
            using var archive = new ZipArchive(stream);
            archive.Entries.Should().Contain(entry => entry.FullName == "README.txt");
            archive.Entries.Should().Contain(entry => entry.FullName == "LICENSE.txt");
        }

        [Test]
        public async Task ShouldPutPendingCommitStatus()
        {
            var handler = new Handler(s3Factory, statusNotifier, s3GetObjectFacade, logger);
            var request = CreateRequest();
            await handler.Handle(request);

            await statusNotifier.Received().NotifyPending(Is(destinationBucket), Is(environmentName), Is(githubRepo), Is(githubRef));
        }

        [Test]
        public async Task ShouldCreateClientWithRoleArn()
        {
            var handler = new Handler(s3Factory, statusNotifier, s3GetObjectFacade, logger);
            var request = CreateRequest();

            await handler.Handle(request);

            await s3Factory.Received().Create(Is(roleArn));
        }

        [Test]
        public async Task ShouldListObjects()
        {
            var handler = new Handler(s3Factory, statusNotifier, s3GetObjectFacade, logger);
            var request = CreateRequest();

            await handler.Handle(request);

            await s3Client.Received().ListObjectsV2Async(Is<ListObjectsV2Request>(req => req.BucketName == destinationBucket));
        }

        [Test]
        public async Task ShouldMarkExistingObjectsAsDirty([ValueSource("existingObjectKeys")] string objectKey)
        {
            var handler = new Handler(s3Factory, statusNotifier, s3GetObjectFacade, logger);
            var request = CreateRequest();

            await handler.Handle(request);

            await s3Client.Received().PutObjectTaggingAsync(
                Is<PutObjectTaggingRequest>(req =>
                    req.BucketName == destinationBucket &&
                    req.Key == objectKey &&
                    req.Tagging.TagSet.Any(tag => tag.Key == "dirty" && tag.Value == "true")
                )
            );
        }

        [Test]
        public async Task ShouldGetObjectStream()
        {
            var handler = new Handler(s3Factory, statusNotifier, s3GetObjectFacade, logger);
            var request = CreateRequest();
            await handler.Handle(request);

            await s3GetObjectFacade.Received().GetObjectStream(Is(zipLocation));
        }

        [Test]
        public async Task ShouldUploadReadme()
        {
            string? streamContents = null;

            s3Client
            .PutObjectAsync(Arg.Is<PutObjectRequest>(req => req.Key == "README.txt"))
            .Returns(x =>
            {
                using var stream = x.ArgAt<PutObjectRequest>(0).InputStream;
                using var reader = new StreamReader(stream);
                streamContents = reader.ReadToEnd();

                return (PutObjectResponse?)null;
            });

            var handler = new Handler(s3Factory, statusNotifier, s3GetObjectFacade, logger);
            var request = CreateRequest();
            await handler.Handle(request);

            await s3Client.Received().PutObjectAsync(Arg.Is<PutObjectRequest>(req =>
                req.BucketName == destinationBucket &&
                req.Key == "README.txt" &&
                req.Headers.ContentLength == 3 &&
                req.TagSet.Count == 0
            ));

            streamContents.Should().Be("hi\n");
        }

        [Test]
        public async Task ShouldUploadLicense()
        {
            string? streamContents = null;

            s3Client
            .PutObjectAsync(Arg.Is<PutObjectRequest>(req => req.Key == "LICENSE.txt"))
            .Returns(x =>
            {
                using (var stream = x.ArgAt<PutObjectRequest>(0).InputStream)
                using (var reader = new StreamReader(stream))
                {
                    streamContents = reader.ReadToEnd();
                }

                return (PutObjectResponse?)null;
            });

            var handler = new Handler(s3Factory, statusNotifier, s3GetObjectFacade, logger);
            var request = CreateRequest();
            await handler.Handle(request);

            await s3Client.Received().PutObjectAsync(Arg.Is<PutObjectRequest>(req =>
                req.BucketName == destinationBucket &&
                req.Key == "LICENSE.txt" &&
                req.Headers.ContentLength == 5 &&
                req.TagSet.Count == 0
            ));

            streamContents.Should().Be("test\n");
        }

        [Test]
        public async Task ShouldPutSuccessCommitStatus()
        {
            var handler = new Handler(s3Factory, statusNotifier, s3GetObjectFacade, logger);
            var request = CreateRequest();
            await handler.Handle(request);

            await statusNotifier.Received().NotifySuccess(Is(destinationBucket), Is(environmentName), Is(githubRepo), Is(githubRef));
        }

        [Test]
        public async Task ShouldNotPutSuccessCommitStatusIfUploadFailed()
        {
            s3Client.PutObjectAsync(null).ReturnsForAnyArgs<PutObjectResponse>(x => { throw new Exception(); });

            var handler = new Handler(s3Factory, statusNotifier, s3GetObjectFacade, logger);
            var request = CreateRequest();

            Assert.ThrowsAsync<AggregateException>(() => handler.Handle(request));

            await statusNotifier.DidNotReceive().NotifySuccess(Any<string>(), Any<string>(), Any<string>(), Any<string>());
        }


        [Test]
        public async Task ShouldPutFailedCommitStatusIfFailed()
        {
            s3Client.PutObjectAsync(null).ReturnsForAnyArgs<PutObjectResponse>(x => { throw new Exception(); });

            var handler = new Handler(s3Factory, statusNotifier, s3GetObjectFacade, logger);
            var request = CreateRequest();

            Assert.ThrowsAsync<AggregateException>(() => handler.Handle(request));

            await statusNotifier.Received().NotifyFailure(Is(destinationBucket), Is(environmentName), Is(githubRepo), Is(githubRef));
        }
    }
}
