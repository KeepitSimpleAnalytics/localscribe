using System.Text.Json.Serialization;

namespace GramCloneClient.Models;

public sealed class BackendRuntimeConfigUpdate
{
    [JsonPropertyName("ollama_base_url")]
    public string? OllamaBaseUrl { get; set; }

    [JsonPropertyName("grammar_model")]
    public string? GrammarModel { get; set; }

    [JsonPropertyName("general_model")]
    public string? GeneralModel { get; set; }
}
