using System.Collections.Generic;

using Amazon.CloudFormation.Model;

namespace Cythral.CloudFormation.StackDeployment.TemplateConfig
{
    public class TemplateConfiguration
    {
        public List<Parameter> Parameters { get; set; }
        public List<Tag> Tags { get; set; }
        public StackPolicyBody StackPolicy { get; set; }
    }
}