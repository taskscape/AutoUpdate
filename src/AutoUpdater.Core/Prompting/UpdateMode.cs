namespace AutoUpdater.Core.Prompting;

/// <summary>
/// Controls whether an available update is applied automatically or after explicit user
/// confirmation (spec §3.5).
/// </summary>
public enum UpdateMode
{
    /// <summary>
    /// Default. The update is downloaded and installed in the background with no user interaction
    /// (spec §3.2, §4.3).
    /// </summary>
    Silent = 0,

    /// <summary>
    /// A single dialog is shown at startup asking the user whether to close and update now
    /// (intended for interactive WinForms/desktop apps). Requires an <see cref="IUpdatePrompt"/>
    /// to be supplied by the host; declining defers the update to the next startup.
    /// </summary>
    Prompt = 1,
}
