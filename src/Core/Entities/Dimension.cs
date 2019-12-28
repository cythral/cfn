using System.Text.Json.Serialization;

namespace Cythral.CloudFormation.Entities
{
    public class Dimension
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; }
    }
}