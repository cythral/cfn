using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

using Amazon.Lambda.ApplicationLoadBalancerEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.S3;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

using Cythral.CloudFormation.AwsUtils;
using Cythral.CloudFormation.AwsUtils.SimpleStorageService;

using Lambdajection.Core;
using Lambdajection.Attributes;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using static System.Net.HttpStatusCode;
using static System.Text.Json.JsonSerializer;


namespace Cythral.CloudFormation.ApprovalWebhook
{
    [Lambda(typeof(Startup), Serializer = typeof(CamelCaseLambdaJsonSerializer))]
    public partial class Handler
    {
        private readonly IAmazonStepFunctions stepFunctionsClient;
        private readonly IAmazonS3 s3Client;
        private readonly S3GetObjectFacade s3GetObjectFacade;
        private readonly Config config;
        private readonly ILogger<Handler> logger;

        public Handler(
            IAmazonStepFunctions stepFunctionsClient,
            IAmazonS3 s3Client,
            S3GetObjectFacade s3GetObjectFacade,
            IOptions<Config> config,
            ILogger<Handler> logger
        )
        {
            this.stepFunctionsClient = stepFunctionsClient;
            this.s3Client = s3Client;
            this.s3GetObjectFacade = s3GetObjectFacade;
            this.config = config.Value;
            this.logger = logger;
        }

        public async Task<ApplicationLoadBalancerResponse> Handle(ApplicationLoadBalancerRequest request)
        {
            var action = request.QueryStringParameters["action"];
            var pipeline = request.QueryStringParameters["pipeline"];
            var tokenHash = request.QueryStringParameters["token"];
            var key = $"{pipeline}/approvals/{tokenHash}";
            var bucket = config.StateStore;
            var approvalInfo = await s3GetObjectFacade.GetObject<ApprovalInfo>(bucket, key);

            var sendTaskResponse = await stepFunctionsClient.SendTaskSuccessAsync(new SendTaskSuccessRequest
            {
                TaskToken = approvalInfo.Token,
                Output = Serialize(new
                {
                    Action = action,
                })
            });

            logger.LogInformation($"Send task success response: {Serialize(sendTaskResponse)}");

            var deleteResponse = await s3Client.DeleteObjectAsync(bucket, key);
            logger.LogInformation($"Received delete response: {Serialize(deleteResponse)}");

            var body = action == "approve" ? "approved" : "rejected";

            return new ApplicationLoadBalancerResponse
            {
                StatusCode = 200,
                StatusDescription = "200 OK",
                Headers = new Dictionary<string, string> { ["content-type"] = "text/plain" },
                Body = body,
                IsBase64Encoded = false,
            };
        }
    }
}