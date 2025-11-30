using System;
using System.IO;

namespace GramCloneClient.Services
{
    public static class Logger
    {
        private static string _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "client_debug.log");

        public static void Log(string message)
        {
            try
            {
                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {message}{Environment.NewLine}";
                File.AppendAllText(_logPath, line);
                System.Diagnostics.Debug.Write(line);
            }
            catch
            {
                // Swallow logging errors
            }
        }
    }
}
