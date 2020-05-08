using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using Amazon.CloudFormation.Model;

namespace Cythral.CloudFormation.StackDeployment.TemplateConfig.Converters
{
    public class TagConverter : JsonConverter<List<Tag>>
    {
        public override List<Tag> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var list = new List<Tag>();
            reader.Read();

            while (reader.TokenType != JsonTokenType.EndObject)
            {
                try
                {
                    var key = reader.GetString();
                    reader.Read();

                    var value = reader.GetString();
                    reader.Read();

                    list.Add(new Tag
                    {
                        Key = key,
                        Value = value
                    });
                }
                catch (Exception)
                {
                    break;
                }
            }

            return list;
        }

        public override void Write(Utf8JsonWriter writer, List<Tag> value, JsonSerializerOptions options)
        {
            writer.WriteStringValue("{");
            int i = 0;

            foreach (var tag in value)
            {
                writer.WriteStringValue($"\"{tag.Key}\":\"{tag.Value}\"");

                if (i != value.Count() - 1)
                {
                    writer.WriteStringValue(",");
                }

                writer.WriteStringValue("\n");
                i++;
            }

            writer.WriteStringValue("}");
        }
    }
}