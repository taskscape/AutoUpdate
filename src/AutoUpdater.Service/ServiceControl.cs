using System.Diagnostics;

namespace AutoUpdater.Service;

/// <summary>
/// Install/uninstall/start/stop helpers invoked from the command line by the application's
/// InnoSetup installer (spec §2.3). Implemented over <c>sc.exe</c> so the service can register
/// itself to run as LocalSystem (spec §2.1). All operations are idempotent so they are safe to
/// re-run during silent upgrades.
/// </summary>
public static class ServiceControl
{
    public static int Install(string applicationId)
    {
        var serviceName = $"AutoUpdater.{applicationId}";
        var exePath = Environment.ProcessPath
                      ?? throw new InvalidOperationException("Could not resolve the service executable path.");

        if (ServiceExists(serviceName))
        {
            Console.WriteLine($"Service '{serviceName}' already exists — ensuring it is started.");
            Sc("start", serviceName); // ignore failure if already running
            return 0;
        }

        // binPath value (quoted exe + args) is passed as a single token; ArgumentList quotes it.
        var binPath = $"\"{exePath}\" --application-id {applicationId}";

        var create = Sc("create", serviceName,
            "binPath=", binPath,
            "obj=", "LocalSystem",
            "start=", "auto",
            "DisplayName=", $"Auto Updater ({applicationId})");

        if (create != 0)
            return create;

        Sc("description", serviceName, $"Universal Application Updater service for {applicationId}.");
        Sc("start", serviceName);
        Console.WriteLine($"Service '{serviceName}' installed and started.");
        return 0;
    }

    public static int Uninstall(string applicationId)
    {
        var serviceName = $"AutoUpdater.{applicationId}";
        if (!ServiceExists(serviceName))
        {
            Console.WriteLine($"Service '{serviceName}' is not installed — nothing to do.");
            return 0;
        }

        Sc("stop", serviceName); // best-effort
        var delete = Sc("delete", serviceName);
        Console.WriteLine($"Service '{serviceName}' removed.");
        return delete;
    }

    public static int Stop(string applicationId) => Sc("stop", $"AutoUpdater.{applicationId}");

    public static int Start(string applicationId) => Sc("start", $"AutoUpdater.{applicationId}");

    private static bool ServiceExists(string serviceName) =>
        Sc(out _, "query", serviceName) == 0;

    private static int Sc(string verb, params string[] args) => Sc(out _, Prepend(verb, args));

    private static int Sc(out string output, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "sc.exe",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi)!;
        output = proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        return proc.ExitCode;
    }

    private static string[] Prepend(string first, string[] rest)
    {
        var result = new string[rest.Length + 1];
        result[0] = first;
        Array.Copy(rest, 0, result, 1, rest.Length);
        return result;
    }
}
