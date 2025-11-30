using System;
using System.Diagnostics;
using System.IO;

namespace GramCloneClient.Services;

public class BackendProcessManager : IDisposable
{
    private Process? _process;

    public void Start(string command, string workingDirectory)
    {
        if (_process != null && !_process.HasExited)
        {
            return; // Already running
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        // Simple parsing: assume first part is executable, rest are args
        var parts = command.Trim().Split(' ', 2);
        string fileName = parts[0];
        string arguments = parts.Length > 1 ? parts[1] : string.Empty;

        string finalWorkingDirectory = workingDirectory;
        if (string.IsNullOrWhiteSpace(finalWorkingDirectory))
        {
            finalWorkingDirectory = FindProjectRoot() ?? AppDomain.CurrentDomain.BaseDirectory;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = finalWorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true, // Run invisible
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        try
        {
            _process = Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            // Log or rethrow? For now, we can't easily log to UI from here without events.
            // We'll let the caller handle exceptions if they want, but here we just swallow or debug.
            Debug.WriteLine($"Failed to start backend: {ex.Message}");
            throw; // Rethrow so TrayApplication knows it failed
        }
    }

    private string? FindProjectRoot()
    {
        // Start from the directory where the exe is running
        var currentDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

        while (currentDir != null)
        {
            // Check if "app" folder exists and "app/main.py" exists inside it
            var appDir = Path.Combine(currentDir.FullName, "app");
            if (Directory.Exists(appDir) && File.Exists(Path.Combine(appDir, "main.py")))
            {
                return currentDir.FullName;
            }

            currentDir = currentDir.Parent;
        }

        return null;
    }

    public void Stop()
    {
        if (_process == null) return;

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true); // available in .NET Core / .NET 5+
                _process.WaitForExit(2000);
            }
        }
        catch (Exception)
        {
            // Ignore errors during shutdown
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
