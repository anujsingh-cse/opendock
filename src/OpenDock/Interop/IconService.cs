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

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint ExtractIconExW(
        string lpszFile, int nIconIndex, nint[]? phiconLarge, nint[]? phiconSmall, uint nIcons);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(nint hObject);

    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_LARGEICON = 0x0;

    // Recycle Bin artwork lives in imageres.dll (indices stable across Win10/11).
    private static readonly string ImageresPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "imageres.dll");
    private const int ImageresRecycleEmpty = 54;
    private const int ImageresRecycleFull = 55;

    private readonly Dictionary<string, ImageSource> _cache = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public object? GetIcon(DockItem item)
    {
        string key = string.IsNullOrEmpty(item.IconKey) ? item.TargetPath ?? string.Empty : item.IconKey;
        if (string.IsNullOrEmpty(key))
            return null;

        if (_cache.TryGetValue(key, out var cached))
            return cached;

        ImageSource? icon = key switch
        {
            // Synthetic tiles whose artwork we own.
            "special:launchpad" => DrawLaunchpadIcon(),
            "special:trash-empty" => ExtractModuleIcon(ImageresPath, ImageresRecycleEmpty),
            "special:trash-full" => ExtractModuleIcon(ImageresPath, ImageresRecycleFull),
            _ => Extract(key),
        };
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

    /// <summary>
    /// Extracts one icon from a module's resources (imageres.dll, shell32.dll…).
    /// Returns null when the module or index doesn't yield an icon.
    /// </summary>
    private static ImageSource? ExtractModuleIcon(string modulePath, int index)
    {
        try
        {
            var large = new nint[1];
            uint extracted = ExtractIconExW(modulePath, index, large, null, 1);
            if (extracted == 0 || large[0] == nint.Zero)
                return null;
            try
            {
                var bitmap = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    new IntPtr(large[0]),
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                bitmap.Freeze();
                return TrimTransparentMargins(bitmap);
            }
            finally
            {
                DestroyIcon(large[0]);
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Draws the Launchpad tile glyph at runtime (a grid of rounded squares),
    /// so the repo needs no binary asset for it. macOS-flavored, original art.
    /// </summary>
    private static ImageSource? DrawLaunchpadIcon()
    {
        try
        {
            const int size = 96;
            using var bmp = new System.Drawing.Bitmap(size, size);
            using var g = System.Drawing.Graphics.FromImage(bmp);
            g.Clear(System.Drawing.Color.Transparent);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var colors = new[]
            {
                System.Drawing.Color.FromArgb(0, 122, 204),
                System.Drawing.Color.FromArgb(16, 124, 16),
                System.Drawing.Color.FromArgb(204, 70, 70),
                System.Drawing.Color.FromArgb(180, 120, 20),
                System.Drawing.Color.FromArgb(120, 80, 180),
                System.Drawing.Color.FromArgb(20, 140, 150),
            };

            const int cols = 3, gap = 10, cell = (size - gap * (cols + 1)) / cols;
            int i = 0;
            for (int row = 0; row < cols; row++)
            for (int col = 0; col < cols; col++)
            {
                int x = gap + col * (cell + gap);
                int y = gap + row * (cell + gap);
                using var brush = new System.Drawing.SolidBrush(colors[i++ % colors.Length]);
                // Rounded square via a path: cheap and looks right at tile sizes.
                using var path = new System.Drawing.Drawing2D.GraphicsPath();
                int r = cell / 4;
                path.AddArc(x, y, r * 2, r * 2, 180, 90);
                path.AddArc(x + cell - r * 2, y, r * 2, r * 2, 270, 90);
                path.AddArc(x + cell - r * 2, y + cell - r * 2, r * 2, r * 2, 0, 90);
                path.AddArc(x, y + cell - r * 2, r * 2, r * 2, 90, 90);
                path.CloseFigure();
                g.FillPath(brush, path);
            }

            nint hBitmap = bmp.GetHbitmap();
            try
            {
                var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, nint.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally
            {
                DeleteObject(hBitmap);
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
