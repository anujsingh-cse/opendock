using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenDock.Core.Abstractions;
using OpenDock.Core.Models;

namespace OpenDock.Interop;

/// <summary>
/// Single funnel for all tile artwork: shell icon extraction, transparent-margin
/// trimming, and an in-memory cache.
/// Windows icons disagree wildly about their transparent margins (.exe art
/// fills 100% of its canvas, packaged-app assets ~70%), so trimming keeps
/// pinned and running tiles visually equal instead of one looking smaller.
///
/// Shell icon extraction is hand-declared here: CsWin32's generated
/// SHFILEINFOW has internal fields, so the icon handle can't be read back
/// through the generated API.
/// </summary>
public sealed class IconService : IIconProvider, IDisposable
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileInfo
    {
        public nint hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHGetFileInfoW", SetLastError = true)]
    private static extern nuint SHGetFileInfo(
        string pszPath, uint dwFileAttributes, ref ShellFileInfo psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(nint hIcon);

    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_LARGEICON = 0x0;

    private readonly Dictionary<string, ImageSource> _cache = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public object? GetIcon(DockItem item)
    {
        string key = string.IsNullOrEmpty(item.IconKey) ? item.TargetPath ?? string.Empty : item.IconKey;
        if (string.IsNullOrEmpty(key))
            return null;

        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var icon = Extract(key);
        if (icon is not null)
            _cache[key] = icon;
        return icon;
    }

    public void Invalidate(string iconKey) => _cache.Remove(iconKey);

    private static ImageSource? Extract(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
            return null;

        try
        {
            var info = new ShellFileInfo();
            nuint ok = SHGetFileInfo(
                path, 0, ref info, (uint)Marshal.SizeOf<ShellFileInfo>(), SHGFI_ICON | SHGFI_LARGEICON);

            if (ok == 0 || info.hIcon == nint.Zero)
                return null;

            try
            {
                var bitmap = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    new IntPtr(info.hIcon),
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                bitmap.Freeze();
                return TrimTransparentMargins(bitmap);
            }
            finally
            {
                DestroyIcon(info.hIcon);
            }
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource TrimTransparentMargins(BitmapSource source)
    {
        var bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int w = bgra.PixelWidth;
        int h = bgra.PixelHeight;
        int stride = w * 4;
        var pixels = new byte[h * stride];
        bgra.CopyPixels(pixels, stride, 0);

        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (pixels[y * stride + x * 4 + 3] > 16) // alpha threshold
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (maxX < minX)
            return source; // fully transparent; keep as-is

        const int pad = 2; // breathing room so art never touches the tile edge
        minX = Math.Max(0, minX - pad);
        minY = Math.Max(0, minY - pad);
        maxX = Math.Min(w - 1, maxX + pad);
        maxY = Math.Min(h - 1, maxY + pad);

        var cropped = new CroppedBitmap(bgra, new Int32Rect(minX, minY, maxX - minX + 1, maxY - minY + 1));
        cropped.Freeze();
        return cropped;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _cache.Clear();
    }
}
