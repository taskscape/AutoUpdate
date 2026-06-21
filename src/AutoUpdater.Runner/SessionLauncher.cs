using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using AutoUpdater.Core.Logging;

namespace AutoUpdater.Runner;

/// <summary>
/// Relaunches a desktop/console app in the active interactive user session (spec §5.1). The
/// runner is started by a SYSTEM service, so a plain Process.Start would land in session 0 with
/// no desktop. We duplicate the active session's user token and CreateProcessAsUser.
/// </summary>
[SupportedOSPlatform("windows")]
public static class SessionLauncher
{
    public static void LaunchInActiveSession(string executablePath, string[] arguments, string? workingDirectory, IUpdaterLogger logger)
    {
        var commandLine = BuildCommandLine(executablePath, arguments);

        // If we are NOT running as SYSTEM (e.g. dev/debug), a normal start is sufficient.
        if (!IsRunningAsSystem())
        {
            logger.Info("Not running as SYSTEM — relaunching via Process.Start.");
            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = string.Join(' ', arguments.Select(QuoteIfNeeded)),
                WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(executablePath),
                UseShellExecute = false,
            });
            return;
        }

        var sessionId = Native.WTSGetActiveConsoleSessionId();
        if (sessionId == 0xFFFFFFFF)
        {
            logger.Warn("No active console session; cannot relaunch the application in a user session.");
            return;
        }

        if (!Native.WTSQueryUserToken(sessionId, out var userToken))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "WTSQueryUserToken failed.");

        try
        {
            if (!Native.DuplicateTokenEx(userToken, Native.MAXIMUM_ALLOWED, IntPtr.Zero,
                    Native.SECURITY_IMPERSONATION_LEVEL.SecurityIdentification,
                    Native.TOKEN_TYPE.TokenPrimary, out var primaryToken))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "DuplicateTokenEx failed.");

            try
            {
                var startup = new Native.STARTUPINFO();
                startup.cb = Marshal.SizeOf<Native.STARTUPINFO>();
                startup.lpDesktop = @"winsta0\default";

                var ok = Native.CreateProcessAsUser(
                    primaryToken,
                    null,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    Native.CREATE_UNICODE_ENVIRONMENT | Native.CREATE_NEW_CONSOLE,
                    IntPtr.Zero,
                    workingDirectory ?? Path.GetDirectoryName(executablePath),
                    ref startup,
                    out var procInfo);

                if (!ok)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateProcessAsUser failed.");

                logger.Info($"Relaunched '{executablePath}' in session {sessionId} (pid {procInfo.dwProcessId}).");
                Native.CloseHandle(procInfo.hThread);
                Native.CloseHandle(procInfo.hProcess);
            }
            finally
            {
                Native.CloseHandle(primaryToken);
            }
        }
        finally
        {
            Native.CloseHandle(userToken);
        }
    }

    private static bool IsRunningAsSystem()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        return identity.IsSystem;
    }

    private static string BuildCommandLine(string executablePath, string[] arguments)
    {
        var parts = new List<string> { Quote(executablePath) };
        parts.AddRange(arguments.Select(QuoteIfNeeded));
        return string.Join(' ', parts);
    }

    private static string QuoteIfNeeded(string value) =>
        value.Contains(' ') || value.Contains('"') ? Quote(value) : value;

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    private static class Native
    {
        public const uint MAXIMUM_ALLOWED = 0x02000000;
        public const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
        public const uint CREATE_NEW_CONSOLE = 0x00000010;

        public enum SECURITY_IMPERSONATION_LEVEL { SecurityAnonymous, SecurityIdentification, SecurityImpersonation, SecurityDelegation }
        public enum TOKEN_TYPE { TokenPrimary = 1, TokenImpersonation }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFO
        {
            public int cb;
            public string? lpReserved;
            public string? lpDesktop;
            public string? lpTitle;
            public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
            public short wShowWindow, cbReserved2;
            public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
        }

        [DllImport("kernel32.dll")]
        public static extern uint WTSGetActiveConsoleSessionId();

        [DllImport("wtsapi32.dll", SetLastError = true)]
        public static extern bool WTSQueryUserToken(uint sessionId, out IntPtr token);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool DuplicateTokenEx(IntPtr existingToken, uint desiredAccess, IntPtr tokenAttributes,
            SECURITY_IMPERSONATION_LEVEL impersonationLevel, TOKEN_TYPE tokenType, out IntPtr newToken);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool CreateProcessAsUser(IntPtr token, string? applicationName, string? commandLine,
            IntPtr processAttributes, IntPtr threadAttributes, bool inheritHandles, uint creationFlags,
            IntPtr environment, string? currentDirectory, ref STARTUPINFO startupInfo, out PROCESS_INFORMATION processInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr handle);
    }
}
