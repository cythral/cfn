using System;
using System.Text.Json.Serialization;

namespace Cythral.CloudFormation.Entities
{
    public class Commit
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonPropertyName("author")]
        public Author Author { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("distinct")]
        public bool Distinct { get; set; }

        [JsonPropertyName("repository")]
        public Repository Repository { get; set; }
    }
}