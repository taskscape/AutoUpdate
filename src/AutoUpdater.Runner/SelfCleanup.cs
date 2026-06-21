using System.Diagnostics;
using AutoUpdater.Core.Logging;

namespace AutoUpdater.Runner;

/// <summary>
/// Schedules deletion of the runner's own files after it exits (spec §2.1: runner self-deletes).
/// A running .exe cannot delete itself, so we spawn a short-lived cmd that waits and then removes
/// the temp working directory.
/// </summary>
public static class SelfCleanup
{
    public static void Schedule(string requestPath, string runnerDirectory, IUpdaterLogger logger)
    {
        try
        {
            // Delete the request file immediately; defer the runner dir until after we exit.
            if (File.Exists(requestPath))
                File.Delete(requestPath);

            var script =
                $"ping 127.0.0.1 -n 3 > nul & rmdir /s /q \"{runnerDirectory}\"";

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {script}",
                CreateNoWindow = true,
                UseShellExecute = false,
            });

            logger.Info("Scheduled temp runner cleanup.");
        }
        catch (Exception ex)
        {
            logger.Warn("Failed to schedule self-cleanup.", ex);
        }
    }
}
