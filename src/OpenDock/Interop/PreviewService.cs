using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;

namespace OpenDock.Interop;

/// <summary>
/// Live window previews via DWM thumbnails. DWM can only mirror a thumbnail
/// into an OPAQUE window — it refuses layered windows — so the preview host
/// is deliberately a plain window with a solid background, never
/// AllowsTransparency. (A BitBlt fallback would be needed for occluded or
/// minimized windows; DWM thumbnails handle those correctly on their own.)
/// </summary>
public sealed class PreviewService : IDisposable
{
    private const uint DWM_TNP_RECTDESTINATION = 0x1;
    private const uint DWM_TNP_OPACITY = 0x4;
    private const uint DWM_TNP_VISIBLE = 0x8;

    private const int PreviewWidth = 320;
    private const int PreviewHeight = 200;

    private readonly Window _host;
    // CsWin32 projects HTHUMBNAIL as nint (a per-process thumbnail handle).
    private nint _thumbnail;
    private bool _disposed;

    public PreviewService()
    {
        _host = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = false, // required: DWM won't mirror into a layered window
            Background = Brushes.Black,
            ShowInTaskbar = false,
            Topmost = true,
            ResizeMode = ResizeMode.NoResize,
            ShowActivated = false,
            Width = PreviewWidth,
            Height = PreviewHeight,
        };
    }

    /// <param name="sourceHwnd">Window to mirror.</param>
    /// <param name="screenAnchor">Screen point the preview appears above (usually the tile's top-center).</param>
    public void Show(nint sourceHwnd, Point screenAnchor)
    {
        Hide();

        nint dest = new WindowInteropHelper(_host).EnsureHandle();
        // HWND's constructor is internal; the public IntPtr operators are the way in.
        var hr = PInvoke.DwmRegisterThumbnail((HWND)(IntPtr)dest, (HWND)(IntPtr)sourceHwnd, out _thumbnail);
        if (hr.Failed || _thumbnail == 0)
            return;

        // DWM_THUMBNAIL_PROPERTIES is internal to the generated code, but so is
        // this call site (same assembly), so field assignment works fine.
        var props = new DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = DWM_TNP_RECTDESTINATION | DWM_TNP_OPACITY | DWM_TNP_VISIBLE,
            rcDestination = new RECT(new System.Drawing.Rectangle(0, 0, PreviewWidth, PreviewHeight)),
            opacity = 255,
            fVisible = true,
        };
        PInvoke.DwmUpdateThumbnailProperties(_thumbnail, in props);

        _host.Left = screenAnchor.X - PreviewWidth / 2.0;
        _host.Top = screenAnchor.Y - PreviewHeight - 8;
        _host.Show();
    }

    public void Hide()
    {
        if (_thumbnail != 0)
        {
            PInvoke.DwmUnregisterThumbnail(_thumbnail);
            _thumbnail = 0;
        }
        if (_host.IsVisible)
            _host.Hide();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Hide();
        _host.Close();
    }
}
