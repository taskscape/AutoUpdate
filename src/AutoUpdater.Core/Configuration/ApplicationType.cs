namespace AutoUpdater.Core.Configuration;

/// <summary>
/// Declares how the host application must be shut down and restarted during an update.
/// Spec §4.3 / §5.1: the type is set explicitly by the app author in configuration.
/// </summary>
public enum ApplicationType
{
    /// <summary>ASP.NET Core hosted in IIS. Uses app_offline.htm to release file locks.</summary>
    WebApp = 0,

    /// <summary>WinForms / WPF desktop app. Force-closed, then relaunched in the user session.</summary>
    Desktop = 1,

    /// <summary>Console application. Force-closed, then relaunched in the user session.</summary>
    Console = 2,
}
