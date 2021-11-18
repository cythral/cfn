using System.Collections.Generic;

using Amazon.CloudFormation.Model;

namespace Cythral.CloudFormation.StackDeployment.TemplateConfig
{
    public class TemplateConfiguration
    {
        public IEnumerable<Parameter> Parameters { get; set; } = null!;
        public List<Tag> Tags { get; set; } = null!;
        public StackPolicyBody? StackPolicy { get; set; }
    }
}