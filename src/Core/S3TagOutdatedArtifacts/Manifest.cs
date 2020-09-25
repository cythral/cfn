using System.Collections.Generic;

namespace Cythral.CloudFormation.S3TagOutdatedArtifacts
{
    public class Manifest
    {
        public string Bucket { get; set; }
        public string Prefix { get; set; }
        public Dictionary<string, string> Files { get; set; }
    }
}