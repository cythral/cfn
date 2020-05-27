using System.Linq;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

using Amazon.CloudFormation.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SNSEvents;
using Amazon.SQS.Model;

using Cythral.CloudFormation.AwsUtils.SimpleStorageService;
using Cythral.CloudFormation.GithubUtils;
using Cythral.CloudFormation.StackDeploymentStatus.Request;

using Octokit;

using static System.Text.Json.JsonSerializer;

using CloudFormationFactory = Cythral.CloudFormation.AwsUtils.AmazonClientFactory<
    Amazon.CloudFormation.IAmazonCloudFormation,
    Amazon.CloudFormation.AmazonCloudFormationClient
>;

using SqsFactory = Cythral.CloudFormation.AwsUtils.AmazonClientFactory<
    Amazon.SQS.IAmazonSQS,
    Amazon.SQS.AmazonSQSClient
>;

using StepFunctionsClientFactory = Cythral.CloudFormation.AwsUtils.AmazonClientFactory<
    Amazon.StepFunctions.IAmazonStepFunctions,
    Amazon.StepFunctions.AmazonStepFunctionsClient
>;

namespace Cythral.CloudFormation.StackDeploymentStatus
{
    public class Handler
    {
        private static StackDeploymentStatusRequestFactory requestFactory = new StackDeploymentStatusRequestFactory();
        private static StepFunctionsClientFactory stepFunctionsClientFactory = new StepFunctionsClientFactory();
        private static S3GetObjectFacade s3GetObjectFacade = new S3GetObjectFacade();
        private static SqsFactory sqsFactory = new SqsFactory();
        private static CloudFormationFactory cloudFormationFactory = new CloudFormationFactory();
        private static PutCommitStatusFacade putCommitStatusFacade = new PutCommitStatusFacade();

        [LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]
        public static async Task<Response> Handle(
            SNSEvent snsRequest,
            ILambdaContext context = null
        )
        {
            Console.WriteLine($"Received request: {Serialize(snsRequest)}");

            var client = await stepFunctionsClientFactory.Create();
            var request = requestFactory.CreateFromSnsEvent(snsRequest);
            var status = request.ResourceStatus;

            if (request.ResourceType == "AWS::CloudFormation::Stack" && request.ClientRequestToken.Length > 0)
            {
                if (status.EndsWith("ROLLBACK_COMPLETE") || status.EndsWith("FAILED"))
                {
                    await SendFailure(request, client);
                }

                if (status.EndsWith("COMPLETE"))
                {
                    await SendSuccess(request, client);
                }

            }

            return new Response { Success = true };
        }

        private static string TranslateTokenToS3Location(string clientRequestToken)
        {
            var index = clientRequestToken.LastIndexOf("-");
            var bucket = clientRequestToken[0..index];
            var key = clientRequestToken[(index + 1)..];

            return $"s3://{bucket}/tokens/{key}";
        }

        private static async Task<TokenInfo> GetTokenInfoFromRequest(StackDeploymentStatusRequest request)
        {
            var location = TranslateTokenToS3Location(request.ClientRequestToken);
            var sourceString = await s3GetObjectFacade.GetObject(location);
            return Deserialize<TokenInfo>(sourceString);
        }

        private static async Task SendFailure(StackDeploymentStatusRequest request, IAmazonStepFunctions client)
        {
            var tokenInfo = await GetTokenInfoFromRequest(request);
            var response = await client.SendTaskFailureAsync(new SendTaskFailureRequest
            {
                TaskToken = tokenInfo.ClientRequestToken,
                Cause = request.ResourceStatus
            });

            Console.WriteLine($"Received send task failure response: {Serialize(response)}");

            await Dequeue(tokenInfo);
            await PutCommitStatus(tokenInfo, request.StackName, CommitState.Failure);
        }

        private static async Task SendSuccess(StackDeploymentStatusRequest request, IAmazonStepFunctions client)
        {
            var tokenInfo = await GetTokenInfoFromRequest(request);
            var outputs = await GetStackOutputs(request.StackId, tokenInfo.RoleArn);
            var response = await client.SendTaskSuccessAsync(new SendTaskSuccessRequest
            {
                TaskToken = tokenInfo.ClientRequestToken,
                Output = Serialize(outputs)
            });

            Console.WriteLine($"Received send task failure response: {Serialize(response)}");

            await Dequeue(tokenInfo);
            await PutCommitStatus(tokenInfo, request.StackName, CommitState.Success);
        }

        private static async Task<Dictionary<string, string>> GetStackOutputs(string stackId, string roleArn)
        {
            var client = await cloudFormationFactory.Create(roleArn);
            var response = await client.DescribeStacksAsync(new DescribeStacksRequest
            {
                StackName = stackId
            });

            return response.Stacks[0].Outputs.ToDictionary(entry => entry.OutputKey, entry => entry.OutputValue);
        }

        private static async Task Dequeue(TokenInfo tokenInfo)
        {
            var client = await sqsFactory.Create();
            var response = await client.DeleteMessageAsync(new DeleteMessageRequest
            {
                QueueUrl = tokenInfo.QueueUrl,
                ReceiptHandle = tokenInfo.ReceiptHandle,
            });

            Console.WriteLine($"Got delete message response: {Serialize(response)}");
        }

        private static async Task PutCommitStatus(TokenInfo tokenInfo, string stackName, CommitState state)
        {
            await putCommitStatusFacade.PutCommitStatus(new PutCommitStatusRequest
            {
                CommitState = state,
                ServiceName = "AWS CloudFormation",
                DetailsUrl = $"https://console.aws.amazon.com/cloudformation/home?region=us-east-1#/stacks/stackinfo?filteringText=&filteringStatus=active&viewNested=true&hideStacks=false&stackId={stackName}",
                ProjectName = stackName,
                EnvironmentName = tokenInfo.EnvironmentName,
                GithubOwner = tokenInfo.GithubOwner,
                GithubRepo = tokenInfo.GithubRepo,
                GithubRef = tokenInfo.GithubRef,
                GoogleClientId = tokenInfo.GoogleClientId,
                IdentityPoolId = tokenInfo.IdentityPoolId
            });
        }
    }
}