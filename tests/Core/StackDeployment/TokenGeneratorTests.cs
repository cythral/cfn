using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;
using Amazon.CloudFormation.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using Amazon.ElasticLoadBalancingV2;
using Amazon.ElasticLoadBalancingV2.Model;
using Amazon.Lambda.SQSEvents;

using Cythral.CloudFormation.Aws;
using Cythral.CloudFormation.Events;
using Cythral.CloudFormation.StackDeployment;
using Cythral.CloudFormation.StackDeployment.TemplateConfig;

using NSubstitute;

using NUnit.Framework;

using static Amazon.ElasticLoadBalancingV2.TargetHealthStateEnum;
using static System.Text.Json.JsonSerializer;

using SNSRecord = Amazon.Lambda.SNSEvents.SNSEvent.SNSRecord;
using SNSMessage = Amazon.Lambda.SNSEvents.SNSEvent.SNSMessage;
using Tag = Amazon.CloudFormation.Model.Tag;

namespace Cythral.CloudFormation.Tests.StackDeployment
{
    public class TokenGeneratorTests
    {
        public static S3Factory s3Factory = Substitute.For<S3Factory>();
        public static IAmazonS3 s3Client = Substitute.For<IAmazonS3>();
        public static TokenGenerator tokenGenerator = new TokenGenerator();
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
        private string templateConfiguration = "templateConfiguration";
        private string sqsArn = "arn:sqs:aws:us-east-1:5:testQueue";
        private string receiptHandle = "5";
        private string sqsUrl = "https://sqs.us-east-1.amazonaws.com/5/testQueue";
        private static List<string> Locations = new List<string> { $"s3://{bucket}/{key}", $"arn:s3:aws:::{bucket}/{key}" };

        [SetUp]
        public void SetupS3()
        {
            TestUtils.SetPrivateField(tokenGenerator, "s3Factory", s3Factory);
            s3Factory.ClearReceivedCalls();
            s3Client.ClearReceivedCalls();

            s3Factory.Create().Returns(s3Client);
        }

        private Request CreateRequest()
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

        private SQSEvent CreateSQSEvent()
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
        public async Task S3ClientIsCreated()
        {
            var request = CreateRequest();
            var sqsEvent = CreateSQSEvent();
            await tokenGenerator.Generate(sqsEvent, request);

            await s3Factory.Received().Create();
        }

        [Test]
        public async Task PutObjectIsCalled([ValueSource("Locations")] string location)
        {
            var request = CreateRequest();
            var sqsEvent = CreateSQSEvent();
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
            request.ZipLocation = location;

            var result = await tokenGenerator.Generate(sqsEvent, request);
            Assert.That(result, Is.EqualTo($"{bucket}-{clientRequestTokenSum}"));
        }
    }
}