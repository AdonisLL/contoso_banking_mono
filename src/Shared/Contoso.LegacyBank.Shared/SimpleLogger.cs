using System;
using System.IO;

namespace Contoso.LegacyBank.Shared
{
    /// <summary>
    /// A deliberately simple logger (console + a rolling daily text file) that matches what
    /// most of these legacy services really had: no structured logging framework, just
    /// timestamped lines. The modernization target is App Insights / structured logging.
    /// </summary>
    public sealed class SimpleLogger
    {
        private readonly string _component;
        private readonly object _sync = new object();

        public SimpleLogger(string component)
        {
            _component = component ?? "App";
            Directory.CreateDirectory(AppPaths.LogsRoot);
        }

        public void Info(string message) => Write("INFO", message);

        public void Warn(string message) => Write("WARN", message);

        public void Error(string message, Exception ex = null) =>
            Write("ERROR", ex == null ? message : $"{message} :: {ex}");

        private void Write(string level, string message)
        {
            var line = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} [{level}] ({_component}) {message}";
            lock (_sync)
            {
                Console.WriteLine(line);
                var logFile = Path.Combine(AppPaths.LogsRoot, $"{_component}.{DateTime.UtcNow:yyyyMMdd}.log");
                File.AppendAllText(logFile, line + Environment.NewLine);
            }
        }
    }
}
