using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using GramCloneClient.Models;

namespace GramCloneClient.Windows;

/// <summary>
/// Popup editor window that displays original/improved text and mode selectors.
/// </summary>
public partial class EditorWindow : Window
{
    private bool _isBusy;
    private AppSettings _settings = new();

    public event EventHandler<EditRequestEventArgs>? RunEditingRequested;
    public event EventHandler<string>? ApplyRequested;

    public EditorWindow()
    {
        InitializeComponent();
        ModeCombo.ItemsSource = Enum.GetValues(typeof(EditingMode));
        ToneCombo.ItemsSource = Enum.GetValues(typeof(ToneStyle));
        ApplySettings(_settings);
    }

    public void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        ModeCombo.SelectedItem = settings.DefaultMode;
        ToneCombo.SelectedItem = settings.DefaultTone;
        UpdateToneVisibility();
    }

    public void SetOriginalText(string text) => OriginalTextBox.Text = text;

    public void SetImprovedText(string text)
    {
        ImprovedTextBox.Text = text;
        SetBusy(false);
        StatusText.Text = text.Length > 0 ? "Completed" : string.Empty;
    }

    public void ShowWindow()
    {
        ModeCombo.SelectedItem = _settings.DefaultMode;
        ToneCombo.SelectedItem = _settings.DefaultTone;
        UpdateToneVisibility();
        Show();
        Activate();
    }

    public void HideWindow()
    {
        Hide();
        SetBusy(false);
        StatusText.Text = string.Empty;
    }

    public void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        RunButton.IsEnabled = !isBusy;
        ApplyButton.IsEnabled = !isBusy;
        StatusText.Text = isBusy ? "Running..." : StatusText.Text;
    }

    private void ModeCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateToneVisibility();
    }

    private void UpdateToneVisibility()
    {
        bool toneVisible = (ModeCombo.SelectedItem as EditingMode?) == EditingMode.Tone;
        TonePanel.Visibility = toneVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RunButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        SetBusy(true);
        var mode = (EditingMode)(ModeCombo.SelectedItem ?? EditingMode.Proofread);
        var tone = (ToneStyle)(ToneCombo.SelectedItem ?? ToneStyle.Professional);
        RunEditingRequested?.Invoke(this, new EditRequestEventArgs(OriginalTextBox.Text, mode, tone));
    }

    private void ApplyButton_OnClick(object sender, RoutedEventArgs e)
    {
        ApplyRequested?.Invoke(this, ImprovedTextBox.Text);
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        HideWindow();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        HideWindow();
    }
}
