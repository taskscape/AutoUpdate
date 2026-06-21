using System.IO.Pipes;

namespace AutoUpdater.Core.Ipc;

/// <summary>
/// Host-side named-pipe client used to hand an <see cref="UpdateRequest"/> to the SYSTEM service
/// (spec §2.2). One message + one response, then the connection closes.
/// </summary>
public sealed class UpdaterServiceClient
{
    private readonly string _applicationId;

    public UpdaterServiceClient(string applicationId) => _applicationId = applicationId;

    public async Task<UpdateResponse> SendAsync(UpdateRequest request, TimeSpan? connectTimeout = null, CancellationToken cancellationToken = default)
    {
        var timeout = connectTimeout ?? TimeSpan.FromSeconds(10);

        using var pipe = new NamedPipeClientStream(".", IpcContract.PipeName(_applicationId),
            PipeDirection.InOut, PipeOptions.Asynchronous);

        await pipe.ConnectAsync((int)timeout.TotalMilliseconds, cancellationToken).ConfigureAwait(false);

        using var reader = new StreamReader(pipe, leaveOpen: true);
        await using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };

        await writer.WriteLineAsync(IpcContract.Serialize(request)).ConfigureAwait(false);

        var responseLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        return (responseLine is null ? null : IpcContract.Deserialize<UpdateResponse>(responseLine))
               ?? new UpdateResponse { Accepted = false, Message = "No response from service." };
    }
}
