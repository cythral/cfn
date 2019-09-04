using System;
using System.Collections.Generic;

namespace Cythral.CloudFormation.CustomResource.Yaml {
    public class PolicyDocument {
        public string Version { get; set; } = "2012-10-17";
        public List<PolicyStatement> Statement { get; set; } = new List<PolicyStatement>();      
    }
}