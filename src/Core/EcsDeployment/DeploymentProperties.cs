namespace Cythral.CloudFormation.EcsDeployment
{
    public class DeploymentProperties
    {
        public string RoleArn { get; set; }

        public string ClusterName { get; set; }

        public string ServiceName { get; set; }
    }
}