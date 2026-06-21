using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace AutoUpdater.Core.GitHub;

/// <summary>
/// Default <see cref="IGitHubReleaseClient"/> backed by the GitHub REST API v3.
/// Uses an authenticated token to raise rate limits and reach private repos (spec §3.3).
/// </summary>
public sealed class GitHubReleaseClient : IGitHubReleaseClient, IDisposable
{
    private const string ApiBase = "https://api.github.com";

    private readonly HttpClient _http;
    private readonly string _owner;
    private readonly string _repo;
    private readonly bool _ownsHttpClient;

    public GitHubReleaseClient(string repositoryUrl, string? token, HttpClient? httpClient = null)
    {
        (_owner, _repo) = ParseRepository(repositoryUrl);
        _http = httpClient ?? new HttpClient();
        _ownsHttpClient = httpClient is null;

        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AutoUpdater/0.1");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        _http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        if (!string.IsNullOrWhiteSpace(token))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<GitHubRelease?> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        // /releases/latest excludes drafts and pre-releases by definition (spec §3.4).
        var url = $"{ApiBase}/repos/{_owner}/{_repo}/releases/latest";
        using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GitHubRelease>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DownloadAssetAsync(GitHubAsset asset, string destinationPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        // Use the asset API URL with the octet-stream Accept header so private-repo downloads work.
        using var request = new HttpRequestMessage(HttpMethod.Get, asset.ApiUrl);
        request.Headers.Accept.Clear();
        request.Headers.Accept.ParseAdd("application/octet-stream");

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? asset.Size;
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var destination = File.Create(destinationPath);

        var buffer = new byte[81920];
        long readTotal = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            readTotal += read;
            if (total > 0)
                progress?.Report((double)readTotal / total);
        }
    }

    internal static (string Owner, string Repo) ParseRepository(string repositoryUrl)
    {
        if (string.IsNullOrWhiteSpace(repositoryUrl))
            throw new ArgumentException("Repository URL is required.", nameof(repositoryUrl));

        var trimmed = repositoryUrl.Trim().TrimEnd('/');
        if (trimmed.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^4];

        var uri = new Uri(trimmed, UriKind.Absolute);
        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
            throw new ArgumentException($"Could not parse owner/repo from '{repositoryUrl}'.", nameof(repositoryUrl));

        return (segments[0], segments[1]);
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
            _http.Dispose();
    }
}
