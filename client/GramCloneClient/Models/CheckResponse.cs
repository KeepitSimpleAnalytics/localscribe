using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GramCloneClient.Models;

public class CheckResponse
{
    [JsonPropertyName("matches")]
    public List<GrammarMatch> Matches { get; set; } = new();
}

public class GrammarMatch
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("length")]
    public int Length { get; set; }

    [JsonPropertyName("replacements")]
    public List<string> Replacements { get; set; } = new();

    [JsonPropertyName("rule_id")]
    public string RuleId { get; set; } = "";

    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("context")]
    public string Context { get; set; } = "";

    [JsonPropertyName("sentence")]
    public string Sentence { get; set; } = "";

    [JsonPropertyName("offset_in_context")]
    public int OffsetInContext { get; set; }
}
