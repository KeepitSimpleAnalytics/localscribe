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

    /// <summary>
    /// Application theme (Light/Dark).
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AppTheme Theme { get; set; } = AppTheme.Light;

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

    /// <summary>
    /// LanguageTool grammar checking configuration.
    /// </summary>
    public LanguageToolSettings LanguageTool { get; set; } = new();

    /// <summary>
    /// Enable developer diagnostics dashboard.
    /// </summary>
    public bool EnableDiagnostics { get; set; } = false;
}

/// <summary>
/// Application theme mode.
/// </summary>
public enum AppTheme
{
    Light,
    Dark
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
/// Preset levels for LanguageTool checking strictness.
/// </summary>
public enum GrammarCheckPreset
{
    EnableAll,
    Minimal,
    Strict,
    Custom
}

/// <summary>
/// Configuration for LanguageTool grammar checking rules.
/// </summary>
public sealed class LanguageToolSettings
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public GrammarCheckPreset Preset { get; set; } = GrammarCheckPreset.EnableAll;

    // Category toggles - all enabled by default
    public bool EnableGrammar { get; set; } = true;
    public bool EnableSpelling { get; set; } = true;
    public bool EnablePunctuation { get; set; } = true;
    public bool EnableTypography { get; set; } = true;
    public bool EnableStyle { get; set; } = true;
    public bool EnableConfusedWords { get; set; } = true;
    public bool EnableRedundancy { get; set; } = true;
    public bool EnableCasing { get; set; } = true;
    public bool EnableSemantics { get; set; } = true;
    public bool EnableColloquialisms { get; set; } = true;
    public bool EnableCompounding { get; set; } = true;
    public bool EnablePlainEnglish { get; set; } = true;
    public bool EnableWikipedia { get; set; } = true;
    public bool EnableMisc { get; set; } = true;

    /// <summary>
    /// Apply a preset, setting all category toggles accordingly.
    /// </summary>
    public void ApplyPreset(GrammarCheckPreset preset)
    {
        Preset = preset;
        switch (preset)
        {
            case GrammarCheckPreset.EnableAll:
            case GrammarCheckPreset.Strict:
                SetAllCategories(true);
                break;
            case GrammarCheckPreset.Minimal:
                SetAllCategories(false);
                EnableGrammar = true;
                EnableSpelling = true;
                break;
            case GrammarCheckPreset.Custom:
                // Don't change individual settings
                break;
        }
    }

    private void SetAllCategories(bool enabled)
    {
        EnableGrammar = enabled;
        EnableSpelling = enabled;
        EnablePunctuation = enabled;
        EnableTypography = enabled;
        EnableStyle = enabled;
        EnableConfusedWords = enabled;
        EnableRedundancy = enabled;
        EnableCasing = enabled;
        EnableSemantics = enabled;
        EnableColloquialisms = enabled;
        EnableCompounding = enabled;
        EnablePlainEnglish = enabled;
        EnableWikipedia = enabled;
        EnableMisc = enabled;
    }

    /// <summary>
    /// Get list of disabled category IDs for the API.
    /// </summary>
    public List<string> GetDisabledCategories()
    {
        var disabled = new List<string>();
        if (!EnableGrammar) disabled.Add("GRAMMAR");
        if (!EnableSpelling) disabled.Add("SPELLING");
        if (!EnablePunctuation) disabled.Add("PUNCTUATION");
        if (!EnableTypography) disabled.Add("TYPOGRAPHY");
        if (!EnableStyle) disabled.Add("STYLE");
        if (!EnableConfusedWords) disabled.Add("CONFUSED_WORDS");
        if (!EnableRedundancy) disabled.Add("REDUNDANCY");
        if (!EnableCasing) disabled.Add("CASING");
        if (!EnableSemantics) disabled.Add("SEMANTICS");
        if (!EnableColloquialisms) disabled.Add("COLLOQUIALISMS");
        if (!EnableCompounding) disabled.Add("COMPOUNDING");
        if (!EnablePlainEnglish) disabled.Add("PLAIN_ENGLISH");
        if (!EnableWikipedia) disabled.Add("WIKIPEDIA");
        if (!EnableMisc) disabled.Add("MISC");
        return disabled;
    }
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
