using System.Text.Json.Serialization;

namespace GramCloneClient.Models;

public sealed class BackendRuntimeConfig
{
    [JsonPropertyName("ollama_base_url")]
    public string OllamaBaseUrl { get; set; } = string.Empty;

    [JsonPropertyName("grammar_model")]
    public string GrammarModel { get; set; } = string.Empty;

    [JsonPropertyName("general_model")]
    public string GeneralModel { get; set; } = string.Empty;
}
