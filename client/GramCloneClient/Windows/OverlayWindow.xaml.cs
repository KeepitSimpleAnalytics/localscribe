using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using GramCloneClient.Models;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Rectangle = System.Windows.Shapes.Rectangle;
using Point = System.Windows.Point;

namespace GramCloneClient.Windows;

/// <summary>
/// Transparent overlay window for drawing error underlines.
/// This window is click-through and vanishes on user input.
/// </summary>
public partial class OverlayWindow : Window
{
    // Underline styling - now configurable via settings
    private Brush _errorBrush = new SolidColorBrush(Color.FromArgb(200, 255, 60, 60));
    private double _underlineHeight = 2.5;
    private double _underlineOffset = 2.0;
    private double _waveAmplitude = 2.0;
    private double _waveLength = 4.0;
    private UnderlineStyle _underlineStyle = UnderlineStyle.Solid;
    private bool _overlayEnabled = true;

    // Track if overlay is currently showing errors
    private bool _isShowingErrors = false;

    // Hover detection
    private DispatcherTimer? _hoverTimer;
    private List<(Rect Bounds, GrammarMatch Match)> _errorRegions = new();
    private readonly ErrorTooltipWindow _tooltip = new();
    private GrammarMatch? _lastHoveredMatch = null;

    // Event for replacement requests from tooltip
    public event EventHandler<(GrammarMatch Match, string Replacement)>? ReplacementRequested;

    public OverlayWindow()
    {
        InitializeComponent();

        // Ensure window is ready for drawing
        this.Loaded += OnLoaded;

        // Wire up tooltip replacement event
        _tooltip.ReplacementRequested += (s, args) => ReplacementRequested?.Invoke(this, args);
    }

    /// <summary>
    /// Update overlay appearance from settings. Call this when settings change.
    /// </summary>
    /// <param name="settings">The overlay display settings to apply.</param>
    public void ApplySettings(OverlayDisplaySettings settings)
    {
        // Validate and clamp settings first
        SettingsValidator.ValidateAndClamp(settings);

        _overlayEnabled = settings.Enabled;
        _underlineStyle = settings.Style;
        _underlineHeight = settings.UnderlineHeight;
        _underlineOffset = settings.UnderlineOffset;
        _waveAmplitude = settings.WaveAmplitude;
        _waveLength = settings.WaveLength;

        // Build the brush from settings
        var (r, g, b) = settings.GetColor();
        var alpha = settings.GetAlpha();
        _errorBrush = new SolidColorBrush(Color.FromArgb(alpha, r, g, b));
    }

    /// <summary>
    /// Check if overlay is enabled in settings.
    /// </summary>
    public bool IsOverlayEnabled => _overlayEnabled;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Cover all screens (virtual screen includes all monitors)
        this.Left = SystemParameters.VirtualScreenLeft;
        this.Top = SystemParameters.VirtualScreenTop;
        this.Width = SystemParameters.VirtualScreenWidth;
        this.Height = SystemParameters.VirtualScreenHeight;

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

        // Add WS_EX_TRANSPARENT (click-through), WS_EX_LAYERED, and WS_EX_NOACTIVATE (no focus steal)
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE,
            extendedStyle | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_NOACTIVATE);
    }

    /// <summary>
    /// Set error regions for hover detection and draw underlines/highlights.
    /// </summary>
    public void SetErrorRegions(
        List<(Rect Bounds, GrammarMatch Match)> grammarRegions,
        List<(Rect Bounds, GrammarMatch Match)>? analysisRegions = null)
    {
        _errorRegions = new List<(Rect Bounds, GrammarMatch Match)>();
        
        if (grammarRegions != null)
            _errorRegions.AddRange(grammarRegions);
            
        if (analysisRegions != null)
            _errorRegions.AddRange(analysisRegions);

        if (!_overlayEnabled)
        {
            return; 
        }

        // Clear everything once at the start
        ErrorCanvas.Children.Clear();

        // 1. Draw Analysis Highlights (Background)
        if (analysisRegions != null && analysisRegions.Count > 0)
        {
            // Extract issue type from RuleId (e.g., "SEMANTIC_COMPLEXITY" -> "complexity")
            var issues = analysisRegions.Select(r => {
                string type = r.Match.RuleId.Replace("SEMANTIC_", "").ToLower();
                return (r.Bounds, type);
            }).ToList();
            
            DrawAnalysisIssues(issues);
        }

        // 2. Draw Grammar Underlines (Foreground)
        if (grammarRegions != null && grammarRegions.Count > 0)
        {
            var rects = grammarRegions.Select(r => r.Bounds).ToList();
            DrawMatches(rects, clearCanvas: false);
        }

        _isShowingErrors = _errorRegions.Count > 0;

        // Start hover detection if not already running
        if (_hoverTimer == null)
        {
            _hoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _hoverTimer.Tick += CheckMousePosition;
        }
        _hoverTimer.Start();
    }

    private void CheckMousePosition(object? sender, EventArgs e)
    {
        if (!IsVisible || _errorRegions.Count == 0)
        {
            _tooltip.HideTooltip();
            return;
        }

        // Get mouse position via P/Invoke
        if (!NativeMethods.GetCursorPos(out var pt))
            return;

        var mousePos = new Point(pt.X, pt.Y);

        // Check if mouse is over the tooltip itself - keep it visible
        if (_tooltip.IsVisible)
        {
            var tooltipBounds = new Rect(_tooltip.Left, _tooltip.Top, _tooltip.ActualWidth, _tooltip.ActualHeight);
            if (tooltipBounds.Contains(mousePos))
            {
                // Mouse is over tooltip, keep it visible
                return;
            }
        }

        // Check which error region (if any) contains the mouse
        GrammarMatch? hoveredMatch = null;
        foreach (var (bounds, match) in _errorRegions)
        {
            // Expand hit area generously for easier hover
            var expandedBounds = new Rect(
                bounds.X - 5,
                bounds.Y - 5,
                bounds.Width + 10,
                bounds.Height + 15);  // Extra height below for underline area

            if (expandedBounds.Contains(mousePos))
            {
                hoveredMatch = match;
                break;
            }
        }

        // Only update tooltip if hovered error changed
        if (hoveredMatch != _lastHoveredMatch)
        {
            _lastHoveredMatch = hoveredMatch;
            if (hoveredMatch != null)
                _tooltip.ShowForMatch(hoveredMatch, mousePos);
            else
                _tooltip.HideTooltip();
        }
    }

    /// <summary>
    /// Draw underlines at the specified screen rectangles.
    /// </summary>
    public void DrawMatches(List<Rect> errorRects, bool clearCanvas = true)
    {
        if (clearCanvas)
        {
            ErrorCanvas.Children.Clear();
        }

        if (!_overlayEnabled || _underlineStyle == UnderlineStyle.None)
        {
            if (clearCanvas) _isShowingErrors = false;
            return;
        }

        if (errorRects == null || errorRects.Count == 0)
        {
            if (clearCanvas) _isShowingErrors = false;
            return;
        }

        foreach (var rect in errorRects)
        {
            Point topLeft = PointFromScreen(new Point(rect.Left, rect.Top));
            Point bottomRight = PointFromScreen(new Point(rect.Right, rect.Bottom));

            double width = Math.Max(bottomRight.X - topLeft.X, 10); // Minimum width
            double y = bottomRight.Y + _underlineOffset;

            switch (_underlineStyle)
            {
                case UnderlineStyle.Solid:
                    DrawSolidUnderline(topLeft.X, y, width);
                    break;
                case UnderlineStyle.Wavy:
                    DrawWavyUnderline(topLeft.X, y, width);
                    break;
                case UnderlineStyle.Dotted:
                    DrawDottedUnderline(topLeft.X, y, width);
                    break;
                case UnderlineStyle.Dashed:
                    DrawDashedUnderline(topLeft.X, y, width);
                    break;
            }
        }

        _isShowingErrors = true;
    }

    /// <summary>
    /// Draw highlights for semantic analysis issues.
    /// Uses background highlights instead of underlines.
    /// </summary>
    private void DrawAnalysisIssues(List<(Rect Rect, string IssueType)> issues)
    {
        foreach (var (rect, issueType) in issues)
        {
            Point topLeft = PointFromScreen(new Point(rect.Left, rect.Top));
            Point bottomRight = PointFromScreen(new Point(rect.Right, rect.Bottom));

            double width = Math.Max(bottomRight.X - topLeft.X, 10);
            double height = Math.Max(bottomRight.Y - topLeft.Y, 10);
            
            // Draw highlight behind text (semi-transparent)
            DrawHighlight(topLeft.X, topLeft.Y, width, height, issueType);
        }
    }

    private void DrawHighlight(double x, double y, double width, double height, string issueType)
    {
        var color = issueType switch
        {
            "complexity" => Color.FromArgb(80, 255, 200, 0),    // Yellow
            "passive_voice" => Color.FromArgb(80, 0, 100, 255), // Blue
            "wordiness" => Color.FromArgb(80, 150, 150, 150),   // Gray
            "jargon" => Color.FromArgb(80, 180, 50, 200),       // Purple
            _ => Color.FromArgb(80, 255, 255, 0)                // Default Yellow
        };

        var highlight = new Rectangle
        {
            Width = width,
            Height = height,
            Fill = new SolidColorBrush(color),
            RadiusX = 2,
            RadiusY = 2
        };

        Canvas.SetLeft(highlight, x);
        Canvas.SetTop(highlight, y);
        // Insert at 0 so it's behind everything
        ErrorCanvas.Children.Insert(0, highlight);
    }

    /// <summary>
    /// Draw a solid underline rectangle.
    /// </summary>
    private void DrawSolidUnderline(double x, double y, double width)
    {
        var underline = new Rectangle
        {
            Width = width,
            Height = _underlineHeight,
            Fill = _errorBrush,
            RadiusX = 1,
            RadiusY = 1
        };

        Canvas.SetLeft(underline, x);
        Canvas.SetTop(underline, y);
        ErrorCanvas.Children.Add(underline);
    }

    /// <summary>
    /// Draw a dotted underline using small circles.
    /// </summary>
    private void DrawDottedUnderline(double x, double y, double width)
    {
        double dotSpacing = _underlineHeight * 2;
        double dotSize = _underlineHeight;

        for (double dx = 0; dx < width; dx += dotSpacing)
        {
            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = dotSize,
                Height = dotSize,
                Fill = _errorBrush
            };

            Canvas.SetLeft(dot, x + dx);
            Canvas.SetTop(dot, y);
            ErrorCanvas.Children.Add(dot);
        }
    }

    /// <summary>
    /// Draw a dashed underline using short rectangles.
    /// </summary>
    private void DrawDashedUnderline(double x, double y, double width)
    {
        double dashLength = _underlineHeight * 3;
        double gapLength = _underlineHeight * 1.5;

        for (double dx = 0; dx < width; dx += dashLength + gapLength)
        {
            double actualDashLength = Math.Min(dashLength, width - dx);
            if (actualDashLength <= 0) break;

            var dash = new Rectangle
            {
                Width = actualDashLength,
                Height = _underlineHeight,
                Fill = _errorBrush,
                RadiusX = 0.5,
                RadiusY = 0.5
            };

            Canvas.SetLeft(dash, x + dx);
            Canvas.SetTop(dash, y);
            ErrorCanvas.Children.Add(dash);
        }
    }

    /// <summary>
    /// Draw a wavy underline using polyline.
    /// </summary>
    private void DrawWavyUnderline(double x, double y, double width)
    {
        var wavyLine = CreateWavyLine(x, y, width);
        ErrorCanvas.Children.Add(wavyLine);
    }

    /// <summary>
    /// Create a wavy polyline for a squiggly underline effect.
    /// Uses configurable wave amplitude and length from settings.
    /// </summary>
    private System.Windows.Shapes.Polyline CreateWavyLine(double startX, double y, double width)
    {
        var points = new PointCollection();

        for (double x = 0; x <= width; x += _waveLength / 2)
        {
            double waveY = (x / (_waveLength / 2)) % 2 == 0 ? y : y + _waveAmplitude;
            points.Add(new Point(startX + x, waveY));
        }

        return new System.Windows.Shapes.Polyline
        {
            Points = points,
            Stroke = _errorBrush,
            StrokeThickness = Math.Max(_underlineHeight * 0.6, 1.0),
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
        if (!_overlayEnabled || _underlineStyle == UnderlineStyle.None)
        {
            return; // Overlay is disabled in settings
        }

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

        // Stop hover detection and hide tooltip
        _hoverTimer?.Stop();
        _tooltip.HideTooltip();
        _lastHoveredMatch = null;
        _errorRegions.Clear();
    }
}
