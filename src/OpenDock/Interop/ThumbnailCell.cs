using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;

namespace OpenDock.Interop;

/// <summary>
/// One live window thumbnail for Exposé: an HwndHost child window with a
/// DWM thumbnail registered into it. DWM only mirrors into real HWNDs, so
/// each grid cell gets its own. Clicking the cell is handled by the
/// parent (HwndHost is a FrameworkElement, so input works normally).
/// </summary>
public sealed class ThumbnailCell : HwndHost
{
    // Hand-declared: tiny signatures for raw child-window management.
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowExW(
        uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
        int x, int y, int nWidth, int nHeight,
        nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(nint hWnd);

    private const uint WS_CHILD = 0x40000000;
    private const uint WS_VISIBLE = 0x10000000;
    private const uint WS_EX_TRANSPARENT = 0x00000020;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    private const uint DWM_TNP_RECTDESTINATION = 0x1;
    private const uint DWM_TNP_OPACITY = 0x4;
    private const uint DWM_TNP_VISIBLE = 0x8;

    private readonly nint _sourceHwnd;
    private nint _thumbnail;

    public ThumbnailCell(nint sourceHwnd)
    {
        _sourceHwnd = sourceHwnd;
        SizeChanged += (_, _) => UpdateThumbnailRect();
    }

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        // WS_EX_TRANSPARENT: the thumbnail is visual-only; mouse input passes
        // through to the WPF cell above so clicks hit-test normally.
        nint child = CreateWindowExW(WS_EX_TRANSPARENT, "STATIC", "", WS_CHILD | WS_VISIBLE,
            0, 0, (int)Width, (int)Height, hwndParent.Handle, nint.Zero, nint.Zero, nint.Zero);

        if (child != nint.Zero)
        {
            var hr = PInvoke.DwmRegisterThumbnail(
                (HWND)(IntPtr)child, (HWND)(IntPtr)_sourceHwnd, out _thumbnail);
            if (hr.Failed)
                _thumbnail = 0;
            UpdateThumbnailRect();
        }

        return new HandleRef(this, child);
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        if (_thumbnail != 0)
        {
            PInvoke.DwmUnregisterThumbnail(_thumbnail);
            _thumbnail = 0;
        }
        DestroyWindow(hwnd.Handle);
    }

    private void UpdateThumbnailRect()
    {
        if (_thumbnail == 0)
            return;

        // The child fills this HwndHost; mirror the source into all of it.
        var source = PresentationSource.FromVisual(this);
        double dpiX = 1, dpiY = 1;
        if (source?.CompositionTarget is { } target)
        {
            dpiX = target.TransformToDevice.M11;
            dpiY = target.TransformToDevice.M22;
        }

        int w = Math.Max(1, (int)(ActualWidth * dpiX));
        int h = Math.Max(1, (int)(ActualHeight * dpiY));

        var props = new DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = DWM_TNP_RECTDESTINATION | DWM_TNP_OPACITY | DWM_TNP_VISIBLE,
            rcDestination = new RECT(new System.Drawing.Rectangle(0, 0, w, h)),
            opacity = 255,
            fVisible = true,
        };
        PInvoke.DwmUpdateThumbnailProperties(_thumbnail, in props);

        SetWindowPos(Handle, nint.Zero, 0, 0, w, h, SWP_NOZORDER | SWP_NOACTIVATE);
    }
}
