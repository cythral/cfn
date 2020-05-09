using System.Collections.Generic;
using System.Threading.Tasks;

using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using Amazon.Lambda.ApplicationLoadBalancerEvents;

using Cythral.CloudFormation.Aws;

using NUnit.Framework;
using NSubstitute;

using static System.Text.Json.JsonSerializer;

using Handler = Cythral.CloudFormation.ApprovalWebhook.Handler;

namespace Cythral.CloudFormation.Tests.ApprovalWebhook
{
    public class HandlerTests
    {
        private static StepFunctionsClientFactory stepFunctionsClientFactory = Substitute.For<StepFunctionsClientFactory>();
        private static IAmazonStepFunctions stepFunctionsClient = Substitute.For<IAmazonStepFunctions>();

        [SetUp]
        public void SetUpStepFunctions()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "stepFunctionsClientFactory", stepFunctionsClientFactory);
            stepFunctionsClientFactory.ClearReceivedCalls();
            stepFunctionsClient.ClearReceivedCalls();

            stepFunctionsClientFactory.Create().Returns(stepFunctionsClient);
            stepFunctionsClient.SendTaskSuccessAsync(Arg.Any<SendTaskSuccessRequest>()).Returns(new SendTaskSuccessResponse { });
        }

        [Test]
        public async Task ShouldCreateStepFunctionsClient()
        {
            var token = "token";
            var action = "approve";
            var request = new ApplicationLoadBalancerRequest
            {
                QueryStringParameters = new Dictionary<string, string>
                {
                    ["token"] = token,
                    ["action"] = action
                }
            };

            await Handler.Handle(request);

            stepFunctionsClientFactory.Received().Create();
        }

        [Test]
        public async Task ShouldCallSendTaskSuccess()
        {
            var token = "token";
            var action = "approve";
            var serializedOutput = Serialize(new { Action = action });
            var request = new ApplicationLoadBalancerRequest
            {
                QueryStringParameters = new Dictionary<string, string>
                {
                    ["token"] = token,
                    ["action"] = action
                }
            };

            await Handler.Handle(request);

            await stepFunctionsClient.Received().SendTaskSuccessAsync(Arg.Is<SendTaskSuccessRequest>(req =>
                req.TaskToken == token &&
                req.Output == serializedOutput
            ));
        }
    }
}