using System;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Interop;

namespace GramCloneClient.Services;

/// <summary>
/// Registers a global hotkey and raises events when triggered.
/// </summary>
public sealed class HotkeyListener : IDisposable
{
    private const int HotkeyId = 0x7000;
    private HwndSource? _source;
    private uint _modifiers;
    private uint _key;
    private string _hotkeySpec;

    public event EventHandler? HotkeyPressed;

    public HotkeyListener(string hotkeySpec)
    {
        _hotkeySpec = hotkeySpec;
        (_modifiers, _key) = ParseHotkeySpec(hotkeySpec);
    }

    public void Register()
    {
        if (_source != null)
        {
            return;
        }

        var parameters = new HwndSourceParameters("GramCloneHotkeySink")
        {
            WindowStyle = NativeMethods.WS_POPUP,
            Width = 0,
            Height = 0,
            PositionX = 0,
            PositionY = 0
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);

        RegisterHotkeyInternal();
    }

    public void UpdateHotkey(string hotkeySpec)
    {
        _hotkeySpec = hotkeySpec;
        (_modifiers, _key) = ParseHotkeySpec(hotkeySpec);

        if (_source != null)
        {
            NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
            RegisterHotkeyInternal();
        }
    }

    private void RegisterHotkeyInternal()
    {
        if (_source == null)
        {
            return;
        }

        if (!NativeMethods.RegisterHotKey(_source.Handle, HotkeyId, _modifiers, _key))
        {
            throw new InvalidOperationException($"Failed to register hotkey '{_hotkeySpec}'.");
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static (uint Modifiers, uint Key) ParseHotkeySpec(string spec)
    {
        var parts = spec.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return (NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, (uint)Keys.G);
        }

        uint modifiers = 0;
        string keyPart = parts.Last();

        foreach (string part in parts.SkipLast(1))
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= NativeMethods.MOD_CONTROL;
                    break;
                case "ALT":
                    modifiers |= NativeMethods.MOD_ALT;
                    break;
                case "SHIFT":
                    modifiers |= NativeMethods.MOD_SHIFT;
                    break;
                case "WIN":
                case "WINDOWS":
                    modifiers |= NativeMethods.MOD_WIN;
                    break;
            }
        }

        if (!Enum.TryParse(keyPart, true, out Keys key))
        {
            char ch = keyPart.ToUpperInvariant()[0];
            key = (Keys)ch;
        }

        return (modifiers == 0 ? NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT : modifiers, (uint)key);
    }

    public void Dispose()
    {
        if (_source != null)
        {
            NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
        }

        GC.SuppressFinalize(this);
    }
}
