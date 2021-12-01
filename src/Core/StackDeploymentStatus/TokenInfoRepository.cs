using System;
using System.IO;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using Lambdajection.Sns;

using Microsoft.Extensions.Options;

using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.StackDeploymentStatus
{
    public class TokenInfoRepository
    {
        private readonly IAmazonS3 s3Client;
        private readonly Config config;

        public TokenInfoRepository(IAmazonS3 s3Client, IOptions<Config> config)
        {
            this.s3Client = s3Client;
            this.config = config.Value;
        }

        internal TokenInfoRepository()
        {
            // for testing only
            s3Client = null!;
            config = null!;
        }

        public virtual async Task<TokenInfo> FindByRequest(SnsMessage<CloudFormationStackEvent> request)
        {
            if (request.TopicArn == config.GithubTopicArn)
            {
                return new TokenInfo
                {
                    EnvironmentName = "shared",
                    GithubOwner = config.GithubOwner,
                    GithubRepo = request.Message.StackName.Replace($"-{config.StackSuffix}", ""),
                    GithubRef = request.Message.ClientRequestToken,
                };
            }

            var (bucket, key) = GetBucketAndKeyFromRequestToken(request.Message.ClientRequestToken);
            var sourceString = await GetObject(bucket, $"tokens/{key}");
            return Deserialize<TokenInfo>(sourceString) ?? throw new Exception("Token info not found.");
        }

        public virtual async Task<string> GetObject(string bucket, string key)
        {
            var response = await s3Client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = bucket,
                Key = key,
            });

            using var reader = new StreamReader(response.ResponseStream);
            return await reader.ReadToEndAsync();
        }

        private static (string, string) GetBucketAndKeyFromRequestToken(string clientRequestToken)
        {
            var index = clientRequestToken.LastIndexOf("-");
            var bucket = clientRequestToken[0..index];
            var key = clientRequestToken[(index + 1)..];
            return (bucket, key);
        }
    }
}

