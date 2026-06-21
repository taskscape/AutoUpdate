using System.Diagnostics;
using System.Reflection;

namespace AutoUpdater.Core;

/// <summary>
/// Detects whether the host was compiled in Debug or Release, to enforce the Release-mode lock
/// (spec §2.4: auto-update is disabled in Debug).
/// </summary>
public static class BuildConfiguration
{
    /// <summary>
    /// Returns true when the given assembly (default: entry assembly) was compiled in Release.
    /// Relies on the JIT-tracking/optimization flags emitted by Debug builds.
    /// </summary>
    public static bool IsReleaseBuild(Assembly? assembly = null)
    {
        assembly ??= Assembly.GetEntryAssembly();
        if (assembly is null)
            return false; // be conservative: treat unknown as "not release"

        var debuggable = assembly.GetCustomAttribute<DebuggableAttribute>();
        if (debuggable is null)
            return true; // no debug metadata emitted → release

        return !debuggable.IsJITTrackingEnabled;
    }
}
