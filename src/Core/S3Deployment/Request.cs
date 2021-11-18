namespace Cythral.CloudFormation.S3Deployment
{
    public class Request
    {
        public string ZipLocation { get; set; } = string.Empty;
        public string DestinationBucket { get; set; } = string.Empty;
        public string RoleArn { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public CommitInfo CommitInfo { get; set; } = new();
    }
}