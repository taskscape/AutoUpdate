using System.Runtime.Versioning;
using AutoUpdater.Core.Prompting;

namespace AutoUpdater.WinForms;

/// <summary>
/// Turnkey <see cref="IUpdatePrompt"/> for WinForms apps (spec §3.5). Shows a single Yes/No
/// dialog at startup asking whether to close and update now, and returns the user's choice.
///
/// Construct this on the UI thread (e.g. inside your main form's <c>Load</c>/<c>Shown</c> handler,
/// or after a WinForms synchronization context exists) so the dialog is marshaled correctly:
/// <code>
/// private async void MainForm_Shown(object? sender, EventArgs e)
/// {
///     var prompt = new WinFormsUpdatePrompt();
///     await UpdaterClient.FromConfigFile().CheckForUpdatesAsync(Program.StartupArgs, prompt);
/// }
/// </code>
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WinFormsUpdatePrompt : IUpdatePrompt
{
    private readonly SynchronizationContext? _uiContext;
    private readonly string _caption;
    private readonly Func<UpdatePromptInfo, string> _messageFactory;

    public WinFormsUpdatePrompt(
        SynchronizationContext? uiContext = null,
        string caption = "Update available",
        Func<UpdatePromptInfo, string>? messageFactory = null)
    {
        // Capture the UI thread's context at construction so we can post the dialog back to it.
        _uiContext = uiContext ?? SynchronizationContext.Current;
        _caption = caption;
        _messageFactory = messageFactory ?? DefaultMessage;
    }

    public Task<bool> ConfirmUpdateAsync(UpdatePromptInfo info, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void Show(object? _)
        {
            try
            {
                var result = MessageBox.Show(
                    _messageFactory(info),
                    _caption,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button1);

                tcs.TrySetResult(result == DialogResult.Yes);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }

        if (_uiContext is not null)
            _uiContext.Post(Show, null);
        else
            Show(null); // best effort: no captured context, show on the current thread

        return tcs.Task;
    }

    private static string DefaultMessage(UpdatePromptInfo info) =>
        $"A new version ({info.NewTag}) of this application is available." +
        Environment.NewLine + Environment.NewLine +
        "Do you want to close the application and update now?";
}
