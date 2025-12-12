using System.Reflection;

namespace GramCloneClient.Services;

/// <summary>
/// Provides version information for the application.
/// </summary>
public static class AppVersion
{
    private static readonly Lazy<string> _version = new(() =>
    {
        var assembly = Assembly.GetExecutingAssembly();
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (infoVersion != null)
        {
            // Remove git hash suffix if present (e.g., "0.1.0-beta+abc123" -> "0.1.0-beta")
            string version = infoVersion.InformationalVersion;
            int plusIndex = version.IndexOf('+');
            return plusIndex > 0 ? version[..plusIndex] : version;
        }

        var fileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
        if (fileVersion != null)
        {
            return fileVersion.Version;
        }

        return assembly.GetName().Version?.ToString() ?? "0.0.0";
    });

    /// <summary>
    /// Gets the application version string (e.g., "0.1.0-beta").
    /// </summary>
    public static string Current => _version.Value;

    /// <summary>
    /// Gets the semantic version without any prerelease suffix.
    /// </summary>
    public static string SemanticVersion
    {
        get
        {
            string version = Current;
            int dashIndex = version.IndexOf('-');
            return dashIndex > 0 ? version[..dashIndex] : version;
        }
    }

    /// <summary>
    /// Compares the client version with the backend version.
    /// Returns true if they match (ignoring prerelease suffixes).
    /// </summary>
    public static bool IsCompatibleWith(string? backendVersion)
    {
        if (string.IsNullOrWhiteSpace(backendVersion))
            return false;

        // Strip prerelease suffix from backend version
        int dashIndex = backendVersion.IndexOf('-');
        string backendSemVer = dashIndex > 0 ? backendVersion[..dashIndex] : backendVersion;

        return string.Equals(SemanticVersion, backendSemVer, StringComparison.OrdinalIgnoreCase);
    }
}
