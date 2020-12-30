using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

using Cythral.CloudFormation.ApprovalNotification.Links;

using Lambdajection.Attributes;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using static System.Text.Json.JsonSerializer;

public delegate string ComputeHash(string plaintext);

namespace Cythral.CloudFormation.ApprovalNotification
{
    [Lambda(typeof(Startup))]
    public partial class Handler
    {
        private readonly IAmazonSimpleNotificationService snsClient;
        private readonly IAmazonS3 s3Client;
        private readonly ApprovalCanceler approvalCanceler;
        private readonly ComputeHash computeHash;
        private readonly ILinkService linkService;
        private readonly Config config;
        private readonly ILogger<Handler> logger;

        public Handler(
            IAmazonSimpleNotificationService snsClient,
            IAmazonS3 s3Client,
            ApprovalCanceler approvalCanceler,
            ComputeHash hash,
            ILinkService linkService,
            IOptions<Config> config,
            ILogger<Handler> logger
        )
        {
            this.snsClient = snsClient;
            this.s3Client = s3Client;
            this.approvalCanceler = approvalCanceler;
            this.computeHash = hash;
            this.linkService = linkService;
            this.config = config.Value;
            this.logger = logger;
        }

        public async Task<Response> Handle(Request request, CancellationToken cancellationToken = default)
        {
            logger.LogDebug($"Received request: {Serialize(request)}");
            await approvalCanceler.CancelPreviousApprovalsForPipeline(request.Pipeline);

            var approvalHash = await CreateApprovalObject(request);
            var pipeline = request.Pipeline;
            var approveUrl = await linkService.Shorten($"{config.BaseUrl}?action=approve&pipeline={pipeline}&token={approvalHash}");
            var rejectUrl = await linkService.Shorten($"{config.BaseUrl}?action=reject&pipeline={pipeline}&token={approvalHash}");

            var defaultMessage = $"{request.CustomMessage}.\n\nApprove:\n{approveUrl}\n\nReject:\n{rejectUrl}";
            var response = await snsClient.PublishAsync(new PublishRequest
            {
                TopicArn = config.TopicArn,
                MessageStructure = "json",
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    ["pipeline"] = new MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = request.Pipeline
                    }
                },
                Message = Serialize(new Dictionary<string, string>
                {
                    ["default"] = defaultMessage,
                    ["email"] = defaultMessage,
                    ["email - json"] = Serialize(new
                    {
                        Pipeline = request.Pipeline,
                        Message = request.CustomMessage,
                        ApprovalUrl = approveUrl,
                        RejectionUrl = rejectUrl
                    })
                })
            });

            logger.LogDebug($"Publish response: {Serialize(response)}");

            return new Response
            {
                Success = true
            };
        }

        private async Task<string> CreateApprovalObject(Request request)
        {
            var key = computeHash(request.Token);
            var bucket = config.StateStore;
            var pipeline = request.Pipeline;
            var response = await s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucket,
                Key = $"{pipeline}/approvals/{key}",
                ContentBody = Serialize(new ApprovalInfo
                {
                    Token = request.Token
                })
            });

            logger.LogDebug($"Put object response: {Serialize(response)}");
            return key;
        }
    }
}
