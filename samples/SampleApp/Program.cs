namespace SampleApp;

internal static class Program
{
    /// <summary>
    /// Original startup arguments, preserved so the updater can pass them to the relaunched
    /// executable after an update (spec §5.1).
    /// </summary>
    public static string[] StartupArgs { get; private set; } = Array.Empty<string>();

    [STAThread]
    private static void Main(string[] args)
    {
        StartupArgs = args;

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
