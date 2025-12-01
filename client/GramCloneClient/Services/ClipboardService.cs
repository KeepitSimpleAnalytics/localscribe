using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using WpfClipboard = System.Windows.Clipboard;

namespace GramCloneClient.Services;

/// <summary>
/// Captures currently selected text and replaces it via clipboard operations.
/// </summary>
public sealed class ClipboardService
{
    public Task<string> CaptureSelectionAsync()
    {
        // Use UI Automation to directly read selected text without clipboard
        string selectedText = CaptureSelectionViaUIAutomation();
        return Task.FromResult(selectedText);
    }

    private string CaptureSelectionViaUIAutomation()
    {
        try
        {
            // Get the currently focused element
            var focusedElement = AutomationElement.FocusedElement;
            if (focusedElement == null)
            {
                Logger.Log("CaptureSelection: No focused element found");
                return string.Empty;
            }

            // Try to get the TextPattern
            if (focusedElement.TryGetCurrentPattern(TextPattern.Pattern, out object patternObj))
            {
                var textPattern = (TextPattern)patternObj;

                // Get the selected text range(s)
                var selections = textPattern.GetSelection();
                if (selections == null || selections.Length == 0)
                {
                    Logger.Log("CaptureSelection: No text selected");
                    return string.Empty;
                }

                // Get text from the first selection range
                var selectedRange = selections[0];
                string text = selectedRange.GetText(-1); // -1 means get all text in range

                Logger.Log($"CaptureSelection: Captured {text.Length} characters via UI Automation");
                return text;
            }
            else
            {
                Logger.Log("CaptureSelection: TextPattern not supported on focused element");
                return string.Empty;
            }
        }
        catch (ElementNotAvailableException ex)
        {
            Logger.Log($"CaptureSelection: Element not available - {ex.Message}");
            return string.Empty;
        }
        catch (Exception ex)
        {
            Logger.Log($"CaptureSelection: Failed to capture via UI Automation - {ex.Message}");
            return string.Empty;
        }
    }

    public async Task ReplaceSelectionAsync(string newText, IntPtr targetHandle)
    {
        string? backup = GetCurrentClipboardText();
        WpfClipboard.SetText(newText);

        if (targetHandle != IntPtr.Zero)
        {
            NativeMethods.SetForegroundWindow(targetHandle);
        }

        await Task.Delay(60);
        NativeMethods.SendPasteShortcut();
        await Task.Delay(60);

        RestoreClipboard(backup);
    }

    private static string? GetCurrentClipboardText()
    {
        return WpfClipboard.ContainsText() ? WpfClipboard.GetText() : null;
    }

    private static void RestoreClipboard(string? text)
    {
        if (text is null)
        {
            WpfClipboard.Clear();
        }
        else
        {
            WpfClipboard.SetText(text);
        }
    }
}
