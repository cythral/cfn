using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cythral.CloudFormation.Entities {
    public class Commit {
        [JsonPropertyName("sha")]
        public string Sha { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

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