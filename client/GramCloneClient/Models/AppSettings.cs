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

    /// <summary>
    /// Overlay display settings for error highlighting.
    /// </summary>
    public OverlayDisplaySettings Overlay { get; set; } = new();

    /// <summary>
    /// Timing settings for debounce, popups, and polling.
    /// </summary>
    public TimingSettings Timing { get; set; } = new();
}

/// <summary>
/// User-friendly color presets for error highlighting.
/// </summary>
public enum OverlayColorPreset
{
    Red,
    Blue,
    Orange,
    Purple,
    Green,
    Yellow,
    Custom
}

/// <summary>
/// Underline style options for error highlighting.
/// </summary>
public enum UnderlineStyle
{
    Solid,
    Wavy,
    Dotted,
    Dashed,
    None
}

/// <summary>
/// Configuration for overlay display appearance.
/// </summary>
public sealed class OverlayDisplaySettings
{
    /// <summary>
    /// Enable or disable the error underline overlay.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Color preset for error underlines. Use Custom to specify your own color.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public OverlayColorPreset ColorPreset { get; set; } = OverlayColorPreset.Red;

    /// <summary>
    /// Custom color in hex format (e.g., "#FF3C3C"). Only used when ColorPreset is Custom.
    /// </summary>
    public string CustomColor { get; set; } = "#FF3C3C";

    /// <summary>
    /// Opacity percentage for the underline (20-100).
    /// </summary>
    public int OpacityPercent { get; set; } = 78;

    /// <summary>
    /// Style of the underline.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UnderlineStyle Style { get; set; } = UnderlineStyle.Solid;

    /// <summary>
    /// Height/thickness of the underline in pixels (1.0-6.0).
    /// </summary>
    public double UnderlineHeight { get; set; } = 2.5;

    /// <summary>
    /// Offset below text baseline in pixels (0.0-10.0).
    /// </summary>
    public double UnderlineOffset { get; set; } = 2.0;

    /// <summary>
    /// Wave amplitude for wavy style in pixels (1.0-4.0).
    /// </summary>
    public double WaveAmplitude { get; set; } = 2.0;

    /// <summary>
    /// Wave length for wavy style in pixels (2.0-8.0).
    /// </summary>
    public double WaveLength { get; set; } = 4.0;

    /// <summary>
    /// Get the RGB color based on the preset or custom value.
    /// Returns (R, G, B) tuple.
    /// </summary>
    public (byte R, byte G, byte B) GetColor()
    {
        return ColorPreset switch
        {
            OverlayColorPreset.Red => (255, 60, 60),
            OverlayColorPreset.Blue => (60, 120, 255),
            OverlayColorPreset.Orange => (255, 140, 0),
            OverlayColorPreset.Purple => (160, 60, 255),
            OverlayColorPreset.Green => (60, 180, 60),
            OverlayColorPreset.Yellow => (255, 200, 0),
            OverlayColorPreset.Custom => ParseHexColor(CustomColor),
            _ => (255, 60, 60)
        };
    }

    /// <summary>
    /// Get the alpha value (0-255) from opacity percentage.
    /// </summary>
    public byte GetAlpha()
    {
        var clamped = SettingsValidator.Clamp(OpacityPercent, 20, 100);
        return (byte)(clamped * 255 / 100);
    }

    private static (byte R, byte G, byte B) ParseHexColor(string hex)
    {
        try
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
            {
                return (
                    Convert.ToByte(hex.Substring(0, 2), 16),
                    Convert.ToByte(hex.Substring(2, 2), 16),
                    Convert.ToByte(hex.Substring(4, 2), 16)
                );
            }
        }
        catch { }
        return (255, 60, 60); // Default to red on parse error
    }
}

/// <summary>
/// Configuration for timing and delays.
/// </summary>
public sealed class TimingSettings
{
    /// <summary>
    /// Delay after typing before checking for errors in milliseconds (200-2000).
    /// </summary>
    public int DebounceDelayMs { get; set; } = 750;

    /// <summary>
    /// How long the error popup stays visible in milliseconds (1000-30000).
    /// </summary>
    public int PopupDisplayDurationMs { get; set; } = 5000;

    /// <summary>
    /// Delay before showing tooltip on hover in milliseconds (100-1000).
    /// </summary>
    public int TooltipHoverDelayMs { get; set; } = 300;

    /// <summary>
    /// Auto-dismiss popup after inactivity in milliseconds (5000-60000).
    /// </summary>
    public int AutoDismissTimeoutMs { get; set; } = 15000;

    /// <summary>
    /// Polling interval for text changes in milliseconds (100-500).
    /// </summary>
    public int TextPollingIntervalMs { get; set; } = 150;
}

/// <summary>
/// Validates and clamps settings values to safe ranges.
/// </summary>
public static class SettingsValidator
{
    public static T Clamp<T>(T value, T min, T max) where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0) return min;
        if (value.CompareTo(max) > 0) return max;
        return value;
    }

    /// <summary>
    /// Validate and clamp overlay display settings to safe ranges.
    /// </summary>
    public static void ValidateAndClamp(OverlayDisplaySettings settings)
    {
        settings.OpacityPercent = Clamp(settings.OpacityPercent, 20, 100);
        settings.UnderlineHeight = Clamp(settings.UnderlineHeight, 1.0, 6.0);
        settings.UnderlineOffset = Clamp(settings.UnderlineOffset, 0.0, 10.0);
        settings.WaveAmplitude = Clamp(settings.WaveAmplitude, 1.0, 4.0);
        settings.WaveLength = Clamp(settings.WaveLength, 2.0, 8.0);
    }

    /// <summary>
    /// Validate and clamp timing settings to safe ranges.
    /// </summary>
    public static void ValidateAndClamp(TimingSettings settings)
    {
        settings.DebounceDelayMs = Clamp(settings.DebounceDelayMs, 200, 2000);
        settings.PopupDisplayDurationMs = Clamp(settings.PopupDisplayDurationMs, 1000, 30000);
        settings.TooltipHoverDelayMs = Clamp(settings.TooltipHoverDelayMs, 100, 1000);
        settings.AutoDismissTimeoutMs = Clamp(settings.AutoDismissTimeoutMs, 5000, 60000);
        settings.TextPollingIntervalMs = Clamp(settings.TextPollingIntervalMs, 100, 500);
    }

    /// <summary>
    /// Validate and clamp all settings in AppSettings.
    /// </summary>
    public static void ValidateAll(AppSettings settings)
    {
        ValidateAndClamp(settings.Overlay);
        ValidateAndClamp(settings.Timing);
    }
}
