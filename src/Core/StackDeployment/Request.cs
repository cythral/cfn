namespace Cythral.CloudFormation.StackDeployment
{
    public class Request
    {
        public string ZipLocation { get; set; }
        public string TemplateFileName { get; set; }
        public string TemplateConfigurationFileName { get; set; }
        public string StackName { get; set; }
        public string RoleArn { get; set; }
        public string Token { get; set; }
    }
}