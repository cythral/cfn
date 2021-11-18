using System.Collections.Generic;

namespace Cythral.CloudFormation.S3TagOutdatedArtifacts
{
    public class Manifest
    {
        public string BucketName { get; set; } = string.Empty;
        public string Prefix { get; set; } = string.Empty;
        public Dictionary<string, string> Files { get; set; } = new();
    }
}