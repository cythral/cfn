using System.IO.Compression;
using System;
using System.IO;
using System.Threading.Tasks;
using static System.Text.Json.JsonSerializer;

using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;

using Cythral.CloudFormation.Aws;

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

            using (var zip = new ZipArchive(getObjResponse.ResponseStream))
            {
                var file = zip.GetEntry(entry);

                if (file == null)
                {
                    throw new Exception($"{entry} could not be found in {key}");
                }

                using (var inputStream = file.Open())
                using (var reader = new StreamReader(inputStream))
                {
                    var result = await reader.ReadToEndAsync();

                    return result;
                }
            }
        }

        public virtual async Task<string> GetObject(string bucket, string key)
        {
            var s3Client = await s3Factory.Create();
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

        public virtual async Task<string> GetObject(string location)
        {
            var (bucket, key) = GetBucketAndKey(location);
            return await GetObject(bucket, key);
        }

        public virtual async Task<T> GetObject<T>(string bucket, string key)
        {
            var stringContent = await GetObject(bucket, key);
            return Deserialize<T>(stringContent);
        }

        public virtual async Task<T> TryGetObject<T>(string bucket, string key) where T : class
        {
            try
            {
                var stringContent = await GetObject(bucket, key);
                return Deserialize<T>(stringContent);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public virtual async Task<T> GetObject<T>(string location)
        {
            var stringContent = await GetObject(location);
            return Deserialize<T>(stringContent);
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