using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cythral.CloudFormation.Cicd.Entities;

namespace Cythral.CloudFormation.Cicd.Events {
    public class PushEvent {
        [JsonPropertyName("ref")]
        public string Ref { get; set; }

        [JsonPropertyName("head")]
        public string Head { get; set; }

        [JsonPropertyName("before")]
        public string Before { get; set; }

        [JsonPropertyName("size")]
        public int Size { get; set; }

        [JsonPropertyName("distinct_size")]
        public int DistinctSize { get; set; }

        [JsonPropertyName("commits")]
        public IEnumerable<Commit> Commits { get; set; }

        [JsonPropertyName("pusher")]
        public Author Pusher { get; set; }

        [JsonPropertyName("sender")]
        public User Sender { get; set; }
    }
}