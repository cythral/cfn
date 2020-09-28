extern alias StackDeployment;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;

using NSubstitute;

using NUnit.Framework;

using StackDeployment::Cythral.CloudFormation.AwsUtils;
using StackDeployment::Cythral.CloudFormation.AwsUtils.CloudFormation;
using StackDeployment::Cythral.CloudFormation.StackDeployment;

using static Amazon.CloudFormation.OnFailure;

namespace Cythral.CloudFormation.Tests.StackDeployment
{
    public class StackDeployerTests
    {
        private const string stackName = "test-stack";
        private const string exampleTemplate = "this is a bad example template.";
        private const string roleArn = "arn:aws:iam::1:role/Facade";
        private IAmazonCloudFormation cloudformationClient = Substitute.For<IAmazonCloudFormation>();
        private const string notificationArn = "arn:aws:sns::1:topic/Topic";
        private const string clientRequestToken = "token";

        private List<Tag> tags = new List<Tag>
        {
            new Tag { Key = "A", Value = "B" }
        };

        private List<Parameter> parameters = new List<Parameter>
        {
            new Parameter { ParameterKey = "GithubToken", ParameterValue = "this is definitely the token" }
        };

        [Test]
        public async Task DeployCallsCreateStackIfNotExists()
        {

            cloudformationClient
            .DescribeStacksAsync(Arg.Is<DescribeStacksRequest>(req =>
                req.StackName == stackName
            ))
            .Returns(new DescribeStacksResponse
            {
                Stacks = new List<Stack>()
            });

            cloudformationClient
            .CreateStackAsync(Arg.Any<CreateStackRequest>())
            .Returns(new CreateStackResponse { });

            var stackDeployer = new DeployStackFacade();
            var cloudformationFactory = Substitute.For<AmazonClientFactory<IAmazonCloudFormation>>();
            cloudformationFactory.Create().Returns(cloudformationClient);
            TestUtils.SetPrivateField(stackDeployer, "cloudformationFactory", cloudformationFactory);

            await stackDeployer.Deploy(new DeployStackContext
            {
                StackName = stackName,
                Template = exampleTemplate,
                NotificationArn = notificationArn,
                Tags = tags,
                PassRoleArn = roleArn,
                Parameters = parameters,
                ClientRequestToken = clientRequestToken
            });

            await cloudformationClient
            .Received()
            .CreateStackAsync(Arg.Is<CreateStackRequest>(req =>
                req.StackName == stackName &&
                req.TemplateBody == exampleTemplate &&
                req.RoleARN == roleArn &&
                req.NotificationARNs.Contains(notificationArn) &&
                tags.All(req.Tags.Contains) &&
                req.ClientRequestToken == clientRequestToken &&
                req.Parameters.Any(parameter => parameter.ParameterKey == "GithubToken" && parameter.ParameterValue == "this is definitely the token") &&
                req.Capabilities.Any(capability => capability == "CAPABILITY_IAM") &&
                req.Capabilities.Any(capability => capability == "CAPABILITY_NAMED_IAM") &&
                req.OnFailure == DELETE
            ));
        }

        [Test]
        public async Task DeployCallsUpdateStackIfExists()
        {

            cloudformationClient
            .DescribeStacksAsync(Arg.Is<DescribeStacksRequest>(req =>
                req.StackName == stackName
            ))
            .Returns(new DescribeStacksResponse
            {
                Stacks = new List<Stack> {
                    new Stack {
                        StackName = stackName
                    }
                }
            });

            cloudformationClient
            .UpdateStackAsync(Arg.Any<UpdateStackRequest>())
            .Returns(new UpdateStackResponse { });

            var stackDeployer = new DeployStackFacade();
            var cloudformationFactory = Substitute.For<AmazonClientFactory<IAmazonCloudFormation>>();
            cloudformationFactory.Create().Returns(cloudformationClient);
            TestUtils.SetPrivateField(stackDeployer, "cloudformationFactory", cloudformationFactory);


            await stackDeployer.Deploy(new DeployStackContext
            {
                StackName = stackName,
                Template = exampleTemplate,
                NotificationArn = notificationArn,
                PassRoleArn = roleArn,
                Parameters = parameters,
                Tags = tags,
                ClientRequestToken = clientRequestToken
            });

            await cloudformationClient
            .Received()
            .UpdateStackAsync(Arg.Is<UpdateStackRequest>(req =>
                req.StackName == stackName &&
                req.TemplateBody == exampleTemplate &&
                req.RoleARN == roleArn &&
                req.NotificationARNs.Contains(notificationArn) &&
                tags.All(req.Tags.Contains) &&
                req.ClientRequestToken == clientRequestToken &&
                req.Parameters.Any(parameter => parameter.ParameterKey == "GithubToken" && parameter.ParameterValue == "this is definitely the token") &&
                req.Capabilities.Any(capability => capability == "CAPABILITY_IAM") &&
                req.Capabilities.Any(capability => capability == "CAPABILITY_NAMED_IAM")
            ));
        }

        [Test]
        public void UpdateRethrowsWithNoUpdatesException()
        {

            cloudformationClient
            .DescribeStacksAsync(Arg.Is<DescribeStacksRequest>(req =>
                req.StackName == stackName
            ))
            .Returns(new DescribeStacksResponse
            {
                Stacks = new List<Stack> {
                    new Stack {
                        StackName = stackName
                    }
                }
            });

            cloudformationClient
            .UpdateStackAsync(Arg.Any<UpdateStackRequest>())
            .Returns<UpdateStackResponse>(x => { throw new Exception("No updates are to be performed."); });

            var stackDeployer = new DeployStackFacade();
            var cloudformationFactory = Substitute.For<AmazonClientFactory<IAmazonCloudFormation>>();
            cloudformationFactory.Create().Returns(cloudformationClient);
            TestUtils.SetPrivateField(stackDeployer, "cloudformationFactory", cloudformationFactory);


            Assert.ThrowsAsync<NoUpdatesException>(() => stackDeployer.Deploy(new DeployStackContext
            {
                StackName = stackName,
                Template = exampleTemplate,
                NotificationArn = notificationArn,
                PassRoleArn = roleArn,
                Parameters = parameters,
                Tags = tags,
                ClientRequestToken = clientRequestToken
            }));
        }

        [Test]
        public void UpdateThrowsIfMessageIsNotNoUpdates()
        {

            cloudformationClient
            .DescribeStacksAsync(Arg.Is<DescribeStacksRequest>(req =>
                req.StackName == stackName
            ))
            .Returns(new DescribeStacksResponse
            {
                Stacks = new List<Stack> {
                    new Stack {
                        StackName = stackName
                    }
                }
            });

            cloudformationClient
            .UpdateStackAsync(Arg.Any<UpdateStackRequest>())
            .Returns<UpdateStackResponse>(x => { throw new Exception("Some other exception"); });

            var stackDeployer = new DeployStackFacade();
            var cloudformationFactory = Substitute.For<AmazonClientFactory<IAmazonCloudFormation>>();
            cloudformationFactory.Create().Returns(cloudformationClient);
            TestUtils.SetPrivateField(stackDeployer, "cloudformationFactory", cloudformationFactory);


            Assert.ThrowsAsync<Exception>(() => stackDeployer.Deploy(new DeployStackContext
            {
                StackName = stackName,
                Template = exampleTemplate,
                NotificationArn = notificationArn,
                PassRoleArn = roleArn,
                Parameters = parameters,
                Tags = tags,
                ClientRequestToken = clientRequestToken
            }));
        }
    }
}