using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using GramCloneClient.Models;

namespace GramCloneClient.Windows
{
    public partial class BubbleWindow : Window
    {
        private List<GrammarMatch> _currentMatches = new();

        public BubbleWindow()
        {
            InitializeComponent();
        }

        public void UpdateState(List<GrammarMatch> matches)
        {
            _currentMatches = matches;
            int errorCount = matches.Count;

            if (errorCount == 0)
            {
                // Green / Good
                BubbleBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80)); // Green
                CountText.Text = "✓";
            }
            else
            {
                // Red / Error
                BubbleBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 67, 54)); // Red
                CountText.Text = errorCount.ToString();
                this.Show();
            }
            
            // If popup is open, refresh it? Or close it?
            if (ErrorPopup.IsOpen)
            {
                ErrorsList.ItemsSource = _currentMatches;
            }
        }

        public void UpdatePosition(Rect targetRect)
        {
            // Position the bubble to the right of the target element
            // targetRect is in screen coordinates
            
            if (targetRect == Rect.Empty) return;

            double targetX = targetRect.Right + 10; // 10px padding
            double targetY = targetRect.Top;

            // Ensure it stays on screen (basic check)
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            if (targetX + this.Width > screenWidth)
            {
                targetX = targetRect.Left - this.Width - 10; // Flip to left if offscreen
            }
            
            if (targetY + this.Height > screenHeight)
            {
                 targetY = screenHeight - this.Height;
            }

            this.Left = targetX;
            this.Top = targetY;
        }

        private void BubbleBorder_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_currentMatches.Count > 0)
            {
                ErrorsList.ItemsSource = _currentMatches;
                ErrorPopup.IsOpen = !ErrorPopup.IsOpen;
            }
        }
    }
}
