using System.Runtime.InteropServices;

namespace OpenDock.Interop;

/// <summary>
/// Recycle Bin state and emptying, via shell32.
/// Hand-declared: the SHQUERYRBINFO struct has no useful CsWin32 projection
/// for our needs, and both calls are one-liners.
/// </summary>
public static class RecycleBin
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SHQUERYRBINFO
    {
        public uint cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHQueryRecycleBinW(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHEmptyRecycleBinW(nint hwnd, string? pszRootPath, uint dwFlags);

    private const uint SHERB_NOCONFIRMATION = 0x1;
    private const uint SHERB_NOPROGRESSUI = 0x2;
    private const uint SHERB_NOSOUND = 0x4;

    /// <summary>True when every drive's bin is empty. Never throws.</summary>
    public static bool IsEmpty()
    {
        try
        {
            var info = new SHQUERYRBINFO { cbSize = (uint)Marshal.SizeOf<SHQUERYRBINFO>() };
            int hr = SHQueryRecycleBinW(null, ref info);
            return hr != 0 || info.i64NumItems == 0;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>Empties all bins. Shows the standard progress UI; no confirmation.</summary>
    public static bool Empty()
    {
        try
        {
            int hr = SHEmptyRecycleBinW(nint.Zero, null,
                SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
            return hr == 0;
        }
        catch
        {
            return false;
        }
    }
}
