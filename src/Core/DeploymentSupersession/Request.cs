using System;

namespace Cythral.CloudFormation.DeploymentSupersession
{
    public class Request
    {
        public string Pipeline { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public DateTime CommitTimestamp { get; set; }
    }
}