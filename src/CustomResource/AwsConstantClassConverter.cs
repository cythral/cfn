using System;
using System.Text.Json;
using System.Text.Json.Serialization;

using Amazon.Runtime;

namespace Cythral.CloudFormation.CustomResource
{
    public class AwsConstantClassConverter<T> : JsonConverter<T> where T : ConstantClass
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            return (T)Activator.CreateInstance(typeToConvert, new string[] { value });
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.Value);
        }
    }
}