using System;
using System.Windows;
using WpfApplication = System.Windows.Application;

namespace GramCloneClient;

public partial class App : WpfApplication
{
    private TrayApplication? _trayApp;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        _trayApp = new TrayApplication();
        _trayApp.Start();
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _trayApp?.Dispose();

        // Force exit - UI Automation COM threads can keep the process alive
        // even after WPF shutdown completes
        Environment.Exit(0);
    }
}
