using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Lambda.SQSEvents;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

using Cythral.CloudFormation.StackDeployment.Github;
using Cythral.CloudFormation.StackDeployment.TemplateConfig;

using Lambdajection.Core;

using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ClearExtensions;

using NUnit.Framework;

using static System.Text.Json.JsonSerializer;
using static NSubstitute.Arg;

using Tag = Amazon.CloudFormation.Model.Tag;

namespace Cythral.CloudFormation.StackDeployment.Tests
{
    public class HandlerTests
    {
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
        private static List<string> capabilities = new List<string> { "a", "b" };
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

        private IOptions<Config> config = Options.Create(new Config
        {
            NotificationArn = notificationArn
        });

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

        private Request CreateRequest()
        {
            return new Request
            {
                ZipLocation = location,
                TemplateFileName = templateFileName,
                TemplateConfigurationFileName = templateConfigurationFileName,
                StackName = stackName,
                RoleArn = roleArn,
                Token = clientRequestToken,
                Capabilities = capabilities,
                EnvironmentName = environmentName,
                CommitInfo = new GithubCommitInfo
                {
                    GithubOwner = githubOwner,
                    GithubRepository = githubRepo,
                    GithubRef = githubRef,
                }
            };
        }

        private S3Util CreateS3Util()
        {
            var s3GetObjectFacade = Substitute.For<S3Util>();
            s3GetObjectFacade.GetZipEntryInObject(Arg.Any<string>(), Arg.Is(templateFileName)).Returns(template);
            s3GetObjectFacade.GetZipEntryInObject(Arg.Any<string>(), Arg.Is(templateConfigurationFileName)).Returns(templateConfiguration);
            return s3GetObjectFacade;
        }

        private DeployStackFacade CreateStackDeployer()
        {
            var stackDeployer = Substitute.For<DeployStackFacade>();
            return stackDeployer;
        }

        private ParseConfigFileFacade CreateParseConfigFileFacade()
        {
            var parseConfigFileFacade = Substitute.For<ParseConfigFileFacade>();
            parseConfigFileFacade.Parse(Arg.Any<string>()).Returns(configuration);
            return parseConfigFileFacade;
        }

        private TokenGenerator CreateTokenGenerator()
        {
            var tokenGenerator = Substitute.For<TokenGenerator>();
            tokenGenerator.Generate(Arg.Any<SQSEvent>(), Arg.Any<Request>()).Returns(createdToken);
            return tokenGenerator;
        }

        private RequestFactory SetupRequestFactory(Request request)
        {
            var requestFactory = Substitute.For<RequestFactory>();
            requestFactory.CreateFromSqsEvent(Arg.Any<SQSEvent>()).Returns(request);
            return requestFactory;
        }

        public IAmazonStepFunctions CreateStepFunctions()
        {
            var stepFunctions = Substitute.For<IAmazonStepFunctions>();
            return stepFunctions;
        }

        public IAmazonCloudFormation CreateCloudFormation()
        {
            var cloudFormationClient = Substitute.For<IAmazonCloudFormation>();
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
            return cloudFormationClient;
        }

        public IAwsFactory<IAmazonCloudFormation> CreateCloudFormationFactory(IAmazonCloudFormation client)
        {
            var factory = Substitute.For<IAwsFactory<IAmazonCloudFormation>>();
            factory.Create(Arg.Any<string>()).Returns(client);
            return factory;
        }

        private GithubStatusNotifier CreateStatusNotifier()
        {
            var statusNotifier = Substitute.For<GithubStatusNotifier>();
            return statusNotifier;
        }

        private RequestFactory CreateRequestFactory(Request request)
        {
            var requestFactory = Substitute.For<RequestFactory>();
            requestFactory.CreateFromSqsEvent(Arg.Any<SQSEvent>()).Returns(request);
            return requestFactory;
        }

        [Test]
        public void SQSEventIsParsed()
        {
            var request = CreateRequest();
            var deployer = CreateStackDeployer();
            var s3GetObjectFacade = CreateS3Util();
            var parseConfigFileFacade = CreateParseConfigFileFacade();
            var tokenGenerator = CreateTokenGenerator();
            var requestFactory = CreateRequestFactory(request);
            var stepFunctionsClient = CreateStepFunctions();
            var cloudformationClient = CreateCloudFormation();
            var cloudformationFactory = CreateCloudFormationFactory(cloudformationClient);
            var putCommitStatusFacade = CreateStatusNotifier();
            var sqs = Substitute.For<SQSEvent>();
            var handler = new Handler(deployer, s3GetObjectFacade, parseConfigFileFacade, tokenGenerator, requestFactory, stepFunctionsClient, cloudformationFactory, putCommitStatusFacade, config);

            Assert.ThrowsAsync<Exception>(() => handler.Handle(sqs));

            requestFactory.Received().CreateFromSqsEvent(Arg.Is(sqs));
        }

        [Test]
        public async Task ShouldPutPendingCommitStatus()
        {
            var request = CreateRequest();
            var deployer = CreateStackDeployer();
            var s3GetObjectFacade = CreateS3Util();
            var parseConfigFileFacade = CreateParseConfigFileFacade();
            var tokenGenerator = CreateTokenGenerator();
            var requestFactory = CreateRequestFactory(request);
            var stepFunctionsClient = CreateStepFunctions();
            var cloudformationClient = CreateCloudFormation();
            var cloudformationFactory = CreateCloudFormationFactory(cloudformationClient);
            var statusNotifier = CreateStatusNotifier();
            var sqs = Substitute.For<SQSEvent>();
            var handler = new Handler(deployer, s3GetObjectFacade, parseConfigFileFacade, tokenGenerator, requestFactory, stepFunctionsClient, cloudformationFactory, statusNotifier, config);

            Assert.ThrowsAsync<Exception>(() => handler.Handle(sqs));

            await statusNotifier.Received().NotifyPending(Is(githubOwner), Is(githubRepo), Is(githubRef), Is(stackName), Is(environmentName));
        }

        [Test]
        public async Task TemplateIsRetrieved()
        {
            var request = CreateRequest();
            var deployer = CreateStackDeployer();
            var s3GetObjectFacade = CreateS3Util();
            var parseConfigFileFacade = CreateParseConfigFileFacade();
            var tokenGenerator = CreateTokenGenerator();
            var requestFactory = CreateRequestFactory(request);
            var stepFunctionsClient = CreateStepFunctions();
            var cloudformationClient = CreateCloudFormation();
            var cloudformationFactory = CreateCloudFormationFactory(cloudformationClient);
            var putCommitStatusFacade = CreateStatusNotifier();
            var sqs = Substitute.For<SQSEvent>();
            var handler = new Handler(deployer, s3GetObjectFacade, parseConfigFileFacade, tokenGenerator, requestFactory, stepFunctionsClient, cloudformationFactory, putCommitStatusFacade, config);

            Assert.ThrowsAsync<Exception>(() => handler.Handle(sqs));

            await s3GetObjectFacade.Received().GetZipEntryInObject(Arg.Is(location), Arg.Is(templateFileName));
        }

        [Test]
        public async Task TemplateConfigurationIsRetrieved()
        {
            var request = CreateRequest();
            var deployer = CreateStackDeployer();
            var s3GetObjectFacade = CreateS3Util();
            var parseConfigFileFacade = CreateParseConfigFileFacade();
            var tokenGenerator = CreateTokenGenerator();
            var requestFactory = CreateRequestFactory(request);
            var stepFunctionsClient = CreateStepFunctions();
            var cloudformationClient = CreateCloudFormation();
            var cloudformationFactory = CreateCloudFormationFactory(cloudformationClient);
            var putCommitStatusFacade = CreateStatusNotifier();
            var sqs = Substitute.For<SQSEvent>();
            var handler = new Handler(deployer, s3GetObjectFacade, parseConfigFileFacade, tokenGenerator, requestFactory, stepFunctionsClient, cloudformationFactory, putCommitStatusFacade, config);

            Assert.ThrowsAsync<Exception>(() => handler.Handle(sqs));

            await s3GetObjectFacade.Received().GetZipEntryInObject(Arg.Is(location), Arg.Is(templateConfigurationFileName));
        }

        [Test]
        public async Task TokenIsGenerated()
        {
            var request = CreateRequest();
            var deployer = CreateStackDeployer();
            var s3GetObjectFacade = CreateS3Util();
            var parseConfigFileFacade = CreateParseConfigFileFacade();
            var tokenGenerator = CreateTokenGenerator();
            var requestFactory = CreateRequestFactory(request);
            var stepFunctionsClient = CreateStepFunctions();
            var cloudformationClient = CreateCloudFormation();
            var cloudformationFactory = CreateCloudFormationFactory(cloudformationClient);
            var putCommitStatusFacade = CreateStatusNotifier();
            var sqs = Substitute.For<SQSEvent>();
            var handler = new Handler(deployer, s3GetObjectFacade, parseConfigFileFacade, tokenGenerator, requestFactory, stepFunctionsClient, cloudformationFactory, putCommitStatusFacade, config);

            Assert.ThrowsAsync<Exception>(() => handler.Handle(sqs));

            await tokenGenerator.Received().Generate(Arg.Is(sqs), Arg.Is(request));
        }

        [Test]
        public async Task TemplateConfigurationIsNotRetrievedIfNotGiven()
        {
            var request = CreateRequest();
            var deployer = CreateStackDeployer();
            var s3GetObjectFacade = CreateS3Util();
            var parseConfigFileFacade = CreateParseConfigFileFacade();
            var tokenGenerator = CreateTokenGenerator();
            var requestFactory = CreateRequestFactory(request);
            var stepFunctionsClient = CreateStepFunctions();
            var cloudformationClient = CreateCloudFormation();
            var cloudformationFactory = CreateCloudFormationFactory(cloudformationClient);
            var putCommitStatusFacade = CreateStatusNotifier();
            var sqs = Substitute.For<SQSEvent>();
            var handler = new Handler(deployer, s3GetObjectFacade, parseConfigFileFacade, tokenGenerator, requestFactory, stepFunctionsClient, cloudformationFactory, putCommitStatusFacade, config);

            request.TemplateConfigurationFileName = null;

            Assert.ThrowsAsync<Exception>(() => handler.Handle(sqs));

            await s3GetObjectFacade.DidNotReceive().GetZipEntryInObject(Arg.Is(location), Arg.Is((string)null!));
        }

        [Test]
        public async Task TemplateConfigurationIsNotRetrievedIfNotBlank()
        {
            var request = CreateRequest();
            var deployer = CreateStackDeployer();
            var s3GetObjectFacade = CreateS3Util();
            var parseConfigFileFacade = CreateParseConfigFileFacade();
            var tokenGenerator = CreateTokenGenerator();
            var requestFactory = CreateRequestFactory(request);
            var stepFunctionsClient = CreateStepFunctions();
            var cloudformationClient = CreateCloudFormation();
            var cloudformationFactory = CreateCloudFormationFactory(cloudformationClient);
            var putCommitStatusFacade = CreateStatusNotifier();
            var sqs = Substitute.For<SQSEvent>();
            var handler = new Handler(deployer, s3GetObjectFacade, parseConfigFileFacade, tokenGenerator, requestFactory, stepFunctionsClient, cloudformationFactory, putCommitStatusFacade, config);

            request.TemplateConfigurationFileName = "";

            Assert.ThrowsAsync<Exception>(() => handler.Handle(sqs));

            await s3GetObjectFacade.DidNotReceive().GetZipEntryInObject(Arg.Is(location), Arg.Is(""));
        }

        [Test]
        public async Task DeployWasCalled()
        {
            var request = CreateRequest();
            var deployer = CreateStackDeployer();
            var s3GetObjectFacade = CreateS3Util();
            var parseConfigFileFacade = CreateParseConfigFileFacade();
            var tokenGenerator = CreateTokenGenerator();
            var requestFactory = CreateRequestFactory(request);
            var stepFunctionsClient = CreateStepFunctions();
            var cloudformationClient = CreateCloudFormation();
            var cloudformationFactory = CreateCloudFormationFactory(cloudformationClient);
            var putCommitStatusFacade = CreateStatusNotifier();
            var sqs = Substitute.For<SQSEvent>();
            var handler = new Handler(deployer, s3GetObjectFacade, parseConfigFileFacade, tokenGenerator, requestFactory, stepFunctionsClient, cloudformationFactory, putCommitStatusFacade, config);

            Assert.ThrowsAsync<Exception>(() => handler.Handle(sqs));

            await deployer.Received().Deploy(
                Arg.Is<DeployStackContext>(c =>
                    c.StackName == stackName &&
                    c.Template == template &&
                    c.RoleArn == roleArn &&
                    c.NotificationArn == notificationArn &&
                    configuration.Parameters!.All(entry => c.Parameters!.Any(param => param.ParameterKey == entry.ParameterKey && param.ParameterValue == entry.ParameterValue)) &&
                    c.Tags == configuration.Tags &&
                    c.StackPolicyBody == configuration.StackPolicy!.ToString() &&
                    c.ClientRequestToken == createdToken &&
                    capabilities.All(c.Capabilities!.Contains)
                )
            );
        }

        [Test]
        public async Task HandleDoesntThrowIfNoUpdatesExceptionWasCaught()
        {
            var request = CreateRequest();
            var deployer = CreateStackDeployer();
            var s3GetObjectFacade = CreateS3Util();
            var parseConfigFileFacade = CreateParseConfigFileFacade();
            var tokenGenerator = CreateTokenGenerator();
            var requestFactory = CreateRequestFactory(request);
            var stepFunctionsClient = CreateStepFunctions();
            var cloudformationClient = CreateCloudFormation();
            var cloudformationFactory = CreateCloudFormationFactory(cloudformationClient);
            var putCommitStatusFacade = CreateStatusNotifier();
            var sqs = Substitute.For<SQSEvent>();
            var handler = new Handler(deployer, s3GetObjectFacade, parseConfigFileFacade, tokenGenerator, requestFactory, stepFunctionsClient, cloudformationFactory, putCommitStatusFacade, config);

            deployer.Deploy(new()).ReturnsForAnyArgs(x => { throw new NoUpdatesException("no updates"); });

            await handler.Handle(sqs);

            var serializedOutput = Serialize(outputs);
            await stepFunctionsClient.Received().SendTaskSuccessAsync(
                Arg.Is<SendTaskSuccessRequest>(c =>
                    c.TaskToken == clientRequestToken &&
                    c.Output == serializedOutput
                )
            );

            await cloudformationClient.Received().DescribeStacksAsync(Arg.Is<DescribeStacksRequest>(req =>
                req.StackName == stackName
            ));
        }

        [Test]
        public async Task ShouldPutSuccessCommitStatusIfNoUpdatesExceptionWasCaught()
        {
            var request = CreateRequest();
            var deployer = CreateStackDeployer();
            var s3GetObjectFacade = CreateS3Util();
            var parseConfigFileFacade = CreateParseConfigFileFacade();
            var tokenGenerator = CreateTokenGenerator();
            var requestFactory = CreateRequestFactory(request);
            var stepFunctionsClient = CreateStepFunctions();
            var cloudformationClient = CreateCloudFormation();
            var cloudformationFactory = CreateCloudFormationFactory(cloudformationClient);
            var statusNotifier = CreateStatusNotifier();
            var sqs = Substitute.For<SQSEvent>();
            var handler = new Handler(deployer, s3GetObjectFacade, parseConfigFileFacade, tokenGenerator, requestFactory, stepFunctionsClient, cloudformationFactory, statusNotifier, config);

            deployer.Deploy(new()).ReturnsForAnyArgs(x => { throw new NoUpdatesException("no updates"); });
            await handler.Handle(sqs);

            await statusNotifier.Received().NotifySuccess(Is(githubOwner), Is(githubRepo), Is(githubRef), Is(stackName), Is(environmentName));
        }

        [Test]
        public async Task ParameterOverridesAreRespected()
        {
            var request = CreateRequest();
            var deployer = CreateStackDeployer();
            var s3GetObjectFacade = CreateS3Util();
            var parseConfigFileFacade = CreateParseConfigFileFacade();
            var tokenGenerator = CreateTokenGenerator();
            var requestFactory = CreateRequestFactory(request);
            var stepFunctionsClient = CreateStepFunctions();
            var cloudformationClient = CreateCloudFormation();
            var cloudformationFactory = CreateCloudFormationFactory(cloudformationClient);
            var putCommitStatusFacade = CreateStatusNotifier();
            var sqs = Substitute.For<SQSEvent>();
            var handler = new Handler(deployer, s3GetObjectFacade, parseConfigFileFacade, tokenGenerator, requestFactory, stepFunctionsClient, cloudformationFactory, putCommitStatusFacade, config);

            request.ParameterOverrides = new Dictionary<string, string>
            {
                ["A"] = "C"
            };

            Assert.ThrowsAsync<Exception>(() => handler.Handle(sqs));

            await deployer.Received().Deploy(
                Arg.Is<DeployStackContext>(c =>
                    (from param in c.Parameters where param.ParameterKey == "A" select param.ParameterValue).First() == "C"
                )
            );
        }

        [Test]
        public async Task StepFunctionsNotifiedIfDeployFailed()
        {
            var request = CreateRequest();
            var deployer = CreateStackDeployer();
            var s3GetObjectFacade = CreateS3Util();
            var parseConfigFileFacade = CreateParseConfigFileFacade();
            var tokenGenerator = CreateTokenGenerator();
            var requestFactory = CreateRequestFactory(request);
            var stepFunctionsClient = CreateStepFunctions();
            var cloudformationClient = CreateCloudFormation();
            var cloudformationFactory = CreateCloudFormationFactory(cloudformationClient);
            var putCommitStatusFacade = CreateStatusNotifier();
            var sqs = Substitute.For<SQSEvent>();
            var message = "message";
            var handler = new Handler(deployer, s3GetObjectFacade, parseConfigFileFacade, tokenGenerator, requestFactory, stepFunctionsClient, cloudformationFactory, putCommitStatusFacade, config);


            deployer.Deploy(Arg.Any<DeployStackContext>()).Returns(x => throw new Exception(message));
            await handler.Handle(sqs);

            await stepFunctionsClient.Received().SendTaskFailureAsync(Arg.Is<SendTaskFailureRequest>(req =>
                req.TaskToken == clientRequestToken &&
                req.Cause == message
            ));
        }

        [Test]
        public async Task ShouldPutFailedCommitStatusIfDeployFailed()
        {
            var request = CreateRequest();
            var deployer = CreateStackDeployer();
            var s3GetObjectFacade = CreateS3Util();
            var parseConfigFileFacade = CreateParseConfigFileFacade();
            var tokenGenerator = CreateTokenGenerator();
            var requestFactory = CreateRequestFactory(request);
            var stepFunctionsClient = CreateStepFunctions();
            var cloudformationClient = CreateCloudFormation();
            var cloudformationFactory = CreateCloudFormationFactory(cloudformationClient);
            var statusNotifier = CreateStatusNotifier();
            var sqs = Substitute.For<SQSEvent>();
            var message = "message";
            var handler = new Handler(deployer, s3GetObjectFacade, parseConfigFileFacade, tokenGenerator, requestFactory, stepFunctionsClient, cloudformationFactory, statusNotifier, config);

            deployer.Deploy(Arg.Any<DeployStackContext>()).Returns(x => throw new Exception(message));
            await handler.Handle(sqs);

            await statusNotifier.Received().NotifyFailure(Is(githubOwner), Is(githubRepo), Is(githubRef), Is(stackName), Is(environmentName));
        }
    }
}