using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GramCloneClient.Windows;

public partial class DiagnosticsWindow : Window
{
    public DiagnosticsWindow()
    {
        InitializeComponent();
    }

    public void UpdateTextMetrics(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            WordCountText.Text = "0";
            CharCountText.Text = "0";
            ReadTimeText.Text = "0s";
            GradeLevelText.Text = "N/A";
            return;
        }

        int charCount = text.Length;
        int wordCount = CountWords(text);
        
        // Reading time: ~200 wpm
        int seconds = (int)(wordCount / (200.0 / 60.0));
        string timeStr = seconds < 60 ? $"{seconds}s" : $"{seconds / 60}m {seconds % 60}s";

        // Grade Level (Approximate Flesch-Kincaid)
        double grade = CalculateGradeLevel(text, wordCount);

        WordCountText.Text = wordCount.ToString();
        CharCountText.Text = charCount.ToString();
        ReadTimeText.Text = timeStr;
        GradeLevelText.Text = $"Grade {Math.Max(0, Math.Round(grade, 1))}";
    }

    public void UpdateFocusInfo(string process, string control, bool hasPattern, Rect bounds)
    {
        ProcessNameText.Text = process;
        ControlTypeText.Text = control;
        
        if (hasPattern)
        {
            PatternStatusText.Text = "Supported (TextPattern)";
            PatternStatusText.Foreground = Brushes.LightGreen;
        }
        else
        {
            PatternStatusText.Text = "Unsupported / Fallback";
            PatternStatusText.Foreground = Brushes.Orange; // or Red
        }

        BoundsText.Text = $"Bounds: ({{bounds.X:F0}}, {{bounds.Y:F0}}, {{bounds.Width:F0}}, {{bounds.Height:F0}})";
    }

    public void AppendLog(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        LogBox.AppendText($"[{{timestamp}}] {message}\n");
        LogBox.ScrollToEnd();
    }

    public void UpdateHealth(bool backendOk, bool ollamaOk)
    {
        BackendStatusDot.Fill = backendOk ? Brushes.LightGreen : Brushes.Red;
        BackendStatusText.Text = backendOk ? "Backend: Connected" : "Backend: Disconnected";

        OllamaStatusDot.Fill = ollamaOk ? Brushes.LightGreen : Brushes.Red;
        OllamaStatusText.Text = ollamaOk ? "Ollama: Connected" : "Ollama: Unreachable";
    }

    private int CountWords(string text)
    {
        return text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private double CalculateGradeLevel(string text, int wordCount)
    {
        if (wordCount == 0) return 0;

        int sentenceCount = text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries).Length;
        sentenceCount = Math.Max(1, sentenceCount);

        int syllableCount = CountSyllables(text);

        // Flesch-Kincaid Grade Level = 0.39 (total words / total sentences) + 11.8 (total syllables / total words) - 15.59
        return 0.39 * ((double)wordCount / sentenceCount) + 11.8 * ((double)syllableCount / wordCount) - 15.59;
    }

    private int CountSyllables(string text)
    {
        // Very rough approximation: count vowels
        // A better implementation would handle dipthongs, silent e, etc.
        // For diagnostics, this is "good enough" to see changes.
        return text.Count(c => "aeiouyAEIOUY".Contains(c)); 
    }
    
    // Prevent closing, just hide
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
