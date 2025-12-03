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

    // Extended window style constants for click-through overlay
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_NOACTIVATE = 0x08000000;

    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;

    private const uint KEYEVENTF_KEYUP = 0x0002;

    // For GetCursorPos
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

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
    public static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    public const int SW_RESTORE = 9;
    public const int SW_SHOW = 5;
    public const uint GA_ROOT = 2;  // Get the root window (top-level window that has no owner)

    /// <summary>
    /// Force a window to the foreground by attaching to its input thread.
    /// This bypasses Windows restrictions on SetForegroundWindow.
    /// </summary>
    public static bool ForceForegroundWindow(IntPtr hWnd)
    {
        uint currentThreadId = GetCurrentThreadId();
        uint targetThreadId = GetWindowThreadProcessId(hWnd, out _);

        bool attached = false;
        try
        {
            // Attach our thread to the target window's thread
            if (currentThreadId != targetThreadId)
            {
                attached = AttachThreadInput(currentThreadId, targetThreadId, true);
            }

            // Now we can reliably set foreground
            ShowWindow(hWnd, SW_RESTORE);  // Restore if minimized
            BringWindowToTop(hWnd);
            SetForegroundWindow(hWnd);

            return true;
        }
        finally
        {
            // Detach threads
            if (attached)
            {
                AttachThreadInput(currentThreadId, targetThreadId, false);
            }
        }
    }

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT lpPoint);

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
