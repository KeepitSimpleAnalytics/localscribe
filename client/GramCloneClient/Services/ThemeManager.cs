using System;
using System.Windows;
using GramCloneClient.Models;
using WpfApplication = System.Windows.Application;

namespace GramCloneClient.Services;

/// <summary>
/// Manages application theming (light/dark mode).
/// </summary>
public static class ThemeManager
{
    private static AppTheme _currentTheme = AppTheme.Light;

    /// <summary>
    /// Gets the currently applied theme.
    /// </summary>
    public static AppTheme CurrentTheme => _currentTheme;

    /// <summary>
    /// Applies the specified theme to the application.
    /// </summary>
    public static void ApplyTheme(AppTheme theme)
    {
        _currentTheme = theme;

        var app = WpfApplication.Current;
        if (app == null) return;

        // Clear existing theme dictionaries
        app.Resources.MergedDictionaries.Clear();

        // Load the appropriate theme
        var themePath = theme == AppTheme.Dark
            ? "Themes/DarkTheme.xaml"
            : "Themes/LightTheme.xaml";

        try
        {
            var themeDict = new ResourceDictionary
            {
                Source = new Uri(themePath, UriKind.Relative)
            };
            app.Resources.MergedDictionaries.Add(themeDict);
        }
        catch (Exception ex)
        {
            Logger.Log($"Failed to load theme {theme}: {ex.Message}");
            // Fall back to light theme if dark fails
            if (theme == AppTheme.Dark)
            {
                ApplyTheme(AppTheme.Light);
            }
        }
    }

    /// <summary>
    /// Toggles between light and dark themes.
    /// </summary>
    public static void ToggleTheme()
    {
        var newTheme = _currentTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light;
        ApplyTheme(newTheme);
    }
}
