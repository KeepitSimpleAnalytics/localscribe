using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using GramCloneClient.Models;
using GramCloneClient.Services;

namespace GramCloneClient.Windows
{
    public partial class BubbleWindow : Window
    {
        private List<GrammarMatch> _currentMatches = new();
        private bool _userPositioned = false; // Track if user manually positioned the bubble

        public BubbleWindow()
        {
            InitializeComponent();
            PositionNearTaskbar();
        }

        /// <summary>
        /// Position bubble near the taskbar (bottom-right of screen)
        /// </summary>
        private void PositionNearTaskbar()
        {
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.WorkArea.Height; // WorkArea excludes taskbar

            this.Left = screenWidth - this.Width - 50;  // 50px from right edge
            this.Top = screenHeight - this.Height - 10;  // 10px above taskbar
        }

        public void UpdateState(List<GrammarMatch> matches, string? overflowText = null)
        {
            _currentMatches = matches;
            int errorCount = matches.Count;

            if (errorCount == 0)
            {
                // Green / Good (Semi-transparent)
                BubbleBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 76, 175, 80));
                CountText.Text = "✓";
            }
            else
            {
                // Red / Error (Semi-transparent)
                BubbleBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 244, 67, 54));
                // Show overflow indicator (e.g., "20+") if there are more errors than displayed
                CountText.Text = overflowText ?? errorCount.ToString();
                this.Show();
            }
            
            // If popup is open, refresh it with new matches
            if (ErrorPopup.IsOpen)
            {
                PopulateErrorsList(_currentMatches);
            }
        }

        public void UpdatePosition(Rect targetRect)
        {
            // If user has manually positioned the bubble, don't auto-reposition
            if (_userPositioned) return;

            // Keep bubble near taskbar - don't follow cursor
            // PositionNearTaskbar() was called in constructor
        }

        /// <summary>
        /// Right-click to drag the bubble to a new position
        /// </summary>
        private void BubbleBorder_MouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _userPositioned = true; // User is manually positioning
            this.DragMove();
        }

        private void BubbleBorder_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_currentMatches.Count > 0)
            {
                PopulateErrorsList(_currentMatches);
                ErrorPopup.IsOpen = !ErrorPopup.IsOpen;
            }
        }

        private void PopulateErrorsList(List<GrammarMatch> matches)
        {
            ErrorsList.Items.Clear();
            foreach (var match in matches)
            {
                ErrorsList.Items.Add(CreateErrorPanel(match));
            }
        }

        private StackPanel CreateErrorPanel(GrammarMatch match)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

            // Context display with highlighted error
            if (!string.IsNullOrEmpty(match.Context))
            {
                var contextBorder = new Border
                {
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 245, 245)),
                    Padding = new Thickness(8, 6, 8, 6),
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(0, 0, 0, 6)
                };

                contextBorder.Child = CreateContextTextBlock(match.Context, match.OffsetInContext, match.Length);
                panel.Children.Add(contextBorder);
            }

            // Error message
            var messageBlock = new TextBlock
            {
                Text = match.Message,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4),
                FontSize = 11
            };
            panel.Children.Add(messageBlock);

            // "Suggestions:" label
            var sugLabel = new TextBlock
            {
                Text = "Suggestions:",
                FontSize = 10,
                Foreground = new SolidColorBrush(System.Windows.Media.Colors.Gray)
            };
            panel.Children.Add(sugLabel);

            // Suggestions list
            foreach (var replacement in match.Replacements)
            {
                var replBlock = new TextBlock
                {
                    Text = replacement,
                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(33, 150, 243)),
                    Margin = new Thickness(4, 2, 4, 2),
                    FontStyle = FontStyles.Italic,
                    FontSize = 10
                };
                panel.Children.Add(replBlock);
            }

            // Separator
            var sep = new Separator
            {
                Margin = new Thickness(0, 8, 0, 0),
                Opacity = 0.5
            };
            panel.Children.Add(sep);

            return panel;
        }

        private TextBlock CreateContextTextBlock(string context, int errorOffset, int errorLength)
        {
            var textBlock = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11
            };

            // Bounds checking
            if (errorOffset < 0 || errorLength < 0 ||
                errorOffset + errorLength > context.Length)
            {
                // Fallback: just show context without highlighting
                textBlock.Text = context;
                Logger.Log($"Context bounds error: offset={errorOffset}, length={errorLength}, contextLen={context.Length}");
                return textBlock;
            }

            // Before error
            if (errorOffset > 0)
            {
                textBlock.Inlines.Add(new Run(context.Substring(0, errorOffset)));
            }

            // Error text (highlighted in red)
            string errorText = context.Substring(errorOffset, errorLength);
            var errorRun = new Run(errorText)
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 67, 54)), // Red background
                Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.Bold
            };
            textBlock.Inlines.Add(errorRun);

            // After error
            int afterStart = errorOffset + errorLength;
            if (afterStart < context.Length)
            {
                textBlock.Inlines.Add(new Run(context.Substring(afterStart)));
            }

            return textBlock;
        }
    }
}
