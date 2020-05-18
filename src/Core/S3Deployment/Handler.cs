using System.IO;
using System.Reflection;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO.Compression;
using System.Threading.Tasks;
using static System.Text.Json.JsonSerializer;

using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;

using Cythral.CloudFormation.Aws;
using Cythral.CloudFormation.GithubUtils;

using Octokit;

namespace Cythral.CloudFormation.S3Deployment
{
    public class Handler
    {
        private static S3Factory s3Factory = new S3Factory();
        private static S3GetObjectFacade s3GetObjectFacade = new S3GetObjectFacade();
        private static PutCommitStatusFacade putCommitStatusFacade = new PutCommitStatusFacade();

        public static async Task<object> Handle(Request request, ILambdaContext context = null)
        {
            await PutCommitStatus(request, CommitState.Pending);

            try
            {
                using (var stream = await s3GetObjectFacade.GetObjectStream(request.ZipLocation))
                using (var zipStream = new ZipArchive(stream))
                {
                    var bucket = request.DestinationBucket;
                    var role = request.RoleArn;
                    var entries = zipStream.Entries.ToList();
                    var tasks = entries.Select(entry => UploadEntry(entry, bucket, role));
                    Task.WaitAll(tasks.ToArray());
                }

                await PutCommitStatus(request, CommitState.Success);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Got an error uploading files: {e.Message} {e.StackTrace}");
            }

            await PutCommitStatus(request, CommitState.Failure);

            return new Response
            {
                Success = true
            };
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
                ProjectName = request.DestinationBucket,
                EnvironmentName = request.EnvironmentName,
                GithubOwner = request.CommitInfo?.GithubOwner,
                GithubRepo = request.CommitInfo?.GithubRepository,
                GithubRef = request.CommitInfo?.GithubRef,
                GoogleClientId = request.SsoConfig?.GoogleClientId,
                IdentityPoolId = request.SsoConfig?.IdentityPoolId
            });
        }
    }
}