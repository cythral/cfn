using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;

using Cythral.CloudFormation.AwsUtils.SimpleStorageService;

using Lambdajection.Attributes;

namespace Cythral.CloudFormation.S3TagOutdatedArtifacts
{
    [Lambda(typeof(Startup))]
    public partial class Handler
    {
        private readonly IAmazonS3 s3Client;
        private readonly S3GetObjectFacade getObject;
        private Manifest manifest;

        private static readonly Tagging currentTagSet = new Tagging
        {
            TagSet = new List<Tag>
            {
                new Tag { Key = "outdated", Value = "false" }
            }
        };

        private static readonly Tagging outdatedTagSet = new Tagging
        {
            TagSet = new List<Tag>
            {
                new Tag { Key = "outdated", Value = "true" }
            }
        };

        public Handler(IAmazonS3 s3Client, S3GetObjectFacade getObject)
        {
            this.s3Client = s3Client;
            this.getObject = getObject;
        }

        public async Task<bool> Handle(Request request, ILambdaContext context)
        {
            manifest = await getObject.GetZipEntryInObject<Manifest>(request.ManifestLocation, request.ManifestFilename);

            var objects = await s3Client.ListObjectsV2Async(new ListObjectsV2Request { BucketName = manifest.BucketName, Prefix = manifest.Prefix });
            var tasks = objects.S3Objects.Select(PutObjectTagging);

            await Task.WhenAll(tasks);

            return await Task.FromResult(true);
        }

        private async Task PutObjectTagging(S3Object @object)
        {
            var tagging = manifest.Files.ContainsValue(@object.Key) ? currentTagSet : outdatedTagSet;
            await s3Client.PutObjectTaggingAsync(new PutObjectTaggingRequest
            {
                BucketName = manifest.BucketName,
                Key = @object.Key,
                Tagging = tagging
            });
        }
    }
}