using System.Text.Json.Serialization;

public class CreateStatusRequest
{
    [JsonPropertyName("state")]
    public string State { get; set; }

    [JsonPropertyName("target_url")]
    public string TargetUrl { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("context")]
    public string Context { get; set; }
}