using System.Collections.Generic;
using System.Threading.Tasks;

using Amazon.Lambda.SQSEvents;
using Amazon.S3;
using Amazon.S3.Model;

using NSubstitute;

using NUnit.Framework;

using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.StackDeployment.Tests
{
    public class TokenGeneratorTests
    {
        private const string stackName = "stackName";
        private const string bucket = "bucket";
        private const string key = "key";
        private const string location = "s3://bucket/key";
        private const string templateFileName = "templateFileName";
        private const string roleArn = "roleArn";
        private const string template = "template";
        private const string actionMode = "actionMode";
        private const string templateConfigurationFileName = "configurationFileName";
        private const string notificationArn = "notificationArn";
        private const string clientRequestToken = "clientRequestToken";
        private const string clientRequestTokenSum = "272A689245B6118F1AAB392CED48E3D07C3894CC2EF6A3500F298628CE87F88A";
        private const string sqsArn = "arn:sqs:aws:us-east-1:5:testQueue";
        private const string receiptHandle = "5";
        private const string sqsUrl = "https://sqs.us-east-1.amazonaws.com/5/testQueue";
        private static readonly List<string> Locations = new List<string> { $"s3://{bucket}/{key}", $"arn:s3:aws:::{bucket}/{key}" };

        private static Request CreateRequest()
        {
            return new Request
            {
                ZipLocation = location,
                TemplateFileName = templateFileName,
                TemplateConfigurationFileName = templateConfigurationFileName,
                StackName = stackName,
                RoleArn = roleArn,
                Token = clientRequestToken
            };
        }

        private static SQSEvent CreateSQSEvent()
        {
            return new SQSEvent
            {
                Records = new List<SQSEvent.SQSMessage> {
                    new SQSEvent.SQSMessage {
                        ReceiptHandle = receiptHandle,
                        EventSourceArn = sqsArn
                    }
                }
            };
        }

        [Test]
        public async Task PutObjectIsCalled([ValueSource(nameof(Locations))] string location)
        {
            var request = CreateRequest();
            var sqsEvent = CreateSQSEvent();
            var s3Client = Substitute.For<IAmazonS3>();
            var tokenGenerator = new TokenGenerator(s3Client);
            request.ZipLocation = location;

            var contentBody = Serialize(new TokenInfo
            {
                ClientRequestToken = clientRequestToken,
                ReceiptHandle = receiptHandle,
                QueueUrl = sqsUrl,
                RoleArn = request.RoleArn
            });

            await tokenGenerator.Generate(sqsEvent, request);

            await s3Client.Received().PutObjectAsync(Arg.Is<PutObjectRequest>(req =>
                req.BucketName == bucket &&
                req.Key == $"tokens/{clientRequestTokenSum}" &&
                req.ContentBody == contentBody
            ));
        }

        [Test]
        public async Task ReturnsBucketPlusToken()
        {
            var request = CreateRequest();
            var sqsEvent = CreateSQSEvent();
            var s3Client = Substitute.For<IAmazonS3>();
            var tokenGenerator = new TokenGenerator(s3Client);
            request.ZipLocation = location;

            var result = await tokenGenerator.Generate(sqsEvent, request);
            Assert.That(result, Is.EqualTo($"{bucket}-{clientRequestTokenSum}"));
        }
    }
}