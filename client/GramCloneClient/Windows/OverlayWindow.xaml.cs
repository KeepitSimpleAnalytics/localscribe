using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
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
    private System.Windows.Shapes.Polyline CreateWavyLine(double startX, double y, double width)
    {
        var points = new PointCollection();
        double waveHeight = 2.0;
        double waveLength = 4.0;

        for (double x = 0; x <= width; x += waveLength / 2)
        {
            double waveY = (x / (waveLength / 2)) % 2 == 0 ? y : y + waveHeight;
            points.Add(new Point(startX + x, waveY));
        }

        return new System.Windows.Shapes.Polyline
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
