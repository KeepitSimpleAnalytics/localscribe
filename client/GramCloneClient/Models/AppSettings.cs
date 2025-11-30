using System.Text.Json.Serialization;

namespace GramCloneClient.Models;

/// <summary>
/// Persisted configuration for the Windows client.
/// </summary>
public sealed class AppSettings
{
    public string BackendUrl { get; set; } = "http://127.0.0.1:8000";

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EditingMode DefaultMode { get; set; } = EditingMode.Proofread;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ToneStyle DefaultTone { get; set; } = ToneStyle.Professional;

    public string Hotkey { get; set; } = "Ctrl+Alt+G";

    public bool AutoStartBackend { get; set; } = true;
    public string BackendStartupCommand { get; set; } = "python -m uvicorn app.main:app --host 127.0.0.1 --port 8000";
    public string BackendWorkingDirectory { get; set; } = "";
}
