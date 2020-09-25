extern alias S3TagOutdated;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;

using FluentAssertions;

using NSubstitute;

using NUnit.Framework;

using S3TagOutdated::Cythral.CloudFormation.S3TagOutdatedArtifacts;

using static NSubstitute.Arg;

using S3GetObjectFacade = S3TagOutdated::Cythral.CloudFormation.AwsUtils.SimpleStorageService.S3GetObjectFacade;


namespace Cythral.CloudFormation.S3TagOutdatedArtifacts.Tests
{
    public class HandlerTests
    {
        private const string manifestLocation = "manifestLocation";
        private const string manifestFilename = "manifestFilename";
        private const string bucketName = "bucketName";
        private const string prefix = "prefix";
        private const string newEntry1 = "entry1";
        private const string oldEntry1 = "entry1";
        private const string oldEntry2 = "entry2";

        private Request request = new Request
        {
            ManifestLocation = manifestLocation,
            ManifestFilename = manifestFilename,
        };

        private Manifest manifest = new Manifest
        {
            Bucket = bucketName,
            Prefix = prefix,
            Files = new Dictionary<string, string>
            {
                ["1"] = newEntry1,
            }
        };

        [Test]
        public async Task Handle_FetchesManifest()
        {
            var getObject = Substitute.For<S3GetObjectFacade>();
            var s3Client = Substitute.For<IAmazonS3>();
            var context = Substitute.For<ILambdaContext>();

            getObject.GetZipEntryInObject<Manifest>(Any<string>(), Any<string>()).Returns(manifest);
            s3Client.ListObjectsV2Async(Any<ListObjectsV2Request>()).Returns(new ListObjectsV2Response
            {
                S3Objects = new List<S3Object> {
                    new S3Object { Key = oldEntry1 },
                    new S3Object { Key = oldEntry2 }
                }
            });

            var handler = new Handler(s3Client, getObject);
            await handler.Handle(request, context);

            await getObject.Received().GetZipEntryInObject<Manifest>(Is(manifestLocation), Is(manifestFilename));
        }

        [Test]
        public async Task Handle_ListsFilesInBucketWithPrefix()
        {
            var getObject = Substitute.For<S3GetObjectFacade>();
            var s3Client = Substitute.For<IAmazonS3>();
            var context = Substitute.For<ILambdaContext>();

            getObject.GetZipEntryInObject<Manifest>(Any<string>(), Any<string>()).Returns(manifest);
            s3Client.ListObjectsV2Async(Any<ListObjectsV2Request>()).Returns(new ListObjectsV2Response
            {
                S3Objects = new List<S3Object> {
                    new S3Object { Key = oldEntry1 },
                    new S3Object { Key = oldEntry2 }
                }
            });

            var handler = new Handler(s3Client, getObject);
            await handler.Handle(request, context);

            await s3Client.Received().ListObjectsV2Async(Is<ListObjectsV2Request>(request =>
                request.BucketName == bucketName &&
                request.Prefix == prefix
            ));
        }

        [Test]
        public async Task Handle_TagsOutdatedObjects()
        {
            var getObject = Substitute.For<S3GetObjectFacade>();
            var s3Client = Substitute.For<IAmazonS3>();
            var context = Substitute.For<ILambdaContext>();

            getObject.GetZipEntryInObject<Manifest>(Any<string>(), Any<string>()).Returns(manifest);
            s3Client.ListObjectsV2Async(Any<ListObjectsV2Request>()).Returns(new ListObjectsV2Response
            {
                S3Objects = new List<S3Object> {
                    new S3Object { Key = oldEntry1 },
                    new S3Object { Key = oldEntry2 }
                }
            });

            var handler = new Handler(s3Client, getObject);
            await handler.Handle(request, context);

            await s3Client.Received().PutObjectTaggingAsync(Is<PutObjectTaggingRequest>(request =>
                request.BucketName == manifest.Bucket &&
                request.Key == oldEntry2 &&
                request.Tagging.TagSet.Any(tag => tag.Key == "outdated" && tag.Value == "true")
            ));
        }

        [Test]
        public async Task Handle_UntagsCurrentObjects()
        {
            var getObject = Substitute.For<S3GetObjectFacade>();
            var s3Client = Substitute.For<IAmazonS3>();
            var context = Substitute.For<ILambdaContext>();

            getObject.GetZipEntryInObject<Manifest>(Any<string>(), Any<string>()).Returns(manifest);
            s3Client.ListObjectsV2Async(Any<ListObjectsV2Request>()).Returns(new ListObjectsV2Response
            {
                S3Objects = new List<S3Object> {
                    new S3Object { Key = oldEntry1 },
                    new S3Object { Key = oldEntry2 }
                }
            });
            var handler = new Handler(s3Client, getObject);
            await handler.Handle(request, context);

            await s3Client.Received().PutObjectTaggingAsync(Is<PutObjectTaggingRequest>(request =>
                request.BucketName == manifest.Bucket &&
                request.Key == newEntry1 &&
                request.Tagging.TagSet.Any(tag => tag.Key == "outdated" && tag.Value == "false")
            ));
        }
    }
}