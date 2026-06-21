using System.Diagnostics;
using System.Reflection;
using AutoUpdater.Core.Ipc;
using AutoUpdater.Core.Logging;

namespace AutoUpdater.Service;

/// <summary>
/// Extracts the temporary "runner" executable to %TEMP% and launches it (spec §2.1). The runner
/// is shipped as an embedded resource so it can be written out fresh each time (the running
/// service/host .dll/.exe files are locked, but the runner is not).
/// </summary>
public sealed class RunnerLauncher
{
    // Resource name of the embedded runner. Wired up during build/packaging (EmbedRunner=true).
    private const string RunnerResourceName = "AutoUpdater.Service.runner.AutoUpdater.Runner.exe";
    private const string RunnerFileName = "AutoUpdater.Runner.exe";

    private readonly IUpdaterLogger _logger;

    public RunnerLauncher(IUpdaterLogger logger) => _logger = logger;

    public void LaunchForRequest(UpdateRequest request)
    {
        var workDir = Path.Combine(Path.GetTempPath(), "AutoUpdater", request.ApplicationId, "runner");
        Directory.CreateDirectory(workDir);

        var runnerPath = ExtractRunner(workDir);

        // Hand the request to the runner via a temp JSON file (avoids long/escaped command lines).
        var requestPath = Path.Combine(workDir, $"request-{request.Tag}-{Guid.NewGuid():N}.json");
        File.WriteAllText(requestPath, IpcContract.Serialize(request));

        var psi = new ProcessStartInfo
        {
            FileName = runnerPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workDir,
        };
        psi.ArgumentList.Add("--request");
        psi.ArgumentList.Add(requestPath);

        _logger.Info($"Launching runner '{runnerPath}' with request '{requestPath}'.");
        Process.Start(psi);
    }

    private string ExtractRunner(string workDir)
    {
        var destination = Path.Combine(workDir, RunnerFileName);

        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(RunnerResourceName);
        if (stream is not null)
        {
            using var file = File.Create(destination);
            stream.CopyTo(file);
            return destination;
        }

        // Fallback (dev/debug): use a runner sitting next to the service binary.
        var sideBySide = Path.Combine(AppContext.BaseDirectory, RunnerFileName);
        if (File.Exists(sideBySide))
        {
            File.Copy(sideBySide, destination, overwrite: true);
            return destination;
        }

        throw new FileNotFoundException(
            $"Runner not found. Embed '{RunnerResourceName}' or place '{RunnerFileName}' beside the service.");
    }
}
