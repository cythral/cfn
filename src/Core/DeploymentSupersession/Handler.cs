using System;
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

using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.DeploymentSupersession
{
    public class Handler
    {
        private static RequestFactory requestFactory = new RequestFactory();
        private static S3GetObjectFacade s3GetObjectFacade = new S3GetObjectFacade();
        private static AmazonClientFactory<IAmazonStepFunctions> stepFunctionsClientFactory = new AmazonClientFactory<IAmazonStepFunctions>();
        private static AmazonClientFactory<IAmazonS3> s3Factory = new AmazonClientFactory<IAmazonS3>();

        [LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]
        public static async Task<Response> Handle(SQSEvent sqsEvent, ILambdaContext context = null)
        {
            var request = requestFactory.CreateFromSqsEvent(sqsEvent);
            var stepFunctionsClient = await stepFunctionsClientFactory.Create();
            var bucket = Environment.GetEnvironmentVariable("STATE_STORE");
            var getObjResponse = await s3GetObjectFacade.TryGetObject<StateInfo>(bucket, $"{request.Pipeline}/state.json");
            var s3Client = await s3Factory.Create();
            var stateInfo = getObjResponse ?? new StateInfo
            {
                LastCommitTimestamp = DateTime.MinValue
            };

            var superseded = request.CommitTimestamp < stateInfo.LastCommitTimestamp;
            var sendTaskResponse = await stepFunctionsClient.SendTaskSuccessAsync(new SendTaskSuccessRequest
            {
                TaskToken = request.Token,
                Output = Serialize(new
                {
                    Superseded = superseded
                })
            });

            Console.WriteLine($"Got send task response: {Serialize(sendTaskResponse)}");

            if (!superseded)
            {
                var putObjectResponse = await s3Client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = bucket,
                    Key = $"{request.Pipeline}/state.json",
                    ContentBody = Serialize(new StateInfo
                    {
                        LastCommitTimestamp = request.CommitTimestamp
                    })
                });

                Console.WriteLine($"Got put object response: {Serialize(putObjectResponse)}");
            }

            return new Response
            {
                Success = true
            };
        }
    }
}
