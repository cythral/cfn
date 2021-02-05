using System;
using System.Threading;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SQSEvents;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

using Cythral.CloudFormation.AwsUtils;
using Cythral.CloudFormation.AwsUtils.SimpleStorageService;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Lambdajection.Attributes;
using Lambdajection.Core;

using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.DeploymentSupersession
{
    [Lambda(typeof(Startup))]
    public partial class Handler
    {
        private readonly RequestFactory requestFactory;
        private readonly S3GetObjectFacade s3GetObjectFacade;
        private readonly IAmazonStepFunctions stepFunctionsClient;
        private readonly IAmazonS3 s3Client;
        private readonly Config config;
        private readonly ILogger<Handler> logger;

        public Handler(
            RequestFactory requestFactory,
            S3GetObjectFacade s3GetObjectFacade,
            IAmazonStepFunctions stepFunctionsClient,
            IAmazonS3 s3Client,
            IOptions<Config> options,
            ILogger<Handler> logger
        )
        {
            this.requestFactory = requestFactory;
            this.s3GetObjectFacade = s3GetObjectFacade;
            this.stepFunctionsClient = stepFunctionsClient;
            this.s3Client = s3Client;
            this.config = options.Value;
            this.logger = logger;
        }

        public async Task<Response> Handle(SQSEvent sqsEvent, CancellationToken cancellationToken = default)
        {
            var request = requestFactory.CreateFromSqsEvent(sqsEvent);
            var bucket = config.StateStore;
            var getObjResponse = await s3GetObjectFacade.TryGetObject<StateInfo>(bucket, $"{request.Pipeline}/state.json");
            var stateInfo = getObjResponse ?? new StateInfo
            {
                LastCommitTimestamp = DateTime.MinValue
            };

            var superseded = request.CommitTimestamp < stateInfo.LastCommitTimestamp;
            var sendTaskRequest = new SendTaskSuccessRequest
            {
                TaskToken = request.Token,
                Output = Serialize(new
                {
                    Superseded = superseded
                })
            };

            var sendTaskResponse = await stepFunctionsClient.SendTaskSuccessAsync(sendTaskRequest, cancellationToken);
            logger.LogInformation($"Got send task response: {Serialize(sendTaskResponse)}");

            if (!superseded)
            {
                var putObjectRequest = new PutObjectRequest
                {
                    BucketName = bucket,
                    Key = $"{request.Pipeline}/state.json",
                    ContentBody = Serialize(new StateInfo
                    {
                        LastCommitTimestamp = request.CommitTimestamp
                    })
                };

                var putObjectResponse = await s3Client.PutObjectAsync(putObjectRequest, cancellationToken);
                logger.LogInformation($"Got put object response: {Serialize(putObjectResponse)}");
            }

            return new Response
            {
                Success = true
            };
        }
    }
}
