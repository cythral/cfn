using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using Amazon.Lambda.SQSEvents;

using Cythral.CloudFormation.AwsUtils.SimpleStorageService;
using Cythral.CloudFormation.AwsUtils.CloudFormation;
using Cythral.CloudFormation.GithubUtils;
using Cythral.CloudFormation.StackDeployment;
using Cythral.CloudFormation.StackDeployment.TemplateConfig;

using NSubstitute;

using Octokit;

using NUnit.Framework;
using NSubstitute.ClearExtensions;
using static System.Text.Json.JsonSerializer;
using Tag = Amazon.CloudFormation.Model.Tag;

using CloudFormationFactory = Cythral.CloudFormation.AwsUtils.AmazonClientFactory<
    Amazon.CloudFormation.IAmazonCloudFormation,
    Amazon.CloudFormation.AmazonCloudFormationClient
>;

using StepFunctionsClientFactory = Cythral.CloudFormation.AwsUtils.AmazonClientFactory<
    Amazon.StepFunctions.IAmazonStepFunctions,
    Amazon.StepFunctions.AmazonStepFunctionsClient
>;

namespace Cythral.CloudFormation.Tests.StackDeployment
{
    public class HandlerTests
    {
        private static DeployStackFacade stackDeployer = Substitute.For<DeployStackFacade>();
        private static ParseConfigFileFacade parseConfigFileFacade = Substitute.For<ParseConfigFileFacade>();
        private static IAmazonStepFunctions stepFunctionsClient = Substitute.For<IAmazonStepFunctions>();
        private static S3GetObjectFacade s3GetObjectFacade = Substitute.For<S3GetObjectFacade>();
        private static TokenGenerator tokenGenerator = Substitute.For<TokenGenerator>();
        private static RequestFactory requestFactory = Substitute.For<RequestFactory>();
        private static StepFunctionsClientFactory stepFunctionsClientFactory = Substitute.For<StepFunctionsClientFactory>();
        private static CloudFormationFactory cloudFormationFactory = Substitute.For<CloudFormationFactory>();
        private static IAmazonCloudFormation cloudFormationClient = Substitute.For<IAmazonCloudFormation>();
        private static PutCommitStatusFacade putCommitStatusFacade = Substitute.For<PutCommitStatusFacade>();

        private const string stackName = "stackName";
        private const string location = "location";
        private const string templateFileName = "templateFileName";
        private const string roleArn = "roleArn";
        private const string template = "template";
        private const string actionMode = "actionMode";
        private const string templateConfigurationFileName = "configurationFileName";
        private const string notificationArn = "notificationArn";
        private const string clientRequestToken = "clientRequestToken";
        private const string createdToken = "createdToken";
        private const string templateConfiguration = "templateConfiguration";
        private List<string> capabilities = new List<string> { "a", "b" };
        private const string githubOwner = "githubOwner";
        private const string githubRepo = "githubRepo";
        private const string githubRef = "githubRef";
        private const string googleClientId = "googleClientId";
        private const string identityPoolId = "identityPoolId";
        private const string environmentName = "environmentName";

        private Dictionary<string, string> outputs = new Dictionary<string, string>
        {
            ["A"] = "B"
        };

        private TemplateConfiguration configuration = new TemplateConfiguration
        {
            Parameters = new List<Parameter>
            {
                new Parameter { ParameterKey = "A", ParameterValue = "B" }
            },
            Tags = new List<Tag>
            {
                new Tag { Key = "A", Value = "B" }
            },
            StackPolicy = new StackPolicyBody
            {
                Value = "body"
            }
        };

        [SetUp]
        public void SetupS3GetObjectFacade()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "s3GetObjectFacade", s3GetObjectFacade);
            s3GetObjectFacade.ClearReceivedCalls();
            s3GetObjectFacade.GetZipEntryInObject(Arg.Any<string>(), Arg.Is(templateFileName)).Returns(template);
            s3GetObjectFacade.GetZipEntryInObject(Arg.Any<string>(), Arg.Is(templateConfigurationFileName)).Returns(templateConfiguration);
        }

        [SetUp]
        public void SetupStackDeployer()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "stackDeployer", stackDeployer);
            stackDeployer.ClearSubstitute();
        }

        [SetUp]
        public void SetupParseConfigFileFacade()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "parseConfigFileFacade", parseConfigFileFacade);
            parseConfigFileFacade.ClearSubstitute();
            parseConfigFileFacade.Parse(Arg.Any<string>()).Returns(configuration);
        }

        [SetUp]
        public void SetupTokenGenerator()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "tokenGenerator", tokenGenerator);
            tokenGenerator.ClearSubstitute();
            tokenGenerator.Generate(Arg.Any<SQSEvent>(), Arg.Any<Request>()).Returns(createdToken);
        }

        [SetUp]
        public void SetupEnvvars()
        {
            Environment.SetEnvironmentVariable("NOTIFICATION_ARN", notificationArn);
        }

        [SetUp]
        public void SetupRequestFactory()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "requestFactory", requestFactory);
            requestFactory.ClearSubstitute();
        }

        [SetUp]
        public void SetupStepFunctions()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "stepFunctionsClientFactory", stepFunctionsClientFactory);
            stepFunctionsClientFactory.ClearSubstitute();
            stepFunctionsClientFactory.Create().Returns(stepFunctionsClient);
        }

        [SetUp]
        public void SetupCloudFormation()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "cloudFormationFactory", cloudFormationFactory);
            cloudFormationFactory.ClearSubstitute();
            cloudFormationFactory.Create(Arg.Any<string>()).Returns(cloudFormationClient);

            cloudFormationClient.DescribeStacksAsync(Arg.Any<DescribeStacksRequest>()).Returns(new DescribeStacksResponse
            {
                Stacks = new List<Stack>
                {
                    new Stack
                    {
                        Outputs = outputs
                                    .Select(entry => new Output { OutputKey = entry.Key, OutputValue = entry.Value })
                                    .ToList()
                    }
                }
            });
        }

        [SetUp]
        public void SetupPutCommitStatusFacade()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "putCommitStatusFacade", putCommitStatusFacade);
            putCommitStatusFacade.ClearSubstitute();
        }

        private Request CreateRequest()
        {
            var req = new Request
            {
                ZipLocation = location,
                TemplateFileName = templateFileName,
                TemplateConfigurationFileName = templateConfigurationFileName,
                StackName = stackName,
                RoleArn = roleArn,
                Token = clientRequestToken,
                Capabilities = capabilities,
                EnvironmentName = environmentName,
                CommitInfo = new CommitInfo
                {
                    GithubOwner = githubOwner,
                    GithubRepository = githubRepo,
                    GithubRef = githubRef,
                }
            };

            requestFactory.CreateFromSqsEvent(Arg.Any<SQSEvent>()).Returns(req);
            return req;
        }

        [Test]
        public void SQSEventIsParsed()
        {
            var request = CreateRequest();
            var sqs = Substitute.For<SQSEvent>();

            Assert.ThrowsAsync<Exception>(() => Handler.Handle(sqs));

            requestFactory.Received().CreateFromSqsEvent(Arg.Is(sqs));
        }

        [Test]
        public async Task ShouldPutPendingCommitStatus()
        {
            var request = CreateRequest();
            var sqs = Substitute.For<SQSEvent>();

            Assert.ThrowsAsync<Exception>(() => Handler.Handle(sqs));

            await putCommitStatusFacade.Received().PutCommitStatus(Arg.Is<PutCommitStatusRequest>(req =>
                req.CommitState == CommitState.Pending &&
                req.ServiceName == "AWS CloudFormation" &&
                req.ProjectName == stackName &&
                req.DetailsUrl == $"https://console.aws.amazon.com/cloudformation/home?region=us-east-1#/stacks/stackinfo?filteringText=&filteringStatus=active&viewNested=true&hideStacks=false&stackId={stackName}" &&
                req.EnvironmentName == environmentName &&
                req.GithubOwner == githubOwner &&
                req.GithubRepo == githubRepo &&
                req.GithubRef == githubRef
            ));
        }

        [Test]
        public async Task TemplateIsRetrieved()
        {
            var request = CreateRequest();
            var sqs = Substitute.For<SQSEvent>();

            Assert.ThrowsAsync<Exception>(() => Handler.Handle(sqs));

            await s3GetObjectFacade.Received().GetZipEntryInObject(Arg.Is(location), Arg.Is(templateFileName));
        }

        [Test]
        public async Task TemplateConfigurationIsRetrieved()
        {
            var request = CreateRequest();
            var sqs = Substitute.For<SQSEvent>();

            Assert.ThrowsAsync<Exception>(() => Handler.Handle(sqs));

            await s3GetObjectFacade.Received().GetZipEntryInObject(Arg.Is(location), Arg.Is(templateConfigurationFileName));
        }

        [Test]
        public async Task TokenIsGenerated()
        {
            var request = CreateRequest();
            var sqs = Substitute.For<SQSEvent>();

            Assert.ThrowsAsync<Exception>(() => Handler.Handle(sqs));

            await tokenGenerator.Received().Generate(Arg.Is(sqs), Arg.Is(request));
        }

        [Test]
        public async Task TemplateConfigurationIsNotRetrievedIfNotGiven()
        {
            var request = CreateRequest();
            var sqs = Substitute.For<SQSEvent>();
            request.TemplateConfigurationFileName = null;

            Assert.ThrowsAsync<Exception>(() => Handler.Handle(sqs));

            await s3GetObjectFacade.DidNotReceive().GetZipEntryInObject(Arg.Is(location), Arg.Is((string)null));
        }

        [Test]
        public async Task TemplateConfigurationIsNotRetrievedIfNotBlank()
        {
            var request = CreateRequest();
            var sqs = Substitute.For<SQSEvent>();
            request.TemplateConfigurationFileName = "";

            Assert.ThrowsAsync<Exception>(() => Handler.Handle(sqs));

            await s3GetObjectFacade.DidNotReceive().GetZipEntryInObject(Arg.Is(location), Arg.Is(""));
        }

        [Test]
        public async Task DeployWasCalled()
        {
            var request = CreateRequest();
            var sqs = Substitute.For<SQSEvent>();

            Assert.ThrowsAsync<Exception>(() => Handler.Handle(sqs));

            await stackDeployer.Received().Deploy(
                Arg.Is<DeployStackContext>(c =>
                    c.StackName == stackName &&
                    c.Template == template &&
                    c.RoleArn == roleArn &&
                    c.NotificationArn == notificationArn &&
                    configuration.Parameters.All(entry => c.Parameters.Any(param => param.ParameterKey == entry.ParameterKey && param.ParameterValue == entry.ParameterValue)) &&
                    c.Tags == configuration.Tags &&
                    c.StackPolicyBody == configuration.StackPolicy.ToString() &&
                    c.ClientRequestToken == createdToken &&
                    capabilities.All(c.Capabilities.Contains)
                )
            );
        }

        [Test]
        public async Task HandleDoesntThrowIfNoUpdatesExceptionWasCaught()
        {
            var request = CreateRequest();
            var sqs = Substitute.For<SQSEvent>();
            var serializedOutput = Serialize(outputs);

            stackDeployer.Deploy(null).ReturnsForAnyArgs(x => { throw new NoUpdatesException("no updates"); });

            await Handler.Handle(sqs);

            await stepFunctionsClient.Received().SendTaskSuccessAsync(
                Arg.Is<SendTaskSuccessRequest>(c =>
                    c.TaskToken == clientRequestToken &&
                    c.Output == serializedOutput
                )
            );

            await cloudFormationFactory.Received().Create(Arg.Is(roleArn));
            await cloudFormationClient.Received().DescribeStacksAsync(Arg.Is<DescribeStacksRequest>(req =>
                req.StackName == stackName
            ));
        }

        [Test]
        public async Task ShouldPutSuccessCommitStatusIfNoUpdatesExceptionWasCaught()
        {
            var request = CreateRequest();
            var sqs = Substitute.For<SQSEvent>();

            stackDeployer.Deploy(null).ReturnsForAnyArgs(x => { throw new NoUpdatesException("no updates"); });
            await Handler.Handle(sqs);

            await putCommitStatusFacade.Received().PutCommitStatus(Arg.Is<PutCommitStatusRequest>(req =>
                req.CommitState == CommitState.Success &&
                req.ServiceName == "AWS CloudFormation" &&
                req.ProjectName == stackName &&
                req.DetailsUrl == $"https://console.aws.amazon.com/cloudformation/home?region=us-east-1#/stacks/stackinfo?filteringText=&filteringStatus=active&viewNested=true&hideStacks=false&stackId={stackName}" &&
                req.EnvironmentName == environmentName &&
                req.GithubOwner == githubOwner &&
                req.GithubRepo == githubRepo &&
                req.GithubRef == githubRef
            ));
        }

        [Test]
        public async Task ParameterOverridesAreRespected()
        {
            var request = CreateRequest();
            request.ParameterOverrides = new Dictionary<string, string>
            {
                ["A"] = "C"
            };

            var sqs = Substitute.For<SQSEvent>();

            Assert.ThrowsAsync<Exception>(() => Handler.Handle(sqs));

            await stackDeployer.Received().Deploy(
                Arg.Is<DeployStackContext>(c =>
                    (from param in c.Parameters where param.ParameterKey == "A" select param.ParameterValue).First() == "C"
                )
            );
        }

        [Test]
        public async Task StepFunctionsNotifiedIfDeployFailed()
        {
            var request = CreateRequest();
            var sqs = Substitute.For<SQSEvent>();
            var message = "message";

            stackDeployer.Deploy(Arg.Any<DeployStackContext>()).Returns(x => throw new Exception(message));
            await Handler.Handle(sqs);

            await stepFunctionsClientFactory.Received().Create();
            await stepFunctionsClient.Received().SendTaskFailureAsync(Arg.Is<SendTaskFailureRequest>(req =>
                req.TaskToken == clientRequestToken &&
                req.Cause == message
            ));
        }

        [Test]
        public async Task ShouldPutFailedCommitStatusIfDeployFailed()
        {
            var request = CreateRequest();
            var sqs = Substitute.For<SQSEvent>();
            var message = "message";

            stackDeployer.Deploy(Arg.Any<DeployStackContext>()).Returns(x => throw new Exception(message));
            await Handler.Handle(sqs);

            await putCommitStatusFacade.Received().PutCommitStatus(Arg.Is<PutCommitStatusRequest>(req =>
                req.CommitState == CommitState.Failure &&
                req.ServiceName == "AWS CloudFormation" &&
                req.ProjectName == stackName &&
                req.DetailsUrl == $"https://console.aws.amazon.com/cloudformation/home?region=us-east-1#/stacks/stackinfo?filteringText=&filteringStatus=active&viewNested=true&hideStacks=false&stackId={stackName}" &&
                req.EnvironmentName == environmentName &&
                req.GithubOwner == githubOwner &&
                req.GithubRepo == githubRepo &&
                req.GithubRef == githubRef
            ));
        }
    }
}