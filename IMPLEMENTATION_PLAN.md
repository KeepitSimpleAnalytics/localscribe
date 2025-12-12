# LocalScribe Overlay Implementation Plan

## Executive Summary

This plan details the implementation of a transparent visual overlay for real-time error highlighting in the LocalScribe grammar checker.

**Backend Status:** ✅ Complete - Uses `language-tool-python` with Java Runtime.

---

## Phase 1: Backend ✅ COMPLETE

The backend is fully implemented using `language-tool-python`:

- **`app/services/grammar_check.py`** - GrammarCheckService using LanguageTool
- **`app/schemas.py`** - CheckRequest, CheckResponse, GrammarError models
- **`app/main.py`** - `/v1/text/check` endpoint

**Requirements:**
- Java Runtime Environment (JRE 8+) must be installed
- `JAVA_HOME` environment variable configured

**To verify backend is working:**
```bash
curl -X POST http://localhost:8000/v1/text/check -H "Content-Type: application/json" -d '{"text": "I has a apple"}'
```

---

## Phase 2: Client Overlay UI Implementation

### 2.1 Current State Analysis

**Existing Components:**
- `BubbleWindow.xaml/.cs`: Displays error count in a floating bubble
- `TextFocusObserver.cs`: Monitors text changes via UI Automation
- `TrayApplication.cs`: Orchestrates all components

**Current Flow:**
1. `TextFocusObserver` detects text changes (150ms polling)
2. Debounce timer waits 500ms of inactivity
3. `BackendClient.CheckTextAsync()` called
4. `BubbleWindow` shows error count

### 2.2 Target Architecture

**New Overlay System:**
- Transparent, click-through window covering the screen
- Draws red underlines at exact error positions
- Instantly hides when user types or moves mouse
- Shows only during idle periods with detected errors

### 2.3 Implementation Steps

#### Step 2.3.1: Create OverlayWindow.xaml

**File:** `client/GramCloneClient/Windows/OverlayWindow.xaml`

```xml
<Window x:Class="GramCloneClient.Windows.OverlayWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="LocalScribe Overlay"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        Topmost="True"
        ShowInTaskbar="False"
        IsHitTestVisible="False"
        WindowState="Maximized"
        ResizeMode="NoResize">

    <!--
    Key Properties Explained:
    - WindowStyle="None": No title bar or borders
    - AllowsTransparency="True": Enable transparent background
    - Background="Transparent": Fully see-through
    - Topmost="True": Always above other windows
    - ShowInTaskbar="False": Hidden from taskbar/Alt+Tab
    - IsHitTestVisible="False": CRITICAL - clicks pass through to underlying apps
    - WindowState="Maximized": Cover entire screen
    - ResizeMode="NoResize": Cannot be resized
    -->

    <Canvas x:Name="ErrorCanvas">
        <!-- Error underlines will be drawn programmatically here -->
    </Canvas>

</Window>
```

#### Step 2.3.2: Create OverlayWindow.xaml.cs

**File:** `client/GramCloneClient/Windows/OverlayWindow.xaml.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Interop;
using GramCloneClient.Interop;

namespace GramCloneClient.Windows
{
    /// <summary>
    /// Transparent overlay window for drawing error underlines.
    /// This window is click-through and vanishes on user input.
    /// </summary>
    public partial class OverlayWindow : Window
    {
        // Underline styling
        private static readonly Brush ErrorBrush = new SolidColorBrush(Color.FromArgb(200, 255, 60, 60));
        private const double UnderlineHeight = 2.5;
        private const double UnderlineOffset = 2.0; // Pixels below the text baseline

        // Track if overlay is currently showing errors
        private bool _isShowingErrors = false;

        public OverlayWindow()
        {
            InitializeComponent();

            // Ensure window is ready for drawing
            this.Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Make window truly click-through using extended window styles
            MakeClickThrough();
        }

        /// <summary>
        /// Apply WS_EX_TRANSPARENT extended style to make window click-through.
        /// This is more reliable than IsHitTestVisible alone.
        /// </summary>
        private void MakeClickThrough()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            // Get current extended style
            int extendedStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);

            // Add WS_EX_TRANSPARENT flag (0x00000020)
            // This makes the window truly invisible to mouse input
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE,
                extendedStyle | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_LAYERED);
        }

        /// <summary>
        /// Draw underlines at the specified screen rectangles.
        /// Each rect represents the bounding box of an error in screen coordinates.
        /// </summary>
        /// <param name="errorRects">List of rectangles in screen coordinates</param>
        public void DrawMatches(List<Rect> errorRects)
        {
            // Clear previous drawings
            ErrorCanvas.Children.Clear();

            if (errorRects == null || errorRects.Count == 0)
            {
                _isShowingErrors = false;
                return;
            }

            foreach (var rect in errorRects)
            {
                // Convert screen coordinates to window coordinates
                // Since window is maximized, screen coords should work directly
                // but we subtract window position just in case
                Point topLeft = PointFromScreen(new Point(rect.Left, rect.Top));
                Point bottomRight = PointFromScreen(new Point(rect.Right, rect.Bottom));

                // Create underline rectangle positioned at bottom of text
                var underline = new Rectangle
                {
                    Width = Math.Max(bottomRight.X - topLeft.X, 10), // Minimum width
                    Height = UnderlineHeight,
                    Fill = ErrorBrush,
                    RadiusX = 1,
                    RadiusY = 1
                };

                // Position at bottom of the text bounding box
                Canvas.SetLeft(underline, topLeft.X);
                Canvas.SetTop(underline, bottomRight.Y + UnderlineOffset);

                ErrorCanvas.Children.Add(underline);
            }

            _isShowingErrors = true;
        }

        /// <summary>
        /// Draw wavy underlines (more prominent visual style).
        /// Alternative to solid underlines.
        /// </summary>
        /// <param name="errorRects">List of rectangles in screen coordinates</param>
        public void DrawWavyMatches(List<Rect> errorRects)
        {
            ErrorCanvas.Children.Clear();

            if (errorRects == null || errorRects.Count == 0)
            {
                _isShowingErrors = false;
                return;
            }

            foreach (var rect in errorRects)
            {
                Point topLeft = PointFromScreen(new Point(rect.Left, rect.Top));
                Point bottomRight = PointFromScreen(new Point(rect.Right, rect.Bottom));

                double width = Math.Max(bottomRight.X - topLeft.X, 10);
                double y = bottomRight.Y + UnderlineOffset;

                // Create wavy line using polyline
                var wavyLine = CreateWavyLine(topLeft.X, y, width);
                ErrorCanvas.Children.Add(wavyLine);
            }

            _isShowingErrors = true;
        }

        /// <summary>
        /// Create a wavy polyline for a squiggly underline effect.
        /// </summary>
        private Polyline CreateWavyLine(double startX, double y, double width)
        {
            var points = new PointCollection();
            double waveHeight = 2.0;
            double waveLength = 4.0;

            for (double x = 0; x <= width; x += waveLength / 2)
            {
                double waveY = (x / (waveLength / 2)) % 2 == 0 ? y : y + waveHeight;
                points.Add(new Point(startX + x, waveY));
            }

            return new Polyline
            {
                Points = points,
                Stroke = ErrorBrush,
                StrokeThickness = 1.5,
                StrokeLineJoin = PenLineJoin.Round
            };
        }

        /// <summary>
        /// Clear all error drawings from the canvas.
        /// </summary>
        public void Clear()
        {
            ErrorCanvas.Children.Clear();
            _isShowingErrors = false;
        }

        /// <summary>
        /// Check if overlay is currently showing any errors.
        /// </summary>
        public bool IsShowingErrors => _isShowingErrors;

        /// <summary>
        /// Show the overlay window.
        /// </summary>
        public void ShowOverlay()
        {
            if (!IsVisible)
            {
                Show();
            }
        }

        /// <summary>
        /// Hide the overlay window immediately.
        /// Called when user input is detected.
        /// </summary>
        public void HideOverlay()
        {
            if (IsVisible)
            {
                Hide();
            }
            Clear();
        }
    }
}
```

#### Step 2.3.3: Update NativeMethods.cs for Click-Through

**File:** `client/GramCloneClient/Interop/NativeMethods.cs`

Add these constants and methods:

```csharp
// Window Extended Styles
public const int GWL_EXSTYLE = -20;
public const int WS_EX_TRANSPARENT = 0x00000020;
public const int WS_EX_LAYERED = 0x00080000;

[DllImport("user32.dll")]
public static extern int GetWindowLong(IntPtr hwnd, int index);

[DllImport("user32.dll")]
public static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
```

#### Step 2.3.4: Update TextFocusObserver.cs

**File:** `client/GramCloneClient/Services/TextFocusObserver.cs`

Add method to get error bounding rectangles:

```csharp
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Text;

/// <summary>
/// Get screen rectangles for error positions using UI Automation TextPattern.
/// </summary>
/// <param name="offset">Character offset of the error start</param>
/// <param name="length">Length of the error text</param>
/// <returns>List of bounding rectangles in screen coordinates</returns>
public List<Rect> GetErrorRects(int offset, int length)
{
    var rects = new List<Rect>();

    try
    {
        if (_focusedElement == null)
        {
            return rects;
        }

        // Try to get TextPattern from the focused element
        object patternObj;
        if (_focusedElement.TryGetCurrentPattern(TextPattern.Pattern, out patternObj))
        {
            var textPattern = (TextPattern)patternObj;

            // Get the document range
            var documentRange = textPattern.DocumentRange;

            // Clone the range and move to the error position
            var errorRange = documentRange.Clone();

            // Move to start of document
            errorRange.MoveEndpointByRange(
                TextPatternRangeEndpoint.Start,
                documentRange,
                TextPatternRangeEndpoint.Start);
            errorRange.MoveEndpointByRange(
                TextPatternRangeEndpoint.End,
                documentRange,
                TextPatternRangeEndpoint.Start);

            // Move start point to error offset
            int moved = errorRange.MoveEndpointByUnit(
                TextPatternRangeEndpoint.Start,
                TextUnit.Character,
                offset);

            // Move end point to cover error length
            errorRange.MoveEndpointByUnit(
                TextPatternRangeEndpoint.End,
                TextUnit.Character,
                length);

            // Get bounding rectangles for the error range
            var boundingRects = errorRange.GetBoundingRectangles();

            foreach (var rectArray in boundingRects)
            {
                // Each rectArray is [x, y, width, height]
                if (rectArray.Length >= 4)
                {
                    var rect = new Rect(
                        rectArray[0],  // X
                        rectArray[1],  // Y
                        rectArray[2],  // Width
                        rectArray[3]   // Height
                    );

                    // Filter out invalid rectangles
                    if (rect.Width > 0 && rect.Height > 0 &&
                        rect.X >= 0 && rect.Y >= 0)
                    {
                        rects.Add(rect);
                    }
                }
            }
        }
        else
        {
            // Fallback: If no TextPattern, use element bounds as approximation
            // This won't be as accurate but provides some visual feedback
            Logger.Log("TextPattern not available, using element bounds fallback");

            var elementRect = _focusedElement.Current.BoundingRectangle;
            if (!elementRect.IsEmpty)
            {
                // Can't determine exact error position without TextPattern
                // Return empty to avoid incorrect highlighting
            }
        }
    }
    catch (Exception ex)
    {
        Logger.Log($"Error getting error rectangles: {ex.Message}");
    }

    return rects;
}

/// <summary>
/// Get all error rectangles for a list of grammar matches.
/// </summary>
/// <param name="matches">List of grammar matches with offset and length</param>
/// <returns>List of all bounding rectangles</returns>
public List<Rect> GetAllErrorRects(IEnumerable<GrammarMatch> matches)
{
    var allRects = new List<Rect>();

    foreach (var match in matches)
    {
        var rects = GetErrorRects(match.Offset, match.Length);
        allRects.AddRange(rects);
    }

    return allRects;
}
```

#### Step 2.3.5: Update TrayApplication.cs

**File:** `client/GramCloneClient/TrayApplication.cs`

Add overlay management:

```csharp
// Add field for overlay window
private OverlayWindow _overlayWindow;

// In constructor or initialization method:
_overlayWindow = new OverlayWindow();

// Update the TextChanged event handler to hide overlay immediately
private void OnTextObserved(object? sender, TextChangedEventArgs e)
{
    // IMMEDIATELY hide overlay when user types
    _overlayWindow?.HideOverlay();

    // Reset debounce timer
    _debounceTimer.Stop();

    // Store the observed text
    _lastObservedText = e.Text;
    _lastObservedBounds = e.Bounds;
    _lastCaretBounds = e.CaretBounds;

    // Start debounce timer
    _debounceTimer.Start();
}

// Add mouse movement detection to hide overlay
private void SetupMouseHook()
{
    // Option 1: Use a low-level mouse hook (more responsive but complex)
    // Option 2: Use a timer to poll mouse position (simpler but less responsive)

    // Timer-based approach (simpler):
    var mouseCheckTimer = new DispatcherTimer
    {
        Interval = TimeSpan.FromMilliseconds(50)
    };

    Point _lastMousePosition = default;

    mouseCheckTimer.Tick += (s, e) =>
    {
        var currentPos = NativeMethods.GetCursorPosition();

        if (_lastMousePosition != default &&
            (Math.Abs(currentPos.X - _lastMousePosition.X) > 5 ||
             Math.Abs(currentPos.Y - _lastMousePosition.Y) > 5))
        {
            // Mouse moved significantly - hide overlay
            _overlayWindow?.HideOverlay();
        }

        _lastMousePosition = currentPos;
    };

    mouseCheckTimer.Start();
}

// Update debounce timer tick to show overlay when idle
private async void OnDebounceTimerTick(object? sender, EventArgs e)
{
    _debounceTimer.Stop();

    if (string.IsNullOrWhiteSpace(_lastObservedText) || _lastObservedText.Length < 5)
    {
        _bubbleWindow?.Hide();
        _overlayWindow?.HideOverlay();
        return;
    }

    try
    {
        // Check grammar
        var response = await _backendClient.CheckTextAsync(_lastObservedText);

        if (response?.Matches != null && response.Matches.Count > 0)
        {
            // Update bubble window with count
            UpdateBubbleWindow(response.Matches.Count, _lastCaretBounds ?? _lastObservedBounds);

            // Get error rectangles from UI Automation
            var errorRects = _textObserver.GetAllErrorRects(response.Matches);

            if (errorRects.Count > 0)
            {
                // Draw error underlines on overlay
                _overlayWindow.DrawMatches(errorRects);
                _overlayWindow.ShowOverlay();
            }
            else
            {
                // Couldn't get rectangles, just show bubble
                _overlayWindow.HideOverlay();
            }
        }
        else
        {
            // No errors - hide everything
            _bubbleWindow?.ShowNoErrors(_lastCaretBounds ?? _lastObservedBounds);
            _overlayWindow?.HideOverlay();
        }
    }
    catch (Exception ex)
    {
        Logger.Log($"Error during grammar check: {ex.Message}");
        _overlayWindow?.HideOverlay();
    }
}

// Clean up on shutdown
private void Shutdown()
{
    _overlayWindow?.Close();
    // ... other cleanup
}
```

#### Step 2.3.6: Add Mouse Position Helper to NativeMethods

**File:** `client/GramCloneClient/Interop/NativeMethods.cs`

```csharp
[DllImport("user32.dll")]
[return: MarshalAs(UnmanagedType.Bool)]
private static extern bool GetCursorPos(out POINT lpPoint);

[StructLayout(LayoutKind.Sequential)]
public struct POINT
{
    public int X;
    public int Y;
}

public static System.Windows.Point GetCursorPosition()
{
    GetCursorPos(out POINT point);
    return new System.Windows.Point(point.X, point.Y);
}
```

### 2.4 Alternative: Low-Level Input Hooks (More Responsive)

For maximum responsiveness, implement low-level keyboard and mouse hooks:

**File:** `client/GramCloneClient/Services/InputHookService.cs`

```csharp
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace GramCloneClient.Services
{
    /// <summary>
    /// Low-level input hook for detecting any keyboard or mouse activity.
    /// Used to instantly hide the overlay when user interacts.
    /// </summary>
    public class InputHookService : IDisposable
    {
        // Hook types
        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;

        // Hook delegates
        private delegate IntPtr LowLevelHookProc(int nCode, IntPtr wParam, IntPtr lParam);

        private readonly LowLevelHookProc _keyboardProc;
        private readonly LowLevelHookProc _mouseProc;
        private IntPtr _keyboardHookId = IntPtr.Zero;
        private IntPtr _mouseHookId = IntPtr.Zero;

        // Event fired when any input is detected
        public event EventHandler? InputDetected;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelHookProc lpfn,
            IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        public InputHookService()
        {
            _keyboardProc = KeyboardHookCallback;
            _mouseProc = MouseHookCallback;
        }

        public void Start()
        {
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            var moduleHandle = GetModuleHandle(curModule?.ModuleName);

            _keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);
            _mouseHookId = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, moduleHandle, 0);
        }

        public void Stop()
        {
            if (_keyboardHookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_keyboardHookId);
                _keyboardHookId = IntPtr.Zero;
            }
            if (_mouseHookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHookId);
                _mouseHookId = IntPtr.Zero;
            }
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                InputDetected?.Invoke(this, EventArgs.Empty);
            }
            return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                // Only trigger on actual movement or clicks, not just position queries
                const int WM_MOUSEMOVE = 0x0200;
                const int WM_LBUTTONDOWN = 0x0201;
                const int WM_RBUTTONDOWN = 0x0204;

                int msg = (int)wParam;
                if (msg == WM_MOUSEMOVE || msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN)
                {
                    InputDetected?.Invoke(this, EventArgs.Empty);
                }
            }
            return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
```

### 2.5 Testing the Overlay

**Manual Test Cases:**

1. **Visibility Test:**
   - Open Notepad, type text with errors
   - Wait for idle (500ms) - overlay should appear with red underlines
   - Verify underlines are positioned correctly under error words

2. **Click-Through Test:**
   - With overlay visible, click on the underlying text
   - Click should pass through and focus should change
   - Overlay should not intercept any mouse events

3. **Jitter-Hide Test:**
   - With overlay visible, press any key
   - Overlay should INSTANTLY disappear (no delay)
   - Move mouse - overlay should disappear

4. **Multi-Monitor Test:**
   - Test with multiple monitors
   - Overlay should cover only primary monitor (or all monitors if needed)

5. **Performance Test:**
   - Type rapidly for 30 seconds
   - App should remain responsive
   - No memory leaks (check Task Manager)

---

## Phase 3: Integration & Polish

### 3.1 Configuration Options

Add to `AppSettings.cs`:

```csharp
/// <summary>
/// Enable/disable the visual error overlay.
/// </summary>
public bool EnableOverlay { get; set; } = true;

/// <summary>
/// Overlay underline style: "solid" or "wavy".
/// </summary>
public string OverlayStyle { get; set; } = "solid";

/// <summary>
/// Overlay underline color (ARGB hex).
/// </summary>
public string OverlayColor { get; set; } = "#C8FF3C3C";
```

### 3.2 Settings UI Integration

Add toggle to `SettingsWindow.xaml`:

```xml
<CheckBox x:Name="EnableOverlayCheckbox"
          Content="Show error underlines in focused text"
          IsChecked="{Binding EnableOverlay}" />

<ComboBox x:Name="OverlayStyleCombo"
          SelectedValue="{Binding OverlayStyle}">
    <ComboBoxItem Content="Solid Line" Tag="solid"/>
    <ComboBoxItem Content="Wavy Line" Tag="wavy"/>
</ComboBox>
```

### 3.3 Error Handling & Edge Cases

1. **No TextPattern Support:**
   - Some applications don't support UI Automation TextPattern
   - Fallback: Show only bubble, hide overlay
   - Log which apps don't support it for debugging

2. **Overlay on Wrong Monitor:**
   - Detect which monitor has the focused element
   - Position overlay window on that specific monitor

3. **High DPI Displays:**
   - Ensure coordinate transformation accounts for DPI scaling
   - Test on 4K displays at 150%, 200% scaling

4. **Performance with Long Documents:**
   - Limit error checking to visible portion only
   - Consider viewport-based rect queries

---

## Implementation Order & Dependencies

```
┌─────────────────────────────────────────────────────────────────┐
│                 PHASE 1: BACKEND ✅ COMPLETE                     │
├─────────────────────────────────────────────────────────────────┤
│  ✅ language-tool-python integration                             │
│  ✅ /v1/text/check endpoint                                      │
│  ✅ GrammarError schema                                          │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                 PHASE 2: CLIENT ✅ COMPLETE                      │
├─────────────────────────────────────────────────────────────────┤
│  ✅ NativeMethods.cs (click-through support)                     │
│  ✅ OverlayWindow.xaml/.cs                                       │
│  ✅ TextFocusObserver.cs (GetErrorRects)                         │
│  ✅ TrayApplication.cs (overlay integration)                     │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                 PHASE 3: POLISH ✅ COMPLETE                      │
├─────────────────────────────────────────────────────────────────┤
│  ✅ AppSettings.cs (overlay configuration)                       │
│  ✅ SettingsWindow.xaml (overlay toggles)                        │
│  ✅ Multi-monitor & DPI handling                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| Java not installed/configured | High | Document JRE requirements, provide setup guide |
| TextPattern not supported in target app | Medium | Graceful fallback to bubble-only mode |
| Overlay causes performance issues | Medium | Implement throttling, limit to visible viewport |
| Click-through fails on some Windows versions | Medium | Test on Win10/Win11, document requirements |
| High DPI coordinate issues | Low | Test at various DPI settings, use proper transforms |

---

## Success Criteria

### Phase 1 Complete When:
- [x] `language-tool-python` integrated
- [x] `/v1/text/check` endpoint returns correct suggestions
- [x] GrammarError schema defined with all required fields
- [x] Graceful degradation when Java unavailable

### Phase 2 Complete When:
- [x] Overlay window appears with red underlines on errors
- [x] Overlay is truly click-through (WS_EX_TRANSPARENT applied)
- [x] Overlay hides on keyboard input
- [x] Underlines positioned accurately under error text
- [x] Works on both single and multi-monitor setups
- [x] Multiple underline styles (Solid, Wavy, Dotted, Dashed)

### Phase 3 Complete When:
- [x] Toggle in Settings to enable/disable overlay
- [x] Style selection (solid, wavy, dotted, dashed)
- [x] Color preset selection with custom color support
- [x] Opacity and thickness controls
- [x] Graceful fallback when TextPattern unavailable
- [x] High DPI displays handled
