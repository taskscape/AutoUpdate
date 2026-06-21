using AutoUpdater.Core.Ipc;
using AutoUpdater.Core.Logging;
using AutoUpdater.Runner;

// Temporary runner extracted to %TEMP% and launched by the SYSTEM service (spec §2.1).
// It orchestrates: shutdown → silent InnoSetup install → restart, then self-deletes.

var requestPath = GetArg(args, "--request");
if (requestPath is null || !File.Exists(requestPath))
{
    Console.Error.WriteLine("Usage: AutoUpdater.Runner --request <path-to-request.json>");
    return 2;
}

var request = IpcContract.Deserialize<UpdateRequest>(File.ReadAllText(requestPath));
if (request is null)
{
    Console.Error.WriteLine("Invalid request file.");
    return 2;
}

var logger = new FileAndEventLogLogger(request.ApplicationId, "runner");
var orchestrator = new UpdateOrchestrator(logger);

var exitCode = await orchestrator.RunAsync(request);

// Spec §2.1: the runner self-deletes after finishing. We schedule deletion of our own files so
// the locked exe can be removed once the process exits.
SelfCleanup.Schedule(requestPath, AppContext.BaseDirectory, logger);
return exitCode;

static string? GetArg(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
        if (args[i] == name)
            return args[i + 1];
    return null;
}
