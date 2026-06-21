using System.Reflection;
using AutoUpdater.Core;
using AutoUpdater.Core.Configuration;
using AutoUpdater.Core.Logging;
using AutoUpdater.Core.State;
using AutoUpdater.WinForms;

namespace SampleApp;

/// <summary>
/// Minimal WinForms host demonstrating the updater end-to-end (spec §3, §5.1). On startup it runs
/// the update check; in Prompt mode a single dialog asks the user to close and update now.
/// </summary>
public sealed class MainForm : Form
{
    private readonly TextBox _log;
    private readonly Label _versionLabel;

    public MainForm()
    {
        Text = "AutoUpdater Sample App";
        Width = 640;
        Height = 420;
        StartPosition = FormStartPosition.CenterScreen;

        _versionLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(10),
            Font = new Font(Font.FontFamily, 11, FontStyle.Bold),
            Text = $"Running version: {GetVersion()}",
        };

        _log = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font(FontFamily.GenericMonospace, 9),
        };

        var checkButton = new Button
        {
            Dock = DockStyle.Bottom,
            Height = 36,
            Text = "Check for updates now",
        };
        checkButton.Click += async (_, _) => await RunUpdateCheckAsync();

        Controls.Add(_log);
        Controls.Add(checkButton);
        Controls.Add(_versionLabel);

        Shown += async (_, _) => await RunUpdateCheckAsync();
    }

    private async Task RunUpdateCheckAsync()
    {
        try
        {
            var config = UpdaterConfigurationLoader.Load();

            // Route updater log output into the on-screen text box for the demo, in addition to
            // the standard file + Event Log sinks (spec §7).
            var logger = new UiLogger(AppendLog, new FileAndEventLogLogger(config.ApplicationId, "client"));
            var client = new UpdaterClient(config, new DeploymentStateStore(config.ApplicationId), logger);

            AppendLog($"Update mode: {config.UpdateMode}. Checking '{config.RepositoryUrl}'…");

            // Prompt is harmless in Silent mode (ignored); used only when updateMode = Prompt (spec §3.5).
            var prompt = new WinFormsUpdatePrompt();
            var result = await client.CheckForUpdatesAsync(Program.StartupArgs, prompt);

            AppendLog($"Result: {result}");
        }
        catch (Exception ex)
        {
            AppendLog("Update check error: " + ex.Message);
        }
    }

    private void AppendLog(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendLog(message));
            return;
        }

        _log.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
    }

    private static string GetVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";

    /// <summary>Tees updater log lines to the UI as well as the underlying file/Event Log sinks.</summary>
    private sealed class UiLogger : IUpdaterLogger
    {
        private readonly Action<string> _toUi;
        private readonly IUpdaterLogger _inner;

        public UiLogger(Action<string> toUi, IUpdaterLogger inner)
        {
            _toUi = toUi;
            _inner = inner;
        }

        public void Log(UpdaterLogLevel level, string message, Exception? exception = null)
        {
            _inner.Log(level, message, exception);
            _toUi($"[{level}] {message}{(exception is null ? "" : " — " + exception.Message)}");
        }
    }
}
