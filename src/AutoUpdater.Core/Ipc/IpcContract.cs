using System.Text.Json;

namespace AutoUpdater.Core.Ipc;

/// <summary>Shared constants and helpers for the host ↔ service named-pipe protocol (spec §2.2).</summary>
public static class IpcContract
{
    /// <summary>
    /// Per-application pipe name. A dedicated service runs per app (spec §2.1), so the pipe is
    /// namespaced by application id.
    /// </summary>
    public static string PipeName(string applicationId) => $"AutoUpdater.{applicationId}";

    public static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);

    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Json);
}
