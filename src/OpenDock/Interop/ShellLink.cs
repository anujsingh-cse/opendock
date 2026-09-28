using System.Runtime.InteropServices;
using System.Text;

namespace OpenDock.Interop;

/// <summary>
/// Resolves Windows shortcuts (.lnk) to their targets via the IShellLink
/// COM interface. Written clean-room against the public COM contract
/// (CLSID_ShellLink / IShellLinkW / IPersistFile).
/// </summary>
public static class ShellLink
{
    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLinkObject;

    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out nint pfd, uint fFlags);
        void GetIDList(out nint ppidl);
        void SetIDList(nint pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(nint hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [Guid("0000010B-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        void IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }

    private const int MaxPath = 260;
    private const uint STGM_READ = 0;

    public sealed record ResolvedLink(string TargetPath, string Description, string Arguments);

    /// <summary>
    /// Resolves a .lnk file. Returns null when the shortcut can't be read
    /// or points at nothing usable (e.g. a dead target).
    /// </summary>
    public static ResolvedLink? Resolve(string lnkPath)
    {
        try
        {
            var link = (IShellLinkW)new ShellLinkObject();
            ((IPersistFile)link).Load(lnkPath, STGM_READ);

            var target = new StringBuilder(MaxPath);
            link.GetPath(target, target.Capacity, out _, 0);

            var description = new StringBuilder(MaxPath);
            try { link.GetDescription(description, description.Capacity); } catch { /* optional */ }

            var args = new StringBuilder(MaxPath);
            try { link.GetArguments(args, args.Capacity); } catch { /* optional */ }

            string targetPath = target.ToString();
            if (string.IsNullOrWhiteSpace(targetPath))
                return null;

            return new ResolvedLink(targetPath, description.ToString(), args.ToString());
        }
        catch
        {
            return null;
        }
    }
}
