using System.Text.Json.Serialization;

namespace Cythral.CloudFormation.GithubWebhook.Github.Entities
{
    public class Author
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }
    }
}