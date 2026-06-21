using System.Diagnostics;
using System.Reflection;
using System.Runtime.Versioning;
using AutoUpdater.Core.Configuration;
using AutoUpdater.Core.GitHub;
using AutoUpdater.Core.Ipc;
using AutoUpdater.Core.Logging;
using AutoUpdater.Core.Prompting;
using AutoUpdater.Core.State;

namespace AutoUpdater.Core;

/// <summary>
/// Primary entry point referenced by host applications (spec §1).
///
/// Typical usage at host startup:
/// <code>
/// _ = UpdaterClient.FromConfigFile().CheckForUpdatesAsync(args);
/// </code>
///
/// The check runs asynchronously and never blocks startup (spec §3.2). When an update is found it
/// hands off immediately to the SYSTEM service for installation on this same launch.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class UpdaterClient
{
    private readonly UpdaterConfiguration _config;
    private readonly DeploymentStateStore _state;
    private readonly IUpdaterLogger _logger;
    private readonly Func<IGitHubReleaseClient> _gitHubFactory;

    public UpdaterClient(
        UpdaterConfiguration config,
        DeploymentStateStore? state = null,
        IUpdaterLogger? logger = null,
        Func<IGitHubReleaseClient>? gitHubFactory = null)
    {
        _config = config;
        _state = state ?? new DeploymentStateStore(config.ApplicationId);
        _logger = logger ?? new FileAndEventLogLogger(config.ApplicationId, "client");
        _gitHubFactory = gitHubFactory ?? (() => new GitHubReleaseClient(config.RepositoryUrl, config.GitHubToken));
    }

    public static UpdaterClient FromConfigFile(string? path = null)
    {
        var config = UpdaterConfigurationLoader.Load(path);
        return new UpdaterClient(config);
    }

    /// <summary>
    /// Runs the full startup update flow. Safe to fire-and-forget; all errors are logged and
    /// swallowed so a failed check never affects the host.
    /// </summary>
    /// <param name="hostStartupArgs">Original startup args, preserved across restart (spec §5.1).</param>
    /// <param name="prompt">
    /// Optional confirmation UI. Required when <see cref="UpdaterConfiguration.UpdateMode"/> is
    /// <see cref="UpdateMode.Prompt"/> (spec §3.5); ignored in silent mode. If prompt mode is
    /// configured but no prompt is supplied, the update is deferred (fail-safe: never close
    /// without asking).
    /// </param>
    public async Task<UpdateCheckResult> CheckForUpdatesAsync(
        string[]? hostStartupArgs = null,
        IUpdatePrompt? prompt = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Spec §2.4: Release-mode lock.
            if (!BuildConfiguration.IsReleaseBuild())
            {
                _logger.Info("Debug build detected — auto-update disabled.");
                return UpdateCheckResult.Disabled;
            }

            using var gitHub = _gitHubFactory() as IDisposable;
            var client = (IGitHubReleaseClient)gitHub!;

            var latest = await client.GetLatestReleaseAsync(cancellationToken).ConfigureAwait(false);
            if (latest is null)
            {
                _logger.Info("No 'latest' release found.");
                return UpdateCheckResult.UpToDate;
            }

            var tag = latest.TagName;
            var state = _state.Load();

            if (string.Equals(state.CurrentTag, tag, StringComparison.Ordinal))
                return UpdateCheckResult.UpToDate;

            if (state.IsCompleted(tag))
            {
                _logger.Info($"Tag '{tag}' is marked completed/ignored — skipping (spec §5.3).");
                return UpdateCheckResult.UpToDate;
            }

            var installer = latest.Assets.SingleOrDefault(a => a.IsExecutableInstaller);
            if (installer is null)
            {
                _logger.Warn($"Release '{tag}' does not contain exactly one .exe installer asset.");
                return UpdateCheckResult.Failed;
            }

            // Spec §3.5: in Prompt mode, ask the user BEFORE downloading or shutting anything down.
            if (_config.UpdateMode == UpdateMode.Prompt)
            {
                if (prompt is null)
                {
                    _logger.Warn("UpdateMode is 'Prompt' but no IUpdatePrompt was supplied — deferring update (will not close without asking).");
                    return UpdateCheckResult.Deferred;
                }

                var info = new UpdatePromptInfo(_config.ApplicationId, tag, state.CurrentTag);
                var confirmed = await prompt.ConfirmUpdateAsync(info, cancellationToken).ConfigureAwait(false);
                if (!confirmed)
                {
                    _logger.Info($"User declined the update to '{tag}'. Deferring to next startup.");
                    return UpdateCheckResult.Deferred;
                }
            }

            _logger.Info($"New release '{tag}' detected. Downloading installer…");

            var downloadDir = Path.Combine(Path.GetTempPath(), "AutoUpdater", _config.ApplicationId);
            Directory.CreateDirectory(downloadDir);
            var installerPath = Path.Combine(downloadDir, installer.Name);

            await client.DownloadAssetAsync(installer, installerPath,
                new Progress<double>(p => _logger.Info($"Download {p:P0}")), cancellationToken).ConfigureAwait(false);

            // Spec §5.3: register the install attempt only once we're committed to installing
            // (so a declined prompt or failed download never consumes one of the 3 attempts).
            if (!_state.RegisterAttempt(tag, out var attempt))
            {
                _logger.Warn($"Tag '{tag}' exhausted {DeploymentStateStore.MaxAttempts} attempts — abandoned.");
                return UpdateCheckResult.Failed;
            }

            _logger.Info($"Installer downloaded. Attempt {attempt}/{DeploymentStateStore.MaxAttempts}. Handing off to SYSTEM service.");

            var request = BuildRequest(tag, installerPath, attempt, hostStartupArgs);
            var response = await new UpdaterServiceClient(_config.ApplicationId).SendAsync(request, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (!response.Accepted)
            {
                _logger.Error($"Service rejected update request: {response.Message}");
                return UpdateCheckResult.Failed;
            }

            // Spec §3.2: immediate handoff. The service+runner now own shutdown/install/restart.
            _logger.Info("Service accepted the update. Host will be shut down by the runner.");
            return UpdateCheckResult.UpdateStarted;
        }
        catch (Exception ex)
        {
            _logger.Error("Update check failed.", ex);
            return UpdateCheckResult.Failed;
        }
    }

    private UpdateRequest BuildRequest(string tag, string installerPath, int attempt, string[]? hostStartupArgs)
    {
        var entryExe = Process.GetCurrentProcess().MainModule?.FileName
                       ?? Assembly.GetEntryAssembly()?.Location;

        return new UpdateRequest
        {
            ApplicationId = _config.ApplicationId,
            Tag = tag,
            InstallerPath = installerPath,
            InstallDirectory = _config.InstallDirectory,
            ApplicationType = _config.ApplicationType,
            WebRootPath = _config.WebRootPath,
            RestartExecutablePath = entryExe,
            RestartArguments = hostStartupArgs ?? Array.Empty<string>(),
            HostProcessId = Environment.ProcessId,
            AttemptNumber = attempt,
        };
    }
}

public enum UpdateCheckResult
{
    /// <summary>Already on the latest tag, or nothing to do.</summary>
    UpToDate,

    /// <summary>An update was found and the install handoff started (spec §3.2).</summary>
    UpdateStarted,

    /// <summary>
    /// An update was available but deferred this run — the user declined the prompt, or prompt
    /// mode was configured without a prompt handler (spec §3.5). It will be offered again next startup.
    /// </summary>
    Deferred,

    /// <summary>Auto-update disabled (Debug build, spec §2.4).</summary>
    Disabled,

    /// <summary>The check or handoff failed (logged).</summary>
    Failed,
}
