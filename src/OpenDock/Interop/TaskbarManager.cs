using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace OpenDock.Interop;

/// <summary>Hides and restores the Windows taskbar (Shell_TrayWnd).</summary>
public sealed class TaskbarManager
{
    // Hand-declared: trivial signatures, zero CsWin32 overload ambiguity.
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint FindWindowW(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;

    private nint _taskbarHwnd;

    private nint TaskbarHandle()
    {
        if (_taskbarHwnd == nint.Zero)
            _taskbarHwnd = FindWindowW("Shell_TrayWnd", null);
        return _taskbarHwnd;
    }

    public void Hide()
    {
        nint h = TaskbarHandle();
        if (h != nint.Zero)
            ShowWindow(h, SW_HIDE);
    }

    public void Show()
    {
        nint h = TaskbarHandle();
        if (h != nint.Zero)
            ShowWindow(h, SW_SHOW);
    }
}
