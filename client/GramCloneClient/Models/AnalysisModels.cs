using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GramCloneClient.Models;

public class AnalysisRequest
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public class AnalysisResponse
{
    [JsonPropertyName("issues")]
    public List<AnalysisIssue> Issues { get; set; } = new();

    [JsonPropertyName("latency_ms")]
    public double LatencyMs { get; set; }
}

public class AnalysisIssue
{
    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("length")]
    public int Length { get; set; }

    [JsonPropertyName("quoted_text")]
    public string QuotedText { get; set; } = string.Empty;

    [JsonPropertyName("issue_type")]
    public string IssueType { get; set; } = string.Empty;

    [JsonPropertyName("suggestion")]
    public string Suggestion { get; set; } = string.Empty;
    
    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
}
