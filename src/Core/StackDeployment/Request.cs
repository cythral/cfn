using System.Collections.Generic;

using Cythral.CloudFormation.StackDeployment.Github;

namespace Cythral.CloudFormation.StackDeployment
{
    public class Request
    {
        public string ZipLocation { get; set; } = string.Empty;
        public string TemplateFileName { get; set; } = string.Empty;
        public string? TemplateConfigurationFileName { get; set; }
        public List<string>? Capabilities { get; set; }
        public Dictionary<string, string>? ParameterOverrides { get; set; }
        public string StackName { get; set; } = string.Empty;
        public string RoleArn { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = string.Empty;
        public GithubCommitInfo CommitInfo { get; set; } = new();
    }
}