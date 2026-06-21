using System.Diagnostics;
using System.Runtime.Versioning;
using AutoUpdater.Core.Configuration;
using AutoUpdater.Core.Ipc;
using AutoUpdater.Core.Logging;
using AutoUpdater.Core.State;

namespace AutoUpdater.Runner;

/// <summary>
/// Drives a single installation attempt for one release tag (spec §4–§5):
/// shutdown → silent install → restart/recovery → state update.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class UpdateOrchestrator
{
    private readonly IUpdaterLogger _logger;

    public UpdateOrchestrator(IUpdaterLogger logger) => _logger = logger;

    public async Task<int> RunAsync(UpdateRequest request)
    {
        var state = new DeploymentStateStore(request.ApplicationId);
        _logger.Info($"Runner starting for '{request.ApplicationId}' tag '{request.Tag}' (attempt {request.AttemptNumber}/{DeploymentStateStore.MaxAttempts}).");

        try
        {
            // ── 1. Shutdown (spec §4.3) ───────────────────────────────────────────────
            Shutdown(request);

            // ── 2. Silent InnoSetup install (spec §4.2) ──────────────────────────────
            var installOk = await RunInstallerAsync(request);
            if (!installOk)
            {
                // Spec §5.2/§5.3: attempt already registered by the host; just bring the app back
                // and let the next startup retry (or abandon at attempt 3).
                _logger.Error($"Install failed for tag '{request.Tag}'. Will retry on next startup if attempts remain.");
                Recover(request);
                return 1;
            }

            // ── 3. Mark success + restart (spec §5.1) ────────────────────────────────
            state.MarkCompleted(request.Tag, setAsCurrent: true);
            _logger.Info($"Install succeeded. Tag '{request.Tag}' is now current.");

            Recover(request);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.Error("Runner failed.", ex);
            TryRecover(request);
            return 1;
        }
    }

    private void Shutdown(UpdateRequest request)
    {
        switch (request.ApplicationType)
        {
            case ApplicationType.WebApp:
                // Spec §4.3: drop app_offline.htm to gracefully unload IIS and release locks.
                AppOffline.Enable(request.WebRootPath!, _logger);
                break;

            case ApplicationType.Desktop:
            case ApplicationType.Console:
                // Spec §4.3: force-close without warning the user.
                ProcessControl.ForceKill(request.HostProcessId, _logger);
                break;
        }
    }

    private async Task<bool> RunInstallerAsync(UpdateRequest request)
    {
        // Spec §4.2: silent InnoSetup execution, overriding the install directory.
        var args = new[]
        {
            "/VERYSILENT",
            "/SUPPRESSMSGBOXES",
            "/NORESTART",
            "/NOCANCEL",
            $"/DIR=\"{request.InstallDirectory}\"",
        };

        var psi = new ProcessStartInfo
        {
            FileName = request.InstallerPath,
            Arguments = string.Join(' ', args),
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        _logger.Info($"Running installer: {psi.FileName} {psi.Arguments}");
        using var proc = Process.Start(psi);
        if (proc is null)
        {
            _logger.Error("Failed to start installer process.");
            return false;
        }

        await proc.WaitForExitAsync();
        _logger.Info($"Installer exited with code {proc.ExitCode}.");
        return proc.ExitCode == 0;
    }

    private void Recover(UpdateRequest request)
    {
        switch (request.ApplicationType)
        {
            case ApplicationType.WebApp:
                // Spec §5.1: delete app_offline.htm to bring the site back online.
                AppOffline.Disable(request.WebRootPath!, _logger);
                break;

            case ApplicationType.Desktop:
            case ApplicationType.Console:
                // Spec §5.1: relaunch into the user session, preserving original startup args.
                SessionLauncher.LaunchInActiveSession(
                    request.RestartExecutablePath!, request.RestartArguments, request.InstallDirectory, _logger);
                break;
        }
    }

    private void TryRecover(UpdateRequest request)
    {
        try { Recover(request); }
        catch (Exception ex) { _logger.Error("Recovery failed.", ex); }
    }
}
