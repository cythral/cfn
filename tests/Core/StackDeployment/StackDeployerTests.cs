using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;

using Cythral.CloudFormation.StackDeployment;

using NSubstitute;

using NUnit.Framework;

using static Amazon.CloudFormation.OnFailure;

namespace Cythral.CloudFormation.Tests.GithubWebhook
{
    public class StackDeployerTests
    {
        [Test]
        public async Task DeployCallsCreateStackIfNotExists()
        {
            var stackName = "test-stack";
            var exampleTemplate = "this is a bad example template.";
            var roleArn = "arn:aws:iam::1:role/Facade";
            var cloudformationClient = Substitute.For<IAmazonCloudFormation>();
            var notificationArn = "arn:aws:sns::1:topic/Topic";
            var clientRequestToken = "token";

            var tags = new List<Tag>
            {
                new Tag { Key = "A", Value = "B" }
            };

            var parameters = new List<Parameter> {
                new Parameter { ParameterKey = "GithubToken", ParameterValue = "this is definitely the token" }
            };

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
            var cloudFormationFactory = Substitute.For<CloudFormationFactory>();
            cloudFormationFactory.Create().Returns(cloudformationClient);
            TestUtils.SetPrivateField(stackDeployer, "cloudFormationFactory", cloudFormationFactory);

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
            var stackName = "test-stack";
            var exampleTemplate = "this is a bad example template.";
            var roleArn = "arn:aws:iam::1:role/Facade";
            var cloudformationClient = Substitute.For<IAmazonCloudFormation>();
            var notificationArn = "arn:aws:sns::1:topic/Topic";
            var clientRequestToken = "token";

            var tags = new List<Tag>
            {
                new Tag { Key = "A", Value = "B" }
            };

            var parameters = new List<Parameter>
            {
                new Parameter { ParameterKey = "GithubToken", ParameterValue = "this is definitely the token" }
            };

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
            var cloudFormationFactory = Substitute.For<CloudFormationFactory>();
            cloudFormationFactory.Create().Returns(cloudformationClient);
            TestUtils.SetPrivateField(stackDeployer, "cloudFormationFactory", cloudFormationFactory);


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
    }
}