using System.Text.Json.Serialization;
using AutoUpdater.Core.Prompting;

namespace AutoUpdater.Core.Configuration;

/// <summary>
/// Author-provided JSON configuration that ships with each updatable application.
/// Spec §6.1.
/// </summary>
public sealed class UpdaterConfiguration
{
    /// <summary>Target GitHub repository URL, e.g. https://github.com/owner/repo.</summary>
    [JsonPropertyName("repositoryUrl")]
    public string RepositoryUrl { get; set; } = string.Empty;

    /// <summary>
    /// GitHub authentication token. Stored in plaintext for the current iteration (spec §3.3).
    /// Used to bypass the unauthenticated rate limit and access private repositories.
    /// </summary>
    [JsonPropertyName("githubToken")]
    public string GitHubToken { get; set; } = string.Empty;

    /// <summary>Shutdown/restart strategy. Declared explicitly by the app author (spec §4.3).</summary>
    [JsonPropertyName("applicationType")]
    public ApplicationType ApplicationType { get; set; } = ApplicationType.Desktop;

    /// <summary>
    /// Whether updates apply silently (default) or after a user prompt (spec §3.5). Prompt mode is
    /// intended for interactive desktop/WinForms apps and requires the host to supply a prompt UI.
    /// </summary>
    [JsonPropertyName("updateMode")]
    public UpdateMode UpdateMode { get; set; } = UpdateMode.Silent;

    /// <summary>
    /// The currently deployed install location. Passed to InnoSetup as /DIR= to override the
    /// installation directory (spec §4.2).
    /// </summary>
    [JsonPropertyName("installDirectory")]
    public string InstallDirectory { get; set; } = string.Empty;

    /// <summary>
    /// WebApps only: directory where app_offline.htm is created/deleted (spec §4.3 / §5.1).
    /// </summary>
    [JsonPropertyName("webRootPath")]
    public string? WebRootPath { get; set; }

    /// <summary>
    /// Optional name of the per-app Windows Service used for the privileged install.
    /// If omitted, a name is derived from the application identifier.
    /// </summary>
    [JsonPropertyName("serviceName")]
    public string? ServiceName { get; set; }

    /// <summary>Stable identifier for this application, used for state/log file naming.</summary>
    [JsonPropertyName("applicationId")]
    public string ApplicationId { get; set; } = string.Empty;

    public (bool IsValid, string? Error) Validate()
    {
        if (string.IsNullOrWhiteSpace(RepositoryUrl))
            return (false, "repositoryUrl is required.");
        if (string.IsNullOrWhiteSpace(InstallDirectory))
            return (false, "installDirectory is required.");
        if (string.IsNullOrWhiteSpace(ApplicationId))
            return (false, "applicationId is required.");
        if (ApplicationType == ApplicationType.WebApp && string.IsNullOrWhiteSpace(WebRootPath))
            return (false, "webRootPath is required for WebApp application types.");
        if (UpdateMode == UpdateMode.Prompt && ApplicationType == ApplicationType.WebApp)
            return (false, "updateMode 'Prompt' is only valid for interactive Desktop/Console apps, not WebApps.");
        return (true, null);
    }
}
