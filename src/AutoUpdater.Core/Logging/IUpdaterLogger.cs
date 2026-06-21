namespace AutoUpdater.Core.Logging;

public enum UpdaterLogLevel
{
    Info,
    Warning,
    Error,
}

/// <summary>Minimal logging abstraction used across the library, service, and runner (spec §7).</summary>
public interface IUpdaterLogger
{
    void Log(UpdaterLogLevel level, string message, Exception? exception = null);
}

public static class UpdaterLoggerExtensions
{
    public static void Info(this IUpdaterLogger logger, string message) =>
        logger.Log(UpdaterLogLevel.Info, message);

    public static void Warn(this IUpdaterLogger logger, string message, Exception? ex = null) =>
        logger.Log(UpdaterLogLevel.Warning, message, ex);

    public static void Error(this IUpdaterLogger logger, string message, Exception? ex = null) =>
        logger.Log(UpdaterLogLevel.Error, message, ex);
}
