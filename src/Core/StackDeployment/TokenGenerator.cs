using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static System.Text.Json.JsonSerializer;

using Amazon.Lambda.SQSEvents;
using Amazon.S3.Model;

using Cythral.CloudFormation.Aws;

namespace Cythral.CloudFormation.StackDeployment
{
    public class TokenGenerator
    {
        private S3Factory s3Factory = new S3Factory();

        public virtual async Task<string> Generate(SQSEvent sqsEvent, Request request)
        {
            using (var client = await s3Factory.Create())
            using (SHA256 mySHA256 = SHA256.Create())
            {
                var sqsRecord = sqsEvent.Records[0];
                var token = request.Token;
                var bytes = Encoding.UTF8.GetBytes(token);
                var sumBytes = mySHA256.ComputeHash(bytes);
                var sum = string.Join("", sumBytes.Select(byt => $"{byt:X2}"));

                var (bucket, _) = GetBucketAndKey(request.ZipLocation);
                var response = await client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = bucket,
                    Key = $"tokens/{sum}",
                    ContentBody = Serialize(new TokenInfo
                    {
                        ClientRequestToken = token,
                        QueueUrl = ConvertQueueArnToUrl(sqsRecord.EventSourceArn),
                        ReceiptHandle = sqsRecord.ReceiptHandle,
                        RoleArn = request.RoleArn,
                        GithubOwner = request.CommitInfo.GithubOwner,
                        GithubRepo = request.CommitInfo.GithubRepository,
                        GithubRef = request.CommitInfo.GithubRef,
                        EnvironmentName = request.EnvironmentName,
                        GoogleClientId = request.SsoConfig.GoogleClientId,
                        IdentityPoolId = request.SsoConfig.IdentityPoolId
                    })
                });

                return $"{bucket}-{sum}";
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

        private string ConvertQueueArnToUrl(string arn)
        {
            var parts = arn.Split(":");
            return $"https://sqs.{parts[3]}.amazonaws.com/{parts[4]}/{parts[5]}";
        }
    }
}