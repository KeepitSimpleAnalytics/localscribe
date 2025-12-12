using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using GramCloneClient.Backend;
using GramCloneClient.Models;
using GramCloneClient.Services;

using System.Windows.Media;

namespace GramCloneClient.Windows;

/// <summary>
/// Allows editing backend URL and defaults.
/// </summary>
public partial class SettingsWindow : Window
{
    private AppSettings _settings;
    private readonly BackendClient _backendClient;
    private IReadOnlyList<string> _availableModels = Array.Empty<string>();
    private bool _isLoading;
    private bool _isLoadingCategories;

    public event EventHandler<AppSettings>? SettingsSaved;

    public SettingsWindow(AppSettings settings, BackendClient backendClient)
    {
        InitializeComponent();
        _settings = settings;
        _backendClient = backendClient;

        // Initialize combo boxes with enum values
        ModeCombo.ItemsSource = Enum.GetValues(typeof(EditingMode));
        ToneCombo.ItemsSource = Enum.GetValues(typeof(ToneStyle));
        OverlayColorCombo.ItemsSource = Enum.GetValues(typeof(OverlayColorPreset));
        OverlayStyleCombo.ItemsSource = Enum.GetValues(typeof(UnderlineStyle));
        GrammarPresetCombo.ItemsSource = Enum.GetValues(typeof(GrammarCheckPreset));
        ThemeCombo.ItemsSource = Enum.GetValues(typeof(AppTheme));

        LoadValues();
    }

    private void LoadValues()
    {
        // Load theme setting
        ThemeCombo.SelectedItem = _settings.Theme;

        BackendUrlBox.Text = _settings.BackendUrl;
        HotkeyBox.Text = _settings.Hotkey;
        ModeCombo.SelectedItem = _settings.DefaultMode;
        ToneCombo.SelectedItem = _settings.DefaultTone;

        AutoStartCheckBox.IsChecked = _settings.AutoStartBackend;
        StartupCommandBox.Text = _settings.BackendStartupCommand;
        WorkDirBox.Text = _settings.BackendWorkingDirectory;

        // Load overlay settings
        OverlayEnabledCheckBox.IsChecked = _settings.Overlay.Enabled;
        OverlayColorCombo.SelectedItem = _settings.Overlay.ColorPreset;
        OverlayStyleCombo.SelectedItem = _settings.Overlay.Style;
        OpacitySlider.Value = _settings.Overlay.OpacityPercent;
        OpacityValueText.Text = _settings.Overlay.OpacityPercent.ToString();
        ThicknessSlider.Value = _settings.Overlay.UnderlineHeight;
        ThicknessValueText.Text = _settings.Overlay.UnderlineHeight.ToString("F1");

        // Load custom color settings
        CustomColorBox.Text = _settings.Overlay.CustomColor;
        UpdateCustomColorPreview(_settings.Overlay.CustomColor);
        CustomColorPanel.Visibility = _settings.Overlay.ColorPreset == OverlayColorPreset.Custom
            ? Visibility.Visible : Visibility.Collapsed;

        // Load timing settings
        DebounceSlider.Value = _settings.Timing.DebounceDelayMs;
        DebounceValueText.Text = _settings.Timing.DebounceDelayMs.ToString();
        PollingSlider.Value = _settings.Timing.TextPollingIntervalMs;
        PollingValueText.Text = _settings.Timing.TextPollingIntervalMs.ToString();

        // Load LanguageTool settings
        GrammarPresetCombo.SelectedItem = _settings.LanguageTool.Preset;
        LoadCategoryCheckboxes();
    }

    private void LoadCategoryCheckboxes()
    {
        _isLoadingCategories = true;

        CatGrammarCheck.IsChecked = _settings.LanguageTool.EnableGrammar;
        CatSpellingCheck.IsChecked = _settings.LanguageTool.EnableSpelling;
        CatPunctuationCheck.IsChecked = _settings.LanguageTool.EnablePunctuation;
        CatTypographyCheck.IsChecked = _settings.LanguageTool.EnableTypography;
        CatStyleCheck.IsChecked = _settings.LanguageTool.EnableStyle;
        CatConfusedWordsCheck.IsChecked = _settings.LanguageTool.EnableConfusedWords;
        CatRedundancyCheck.IsChecked = _settings.LanguageTool.EnableRedundancy;
        CatCasingCheck.IsChecked = _settings.LanguageTool.EnableCasing;
        CatSemanticsCheck.IsChecked = _settings.LanguageTool.EnableSemantics;
        CatColloquialismsCheck.IsChecked = _settings.LanguageTool.EnableColloquialisms;
        CatCompoundingCheck.IsChecked = _settings.LanguageTool.EnableCompounding;
        CatPlainEnglishCheck.IsChecked = _settings.LanguageTool.EnablePlainEnglish;
        CatWikipediaCheck.IsChecked = _settings.LanguageTool.EnableWikipedia;
        CatMiscCheck.IsChecked = _settings.LanguageTool.EnableMisc;

        _isLoadingCategories = false;
    }

    public void UpdateSettings(AppSettings settings)
    {
        _settings = settings;
        LoadValues();
    }

    public async Task RefreshBackendDataAsync(string? backendOverride = null)
    {
        if (_isLoading)
        {
            return;
        }

        _isLoading = true;
        BackendConnectionStatus.Text = "Backend: Checking...";
        BackendConnectionStatus.Foreground = System.Windows.Media.Brushes.Gray;
        OllamaConnectionStatus.Text = "Ollama: Checking...";
        OllamaConnectionStatus.Foreground = System.Windows.Media.Brushes.Gray;
        
        GrammarModelCombo.ItemsSource = null;
        GeneralModelCombo.ItemsSource = null;

        try
        {
            var config = await _backendClient.GetRuntimeConfigAsync(backendOverride);
            BackendConnectionStatus.Text = "Backend: Connected";
            BackendConnectionStatus.Foreground = System.Windows.Media.Brushes.Green;

            try 
            {
                var models = await _backendClient.GetAvailableModelsAsync(backendOverride);
                _availableModels = models;
                
                OllamaUrlBox.Text = config.OllamaBaseUrl;
                GrammarModelCombo.ItemsSource = _availableModels;
                GrammarModelCombo.SelectedItem = config.GrammarModel;
                GrammarModelCombo.Text = config.GrammarModel;
                GeneralModelCombo.ItemsSource = _availableModels;
                GeneralModelCombo.SelectedItem = config.GeneralModel;
                GeneralModelCombo.Text = config.GeneralModel;

                OllamaConnectionStatus.Text = $"Ollama: Connected ({_availableModels.Count} models)";
                OllamaConnectionStatus.Foreground = System.Windows.Media.Brushes.Green;
            }
            catch (Exception)
            {
                OllamaConnectionStatus.Text = "Ollama: Unreachable / Error";
                OllamaConnectionStatus.Foreground = System.Windows.Media.Brushes.Red;
                // Still populate URL box if we got config, even if models failed
                OllamaUrlBox.Text = config.OllamaBaseUrl;
                _availableModels = Array.Empty<string>();
                GrammarModelCombo.ItemsSource = _availableModels;
                GeneralModelCombo.ItemsSource = _availableModels;
            }
        }
        catch (Exception ex)
        {
            BackendConnectionStatus.Text = "Backend: Unreachable";
            BackendConnectionStatus.Foreground = System.Windows.Media.Brushes.Red;
            OllamaConnectionStatus.Text = "Ollama: Unknown";
            OllamaConnectionStatus.Foreground = System.Windows.Media.Brushes.Gray;
            
            System.Windows.MessageBox.Show($"Backend connection failed: {ex.Message}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);

            _availableModels = Array.Empty<string>();
            GrammarModelCombo.ItemsSource = _availableModels;
            GeneralModelCombo.ItemsSource = _availableModels;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        // Save theme setting
        _settings.Theme = (AppTheme)(ThemeCombo.SelectedItem ?? AppTheme.Light);

        string backendUrl = BackendUrlBox.Text.Trim();
        _settings.BackendUrl = backendUrl;
        _settings.Hotkey = string.IsNullOrWhiteSpace(HotkeyBox.Text) ? _settings.Hotkey : HotkeyBox.Text.Trim();
        _settings.DefaultMode = (EditingMode)(ModeCombo.SelectedItem ?? EditingMode.Proofread);
        _settings.DefaultTone = (ToneStyle)(ToneCombo.SelectedItem ?? ToneStyle.Professional);

        _settings.AutoStartBackend = AutoStartCheckBox.IsChecked ?? false;
        _settings.BackendStartupCommand = StartupCommandBox.Text.Trim();
        _settings.BackendWorkingDirectory = WorkDirBox.Text.Trim();

        // Save overlay settings
        _settings.Overlay.Enabled = OverlayEnabledCheckBox.IsChecked ?? true;
        _settings.Overlay.ColorPreset = (OverlayColorPreset)(OverlayColorCombo.SelectedItem ?? OverlayColorPreset.Red);
        _settings.Overlay.Style = (UnderlineStyle)(OverlayStyleCombo.SelectedItem ?? UnderlineStyle.Solid);
        _settings.Overlay.OpacityPercent = (int)OpacitySlider.Value;
        _settings.Overlay.UnderlineHeight = ThicknessSlider.Value;
        _settings.Overlay.CustomColor = CustomColorBox.Text.Trim();

        // Save timing settings
        _settings.Timing.DebounceDelayMs = (int)DebounceSlider.Value;
        _settings.Timing.TextPollingIntervalMs = (int)PollingSlider.Value;

        string grammarModel = (GrammarModelCombo.Text ?? string.Empty).Trim();
        string generalModel = (GeneralModelCombo.Text ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(grammarModel) || string.IsNullOrWhiteSpace(generalModel))
        {
            System.Windows.MessageBox.Show(
                "Select both proofread and rewrite models before saving.",
                "Gram Clone",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var update = new BackendRuntimeConfigUpdate
        {
            OllamaBaseUrl = string.IsNullOrWhiteSpace(OllamaUrlBox.Text) ? null : PrependHttpIfMissing(OllamaUrlBox.Text.Trim()),
            GrammarModel = grammarModel,
            GeneralModel = generalModel,
        };

        try
        {
            await _backendClient.UpdateRuntimeConfigAsync(update, backendUrl);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to update backend configuration:\n{ex.Message}",
                "Gram Clone",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        _backendClient.UpdateSettings(_settings);
        SettingsSaved?.Invoke(this, _settings);
        Hide();
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    private async void ReloadModelsButton_OnClick(object sender, RoutedEventArgs e)
    {
        string baseUrl = OllamaUrlBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            System.Windows.MessageBox.Show(
                "Enter an Ollama base URL before reloading models.",
                "Gram Clone",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            await _backendClient.UpdateRuntimeConfigAsync(new BackendRuntimeConfigUpdate
            {
                OllamaBaseUrl = PrependHttpIfMissing(baseUrl)
            },
            BackendUrlBox.Text.Trim());
            await RefreshBackendDataAsync(BackendUrlBox.Text.Trim());
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to reload models:\n{ex.Message}",
                "Gram Clone",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Prepends "http://" to the URL if no scheme (http:// or https://) is present.
    /// </summary>
    /// <param name="url">The URL string to check and potentially modify.</param>
    /// <returns>The URL string with "http://" prepended if no scheme was found, otherwise the original URL.</returns>
    private static string PrependHttpIfMissing(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return "http://" + url;
        }
        return url;
    }

    private void ThemeCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ThemeCombo.SelectedItem == null) return;

        var theme = (AppTheme)ThemeCombo.SelectedItem;
        ThemeManager.ApplyTheme(theme);  // Live preview
    }

    private void GrammarPresetCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isLoadingCategories || GrammarPresetCombo.SelectedItem == null) return;

        var preset = (GrammarCheckPreset)GrammarPresetCombo.SelectedItem;
        _settings.LanguageTool.ApplyPreset(preset);
        LoadCategoryCheckboxes();
    }

    private void CategoryCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoadingCategories) return;

        // Update settings from checkboxes
        _settings.LanguageTool.EnableGrammar = CatGrammarCheck.IsChecked ?? true;
        _settings.LanguageTool.EnableSpelling = CatSpellingCheck.IsChecked ?? true;
        _settings.LanguageTool.EnablePunctuation = CatPunctuationCheck.IsChecked ?? true;
        _settings.LanguageTool.EnableTypography = CatTypographyCheck.IsChecked ?? true;
        _settings.LanguageTool.EnableStyle = CatStyleCheck.IsChecked ?? true;
        _settings.LanguageTool.EnableConfusedWords = CatConfusedWordsCheck.IsChecked ?? true;
        _settings.LanguageTool.EnableRedundancy = CatRedundancyCheck.IsChecked ?? true;
        _settings.LanguageTool.EnableCasing = CatCasingCheck.IsChecked ?? true;
        _settings.LanguageTool.EnableSemantics = CatSemanticsCheck.IsChecked ?? true;
        _settings.LanguageTool.EnableColloquialisms = CatColloquialismsCheck.IsChecked ?? true;
        _settings.LanguageTool.EnableCompounding = CatCompoundingCheck.IsChecked ?? true;
        _settings.LanguageTool.EnablePlainEnglish = CatPlainEnglishCheck.IsChecked ?? true;
        _settings.LanguageTool.EnableWikipedia = CatWikipediaCheck.IsChecked ?? true;
        _settings.LanguageTool.EnableMisc = CatMiscCheck.IsChecked ?? true;

        // Set preset to Custom since user modified manually
        _settings.LanguageTool.Preset = GrammarCheckPreset.Custom;
        _isLoadingCategories = true;
        GrammarPresetCombo.SelectedItem = GrammarCheckPreset.Custom;
        _isLoadingCategories = false;
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityValueText != null)
        {
            OpacityValueText.Text = ((int)e.NewValue).ToString();
        }
    }

    private void ThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ThicknessValueText != null)
        {
            ThicknessValueText.Text = e.NewValue.ToString("F1");
        }
    }

    private void DebounceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DebounceValueText != null)
        {
            DebounceValueText.Text = ((int)e.NewValue).ToString();
        }
    }

    private void PollingSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (PollingValueText != null)
        {
            PollingValueText.Text = ((int)e.NewValue).ToString();
        }
    }

    private void OverlayColorCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (CustomColorPanel == null) return;

        var selectedPreset = OverlayColorCombo.SelectedItem as OverlayColorPreset?;
        CustomColorPanel.Visibility = selectedPreset == OverlayColorPreset.Custom
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void CustomColorBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateCustomColorPreview(CustomColorBox.Text);
    }

    private void UpdateCustomColorPreview(string hexColor)
    {
        if (ColorPreviewBox == null) return;

        try
        {
            string hex = hexColor.Trim().TrimStart('#');
            if (hex.Length == 6)
            {
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                ColorPreviewBox.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
            }
        }
        catch
        {
            // Invalid color - show default red
            ColorPreviewBox.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 60, 60));
        }
    }
}
