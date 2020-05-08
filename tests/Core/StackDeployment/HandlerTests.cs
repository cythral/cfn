using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Amazon.CloudFormation.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using Amazon.ElasticLoadBalancingV2;
using Amazon.ElasticLoadBalancingV2.Model;
using Amazon.Lambda.SNSEvents;

using Cythral.CloudFormation.Events;
using Cythral.CloudFormation.Facades;
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
    public class HandlerTests
    {
        private static DeployStackFacade stackDeployer = Substitute.For<DeployStackFacade>();
        private static ParseConfigFileFacade parseConfigFileFacade = Substitute.For<ParseConfigFileFacade>();
        private static IAmazonStepFunctions stepFunctionsClient = Substitute.For<IAmazonStepFunctions>();
        private static S3GetObjectFacade s3GetObjectFacade = Substitute.For<S3GetObjectFacade>();

        private const string stackName = "stackName";
        private const string location = "location";
        private const string templateFileName = "templateFileName";
        private const string roleArn = "roleArn";
        private const string template = "template";
        private const string actionMode = "actionMode";
        private const string templateConfigurationFileName = "configurationFileName";
        private const string notificationArn = "notificationArn";
        private string templateConfiguration = "templateConfiguration";

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
            s3GetObjectFacade.GetObject(Arg.Any<string>(), Arg.Is(templateFileName)).Returns(template);
            s3GetObjectFacade.GetObject(Arg.Any<string>(), Arg.Is(templateConfigurationFileName)).Returns(templateConfiguration);
        }

        [SetUp]
        public void SetupStackDeployer()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "stackDeployer", stackDeployer);
            stackDeployer.ClearReceivedCalls();
        }

        [SetUp]
        public void SetupParseConfigFileFacade()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "parseConfigFileFacade", parseConfigFileFacade);
            parseConfigFileFacade.ClearReceivedCalls();
            parseConfigFileFacade.Parse(Arg.Any<string>()).Returns(configuration);
        }

        [SetUp]
        public void SetupEnvvars()
        {
            Environment.SetEnvironmentVariable("NOTIFICATION_ARN", notificationArn);
        }

        private Request CreateRequest()
        {
            return new Request
            {
                ZipLocation = location,
                TemplateFileName = templateFileName,
                TemplateConfigurationFileName = templateConfigurationFileName,
                StackName = stackName,
                RoleArn = roleArn
            };
        }

        [Test]
        public async Task TemplateIsRetrieved()
        {
            var request = CreateRequest();
            await Handler.Handle(request);

            await s3GetObjectFacade.Received().GetObject(Arg.Is(location), Arg.Is(templateFileName));
        }

        [Test]
        public async Task TemplateConfigurationIsRetrieved()
        {
            var request = CreateRequest();
            await Handler.Handle(request);

            await s3GetObjectFacade.Received().GetObject(Arg.Is(location), Arg.Is(templateConfigurationFileName));
        }

        [Test]
        public async Task TemplateConfigurationIsNotRetrievedIfNotGiven()
        {
            var request = CreateRequest();
            request.TemplateConfigurationFileName = null;

            await Handler.Handle(request);

            await s3GetObjectFacade.DidNotReceive().GetObject(Arg.Is(location), Arg.Is((string)null));
        }

        [Test]
        public async Task TemplateConfigurationIsNotRetrievedIfNotBlank()
        {
            var request = CreateRequest();
            request.TemplateConfigurationFileName = "";

            await Handler.Handle(request);

            await s3GetObjectFacade.DidNotReceive().GetObject(Arg.Is(location), Arg.Is(""));
        }

        [Test]
        public async Task DeployWasCalled()
        {
            var request = CreateRequest();
            await Handler.Handle(request);

            await stackDeployer.Received().Deploy(
                Arg.Is<DeployStackContext>(c =>
                    c.StackName == stackName &&
                    c.Template == template &&
                    c.RoleArn == roleArn &&
                    c.NotificationArn == notificationArn &&
                    c.Parameters == configuration.Parameters &&
                    c.Tags == configuration.Tags &&
                    c.StackPolicyBody == configuration.StackPolicy.ToString()
                )
            );
        }
    }
}