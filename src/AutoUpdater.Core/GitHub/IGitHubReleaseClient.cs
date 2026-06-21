namespace AutoUpdater.Core.GitHub;

/// <summary>Discovers and downloads release assets from GitHub (spec §3, §4.1).</summary>
public interface IGitHubReleaseClient
{
    /// <summary>
    /// Returns the repository's "Latest" release, or null if none. Pre-releases and drafts are
    /// excluded by GitHub's /releases/latest endpoint (spec §3.4).
    /// </summary>
    Task<GitHubRelease?> GetLatestReleaseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads the single .exe installer asset (spec §4.1) to <paramref name="destinationPath"/>.
    /// </summary>
    Task DownloadAssetAsync(GitHubAsset asset, string destinationPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}
