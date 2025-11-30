using System;
using System.Drawing;
using System.Windows.Forms;

namespace GramCloneClient.Services;

/// <summary>
/// Hosts the system tray icon and context menu.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly Action _onShowSettings;
    private readonly Action _onExit;
    private NotifyIcon? _notifyIcon;

    public TrayIconService(Action onShowSettings, Action onExit)
    {
        _onShowSettings = onShowSettings;
        _onExit = onExit;
    }

    public void Initialize()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Information,
            Text = "LocalScribe Assistant",
            Visible = true,
            ContextMenuStrip = BuildContextMenu()
        };
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Settings...", null, (_, _) => _onShowSettings());
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
