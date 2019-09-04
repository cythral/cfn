using System;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Cythral.CloudFormation.CustomResource.Yaml {
    public class GetAttTagConverter : IYamlTypeConverter {
        public bool Accepts(Type type) => 
            type == typeof(GetAttTag);

        public object ReadYaml(IParser parser, Type type) => 
            throw new Exception("Unsupported Operation");

        public void WriteYaml(IEmitter emitter, object value, Type type) {
            var result = (GetAttTag) value;
            emitter.Emit(new Scalar(
                null, 
                "!GetAtt", 
                $"{result.Name}.{result.Attribute}", 
                ScalarStyle.Plain, 
                false, 
                false
            ));
        }
    }
}