using System.Linq;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

using Cythral.CloudFormation.AwsUtils.SimpleStorageService;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.ApprovalNotification
{
    public class ApprovalCanceler
    {
        private readonly IAmazonS3 s3Client;
        private readonly IAmazonStepFunctions stepFunctionsClient;
        private readonly S3GetObjectFacade s3GetObjectFacade;
        private readonly Config config;
        private readonly ILogger<ApprovalCanceler> logger;

        public ApprovalCanceler(
            IAmazonS3 s3Client,
            IAmazonStepFunctions stepFunctionsClient,
            S3GetObjectFacade s3GetObjectFacade,
            IOptions<Config> config,
            ILogger<ApprovalCanceler> logger
        )
        {
            this.s3Client = s3Client;
            this.stepFunctionsClient = stepFunctionsClient;
            this.s3GetObjectFacade = s3GetObjectFacade;
            this.config = config.Value;
            this.logger = logger;
        }

        public virtual async Task CancelPreviousApprovalsForPipeline(string pipeline)
        {
            var approvals = await s3Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = config.StateStore,
                Prefix = $"{pipeline}/approvals"
            });

            var tasks = approvals.S3Objects.Select(CancelPreviousApproval);
            await Task.WhenAll(tasks);
        }

        private async Task CancelPreviousApproval(S3Object location)
        {
            var approvalInfo = await s3GetObjectFacade.GetObject<ApprovalInfo>(location.BucketName, location.Key);

            try
            {
                var sendTaskCancelResponse = await stepFunctionsClient.SendTaskSuccessAsync(new SendTaskSuccessRequest
                {
                    TaskToken = approvalInfo.Token,
                    Output = Serialize(new
                    {
                        Action = "reject"
                    })
                });

                logger.LogDebug($"Cancellation response: {Serialize(sendTaskCancelResponse)}");
            }
            catch (TaskTimedOutException) { }
            catch (TaskDoesNotExistException) { }

            var deleteResponse = await s3Client.DeleteObjectAsync(location.BucketName, location.Key);
            logger.LogDebug($"Delete approval object response: {Serialize(deleteResponse)}");
        }
    }
}