using System.Windows;
using System.Windows.Media;
using GramCloneClient.Backend;
using GramCloneClient.Services;
using MediaColor = System.Windows.Media.Color;

namespace GramCloneClient.Windows;

/// <summary>
/// About dialog showing version information.
/// </summary>
public partial class AboutWindow : Window
{
    private readonly BackendClient _backendClient;

    public AboutWindow(BackendClient backendClient)
    {
        InitializeComponent();
        _backendClient = backendClient;

        ClientVersionText.Text = AppVersion.Current;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await LoadBackendVersionAsync();
    }

    private async Task LoadBackendVersionAsync()
    {
        try
        {
            var health = await _backendClient.GetHealthAsync();

            if (health.Status == "offline")
            {
                BackendVersionText.Text = "Offline";
                ShowVersionStatus("Backend is not running", isWarning: true);
            }
            else if (health.Version == "unknown")
            {
                BackendVersionText.Text = "Unknown";
                ShowVersionStatus("Could not determine backend version", isWarning: true);
            }
            else
            {
                BackendVersionText.Text = health.Version;

                if (!AppVersion.IsCompatibleWith(health.Version))
                {
                    ShowVersionStatus(
                        $"Version mismatch! Client: {AppVersion.SemanticVersion}, Backend: {health.Version}",
                        isWarning: true);
                }
                else
                {
                    ShowVersionStatus("Versions are compatible", isWarning: false);
                }
            }
        }
        catch
        {
            BackendVersionText.Text = "Error";
            ShowVersionStatus("Failed to connect to backend", isWarning: true);
        }
    }

    private void ShowVersionStatus(string message, bool isWarning)
    {
        VersionStatusBorder.Visibility = Visibility.Visible;
        VersionStatusText.Text = message;

        if (isWarning)
        {
            VersionStatusBorder.Background = new SolidColorBrush(MediaColor.FromRgb(255, 243, 205));
            VersionStatusText.Foreground = new SolidColorBrush(MediaColor.FromRgb(133, 100, 4));
        }
        else
        {
            VersionStatusBorder.Background = new SolidColorBrush(MediaColor.FromRgb(212, 237, 218));
            VersionStatusText.Foreground = new SolidColorBrush(MediaColor.FromRgb(21, 87, 36));
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
