using System;
using System.IO;
using System.Threading.Tasks;
using static System.Text.Json.JsonSerializer;

using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;

using Cythral.CloudFormation.Aws;

using ICSharpCode.SharpZipLib.Zip;

namespace Cythral.CloudFormation.Aws
{
    public class S3GetObjectFacade
    {
        private S3Factory s3Factory = new S3Factory();

        public virtual async Task<string> GetZipEntryInObject(string location, string entry)
        {
            var s3Client = await s3Factory.Create();
            var (bucket, key) = GetBucketAndKey(location);
            var getObjResponse = await s3Client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = bucket,
                Key = key,
            });

            var destStream = new MemoryStream();
            getObjResponse.ResponseStream.CopyTo(destStream);
            getObjResponse.ResponseStream.Dispose();

            using (var zip = new ZipFile(destStream))
            {
                var file = zip.GetEntry(entry);

                using (var inputStream = zip.GetInputStream(file))
                using (var reader = new StreamReader(inputStream))
                {
                    var result = await reader.ReadToEndAsync();
                    destStream.Dispose();

                    return result;
                }
            }
        }

        public virtual async Task<string> GetObject(string location)
        {
            var s3Client = await s3Factory.Create();
            var (bucket, key) = GetBucketAndKey(location);
            var getObjResponse = await s3Client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = bucket,
                Key = key,
            });

            using (var reader = new StreamReader(getObjResponse.ResponseStream))
            {
                return reader.ReadToEnd();
            }
        }

        private (string, string) GetBucketAndKey(string location)
        {
            var uriWithoutProtocol = location.StartsWith("arn") ? ConvertToS3Uri(location) : location.Substring(5);
            var index = uriWithoutProtocol.IndexOf('/');
            var bucket = uriWithoutProtocol[0..index];
            var key = uriWithoutProtocol[(index + 1)..];

            return (bucket, key);
        }

        private string ConvertToS3Uri(string arn)
        {
            var parts = arn.Split(':');
            return parts[5];
        }
    }
}