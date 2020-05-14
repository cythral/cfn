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
                var entries = zipStream.Entries.ToList();
                var tasks = entries.Select(entry => UploadEntry(entry, bucket));
                Task.WaitAll(tasks.ToArray());
            }

            return new Response
            {
                Success = true
            };
        }

        public static async Task UploadEntry(ZipArchiveEntry entry, string bucket)
        {
            using (var client = await s3Factory.Create())
            using (var stream = entry.Open())
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
    }
}