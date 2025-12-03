using System;
using System.Collections.Generic;
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
    private readonly BackendProcessManager _processManager = new();
    private readonly TextFocusObserver _textObserver = new();
    
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

        _backendClient = new BackendClient(_settings);

        _hotkeyListener = new HotkeyListener(_settings.Hotkey);
        _hotkeyListener.HotkeyPressed += OnHotkeyPressed;

        _textObserver.TextChanged += OnTextObserved;

        // Initialize Debounce Timer with configurable delay from settings
        _debounceTimer = new DispatcherTimer();
        _debounceTimer.Interval = TimeSpan.FromMilliseconds(_settings.Timing.DebounceDelayMs);
        _debounceTimer.Tick += OnDebounceTimerTick;

        _bubbleWindow = new BubbleWindow();

        _trayIconService = new TrayIconService(
            onShowSettings: ShowSettings,
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
            _trayIconService.Initialize();
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

    private async void OnDebounceTimerTick(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();
        string text = _pendingTextToCheck;

        if (string.IsNullOrWhiteSpace(text) || text.Length < 5)
        {
            _bubbleWindow.Hide();
            _overlayWindow.HideOverlay();
            return;
        }

        try
        {
            Logger.Log($"Checking text ({text.Length} chars)...");
            var response = await _backendClient.CheckTextAsync(text);
            int totalErrors = response.Matches.Count;
            Logger.Log($"Grammar Check Result: {totalErrors} errors found.");

            // Limit to first 20 errors to prevent overwhelming the overlay on complex pages
            const int maxErrors = 20;
            var limitedMatches = response.Matches.Take(maxErrors).ToList();
            bool hasMoreErrors = totalErrors > maxErrors;

            _bubbleWindow.UpdateState(limitedMatches, hasMoreErrors ? $"{maxErrors}+" : null);

            // Prefer caret bounds if available, otherwise use element bounds
            Rect targetRect = _pendingCaretBounds != Rect.Empty ? _pendingCaretBounds : _pendingBounds;
            _bubbleWindow.UpdatePosition(targetRect);

            if (limitedMatches.Count > 0)
            {
                _bubbleWindow.Show();

                // Collect error regions with match data for hover detection
                var errorRegions = new List<(Rect, GrammarMatch)>();
                foreach (var match in limitedMatches)
                {
                    Logger.Log($"Getting rects for error at offset={match.Offset}, length={match.Length}");
                    var rects = _textObserver.GetErrorRects(match.Offset, match.Length);
                    Logger.Log($"Got {rects.Count} rectangles for this error");
                    foreach (var rect in rects)
                    {
                        Logger.Log($"  Rect: X={rect.X}, Y={rect.Y}, W={rect.Width}, H={rect.Height}");
                        errorRegions.Add((rect, match));
                    }
                }

                Logger.Log($"Total error regions collected: {errorRegions.Count}");
                if (errorRegions.Count > 0)
                {
                    // Must show overlay first before drawing (PointFromScreen needs PresentationSource)
                    _overlayWindow.ShowOverlay();
                    _overlayWindow.SetErrorRegions(errorRegions);
                    Logger.Log("Overlay shown with hover detection");
                }
                else
                {
                    Logger.Log("No rectangles to draw - overlay not shown");
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
            Logger.Log($"Check failed: {ex.Message}");
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
            _textObserver.ReplaceText(args.Match.Offset, args.Match.Length, args.Replacement);
            Logger.Log("Replacement successful");

            // Hide overlay after replacement (will re-check on next idle)
            _overlayWindow.HideOverlay();
        }
        catch (Exception ex)
        {
            Logger.Log($"Replacement failed: {ex.Message}");
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
    }
}
