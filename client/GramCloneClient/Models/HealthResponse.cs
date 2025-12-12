using System.Text.Json.Serialization;

namespace GramCloneClient.Models;

/// <summary>
/// Response from the /health endpoint.
/// </summary>
public class HealthResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("environment")]
    public string Environment { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";
}
