using System.Collections.Generic;

using Amazon.CloudFormation.Model;

namespace Cythral.CloudFormation.StackDeployment
{
    public class DeployStackContext
    {
        public string StackName { get; set; }

        public string Template { get; set; }

        public string RoleArn { get; set; }

        public string PassRoleArn { get; set; }

        public string NotificationArn { get; set; }

        public string StackPolicyBody { get; set; }

        public IEnumerable<Parameter> Parameters { get; set; }

        public IEnumerable<Tag> Tags { get; set; }

        public IEnumerable<string> Capabilities { get; set; } = new List<string> { "CAPABILITY_IAM", "CAPABILITY_NAMED_IAM" };

    }
}