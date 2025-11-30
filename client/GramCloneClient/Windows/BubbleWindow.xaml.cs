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
                // Green / Good (Semi-transparent)
                BubbleBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 76, 175, 80)); 
                CountText.Text = "✓";
            }
            else
            {
                // Red / Error (Semi-transparent)
                BubbleBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 244, 67, 54)); 
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
            // Position the bubble slightly above and to the right of the cursor
            // targetRect is in screen coordinates
            
            if (targetRect == Rect.Empty) return;

            // Move 5px right and shift UP by the bubble height + 2px padding
            // This places it "above" the line you are typing on.
            double targetX = targetRect.Right + 5; 
            double targetY = targetRect.Top - this.Height - 2;

            // Ensure it stays on screen (basic check)
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            if (targetX + this.Width > screenWidth)
            {
                targetX = targetRect.Left - this.Width - 5; // Flip to left if offscreen
            }
            
            // If it goes off the top, flip it to below
            if (targetY < 0)
            {
                 targetY = targetRect.Bottom + 2;
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
