using System;
using System.Linq;
using System.Collections.Generic;

namespace Cythral.CloudFormation.CustomResource.Yaml {
    public class Role : Resource {
        public override string Type { get; set; } = "AWS::IAM::Role";

        public override object Properties { get; set; } = new PropertiesDefinition();

        public class PropertiesDefinition {
            public List<Policy> Policies { get; set; } = new List<Policy>();
            public PolicyDocument AssumeRolePolicyDocument { get; set; }     
        }

        public Role AddPolicy(Policy policy) {
            ((PropertiesDefinition) Properties).Policies.Add(policy);
            return this;
        }

        public Role AddTrustedAWSEntity(string principal) {
            InitializeAssumeRolePolicyDocument();

            var props = Properties as PropertiesDefinition;
            var trustStatement = props.AssumeRolePolicyDocument.Statement.First().Principal;

            if(trustStatement.AWS == null) {
                trustStatement.AWS = new HashSet<string>();
            }

            trustStatement.AWS.Add(principal);
            return this;
        }

        public Role AddTrustedServiceEntity(string principal) {
            InitializeAssumeRolePolicyDocument();

            var props = Properties as PropertiesDefinition;
            var trustStatement = props.AssumeRolePolicyDocument.Statement.First().Principal;

            if(trustStatement.Service == null) {
                trustStatement.Service = new HashSet<string>();
            }

            trustStatement.Service.Add(principal);
            return this;
        }

        private void InitializeAssumeRolePolicyDocument() {
            var props = Properties as PropertiesDefinition;

            if(props.AssumeRolePolicyDocument == null) {
                props.AssumeRolePolicyDocument = new PolicyDocument();
                props.AssumeRolePolicyDocument.Statement.Add(new PolicyStatement() {
                    Effect = "Allow",
                    Action = new HashSet<string>() { "sts:AssumeRole" },
                    Principal = new Principal()
                });
            }
        }
    }
}