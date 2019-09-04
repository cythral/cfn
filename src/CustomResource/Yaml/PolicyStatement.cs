using System;
using System.Collections.Generic;

namespace Cythral.CloudFormation.CustomResource.Yaml {
    public class PolicyStatement {
        public string Effect { get; set; }
        public HashSet<string> Action { get; set; }
        public string Resource { get; set; }
        public Principal Principal { get; set; }
    }
}