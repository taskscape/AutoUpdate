using System.Text.Json.Serialization;

namespace AutoUpdater.Core.GitHub;

/// <summary>Subset of the GitHub "release" object we care about (spec §3.4).</summary>
public sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("draft")]
    public bool Draft { get; set; }

    [JsonPropertyName("prerelease")]
    public bool PreRelease { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubAsset> Assets { get; set; } = new();
}

public sealed class GitHubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string ApiUrl { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    public bool IsExecutableInstaller =>
        Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
}
