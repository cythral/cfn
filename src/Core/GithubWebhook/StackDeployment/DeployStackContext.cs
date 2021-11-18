using System;
using System.Collections.Generic;

using Amazon.CloudFormation.Model;

namespace Cythral.CloudFormation.GithubWebhook.StackDeployment
{
    public class DeployStackContext
    {
        public string StackName { get; set; } = string.Empty;

        public string Template { get; set; } = string.Empty;

        public string? RoleArn { get; set; }

        public string PassRoleArn { get; set; } = string.Empty;

        public string NotificationArn { get; set; } = string.Empty;

        public string? StackPolicyBody { get; set; }

        public string ClientRequestToken { get; set; } = string.Empty;

        public IEnumerable<Parameter>? Parameters { get; set; }

        public IEnumerable<Tag>? Tags { get; set; }

        public IEnumerable<string>? Capabilities { get; set; } = new List<string> { "CAPABILITY_IAM", "CAPABILITY_NAMED_IAM" };

    }
}