using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OpenDock.Interop;

/// <summary>
/// Sleep / lock / log off / restart / shut down.
/// Lock and log-off go through user32 directly; shutdown and restart use
/// shutdown.exe (no privilege escalation needed); sleep uses the
/// documented powrprof entry point. All best-effort, never throwing.
/// </summary>
public static class SystemPower
{
    // Hand-declared: trivial signatures.
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool LockWorkStation();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ExitWindowsEx(uint uFlags, uint dwReason);

    private const uint EWX_LOGOFF = 0x00;

    public static bool Lock()
    {
        try { return LockWorkStation(); }
        catch { return false; }
    }

    public static bool LogOff()
    {
        try { return ExitWindowsEx(EWX_LOGOFF, 0); }
        catch { return false; }
    }

    public static void Sleep()
    {
        // SetSuspendState(Hibernate=false, ForceCritical=true, DisableWakeEvent=false)
        Run("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0");
    }

    public static void Restart() => Run("shutdown.exe", "/r /t 0 /c \"Restart requested by OpenDock\"");

    public static void ShutDown() => Run("shutdown.exe", "/s /t 0 /c \"Shut down requested by OpenDock\"");

    /// <summary>Opens a Windows Settings page, e.g. "ms-settings:network-wifi".</summary>
    public static void OpenSettingsPage(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch { /* best effort */ }
    }

    private static void Run(string file, string args)
    {
        try
        {
            Process.Start(new ProcessStartInfo(file, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        }
        catch { /* best effort */ }
    }
}
