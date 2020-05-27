namespace Cythral.CloudFormation.S3Deployment
{
    public class Request
    {
        public string ZipLocation { get; set; }
        public string DestinationBucket { get; set; }
        public string RoleArn { get; set; }
        public string EnvironmentName { get; set; }
        public string ProjectName { get; set; }
        public CommitInfo CommitInfo { get; set; }
    }
}