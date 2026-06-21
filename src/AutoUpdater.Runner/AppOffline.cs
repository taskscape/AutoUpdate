using AutoUpdater.Core.Logging;

namespace AutoUpdater.Runner;

/// <summary>
/// Manages the IIS app_offline.htm marker for WebApp updates (spec §4.3 / §5.1).
/// </summary>
public static class AppOffline
{
    private const string FileName = "app_offline.htm";

    private const string DefaultContent =
        "<!DOCTYPE html><html><head><title>Updating…</title></head>" +
        "<body><h1>This application is being updated.</h1>" +
        "<p>Please try again in a moment.</p></body></html>";

    public static void Enable(string webRoot, IUpdaterLogger logger)
    {
        var path = Path.Combine(webRoot, FileName);
        File.WriteAllText(path, DefaultContent);
        logger.Info($"Created '{path}' — IIS unloading the application.");

        // Give IIS a moment to unload the worker process and release file locks.
        Thread.Sleep(TimeSpan.FromSeconds(3));
    }

    public static void Disable(string webRoot, IUpdaterLogger logger)
    {
        var path = Path.Combine(webRoot, FileName);
        if (File.Exists(path))
        {
            File.Delete(path);
            logger.Info($"Removed '{path}' — application back online.");
        }
    }
}
