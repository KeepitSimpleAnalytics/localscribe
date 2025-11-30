using System;
using System.Threading.Tasks;
using System.Windows;
using WpfClipboard = System.Windows.Clipboard;

namespace GramCloneClient.Services;

/// <summary>
/// Captures currently selected text and replaces it via clipboard operations.
/// </summary>
public sealed class ClipboardService
{
    public async Task<string> CaptureSelectionAsync()
    {
        string? backup = GetCurrentClipboardText();

        NativeMethods.SendCopyShortcut();
        await Task.Delay(150);

        string captured = WpfClipboard.ContainsText() ? WpfClipboard.GetText() : string.Empty;

        RestoreClipboard(backup);
        return captured;
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
