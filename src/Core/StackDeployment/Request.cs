using System.Collections.Generic;
namespace Cythral.CloudFormation.StackDeployment
{
    public class Request
    {
        public string ZipLocation { get; set; }
        public string TemplateFileName { get; set; }
        public string TemplateConfigurationFileName { get; set; }
        public List<string> Capabilities { get; set; }
        public Dictionary<string, string> ParameterOverrides { get; set; }
        public string StackName { get; set; }
        public string RoleArn { get; set; }
        public string Token { get; set; }
        public string EnvironmentName { get; set; }
        public string GithubOwner { get; set; }
        public string GithubRepository { get; set; }
        public string GithubRef { get; set; }
        public string GoogleClientId { get; set; }
        public string IdentityPoolId { get; set; }
    }
}