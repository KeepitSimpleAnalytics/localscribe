using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using GramCloneClient.Backend;
using GramCloneClient.Models;

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

    public event EventHandler<AppSettings>? SettingsSaved;

    public SettingsWindow(AppSettings settings, BackendClient backendClient)
    {
        InitializeComponent();
        _settings = settings;
        _backendClient = backendClient;
        ModeCombo.ItemsSource = Enum.GetValues(typeof(EditingMode));
        ToneCombo.ItemsSource = Enum.GetValues(typeof(ToneStyle));
        LoadValues();
    }

    private void LoadValues()
    {
        BackendUrlBox.Text = _settings.BackendUrl;
        HotkeyBox.Text = _settings.Hotkey;
        ModeCombo.SelectedItem = _settings.DefaultMode;
        ToneCombo.SelectedItem = _settings.DefaultTone;
        
        AutoStartCheckBox.IsChecked = _settings.AutoStartBackend;
        StartupCommandBox.Text = _settings.BackendStartupCommand;
        WorkDirBox.Text = _settings.BackendWorkingDirectory;
        AutoStartConfigPanel.Visibility = _settings.AutoStartBackend ? Visibility.Visible : Visibility.Collapsed;
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
        string backendUrl = BackendUrlBox.Text.Trim();
        _settings.BackendUrl = backendUrl;
        _settings.Hotkey = string.IsNullOrWhiteSpace(HotkeyBox.Text) ? _settings.Hotkey : HotkeyBox.Text.Trim();
        _settings.DefaultMode = (EditingMode)(ModeCombo.SelectedItem ?? EditingMode.Proofread);
        _settings.DefaultTone = (ToneStyle)(ToneCombo.SelectedItem ?? ToneStyle.Professional);

        _settings.AutoStartBackend = AutoStartCheckBox.IsChecked ?? false;
        _settings.BackendStartupCommand = StartupCommandBox.Text.Trim();
        _settings.BackendWorkingDirectory = WorkDirBox.Text.Trim();

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

    private void AutoStartCheckBox_Click(object sender, RoutedEventArgs e)
    {
        AutoStartConfigPanel.Visibility = (AutoStartCheckBox.IsChecked == true) 
            ? Visibility.Visible 
            : Visibility.Collapsed;
    }
}
