using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using OpenDock.Core.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace OpenDock.Interop;

/// <summary>
/// Enumerates top-level windows the way the taskbar does: visible, titled,
/// not a tool window — then groups them by executable so each app gets one tile.
///
/// Hand-declared pieces: the window-text APIs take caller-allocated buffers,
/// and GetWindowLongPtrW's index parameter is an internal CsWin32 enum, so
/// those go through DllImport with a comment instead of the generator.
/// </summary>
public sealed class WindowEnumerator : IWindowEnumerator
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowTextW(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowTextLengthW(nint hWnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

    private const int GWL_EXSTYLE = -20;
    private const nint WS_EX_TOOLWINDOW = 0x80;
    private const nint WS_EX_APPWINDOW = 0x40000;

    private nint _excludedHwnd;

    /// <summary>Never report this window (used for the dock itself).</summary>
    public void ExcludeWindow(nint hwnd) => _excludedHwnd = hwnd;

    public IReadOnlyList<RunningWindowInfo> Enumerate()
    {
        var windows = new List<(nint Hwnd, string Title, string ExePath, int Pid)>();

        PInvoke.EnumWindows((hwnd, _) =>
        {
            // HWND.Value is internal; the public IntPtr operators are the way out.
            nint h = (nint)(IntPtr)hwnd;
            if (h == _excludedHwnd)
                return true;
            if (!PInvoke.IsWindowVisible(hwnd))
                return true;

            int length = GetWindowTextLengthW(h);
            if (length == 0)
                return true;

            var sb = new StringBuilder(length + 1);
            if (GetWindowTextW(h, sb, sb.Capacity) == 0)
                return true;

            // Skip tool windows (tooltips, floating palettes) unless they
            // explicitly ask for a taskbar button.
            nint exStyle = GetWindowLongPtr(h, GWL_EXSTYLE);
            bool isToolWindow = (exStyle & WS_EX_TOOLWINDOW) != 0;
            bool wantsTaskbarButton = (exStyle & WS_EX_APPWINDOW) != 0;
            if (isToolWindow && !wantsTaskbarButton)
                return true;

            uint pid = GetWindowProcessId(hwnd);
            string exePath = TryGetExePath((int)pid);
            if (string.IsNullOrEmpty(exePath))
                return true;

            windows.Add((h, sb.ToString(), exePath, (int)pid));
            return true;
        }, default);

        return windows
            .GroupBy(w => w.ExePath, StringComparer.OrdinalIgnoreCase)
            .Select(g => new RunningWindowInfo(
                g.First().Hwnd,
                g.First().Title,
                g.Key,
                g.First().Pid,
                g.Count()))
            .ToList();
    }

    private static unsafe uint GetWindowProcessId(HWND hwnd)
    {
        uint pid = 0;
        PInvoke.GetWindowThreadProcessId(hwnd, &pid);
        return pid;
    }

    private static string TryGetExePath(int pid)
    {
        try
        {
            return Process.GetProcessById(pid).MainModule?.FileName ?? string.Empty;
        }
        catch
        {
            return string.Empty; // access denied, or the process exited mid-enumeration
        }
    }
}
