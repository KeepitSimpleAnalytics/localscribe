using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace GramCloneClient;

internal static class NativeMethods
{
    public const int WM_HOTKEY = 0x0312;
    public const int WS_POPUP = unchecked((int)0x80000000);

    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;

    private const uint KEYEVENTF_KEYUP = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    public static void SendKeystroke(Keys key, bool ctrl = false, bool alt = false, bool shift = false)
    {
        ReleaseModifiers(); // Ensure no stray modifiers from the hotkey are active

        var inputs = new System.Collections.Generic.List<INPUT>();

        void AddKey(ushort vk, bool keyUp)
        {
            inputs.Add(new INPUT
            {
                type = 1,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = vk,
                        dwFlags = keyUp ? KEYEVENTF_KEYUP : 0
                    }
                }
            });
        }

        if (ctrl) AddKey((ushort)Keys.ControlKey, false);
        if (alt) AddKey((ushort)Keys.Menu, false);
        if (shift) AddKey((ushort)Keys.ShiftKey, false);

        AddKey((ushort)key, false);
        AddKey((ushort)key, true);

        if (shift) AddKey((ushort)Keys.ShiftKey, true);
        if (alt) AddKey((ushort)Keys.Menu, true);
        if (ctrl) AddKey((ushort)Keys.ControlKey, true);

        SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
        Thread.Sleep(50);
    }

    public static void SendCopyShortcut() => SendKeystroke(Keys.C, ctrl: true);

    public static void SendPasteShortcut() => SendKeystroke(Keys.V, ctrl: true);

    public static void ReleaseModifiers()
    {
        var inputs = new System.Collections.Generic.List<INPUT>();

        void AddKeyUp(ushort vk)
        {
            inputs.Add(new INPUT
            {
                type = 1,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = vk,
                        dwFlags = KEYEVENTF_KEYUP
                    }
                }
            });
        }

        AddKeyUp((ushort)Keys.Menu);       // Alt
        AddKeyUp((ushort)Keys.ControlKey); // Ctrl
        AddKeyUp((ushort)Keys.ShiftKey);   // Shift
        AddKeyUp((ushort)Keys.LWin);       // Win

        SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
        Thread.Sleep(50);
    }
}
