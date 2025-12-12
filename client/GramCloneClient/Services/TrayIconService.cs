using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace GramCloneClient.Services;

/// <summary>
/// Hosts the system tray icon and context menu.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly Action _onShowSettings;
    private readonly Action _onShowAbout;
    private readonly Action _onExit;
    private NotifyIcon? _notifyIcon;
    private Icon? _customIcon;

    public TrayIconService(Action onShowSettings, Action onShowAbout, Action onExit)
    {
        _onShowSettings = onShowSettings;
        _onShowAbout = onShowAbout;
        _onExit = onExit;
    }

    public void Initialize()
    {
        _customIcon = LoadOrCreateIcon();

        _notifyIcon = new NotifyIcon
        {
            Icon = _customIcon ?? SystemIcons.Information,
            Text = "LocalScribe Assistant",
            Visible = true,
            ContextMenuStrip = BuildContextMenu()
        };
    }

    /// <summary>
    /// Loads custom icon from file or creates a shield+checkmark icon programmatically.
    /// </summary>
    private Icon? LoadOrCreateIcon()
    {
        // Try to load custom icon from app directory
        string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "localscribe.ico");
        if (File.Exists(iconPath))
        {
            try
            {
                return new Icon(iconPath);
            }
            catch
            {
                Logger.Log($"Failed to load custom icon from {iconPath}");
            }
        }

        // Create shield+checkmark icon programmatically
        return CreateShieldIcon();
    }

    /// <summary>
    /// Creates the LS (LocalScribe) monogram icon - cursive script style.
    /// </summary>
    private static Icon? CreateShieldIcon()
    {
        try
        {
            const int size = 32;
            using var bitmap = new Bitmap(size, size);
            using var g = Graphics.FromImage(bitmap);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // LS monogram color (blue)
            var logoColor = Color.FromArgb(255, 45, 90, 130);

            // Draw LS monogram with thick pen
            using var pen = new Pen(logoColor, 3.2f);
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;

            // Scale factor for 32x32
            float s = size / 32f;

            // Draw the cursive "L" with loop
            using var lPath = new GraphicsPath();

            // L: Start from loop top, go up and around, down the stem, then right for base
            lPath.AddBezier(
                12 * s, 8 * s,    // Loop start
                8 * s, 4 * s,     // Loop top left
                4 * s, 6 * s,     // Loop left
                6 * s, 12 * s     // Coming down
            );
            lPath.AddBezier(
                6 * s, 12 * s,    // Continue down
                8 * s, 18 * s,    // Curving
                7 * s, 24 * s,    // Bottom approach
                8 * s, 26 * s     // Bottom of L
            );
            lPath.AddBezier(
                8 * s, 26 * s,    // Bottom of L
                10 * s, 28 * s,   // Curve right
                14 * s, 26 * s,   // Going to S
                16 * s, 24 * s    // Connect to S
            );

            g.DrawPath(pen, lPath);

            // Draw the cursive "S"
            using var sPath = new GraphicsPath();

            // S: Connected flowing S shape
            sPath.AddBezier(
                16 * s, 24 * s,   // Start (from L)
                20 * s, 22 * s,   // Upper right
                26 * s, 18 * s,   // Top of S curve
                24 * s, 14 * s    // Coming around
            );
            sPath.AddBezier(
                24 * s, 14 * s,   // Middle approach
                22 * s, 10 * s,   // Curve left
                14 * s, 14 * s,   // Cross point
                16 * s, 18 * s    // Going right
            );
            sPath.AddBezier(
                16 * s, 18 * s,   // Lower S start
                18 * s, 22 * s,   // Curving down
                26 * s, 26 * s,   // Bottom right
                22 * s, 30 * s    // S tail end
            );

            g.DrawPath(pen, sPath);

            // Convert to icon
            IntPtr hIcon = bitmap.GetHicon();
            return Icon.FromHandle(hIcon);
        }
        catch (Exception ex)
        {
            Logger.Log($"Failed to create LS icon: {ex.Message}");
            return null;
        }
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Settings...", null, (_, _) => _onShowSettings());
        menu.Items.Add("About...", null, (_, _) => _onShowAbout());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => _onExit());
        return menu;
    }

    public void ShowBalloon(string message)
    {
        if (_notifyIcon == null)
        {
            return;
        }

        _notifyIcon.BalloonTipTitle = "LocalScribe";
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.ShowBalloonTip(3000);
    }

    public void Dispose()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}
