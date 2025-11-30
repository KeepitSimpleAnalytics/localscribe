using System;
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
        _backendClient = new BackendClient(_settings);

        _hotkeyListener = new HotkeyListener(_settings.Hotkey);
        _hotkeyListener.HotkeyPressed += OnHotkeyPressed;
        
        _textObserver.TextChanged += OnTextObserved;
        
        // Initialize Debounce Timer (Wait 2 seconds of inactivity before checking)
        _debounceTimer = new DispatcherTimer();
        _debounceTimer.Interval = TimeSpan.FromSeconds(2);
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
                    "Gram Clone",
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
                "Gram Clone",
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
        _pendingTextToCheck = args.Text;
        _pendingBounds = args.ElementBounds;
        _pendingCaretBounds = args.CaretBounds;
        
        // Hide or reset bubble while typing? 
        // For now, let's just wait.
        
        _debounceTimer.Stop();
        _debounceTimer.Start(); // Restart timer
    }

    private async void OnDebounceTimerTick(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();
        string text = _pendingTextToCheck;

        if (string.IsNullOrWhiteSpace(text) || text.Length < 5) 
        {
             _bubbleWindow.Hide();
             return;
        }

        try
        {
             Logger.Log($"Checking text ({text.Length} chars)...");
             var response = await _backendClient.CheckTextAsync(text);
             int errorCount = response.Matches.Count;
             Logger.Log($"Grammar Check Result: {errorCount} errors found.");

             _bubbleWindow.UpdateState(response.Matches);
             
             // Prefer caret bounds if available, otherwise use element bounds
             Rect targetRect = _pendingCaretBounds != Rect.Empty ? _pendingCaretBounds : _pendingBounds;
             _bubbleWindow.UpdatePosition(targetRect);
             
             if (errorCount > 0)
             if (errorCount > 0)
             {
                 _bubbleWindow.Show();
             }
             else
             {
                 _bubbleWindow.Hide();
             }
        }
        catch (Exception ex)
        {
            Logger.Log($"Check failed: {ex.Message}");
            _bubbleWindow.Hide();
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
                "Gram Clone",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            ShowSettings();
            return;
        }

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
                "Gram Clone",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            ShowSettings();
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
    }
}
