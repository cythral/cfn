using System.IO;
using System.Reflection;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO.Compression;
using System.Threading.Tasks;
using static System.Text.Json.JsonSerializer;

using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.S3;
using Amazon.S3.Model;

using Cythral.CloudFormation.AwsUtils.SimpleStorageService;
using Cythral.CloudFormation.GithubUtils;

using Octokit;

using S3Factory = Cythral.CloudFormation.AwsUtils.AmazonClientFactory<
    Amazon.S3.IAmazonS3,
    Amazon.S3.AmazonS3Client
>;

namespace Cythral.CloudFormation.S3Deployment
{
    public class Handler
    {
        private static S3Factory s3Factory = new S3Factory();
        private static S3GetObjectFacade s3GetObjectFacade = new S3GetObjectFacade();
        private static PutCommitStatusFacade putCommitStatusFacade = new PutCommitStatusFacade();

        [LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]
        public static async Task<object> Handle(Request request, ILambdaContext context = null)
        {
            await PutCommitStatus(request, CommitState.Pending);

            try
            {
                await MarkExistingObjectsAsDirty(request.RoleArn, request.DestinationBucket);

                using (var stream = await s3GetObjectFacade.GetObjectStream(request.ZipLocation))
                using (var zipStream = new ZipArchive(stream))
                {
                    var bucket = request.DestinationBucket;
                    var role = request.RoleArn;
                    var entries = zipStream.Entries.ToList();
                    var tasks = entries.Select(entry => UploadEntry(entry, bucket, role));

                    await Task.WhenAll(tasks);
                }

                await PutCommitStatus(request, CommitState.Success);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Got an error uploading files: {e.Message} {e.StackTrace}");
                await PutCommitStatus(request, CommitState.Failure);
            }

            return new Response
            {
                Success = true
            };
        }

        private static async Task MarkExistingObjectsAsDirty(string roleArn, string destinationBucket)
        {
            using (var client = await s3Factory.Create(roleArn))
            {
                var response = await client.ListObjectsV2Async(new ListObjectsV2Request
                {
                    BucketName = destinationBucket
                });

                var tasks = new List<Task>();

                foreach (var obj in response.S3Objects)
                {
                    tasks.Add(
                        client.PutObjectTaggingAsync(new PutObjectTaggingRequest
                        {
                            BucketName = destinationBucket,
                            Key = obj.Key,
                            Tagging = new Tagging
                            {
                                TagSet = new List<Tag>
                                {
                                    new Tag { Key = "dirty", Value = "true" }
                                }
                            }
                        })
                    );
                }

                await Task.WhenAll(tasks);
            }
        }

        private static async Task UploadEntry(ZipArchiveEntry entry, string bucket, string role)
        {
            var method = entry.GetType().GetMethod("OpenInReadMode", BindingFlags.NonPublic | BindingFlags.Instance);

            using (var client = await s3Factory.Create(role))
            using (var stream = method.Invoke(entry, new object[] { false }) as Stream)
            {
                var request = new PutObjectRequest
                {
                    BucketName = bucket,
                    Key = entry.FullName,
                    InputStream = stream,
                    TagSet = new List<Tag> { }
                };

                request.Headers.ContentLength = entry.Length;

                await client.PutObjectAsync(request);
                Console.WriteLine($"Uploaded {entry.FullName}");
            }
        }

        private static async Task PutCommitStatus(Request request, CommitState state)
        {
            await putCommitStatusFacade.PutCommitStatus(new PutCommitStatusRequest
            {
                CommitState = state,
                ServiceName = "AWS S3",
                DetailsUrl = $"https://s3.console.aws.amazon.com/s3/buckets/{request.DestinationBucket}/?region=us-east-1",
                ProjectName = request.ProjectName ?? request.DestinationBucket,
                EnvironmentName = request.EnvironmentName,
                GithubOwner = request.CommitInfo?.GithubOwner,
                GithubRepo = request.CommitInfo?.GithubRepository,
                GithubRef = request.CommitInfo?.GithubRef,
            });
        }
    }
}