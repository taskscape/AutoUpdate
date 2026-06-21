using System.Text.Json;

namespace AutoUpdater.Core.Configuration;

/// <summary>
/// Loads the author-provided JSON configuration file (spec §6.1).
/// </summary>
public static class UpdaterConfigurationLoader
{
    public const string DefaultFileName = "updater.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    /// <summary>
    /// Loads configuration from the given path, defaulting to "updater.json" beside the host executable.
    /// </summary>
    public static UpdaterConfiguration Load(string? path = null)
    {
        path ??= Path.Combine(AppContext.BaseDirectory, DefaultFileName);

        if (!File.Exists(path))
            throw new FileNotFoundException($"Updater configuration not found at '{path}'.", path);

        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<UpdaterConfiguration>(json, SerializerOptions)
                     ?? throw new InvalidOperationException($"Updater configuration at '{path}' could not be parsed.");

        var (isValid, error) = config.Validate();
        if (!isValid)
            throw new InvalidOperationException($"Invalid updater configuration: {error}");

        return config;
    }
}
