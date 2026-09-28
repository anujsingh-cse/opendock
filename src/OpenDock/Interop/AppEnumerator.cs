using System.IO;
using OpenDock.Core.Models;

namespace OpenDock.Interop;

/// <summary>
/// Discovers installed applications by scanning the per-user and all-users
/// Start Menu Programs folders for .lnk shortcuts. Windows also drops
/// shortcuts for UWP/Store apps here, so one scan covers both worlds.
/// Shortcuts are launched via their .lnk path (UseShellExecute), which
/// works for Win32 exes, UWP apps, and special shell targets alike.
/// </summary>
public sealed class AppEnumerator
{
    private List<LaunchpadItem> _cache = new();
    private bool _scanned;

    public IReadOnlyList<LaunchpadItem> Apps
    {
        get
        {
            if (!_scanned)
                Refresh();
            return _cache;
        }
    }

    public void Refresh()
    {
        var items = new Dictionary<string, LaunchpadItem>(StringComparer.OrdinalIgnoreCase);

        foreach (string programsDir in ProgramsDirectories())
            ScanDirectory(programsDir, items);

        _cache = items.Values
            .OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        _scanned = true;
    }

    private static IEnumerable<string> ProgramsDirectories()
    {
        string user = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
        string common = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
        if (!string.IsNullOrEmpty(user))
            yield return Path.Combine(user, "Programs");
        if (!string.IsNullOrEmpty(common))
            yield return Path.Combine(common, "Programs");
    }

    private static void ScanDirectory(string dir, Dictionary<string, LaunchpadItem> items)
    {
        string[] links;
        try
        {
            links = Directory.GetFiles(dir, "*.lnk", SearchOption.AllDirectories);
        }
        catch
        {
            return; // permission or transient IO — skip the whole tree
        }

        foreach (string lnk in links)
        {
            try
            {
                var resolved = ShellLink.Resolve(lnk);
                // Some shortcuts (UWP entries, uninstallers) resolve to nothing
                // useful; launching the .lnk itself still works, so keep them.
                string target = resolved?.TargetPath ?? string.Empty;

                string name = !string.IsNullOrWhiteSpace(resolved?.Description)
                    ? resolved.Description
                    : Path.GetFileNameWithoutExtension(lnk);

                string id = (string.IsNullOrEmpty(target) ? lnk : target).ToLowerInvariant();
                if (items.ContainsKey(id))
                    continue;

                items[id] = new LaunchpadItem
                {
                    Id = id,
                    Name = name,
                    LaunchPath = lnk,
                    IconKey = lnk,
                };
            }
            catch
            {
                // One bad shortcut must never kill the scan.
            }
        }
    }
}
