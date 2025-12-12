using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using GramCloneClient.Models;
using GramCloneClient.Services;
using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using Cursors = System.Windows.Input.Cursors;
using Button = System.Windows.Controls.Button;

namespace GramCloneClient.Windows;

public partial class ErrorTooltipWindow : Window
{
    public event EventHandler<(GrammarMatch Match, string Replacement)>? ReplacementRequested;

    private GrammarMatch? _currentMatch;

    public ErrorTooltipWindow()
    {
        InitializeComponent();
    }

    public void ShowForMatch(GrammarMatch match, Point screenPosition)
    {
        _currentMatch = match;

        // Set error message
        ErrorMessage.Text = match.Message;

        // Show context if available
        if (!string.IsNullOrEmpty(match.Context))
        {
            ContextBorder.Visibility = Visibility.Visible;
            ContextText.Inlines.Clear();

            // Highlight the error within context
            if (match.OffsetInContext >= 0 && match.OffsetInContext + match.Length <= match.Context.Length)
            {
                if (match.OffsetInContext > 0)
                    ContextText.Inlines.Add(new Run(match.Context.Substring(0, match.OffsetInContext)));

                // Use theme-aware error highlight color
                var errorHighlightBrush = TryFindResource("ErrorBackground") as System.Windows.Media.Brush
                    ?? new SolidColorBrush(Color.FromRgb(255, 200, 200));

                var errorRun = new Run(match.Context.Substring(match.OffsetInContext, match.Length))
                {
                    Background = errorHighlightBrush,
                    FontWeight = FontWeights.Bold
                };
                ContextText.Inlines.Add(errorRun);

                int afterStart = match.OffsetInContext + match.Length;
                if (afterStart < match.Context.Length)
                    ContextText.Inlines.Add(new Run(match.Context.Substring(afterStart)));
            }
            else
            {
                ContextText.Text = match.Context;
            }
        }
        else
        {
            ContextBorder.Visibility = Visibility.Collapsed;
        }

        // Set suggestions as clickable buttons
        SuggestionsList.Items.Clear();

        // Get theme-aware colors for suggestion buttons
        var linkBrush = TryFindResource("InfoColor") as System.Windows.Media.Brush
            ?? new SolidColorBrush(Color.FromRgb(33, 150, 243));
        var hoverBrush = TryFindResource("HoverBackground") as System.Windows.Media.Brush
            ?? new SolidColorBrush(Color.FromRgb(230, 245, 255));

        foreach (var suggestion in match.Replacements)
        {
            var btn = new Button
            {
                Content = suggestion,
                Tag = suggestion,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = linkBrush,
                FontStyle = FontStyles.Italic,
                FontSize = 11,
                Cursor = Cursors.Hand,
                Padding = new Thickness(4, 4, 4, 4),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left
            };
            btn.Click += OnSuggestionClicked;

            // Add hover effect with theme-aware color
            btn.MouseEnter += (s, e) => btn.Background = hoverBrush;
            btn.MouseLeave += (s, e) => btn.Background = Brushes.Transparent;

            SuggestionsList.Items.Add(btn);
        }

        // Position tooltip near cursor but keep on screen
        double x = screenPosition.X + 15;
        double y = screenPosition.Y + 15;

        // Need to show first to get ActualWidth/Height
        if (!IsVisible)
            Show();

        // Ensure tooltip stays on screen
        UpdateLayout();
        double screenWidth = SystemParameters.VirtualScreenWidth;
        double screenHeight = SystemParameters.VirtualScreenHeight;

        if (x + ActualWidth > screenWidth)
            x = screenPosition.X - ActualWidth - 10;
        if (y + ActualHeight > screenHeight)
            y = screenPosition.Y - ActualHeight - 10;

        Left = x;
        Top = y;
    }

    private void OnSuggestionClicked(object sender, RoutedEventArgs e)
    {
        Logger.Log("Tooltip: Suggestion button clicked!");
        if (sender is Button btn && btn.Tag is string replacement && _currentMatch != null)
        {
            // Capture match before hiding (in case hiding clears state)
            var match = _currentMatch;

            // Hide tooltip FIRST to release focus back to the target application
            HideTooltip();
            Thread.Sleep(50);  // Small delay for focus to settle

            Logger.Log($"Tooltip: Invoking ReplacementRequested for '{replacement}'");
            ReplacementRequested?.Invoke(this, (match, replacement));
        }
        else
        {
            Logger.Log($"Tooltip: Click handler - btn={sender is Button}, tag={((sender as Button)?.Tag)}, match={_currentMatch != null}");
        }
    }

    public void HideTooltip()
    {
        if (IsVisible)
            Hide();
    }
}
