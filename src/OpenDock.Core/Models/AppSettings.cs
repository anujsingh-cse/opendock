namespace OpenDock.Core.Models;

public enum DockPosition
{
    Bottom,
    Left,
    Right,
}

public enum DockTheme
{
    System,
    Light,
    Dark,
}

public enum MinimizeEffect
{
    Genie,
    Scale,
    Suck,
}

/// <summary>How a folder stack's contents are displayed.</summary>
public enum StackViewMode
{
    Fan,
    Grid,
    List,
}

public sealed class PinnedApp
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;

    /// <summary>When true, the pin is a folder and opens as a stack.</summary>
    public bool IsFolder { get; set; }
}

/// <summary>One app discovered for Launchpad (installed app or folder).</summary>
public sealed class LaunchpadItem
{
    /// <summary>Stable id: lower-cased launch path.</summary>
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>What to launch: .lnk path, exe path, or app id.</summary>
    public string LaunchPath { get; set; } = string.Empty;

    /// <summary>Cache key used by the icon provider.</summary>
    public string IconKey { get; set; } = string.Empty;
}

/// <summary>A user-created folder of apps inside Launchpad.</summary>
public sealed class LaunchpadFolder
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Folder";
    public List<string> MemberIds { get; set; } = new();
}

/// <summary>
/// All user settings. Serialized to JSON at %APPDATA%/OpenDock/settings.json.
/// New settings must keep a sensible default so old files keep loading.
/// </summary>
public sealed class AppSettings
{
    public double IconSize { get; set; } = 48;

    public bool MagnificationEnabled { get; set; } = true;
    public double MagnifiedSize { get; set; } = 80;
    public double MagnificationRange { get; set; } = 160;

    public DockPosition Position { get; set; } = DockPosition.Bottom;
    public bool AutoHide { get; set; }
    public DockTheme Theme { get; set; } = DockTheme.System;
    public MinimizeEffect MinimizeEffect { get; set; } = MinimizeEffect.Genie;

    public bool HideTaskbar { get; set; } = true;
    public bool ShowWindowPreviews { get; set; } = true;

    public List<PinnedApp> PinnedApps { get; set; } = new();

    // ---- v0.2: full macOS-like experience ----

    /// <summary>Show the macOS-style top menu bar.</summary>
    public bool MenuBarEnabled { get; set; } = true;

    /// <summary>Hotkey strings like "Ctrl+Alt+L". Parsed by HotkeyManager.</summary>
    public string LaunchpadHotkey { get; set; } = "Ctrl+Alt+L";
    public string SpotlightHotkey { get; set; } = "Ctrl+Space";
    public string ExposeHotkey { get; set; } = "Ctrl+Alt+E";

    public StackViewMode DefaultStackView { get; set; } = StackViewMode.Grid;

    /// <summary>Per-folder view overrides, keyed by folder path.</summary>
    public Dictionary<string, StackViewMode> StackViews { get; set; } = new();

    /// <summary>Launchpad tile order: item ids in display order.</summary>
    public List<string> LaunchpadOrder { get; set; } = new();

    public List<LaunchpadFolder> LaunchpadFolders { get; set; } = new();
}
