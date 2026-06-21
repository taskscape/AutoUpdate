using System.Diagnostics;
using System.Runtime.Versioning;

namespace AutoUpdater.Core.Logging;

/// <summary>
/// Writes to a rolling log file under %ProgramData% AND the Windows Event Log (spec §7).
/// Event Log writes are best-effort: registering an event source requires admin, so failures are
/// swallowed and only the file sink is guaranteed.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FileAndEventLogLogger : IUpdaterLogger
{
    private const string EventLogName = "Application";
    private const long MaxFileBytes = 5 * 1024 * 1024; // 5 MB before roll

    private readonly object _gate = new();
    private readonly string _logFilePath;
    private readonly string _eventSource;

    public FileAndEventLogLogger(string applicationId, string component, string? rootDirectory = null)
    {
        rootDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "AutoUpdater", applicationId, "logs");
        Directory.CreateDirectory(rootDirectory);

        _logFilePath = Path.Combine(rootDirectory, $"{component}.log");
        _eventSource = $"AutoUpdater.{applicationId}";
    }

    public string LogFilePath => _logFilePath;

    public void Log(UpdaterLogLevel level, string message, Exception? exception = null)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {message}" +
                   (exception is null ? string.Empty : Environment.NewLine + exception);

        lock (_gate)
        {
            WriteFile(line);
        }

        WriteEventLog(level, line);
    }

    private void WriteFile(string line)
    {
        try
        {
            RollIfNeeded();
            File.AppendAllText(_logFilePath, line + Environment.NewLine);
        }
        catch
        {
            // Logging must never crash the updater.
        }
    }

    private void RollIfNeeded()
    {
        var info = new FileInfo(_logFilePath);
        if (info.Exists && info.Length >= MaxFileBytes)
        {
            var archived = _logFilePath + $".{DateTime.Now:yyyyMMddHHmmss}.bak";
            File.Move(_logFilePath, archived, overwrite: true);
        }
    }

    private void WriteEventLog(UpdaterLogLevel level, string line)
    {
        try
        {
            if (!EventLog.SourceExists(_eventSource))
                EventLog.CreateEventSource(_eventSource, EventLogName); // requires admin (one-time)

            var type = level switch
            {
                UpdaterLogLevel.Error => EventLogEntryType.Error,
                UpdaterLogLevel.Warning => EventLogEntryType.Warning,
                _ => EventLogEntryType.Information,
            };
            EventLog.WriteEntry(_eventSource, line, type);
        }
        catch
        {
            // Best-effort; the file sink remains authoritative.
        }
    }
}
