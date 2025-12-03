# LocalScribe Optimization & Overlay Implementation Plan

## Executive Summary

This plan details the migration from Java-based `language-tool-python` to the native Rust library `nlprule`, and implementation of a transparent visual overlay for real-time error highlighting.

---

## Phase 1: Backend Optimization (Switch to nlprule)

### 1.1 Current State Analysis

**Current Implementation:** `app/services/grammar_check.py`
- Uses `language_tool_python.LanguageTool("en-US")`
- Requires Java Runtime Environment (JRE 8+)
- Returns `CheckResponse` with `GrammarError` objects containing:
  - `message`, `offset`, `length`, `replacements`, `rule_id`, `category`, `context`, `offset_in_context`

**Pain Points:**
- Java dependency adds ~200MB+ to deployment
- Cold start time is slow (JVM initialization)
- Cross-platform Java configuration issues

### 1.2 Target Architecture

**nlprule Benefits:**
- Pure Rust library with Python bindings
- ~10x faster than LanguageTool
- No external runtime dependencies
- Binary model files (~15MB for English)

### 1.3 Implementation Steps

#### Step 1.3.1: Update Dependencies

**File:** `requirements.txt`

```diff
  fastapi==0.110.0
  uvicorn[standard]==0.27.1
  httpx==0.27.0
  pydantic==2.6.3
  pydantic-settings==2.1.0
  python-dotenv==1.0.1
  loguru==0.7.2
  pytest==8.0.2
- language-tool-python==2.7.1
+ nlprule==0.6.4
```

**Note:** nlprule 0.6.4 is the latest stable version with good Python bindings.

#### Step 1.3.2: Refactor GrammarCheckService

**File:** `app/services/grammar_check.py`

**Complete Rewrite:**

```python
"""
Grammar checking service using nlprule (Rust-based).
Provides fast, native grammar and spelling checking without Java dependency.
"""

import os
from pathlib import Path
from typing import Optional
from loguru import logger

# nlprule imports
from nlprule import Tokenizer, Rules

from app.schemas import CheckResponse, GrammarError


class GrammarCheckService:
    """
    Grammar checking service powered by nlprule.

    Model files are automatically downloaded on first use and cached
    in the user's cache directory or a custom location.
    """

    _instance: Optional["GrammarCheckService"] = None

    # Default cache location for model files
    DEFAULT_CACHE_DIR = Path.home() / ".cache" / "nlprule"

    def __init__(self):
        self._tokenizer: Optional[Tokenizer] = None
        self._rules: Optional[Rules] = None
        self._initialized = False
        self._init_error: Optional[str] = None

    @classmethod
    def get_instance(cls) -> "GrammarCheckService":
        """Singleton pattern for service instance."""
        if cls._instance is None:
            cls._instance = cls()
        return cls._instance

    def _get_cache_dir(self) -> Path:
        """
        Get the cache directory for model files.
        Respects NLPRULE_CACHE_DIR environment variable if set.
        """
        cache_dir = os.environ.get("NLPRULE_CACHE_DIR")
        if cache_dir:
            return Path(cache_dir)
        return self.DEFAULT_CACHE_DIR

    def _ensure_models_exist(self) -> tuple[Path, Path]:
        """
        Ensure model files exist, downloading if necessary.
        Returns paths to (tokenizer.bin, rules.bin).

        nlprule automatically downloads models on first Tokenizer/Rules creation,
        but we can also manually specify paths if pre-downloaded.
        """
        cache_dir = self._get_cache_dir()
        cache_dir.mkdir(parents=True, exist_ok=True)

        # Model file names for English
        tokenizer_path = cache_dir / "en_tokenizer.bin"
        rules_path = cache_dir / "en_rules.bin"

        return tokenizer_path, rules_path

    def initialize(self) -> bool:
        """
        Initialize the nlprule tokenizer and rules.
        Returns True if successful, False otherwise.

        This should be called at application startup.
        Models are downloaded automatically if not cached.
        """
        if self._initialized:
            return True

        try:
            logger.info("Initializing nlprule grammar checker...")

            # nlprule downloads models automatically on first use
            # The language code "en" triggers download of English models
            self._tokenizer = Tokenizer.load("en")
            self._rules = Rules.load("en", self._tokenizer)

            self._initialized = True
            logger.info("nlprule grammar checker initialized successfully")
            logger.info(f"Loaded {len(self._rules)} grammar rules")

            return True

        except Exception as e:
            self._init_error = str(e)
            logger.error(f"Failed to initialize nlprule: {e}")
            logger.warning("Grammar checking will be unavailable")
            return False

    def is_available(self) -> bool:
        """Check if the grammar checker is available and initialized."""
        return self._initialized and self._tokenizer is not None and self._rules is not None

    def check(self, text: str) -> CheckResponse:
        """
        Check text for grammar and spelling errors.

        Args:
            text: The text to check

        Returns:
            CheckResponse with list of GrammarError matches
        """
        if not self.is_available():
            logger.warning("Grammar checker not available, returning empty response")
            return CheckResponse(matches=[])

        try:
            # Tokenize the text
            tokens = self._tokenizer.tokenize(text)

            # Apply grammar rules to find suggestions
            suggestions = self._rules.suggest(text)

            matches = []
            for suggestion in suggestions:
                # Extract context around the error (40 chars each side)
                start = suggestion.start
                end = suggestion.end
                error_text = text[start:end]

                context_start = max(0, start - 40)
                context_end = min(len(text), end + 40)
                context = text[context_start:context_end]
                offset_in_context = start - context_start

                # Get the sentence containing the error
                sentence = self._extract_sentence(text, start, end)

                # Map nlprule suggestion to our GrammarError schema
                error = GrammarError(
                    message=suggestion.message,
                    offset=start,
                    length=end - start,
                    replacements=list(suggestion.replacements)[:5],  # Limit to 5
                    rule_id=suggestion.source,  # Rule identifier
                    category=self._categorize_rule(suggestion.source),
                    context=context,
                    sentence=sentence,
                    offset_in_context=offset_in_context
                )
                matches.append(error)

            logger.debug(f"Found {len(matches)} grammar issues in text of length {len(text)}")
            return CheckResponse(matches=matches)

        except Exception as e:
            logger.error(f"Error during grammar check: {e}")
            return CheckResponse(matches=[])

    def _extract_sentence(self, text: str, start: int, end: int) -> str:
        """
        Extract the sentence containing the error position.
        Simple heuristic using period boundaries.
        """
        # Find sentence start (look backwards for period or start of text)
        sentence_start = text.rfind('. ', 0, start)
        sentence_start = sentence_start + 2 if sentence_start != -1 else 0

        # Find sentence end (look forwards for period or end of text)
        sentence_end = text.find('. ', end)
        sentence_end = sentence_end + 1 if sentence_end != -1 else len(text)

        return text[sentence_start:sentence_end].strip()

    def _categorize_rule(self, rule_id: str) -> str:
        """
        Categorize rule ID into a human-readable category.
        nlprule rule IDs follow patterns like:
        - SPELLING_RULE -> SPELLING
        - GRAMMAR_* -> GRAMMAR
        - PUNCTUATION_* -> PUNCTUATION
        - STYLE_* -> STYLE
        """
        rule_upper = rule_id.upper()

        if "SPELL" in rule_upper:
            return "SPELLING"
        elif "GRAMMAR" in rule_upper or "AGREEMENT" in rule_upper:
            return "GRAMMAR"
        elif "PUNCT" in rule_upper or "COMMA" in rule_upper:
            return "PUNCTUATION"
        elif "STYLE" in rule_upper:
            return "STYLE"
        elif "TYPO" in rule_upper:
            return "TYPOS"
        else:
            return "MISC"


# Module-level instance for easy access
_service: Optional[GrammarCheckService] = None


def get_grammar_service() -> GrammarCheckService:
    """Get or create the singleton grammar check service."""
    global _service
    if _service is None:
        _service = GrammarCheckService()
        _service.initialize()
    return _service
```

#### Step 1.3.3: Update Application Startup

**File:** `app/main.py`

Add initialization call at startup:

```python
from app.services.grammar_check import get_grammar_service

@app.on_event("startup")
async def startup_event():
    """Initialize services on application startup."""
    logger.info("Starting LocalScribe backend...")

    # Initialize grammar checker (downloads models if needed)
    grammar_service = get_grammar_service()
    if not grammar_service.is_available():
        logger.warning("Grammar checker failed to initialize - check logs for details")

    logger.info("LocalScribe backend started successfully")
```

#### Step 1.3.4: Update API Endpoint

**File:** `app/main.py` (modify existing endpoint)

```python
from app.services.grammar_check import get_grammar_service

@app.post("/v1/text/check", response_model=CheckResponse)
async def check_text(request: CheckRequest) -> CheckResponse:
    """
    Check text for grammar and spelling errors using nlprule.

    Returns a list of matches with suggestions for corrections.
    """
    service = get_grammar_service()
    return service.check(request.text)
```

#### Step 1.3.5: Model Caching Strategy

**Automatic Caching:**
nlprule automatically caches downloaded models. Default locations:
- Linux/macOS: `~/.cache/nlprule/`
- Windows: `%LOCALAPPDATA%\nlprule\`

**Custom Cache Location (Optional):**
Set environment variable `NLPRULE_CACHE_DIR` to override.

**Docker/Container Deployment:**
Mount a volume to preserve model cache across container restarts:
```yaml
volumes:
  - nlprule-cache:/root/.cache/nlprule
```

### 1.4 Testing the Migration

**Test File:** `tests/test_grammar_check.py`

```python
"""Tests for nlprule-based grammar checking."""

import pytest
from app.services.grammar_check import get_grammar_service, GrammarCheckService


class TestGrammarCheckService:
    """Test suite for GrammarCheckService."""

    def test_service_initialization(self):
        """Test that service initializes successfully."""
        service = get_grammar_service()
        assert service.is_available()

    def test_spelling_error_detection(self):
        """Test detection of spelling errors."""
        service = get_grammar_service()
        response = service.check("This is a speling error.")

        assert len(response.matches) >= 1
        spelling_match = next(
            (m for m in response.matches if "speling" in m.context),
            None
        )
        assert spelling_match is not None
        assert "spelling" in spelling_match.replacements or "spelling" in str(spelling_match.replacements)

    def test_grammar_error_detection(self):
        """Test detection of grammar errors."""
        service = get_grammar_service()
        response = service.check("He go to the store yesterday.")

        assert len(response.matches) >= 1

    def test_empty_text(self):
        """Test handling of empty text."""
        service = get_grammar_service()
        response = service.check("")

        assert response.matches == []

    def test_correct_text(self):
        """Test that correct text returns no matches."""
        service = get_grammar_service()
        response = service.check("This is a correctly written sentence.")

        # May still have style suggestions, but should be minimal
        assert len(response.matches) <= 1

    def test_response_schema(self):
        """Test that response matches expected schema."""
        service = get_grammar_service()
        response = service.check("Their going to the store.")

        assert hasattr(response, 'matches')
        if response.matches:
            match = response.matches[0]
            assert hasattr(match, 'message')
            assert hasattr(match, 'offset')
            assert hasattr(match, 'length')
            assert hasattr(match, 'replacements')
            assert hasattr(match, 'rule_id')
            assert hasattr(match, 'category')
            assert hasattr(match, 'context')
            assert hasattr(match, 'offset_in_context')
```

### 1.5 Rollback Plan

If issues arise with nlprule:

1. Revert `requirements.txt` to include `language-tool-python`
2. Restore original `grammar_check.py` from git
3. Ensure Java is available in environment

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
│                     PHASE 1: BACKEND                            │
├─────────────────────────────────────────────────────────────────┤
│  Step 1.3.1: Update requirements.txt                            │
│       ↓                                                         │
│  Step 1.3.2: Rewrite grammar_check.py                           │
│       ↓                                                         │
│  Step 1.3.3: Update main.py startup                             │
│       ↓                                                         │
│  Step 1.3.4: Update API endpoint                                │
│       ↓                                                         │
│  Step 1.3.5: Test & verify                                      │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                     PHASE 2: CLIENT                             │
├─────────────────────────────────────────────────────────────────┤
│  Step 2.3.3: Update NativeMethods.cs                            │
│       ↓                                                         │
│  Step 2.3.1: Create OverlayWindow.xaml                          │
│       ↓                                                         │
│  Step 2.3.2: Create OverlayWindow.xaml.cs                       │
│       ↓                                                         │
│  Step 2.3.4: Update TextFocusObserver.cs                        │
│       ↓                                                         │
│  Step 2.3.5: Update TrayApplication.cs                          │
│       ↓                                                         │
│  Step 2.3.6: Add mouse position helper                          │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                     PHASE 3: POLISH                             │
├─────────────────────────────────────────────────────────────────┤
│  Step 3.1: Add configuration options                            │
│       ↓                                                         │
│  Step 3.2: Settings UI integration                              │
│       ↓                                                         │
│  Step 3.3: Edge case handling                                   │
│       ↓                                                         │
│  Final testing & documentation                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| nlprule model download fails | High | Implement retry logic, fallback to cached models |
| TextPattern not supported in target app | Medium | Graceful fallback to bubble-only mode |
| Overlay causes performance issues | Medium | Implement throttling, limit to visible viewport |
| Click-through fails on some Windows versions | Medium | Test on Win10/Win11, document requirements |
| High DPI coordinate issues | Low | Test at various DPI settings, use proper transforms |

---

## Success Criteria

### Phase 1 Complete When:
- [ ] `language-tool-python` removed from dependencies
- [ ] `nlprule` installed and models downloaded successfully
- [ ] `/v1/text/check` endpoint returns correct suggestions
- [ ] Response schema unchanged (backwards compatible)
- [ ] Startup time reduced by >50%
- [ ] All existing tests pass

### Phase 2 Complete When:
- [ ] Overlay window appears with red underlines on errors
- [ ] Overlay is truly click-through (verified in Notepad, Chrome, VSCode)
- [ ] Overlay hides within 50ms of any keyboard/mouse input
- [ ] Underlines positioned accurately under error text
- [ ] No visible flicker or performance degradation
- [ ] Works on both single and multi-monitor setups

### Phase 3 Complete When:
- [ ] Toggle in Settings to enable/disable overlay
- [ ] Style selection (solid vs wavy) works
- [ ] Graceful fallback when TextPattern unavailable
- [ ] High DPI displays work correctly
- [ ] Documentation updated
