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

public sealed class PinnedApp
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
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
}
