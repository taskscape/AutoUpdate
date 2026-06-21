using AutoUpdater.Core.Ipc;
using AutoUpdater.Core.Logging;
using Microsoft.Extensions.Hosting;

namespace AutoUpdater.Service;

/// <summary>
/// Background service that runs as SYSTEM and listens on the per-app named pipe for update
/// requests from the host (spec §2.1, §2.2). On a valid request it extracts the temporary runner
/// to %TEMP% and launches it to perform the privileged install (spec §2.1).
/// </summary>
public sealed class UpdaterWorker : BackgroundService
{
    private readonly ServiceOptions _options;
    private readonly IUpdaterLogger _logger;
    private readonly PipeServer _pipeServer;
    private readonly RunnerLauncher _runnerLauncher;

    public UpdaterWorker(ServiceOptions options)
    {
        _options = options;
        _logger = new FileAndEventLogLogger(options.ApplicationId, "service");
        _runnerLauncher = new RunnerLauncher(_logger);
        _pipeServer = new PipeServer(options.ApplicationId, _logger, HandleRequestAsync);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Info($"AutoUpdater service started for '{_options.ApplicationId}'. Listening on pipe '{IpcContract.PipeName(_options.ApplicationId)}'.");
        await _pipeServer.RunAsync(stoppingToken).ConfigureAwait(false);
    }

    private Task<UpdateResponse> HandleRequestAsync(UpdateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (!string.Equals(request.ApplicationId, _options.ApplicationId, StringComparison.Ordinal))
                return Task.FromResult(new UpdateResponse { Accepted = false, Message = "Application id mismatch." });

            if (!File.Exists(request.InstallerPath))
                return Task.FromResult(new UpdateResponse { Accepted = false, Message = "Installer not found." });

            _logger.Info($"Update request received for tag '{request.Tag}' (attempt {request.AttemptNumber}). Launching runner.");

            // Extract the runner to %TEMP% and launch it; it owns shutdown/install/restart (spec §2.1).
            _runnerLauncher.LaunchForRequest(request);

            return Task.FromResult(new UpdateResponse { Accepted = true, Message = "Runner launched." });
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to handle update request.", ex);
            return Task.FromResult(new UpdateResponse { Accepted = false, Message = ex.Message });
        }
    }
}
