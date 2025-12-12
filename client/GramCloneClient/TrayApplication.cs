using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using WpfApplication = System.Windows.Application;
using GramCloneClient.Backend;
using GramCloneClient.Models;
using GramCloneClient.Services;
using GramCloneClient.Windows;

using System.Windows.Threading;

namespace GramCloneClient;

/// <summary>
/// Coordinates tray icon, hotkey listener, and editor window lifecycle.
/// </summary>
public sealed class TrayApplication : IDisposable
{
    private readonly SettingsService _settingsService = new();
    private AppSettings _settings;
    private readonly HotkeyListener _hotkeyListener;
    private readonly TrayIconService _trayIconService;
    private readonly BackendClient _backendClient;
    private readonly ClipboardService _clipboardService = new();

    private readonly EditorWindow _editorWindow;
    private readonly SettingsWindow _settingsWindow;
    private readonly BubbleWindow _bubbleWindow;
    private readonly OverlayWindow _overlayWindow = new();
    private readonly DiagnosticsWindow _diagnosticsWindow = new(); // New Diagnostics Dashboard
    private readonly BackendProcessManager _processManager = new();
    private readonly TextFocusObserver _textObserver = new();
    private AboutWindow? _aboutWindow;
    
    // Debounce Timer
    private DispatcherTimer _debounceTimer;
    private string _pendingTextToCheck = string.Empty;
    private Rect _pendingBounds = Rect.Empty;
    private Rect _pendingCaretBounds = Rect.Empty;

    private IntPtr _lastFocusedHandle;

    public TrayApplication()
    {
        _settings = _settingsService.Load();

        // Validate and clamp all settings on load
        SettingsValidator.ValidateAll(_settings);

        // Apply saved theme
        ThemeManager.ApplyTheme(_settings.Theme);

        _backendClient = new BackendClient(_settings);

        _hotkeyListener = new HotkeyListener(_settings.Hotkey);
        _hotkeyListener.HotkeyPressed += OnHotkeyPressed;

        _textObserver.TextChanged += OnTextObserved;
        _textObserver.DiagnosticsUpdated += OnDiagnosticsUpdated; // Wire up diagnostics

        // Initialize Debounce Timer with configurable delay from settings
        _debounceTimer = new DispatcherTimer();
        _debounceTimer.Interval = TimeSpan.FromMilliseconds(_settings.Timing.DebounceDelayMs);
        _debounceTimer.Tick += OnDebounceTimerTick;

        _bubbleWindow = new BubbleWindow();

        _trayIconService = new TrayIconService(
            onShowSettings: ShowSettings,
            onShowDiagnostics: ShowDiagnostics,
            onShowAbout: ShowAbout,
            onExit: () => WpfApplication.Current.Shutdown()
        );

        _editorWindow = new EditorWindow();
        _editorWindow.RunEditingRequested += OnRunEditingRequestedAsync;
        _editorWindow.ApplyRequested += OnApplyRequested;
        _editorWindow.ApplySettings(_settings);

        _settingsWindow = new SettingsWindow(_settings, _backendClient);
        _settingsWindow.SettingsSaved += OnSettingsSaved;

        // Wire up overlay replacement event and apply settings
        _overlayWindow.ReplacementRequested += OnReplacementRequested;
        _overlayWindow.ApplySettings(_settings.Overlay);
    }

    public void Start()
    {
        if (_settings.AutoStartBackend)
        {
            try
            {
                _processManager.Start(_settings.BackendStartupCommand, _settings.BackendWorkingDirectory);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to auto-start backend: {ex.Message}",
                    "LocalScribe",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        try
        {
            // Initialize tray menu based on settings
            _trayIconService.Initialize(_settings.EnableDiagnostics);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to initialize tray icon:\n{ex.Message}",
                "LocalScribe",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            WpfApplication.Current.Shutdown();
            return;
        }

        TryRegisterHotkey();
        _textObserver.Start();

        // Check version compatibility after a short delay to allow backend to start
        _ = CheckVersionCompatibilityAsync();
    }

    private async Task CheckVersionCompatibilityAsync()
    {
        // Wait a bit for the backend to be ready (especially if auto-started)
        await Task.Delay(2000);

        try
        {
            var health = await _backendClient.GetHealthAsync();

            if (health.Status == "ok" && !string.IsNullOrEmpty(health.Version))
            {
                if (!Services.AppVersion.IsCompatibleWith(health.Version))
                {
                    _trayIconService.ShowBalloon(
                        $"Version mismatch: Client {Services.AppVersion.SemanticVersion} / Backend {health.Version}. " +
                        "Consider updating for best compatibility.");
                }
            }
        }
        catch
        {
            // Silently ignore - version check is informational only
        }
    }

    private async void OnHotkeyPressed(object? sender, EventArgs e)
    {
        _lastFocusedHandle = NativeMethods.GetForegroundWindow();
        string selectedText = await _clipboardService.CaptureSelectionAsync();

        if (string.IsNullOrWhiteSpace(selectedText))
        {
            _trayIconService.ShowBalloon("No text selected. Highlight text before pressing the hotkey.");
            return;
        }

        _editorWindow.SetOriginalText(selectedText);
        _editorWindow.SetImprovedText(string.Empty);
        _editorWindow.ShowWindow();
    }

    private async void OnRunEditingRequestedAsync(object? sender, EditRequestEventArgs e)
    {
        try
        {
            string result = await _backendClient.EditAsync(e.Text, e.Mode, e.Tone);
            _editorWindow.SetImprovedText(result);
        }
        catch (Exception ex)
        {
            _trayIconService.ShowBalloon($"Edit failed: {ex.Message}");
            _editorWindow.SetBusy(false);
        }
    }

    private async void OnApplyRequested(object? sender, string improvedText)
    {
        if (string.IsNullOrWhiteSpace(improvedText))
        {
            _trayIconService.ShowBalloon("Nothing to paste. Run the edit first.");
            return;
        }

        await _clipboardService.ReplaceSelectionAsync(improvedText, _lastFocusedHandle);
        _editorWindow.HideWindow();
    }

    private void OnTextObserved(object? sender, (string Text, Rect ElementBounds, Rect CaretBounds) args)
    {
        // Update diagnostics dashboard if open
        if (_diagnosticsWindow.IsVisible)
        {
            WpfApplication.Current.Dispatcher.Invoke(() => 
                _diagnosticsWindow.UpdateTextMetrics(args.Text));
        }

        // Marshal to UI thread since this callback comes from UI Automation thread
        WpfApplication.Current.Dispatcher.BeginInvoke(() =>
        {
            bool textChanged = args.Text != _pendingTextToCheck;

            _pendingTextToCheck = args.Text;
            _pendingBounds = args.ElementBounds;
            _pendingCaretBounds = args.CaretBounds;

            // Only hide overlay if text actually changed (user is typing)
            // Don't hide just because focus returned to same text
            if (textChanged)
            {
                _overlayWindow.HideOverlay();
            }

            _debounceTimer.Stop();
            _debounceTimer.Start(); // Restart timer
        });
    }

    private void OnDiagnosticsUpdated(object? sender, TextFocusObserver.FocusDiagnostics e)
    {
        if (_diagnosticsWindow.IsVisible)
        {
            WpfApplication.Current.Dispatcher.Invoke(() => 
                _diagnosticsWindow.UpdateFocusInfo(e.ProcessName, e.ControlType, e.HasTextPattern, e.Bounds));
        }
    }

    private async void OnDebounceTimerTick(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();
        string text = _pendingTextToCheck;

        // Diagnostics Health Check (Ping backend if diagnostics window is open)
        if (_diagnosticsWindow.IsVisible)
        {
            try {
                var health = await _backendClient.GetHealthAsync();
                _diagnosticsWindow.UpdateHealth(health.Status == "ok", true); // Ollama check separate later
            } catch {
                _diagnosticsWindow.UpdateHealth(false, false);
            }
        }

        if (string.IsNullOrWhiteSpace(text) || text.Length < 5)
        {
            _bubbleWindow.Hide();
            _overlayWindow.HideOverlay();
            return;
        }

        try
        {
            Logger.Log($"Checking text ({text.Length} chars)...");
            if (_diagnosticsWindow.IsVisible) _diagnosticsWindow.AppendLog($"Checking text: {text.Length} chars");
            
            // Start tasks in parallel
            var grammarTask = _backendClient.CheckTextAsync(text, _settings.LanguageTool);
            
            // Only run analysis on longer text chunks to save resources
            Task<AnalysisResponse>? analysisTask = null;
            if (text.Length > 30) // lowered threshold for testing
            {
                 analysisTask = _backendClient.AnalyzeTextAsync(text);
            }

            // Wait for grammar first (priority)
            var grammarResponse = await grammarTask;
            
            // Wait for analysis if started
            AnalysisResponse? analysisResponse = null;
            if (analysisTask != null)
            {
                try 
                {
                    // Don't block too long on analysis
                    var completedTask = await Task.WhenAny(analysisTask, Task.Delay(2000));
                    if (completedTask == analysisTask)
                    {
                        analysisResponse = await analysisTask;
                    }
                    else
                    {
                        Logger.Log("Analysis timed out (2s limit)");
                        if (_diagnosticsWindow.IsVisible) _diagnosticsWindow.AppendLog("Analysis timed out");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Analysis failed: {ex.Message}");
                    if (_diagnosticsWindow.IsVisible) _diagnosticsWindow.AppendLog($"Analysis failed: {ex.Message}");
                }
            }

            int totalErrors = grammarResponse.Matches.Count;
            int totalAnalysisIssues = analysisResponse?.Issues.Count ?? 0;
            
            Logger.Log($"Check Result: {totalErrors} grammar errors, {totalAnalysisIssues} analysis issues.");
            if (_diagnosticsWindow.IsVisible) _diagnosticsWindow.AppendLog($"Result: {totalErrors} errors, {totalAnalysisIssues} analysis issues");

            // Limit to first 20 errors to prevent overwhelming the overlay
            const int maxErrors = 20;
            var limitedMatches = grammarResponse.Matches.Take(maxErrors).ToList();
            bool hasMoreErrors = totalErrors > maxErrors;

            _bubbleWindow.UpdateState(limitedMatches, hasMoreErrors ? "{maxErrors}+" : null);

            // Prefer caret bounds if available, otherwise use element bounds
            Rect targetRect = _pendingCaretBounds != Rect.Empty ? _pendingCaretBounds : _pendingBounds;
            _bubbleWindow.UpdatePosition(targetRect);

            if (limitedMatches.Count > 0 || totalAnalysisIssues > 0)
            {
                _bubbleWindow.Show();

                // 1. Collect Grammar Regions
                var grammarRegions = new List<(Rect, GrammarMatch)>();
                foreach (var match in limitedMatches)
                {
                    var rects = _textObserver.GetErrorRects(match.Offset, match.Length);
                    foreach (var rect in rects)
                    {
                        grammarRegions.Add((rect, match));
                    }
                }

                // 2. Collect Analysis Regions
                var analysisRegions = new List<(Rect, GrammarMatch)>();
                if (analysisResponse != null)
                {
                    foreach (var issue in analysisResponse.Issues)
                    {
                        var rects = _textObserver.GetErrorRects(issue.Offset, issue.Length);
                        
                        // Map AnalysisIssue to GrammarMatch for compatibility
                        var fakeMatch = new GrammarMatch
                        {
                            Message = $"{issue.IssueType.ToUpper()}: {issue.Suggestion}",
                            Offset = issue.Offset,
                            Length = issue.Length,
                            Replacements = new List<string> { issue.Suggestion },
                            RuleId = $"SEMANTIC_{issue.IssueType.ToUpper()}", // Used for color mapping
                            Category = "CLARITY",
                            Context = issue.QuotedText
                        };
                        
                        foreach (var rect in rects)
                        {
                            analysisRegions.Add((rect, fakeMatch));
                        }
                    }
                }

                if (grammarRegions.Count > 0 || analysisRegions.Count > 0)
                {
                    // Must show overlay first before drawing (PointFromScreen needs PresentationSource)
                    _overlayWindow.ShowOverlay();
                    _overlayWindow.SetErrorRegions(grammarRegions, analysisRegions);
                }
                else
                {
                    if (_diagnosticsWindow.IsVisible) _diagnosticsWindow.AppendLog("WARN: Errors found but 0 rects (TextPattern issue?)");
                    _overlayWindow.HideOverlay();
                }
            }
            else
            {
                _bubbleWindow.Hide();
                _overlayWindow.HideOverlay();
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Check/Analysis loop failed: {ex.Message}");
            if (_diagnosticsWindow.IsVisible) _diagnosticsWindow.AppendLog($"Error: {ex.Message}");
            _bubbleWindow.Hide();
            _overlayWindow.HideOverlay();
        }
    }

    private void ShowSettings()
    {
        _settingsWindow.Show();
        _settingsWindow.Activate();
        _ = _settingsWindow.RefreshBackendDataAsync();
    }

    private void ShowDiagnostics()
    {
        _diagnosticsWindow.Show();
        _diagnosticsWindow.Activate();
    }

    private void ShowAbout()
    {
        if (_aboutWindow == null || !_aboutWindow.IsLoaded)
        {
            _aboutWindow = new AboutWindow(_backendClient);
        }
        _aboutWindow.Show();
        _aboutWindow.Activate();
    }

    private void OnSettingsSaved(object? sender, AppSettings e)
    {
        _settings = e;

        // Validate and clamp all settings before saving
        SettingsValidator.ValidateAll(_settings);

        _settingsService.Save(_settings);
        _backendClient.UpdateSettings(_settings);
        try
        {
            _hotkeyListener.UpdateHotkey(_settings.Hotkey);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to register hotkey '{e.Hotkey}': {ex.Message}",
                "LocalScribe",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            ShowSettings();
            return;
        }

        // Apply timing settings - update debounce timer interval
        _debounceTimer.Interval = TimeSpan.FromMilliseconds(_settings.Timing.DebounceDelayMs);

        // Apply overlay display settings
        _overlayWindow.ApplySettings(_settings.Overlay);

        // Apply text observer polling interval
        _textObserver.SetPollingInterval(_settings.Timing.TextPollingIntervalMs);
        
        // Update tray menu visibility
        _trayIconService.UpdateMenu(_settings.EnableDiagnostics);

        _editorWindow.ApplySettings(_settings);
        _settingsWindow.UpdateSettings(_settings);
        _trayIconService.ShowBalloon("Settings saved.");
    }

    private void TryRegisterHotkey()
    {
        try
        {
            _hotkeyListener.Register();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to register hotkey '{_settings.Hotkey}'.\nChoose another combination in Settings.\n\nDetails: {ex.Message}",
                "LocalScribe",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            ShowSettings();
        }
    }

    private void OnReplacementRequested(object? sender, (GrammarMatch Match, string Replacement) args)
    {
        try
        {
            Logger.Log($"Replacement requested: '{args.Match.Context}' -> '{args.Replacement}'");
            if (_diagnosticsWindow.IsVisible) _diagnosticsWindow.AppendLog($"Replacing: {args.Match.Context} -> {args.Replacement}");
            
            _textObserver.ReplaceText(args.Match.Offset, args.Match.Length, args.Replacement);
            Logger.Log("Replacement successful");

            // Hide overlay after replacement (will re-check on next idle)
            _overlayWindow.HideOverlay();
        }
        catch (Exception ex)
        {
            Logger.Log($"Replacement failed: {ex.Message}");
            if (_diagnosticsWindow.IsVisible) _diagnosticsWindow.AppendLog($"Replace failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _hotkeyListener.Dispose();
        _trayIconService.Dispose();
        _backendClient.Dispose();
        _processManager.Dispose();
        _textObserver.Dispose();
        _bubbleWindow.Close();
        _overlayWindow.Close();
        _diagnosticsWindow.Close(); // Close dashboard
        _aboutWindow?.Close();
    }
}