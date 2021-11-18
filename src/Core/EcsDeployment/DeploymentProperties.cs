namespace Cythral.CloudFormation.EcsDeployment
{
    public class DeploymentProperties
    {
        public string RoleArn { get; set; } = string.Empty;

        public string ClusterName { get; set; } = string.Empty;

        public string ServiceName { get; set; } = string.Empty;
    }
}