using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon;
using Amazon.Runtime;

namespace Cythral.CloudFormation.CustomResource {
    public class AwsConstantClassConverterFactory : JsonConverterFactory {
        public override bool CanConvert(Type typeToConvert) {
            return typeToConvert.IsSubclassOf(typeof(ConstantClass));
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) {
            var typeArgs = new Type[] { typeToConvert };
            var converterType = typeof(AwsConstantClassConverter<>).MakeGenericType(typeArgs);
            return (JsonConverter) Activator.CreateInstance(converterType);
        }
    }
}