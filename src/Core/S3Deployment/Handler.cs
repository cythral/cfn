using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.S3;
using Amazon.S3.Model;

using Cythral.CloudFormation.AwsUtils;
using Cythral.CloudFormation.AwsUtils.SimpleStorageService;
using Cythral.CloudFormation.GithubUtils;

using Lambdajection.Attributes;
using Lambdajection.Core;

using Microsoft.Extensions.Logging;

using Octokit;

using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.S3Deployment
{
    [Lambda(typeof(Startup))]
    public partial class Handler : IDisposable
    {
        private readonly IAwsFactory<IAmazonS3> s3Factory;
        private readonly GithubStatusNotifier githubStatusNotifier;
        private readonly S3GetObjectFacade s3GetObjectFacade;
        private readonly ILogger<Handler> logger;
        private IAmazonS3 s3Client;
        private bool disposed;

        public Handler(
            IAwsFactory<IAmazonS3> s3Factory,
            GithubStatusNotifier githubStatusNotifier,
            S3GetObjectFacade s3GetObjectFacade,
            ILogger<Handler> logger
        )
        {
            this.s3Factory = s3Factory;
            this.githubStatusNotifier = githubStatusNotifier;
            this.s3GetObjectFacade = s3GetObjectFacade;
            this.logger = logger;
        }

        public async Task<object> Handle(Request request, ILambdaContext context = null)
        {
            await githubStatusNotifier.NotifyPending(
                bucketName: request.DestinationBucket,
                repoName: request.CommitInfo.GithubRepository,
                sha: request.CommitInfo.GithubRef
            );

            s3Client = await s3Factory.Create(request.RoleArn);

            try
            {
                await MarkExistingObjectsAsDirty(request.DestinationBucket);

                using (var stream = await s3GetObjectFacade.GetObjectStream(request.ZipLocation))
                using (var zipStream = new ZipArchive(stream))
                {
                    var bucket = request.DestinationBucket;
                    var role = request.RoleArn;
                    var entries = zipStream.Entries.ToList();

                    // zip archive operations not thread safe
                    foreach (var entry in entries)
                    {
                        await UploadEntry(entry, bucket, role);
                    }
                }

                await githubStatusNotifier.NotifySuccess(
                    bucketName: request.DestinationBucket,
                    repoName: request.CommitInfo.GithubRepository,
                    sha: request.CommitInfo.GithubRef
                );
            }
            catch (Exception e)
            {
                logger.LogError($"Got an error uploading files: {e.Message} {e.StackTrace}");

                await githubStatusNotifier.NotifyFailure(
                    bucketName: request.DestinationBucket,
                    repoName: request.CommitInfo.GithubRepository,
                    sha: request.CommitInfo.GithubRef
                );

                throw new AggregateException(e);
            }

            return new Response
            {
                Success = true
            };
        }

        private async Task MarkExistingObjectsAsDirty(string destinationBucket)
        {
            var request = new ListObjectsV2Request { BucketName = destinationBucket };
            var response = await s3Client.ListObjectsV2Async(request);
            var dirtyTag = new Tag { Key = "dirty", Value = "true" };
            var tagging = new Tagging
            {
                TagSet = new List<Tag> { dirtyTag }
            };

            var tasks = response.S3Objects.Select(obj =>
                s3Client.PutObjectTaggingAsync(new PutObjectTaggingRequest
                {
                    BucketName = destinationBucket,
                    Key = obj.Key,
                    Tagging = tagging,
                })
            );

            await Task.WhenAll(tasks);
        }

        private async Task UploadEntry(ZipArchiveEntry entry, string bucket, string role)
        {
            using var stream = entry.OpenInReadMode();
            var request = new PutObjectRequest
            {
                BucketName = bucket,
                Key = entry.FullName,
                InputStream = stream,
                TagSet = new List<Tag> { },
            };

            request.Headers.ContentLength = entry.Length;
            await s3Client.PutObjectAsync(request);

            logger.LogInformation($"Uploaded {entry.FullName}");
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            s3Client.Dispose();
            s3Client = null;
            disposed = true;
        }
    }
}