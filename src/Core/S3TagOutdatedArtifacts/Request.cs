namespace Cythral.CloudFormation.S3TagOutdatedArtifacts
{
    public class Request
    {
        public string ManifestLocation { get; set; } = string.Empty;
        public string ManifestFilename { get; set; } = string.Empty;
    }
}