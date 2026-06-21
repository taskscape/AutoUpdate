using System.Diagnostics;
using AutoUpdater.Core.Logging;

namespace AutoUpdater.Runner;

/// <summary>Force-closes the running host process for Desktop/Console updates (spec §4.3).</summary>
public static class ProcessControl
{
    public static void ForceKill(int processId, IUpdaterLogger logger, TimeSpan? waitForExit = null)
    {
        try
        {
            using var proc = Process.GetProcessById(processId);
            logger.Info($"Force-closing host process {processId} ({proc.ProcessName}).");
            proc.Kill(entireProcessTree: true);
            proc.WaitForExit((int)(waitForExit ?? TimeSpan.FromSeconds(15)).TotalMilliseconds);
        }
        catch (ArgumentException)
        {
            // Process already exited — nothing to do.
            logger.Info($"Host process {processId} already exited.");
        }
        catch (Exception ex)
        {
            logger.Warn($"Could not kill process {processId}.", ex);
        }
    }
}
