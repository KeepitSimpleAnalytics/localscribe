using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GramCloneClient.Models;

public sealed class ModelListResponse
{
    [JsonPropertyName("models")]
    public List<string> Models { get; set; } = new();
}
