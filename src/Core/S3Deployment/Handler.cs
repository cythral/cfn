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

namespace Cythral.CloudFormation.S3Deployment
{
    public class Handler
    {
        private static S3Factory s3Factory = new S3Factory();
        private static S3GetObjectFacade s3GetObjectFacade = new S3GetObjectFacade();

        public static async Task<object> Handle(Request request, ILambdaContext context = null)
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

            return new Response
            {
                Success = true
            };
        }

        public static async Task UploadEntry(ZipArchiveEntry entry, string bucket, string role)
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

        public static async Task Main(string[] args)
        {
            await Handler.Handle(new Request
            {
                ZipLocation = "s3://cfn-cicd-artifactstore-16nabth253y78/04eea462-9f82-48ee-9fda-d80e74f507cc/buildResults.zip",
                DestinationBucket = "cythral-test-bucket"
            });
        }
    }
}