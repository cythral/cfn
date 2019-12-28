using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;

using Cythral.CloudFormation.Facades;

using NSubstitute;

using NUnit.Framework;

using static Amazon.CloudFormation.OnFailure;

namespace Cythral.CloudFormation.Tests.Facades
{
    public class StackDeployerTest
    {
        [Test]
        public async Task DeployCallsCreateStackIfNotExists()
        {
            var stackName = "test-stack";
            var exampleTemplate = "this is a bad example template.";
            var roleArn = "arn:aws:iam::1:role/Facade";
            var cloudformationClient = Substitute.For<IAmazonCloudFormation>();
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

            await StackDeployer.Deploy(stackName, exampleTemplate, roleArn, parameters, cloudformationClient: cloudformationClient);
            await cloudformationClient
            .Received()
            .CreateStackAsync(Arg.Is<CreateStackRequest>(req =>
                req.StackName == stackName &&
                req.TemplateBody == exampleTemplate &&
                req.RoleARN == roleArn &&
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
            var parameters = new List<Parameter> {
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

            await StackDeployer.Deploy(stackName, exampleTemplate, roleArn, parameters, cloudformationClient: cloudformationClient);
            await cloudformationClient
            .Received()
            .UpdateStackAsync(Arg.Is<UpdateStackRequest>(req =>
                req.StackName == stackName &&
                req.TemplateBody == exampleTemplate &&
                req.RoleARN == roleArn &&
                req.Parameters.Any(parameter => parameter.ParameterKey == "GithubToken" && parameter.ParameterValue == "this is definitely the token") &&
                req.Capabilities.Any(capability => capability == "CAPABILITY_IAM") &&
                req.Capabilities.Any(capability => capability == "CAPABILITY_NAMED_IAM")
            ));
        }
    }
}