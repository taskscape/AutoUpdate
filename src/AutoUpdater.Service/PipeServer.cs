using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.Versioning;
using AutoUpdater.Core.Ipc;
using AutoUpdater.Core.Logging;

namespace AutoUpdater.Service;

/// <summary>
/// Named-pipe server (spec §2.2). Accepts one request per connection, dispatches it to the
/// handler, and writes back the response. Loops until the service stops.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class PipeServer
{
    private readonly string _pipeName;
    private readonly IUpdaterLogger _logger;
    private readonly Func<UpdateRequest, CancellationToken, Task<UpdateResponse>> _handler;

    public PipeServer(string applicationId, IUpdaterLogger logger,
        Func<UpdateRequest, CancellationToken, Task<UpdateResponse>> handler)
    {
        _pipeName = IpcContract.PipeName(applicationId);
        _logger = logger;
        _handler = handler;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await AcceptOneAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Error("Pipe server loop error.", ex);
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task AcceptOneAsync(CancellationToken cancellationToken)
    {
        using var server = NamedPipeServerStreamAcl.Create(
            _pipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 0,
            outBufferSize: 0,
            CreatePipeSecurity());

        await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

        using var reader = new StreamReader(server, leaveOpen: true);
        await using var writer = new StreamWriter(server, leaveOpen: true) { AutoFlush = true };

        var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (line is null)
            return;

        var request = IpcContract.Deserialize<UpdateRequest>(line);
        UpdateResponse response = request is null
            ? new UpdateResponse { Accepted = false, Message = "Malformed request." }
            : await _handler(request, cancellationToken).ConfigureAwait(false);

        await writer.WriteLineAsync(IpcContract.Serialize(response)).ConfigureAwait(false);
        if (server.IsConnected)
            server.WaitForPipeDrain();
    }

    /// <summary>
    /// Restricts the pipe: the SYSTEM service owns it; authenticated local users may connect to
    /// send a request. Tighten further (e.g. to a specific account) if required.
    /// </summary>
    private static PipeSecurity CreatePipeSecurity()
    {
        var security = new PipeSecurity();
        var users = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
        security.AddAccessRule(new PipeAccessRule(users,
            PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance, AccessControlType.Allow));

        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        security.AddAccessRule(new PipeAccessRule(system, PipeAccessRights.FullControl, AccessControlType.Allow));
        return security;
    }
}
