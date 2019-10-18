using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cythral.CloudFormation.Cicd.Entities {
    public class Author {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }
    }
}