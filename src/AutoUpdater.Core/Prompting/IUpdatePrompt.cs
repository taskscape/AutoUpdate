namespace AutoUpdater.Core.Prompting;

/// <summary>
/// Information shown to the user when asking whether to apply an update (spec §3.5).
/// </summary>
public sealed class UpdatePromptInfo
{
    public UpdatePromptInfo(string applicationId, string newTag, string? currentTag)
    {
        ApplicationId = applicationId;
        NewTag = newTag;
        CurrentTag = currentTag;
    }

    public string ApplicationId { get; }

    /// <summary>The release tag that would be installed.</summary>
    public string NewTag { get; }

    /// <summary>The currently deployed tag, if known.</summary>
    public string? CurrentTag { get; }
}

/// <summary>
/// Host-supplied confirmation UI used in <see cref="UpdateMode.Prompt"/>. Kept UI-agnostic so the
/// core library has no dependency on WinForms; a WinForms implementation ships in
/// <c>AutoUpdater.WinForms</c>.
/// </summary>
public interface IUpdatePrompt
{
    /// <summary>
    /// Returns true if the user agreed to close and update now; false to defer to a later startup.
    /// Implementations are responsible for marshaling to the UI thread.
    /// </summary>
    Task<bool> ConfirmUpdateAsync(UpdatePromptInfo info, CancellationToken cancellationToken = default);
}

/// <summary>Adapts a delegate to <see cref="IUpdatePrompt"/> for inline/lambda wiring.</summary>
public sealed class DelegateUpdatePrompt : IUpdatePrompt
{
    private readonly Func<UpdatePromptInfo, CancellationToken, Task<bool>> _callback;

    public DelegateUpdatePrompt(Func<UpdatePromptInfo, CancellationToken, Task<bool>> callback) =>
        _callback = callback;

    public Task<bool> ConfirmUpdateAsync(UpdatePromptInfo info, CancellationToken cancellationToken = default) =>
        _callback(info, cancellationToken);
}
