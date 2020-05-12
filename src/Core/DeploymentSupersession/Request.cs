using System;

namespace Cythral.CloudFormation.DeploymentSupersession
{
    public class Request
    {
        public string Pipeline { get; set; }
        public string Token { get; set; }
        public DateTime CommitTimestamp { get; set; }
    }
}