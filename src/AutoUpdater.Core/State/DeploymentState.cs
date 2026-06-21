using System.Text.Json.Serialization;

namespace AutoUpdater.Core.State;

/// <summary>
/// Machine-wide deployment tracking metadata, stored separately from author config (spec §6.2).
/// Persisted under %ProgramData% so it survives reboots and is shared by the host + service.
/// </summary>
public sealed class DeploymentState
{
    /// <summary>The release tag currently deployed/running.</summary>
    [JsonPropertyName("currentTag")]
    public string? CurrentTag { get; set; }

    /// <summary>Per-tag installation attempt counts (spec §5.3).</summary>
    [JsonPropertyName("retryCounts")]
    public Dictionary<string, int> RetryCounts { get; set; } = new();

    /// <summary>
    /// Tags that are "completed" — either successfully installed or abandoned after 3 failed
    /// attempts. These are never attempted again (spec §5.3, prevents boot loops).
    /// </summary>
    [JsonPropertyName("completedTags")]
    public List<string> CompletedTags { get; set; } = new();

    public bool IsCompleted(string tag) => CompletedTags.Contains(tag);

    public int GetAttempts(string tag) => RetryCounts.TryGetValue(tag, out var n) ? n : 0;
}
