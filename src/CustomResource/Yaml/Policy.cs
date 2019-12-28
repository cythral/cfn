using System.Collections.Generic;

namespace Cythral.CloudFormation.CustomResource.Yaml
{
    public class Policy
    {
        public string PolicyName { get; set; }
        public PolicyDocument PolicyDocument { get; private set; } = new PolicyDocument();

        public Policy(string PolicyName)
        {
            this.PolicyName = PolicyName;
        }

        public Policy AddStatement(HashSet<string> Action, string Effect = "Allow", string Resource = "*")
        {
            PolicyDocument.Statement.Add(new PolicyStatement()
            {
                Effect = Effect,
                Action = Action,
                Resource = Resource
            });

            return this;
        }
    }
}