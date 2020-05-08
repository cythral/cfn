using System;
using System.Threading.Tasks;
using System.Text.Json;

using Cythral.CloudFormation.StackDeployment.TemplateConfig.Converters;

namespace Cythral.CloudFormation.StackDeployment.TemplateConfig
{
    public class ParseConfigFileFacade
    {
        public virtual TemplateConfiguration Parse(string config)
        {
            var options = new JsonSerializerOptions();

            options.Converters.Add(new ParameterConverter());
            options.Converters.Add(new TagConverter());
            options.Converters.Add(new StackPolicyBodyConverter());

            return JsonSerializer.Deserialize<TemplateConfiguration>(config, options);
        }
    }
}