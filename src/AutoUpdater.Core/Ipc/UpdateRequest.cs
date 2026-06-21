using System.Text.Json.Serialization;
using AutoUpdater.Core.Configuration;

namespace AutoUpdater.Core.Ipc;

/// <summary>
/// Message sent from the host application to its SYSTEM service over a named pipe (spec §2.2),
/// describing exactly one privileged installation attempt.
/// </summary>
public sealed class UpdateRequest
{
    [JsonPropertyName("applicationId")]
    public string ApplicationId { get; set; } = string.Empty;

    /// <summary>The release tag being deployed (spec §3.4).</summary>
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = string.Empty;

    /// <summary>Full path to the downloaded InnoSetup installer (spec §4.1).</summary>
    [JsonPropertyName("installerPath")]
    public string InstallerPath { get; set; } = string.Empty;

    /// <summary>Target install directory passed to InnoSetup as /DIR= (spec §4.2).</summary>
    [JsonPropertyName("installDirectory")]
    public string InstallDirectory { get; set; } = string.Empty;

    [JsonPropertyName("applicationType")]
    public ApplicationType ApplicationType { get; set; }

    /// <summary>WebApps only: web root for app_offline.htm (spec §4.3 / §5.1).</summary>
    [JsonPropertyName("webRootPath")]
    public string? WebRootPath { get; set; }

    /// <summary>Desktop/Console: executable to relaunch after install (spec §5.1).</summary>
    [JsonPropertyName("restartExecutablePath")]
    public string? RestartExecutablePath { get; set; }

    /// <summary>Desktop/Console: original startup arguments to preserve on restart (spec §5.1).</summary>
    [JsonPropertyName("restartArguments")]
    public string[] RestartArguments { get; set; } = Array.Empty<string>();

    /// <summary>PID of the host process so the runner can wait for / kill it cleanly.</summary>
    [JsonPropertyName("hostProcessId")]
    public int HostProcessId { get; set; }

    /// <summary>Which attempt this is for the tag (1..3), for logging (spec §5.3).</summary>
    [JsonPropertyName("attemptNumber")]
    public int AttemptNumber { get; set; }
}

/// <summary>Service acknowledgement returned to the host over the pipe.</summary>
public sealed class UpdateResponse
{
    [JsonPropertyName("accepted")]
    public bool Accepted { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
