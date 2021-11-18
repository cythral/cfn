using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

namespace Cythral.CloudFormation.StackDeployment
{
    public class S3Util
    {
        private readonly IAmazonS3 s3Client;

        public S3Util(IAmazonS3 s3Client)
        {
            this.s3Client = s3Client;
        }

        internal S3Util()
        {
            // for testing only
            s3Client = null!;
        }

        public virtual async Task<string> GetZipEntryInObject(string location, string entry)
        {
            var (bucket, key) = GetBucketAndKey(location);
            var getObjResponse = await s3Client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = bucket,
                Key = key,
            });

            using var zip = new ZipArchive(getObjResponse.ResponseStream);
            var file = zip.GetEntry(entry);

            if (file == null)
            {
                throw new Exception($"{entry} could not be found in {key}");
            }

            using var inputStream = file.Open();
            using var reader = new StreamReader(inputStream);
            var result = await reader.ReadToEndAsync();
            return result;
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