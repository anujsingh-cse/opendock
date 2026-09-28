namespace OpenDock.Core.Models;

/// <summary>What kind of tile this is on the dock.</summary>
public enum DockItemKind
{
    /// <summary>An app the user pinned; may or may not be running.</summary>
    PinnedApp,

    /// <summary>A running app with no pin.</summary>
    RunningWindow,

    /// <summary>Visual divider between sections.</summary>
    Separator,

    /// <summary>A folder whose contents fan out on click (roadmap).</summary>
    FolderStack,
}

/// <summary>
/// A single tile on the dock. Plain data model with no UI or platform
/// dependencies, so the layout engine and view models can share it.
/// </summary>
public sealed class DockItem
{
    public Guid Id { get; } = Guid.NewGuid();

    public DockItemKind Kind { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Executable, shortcut, or folder backing a pinned tile.</summary>
    public string? TargetPath { get; set; }

    /// <summary>Cache key used by the icon provider (usually the target path).</summary>
    public string IconKey { get; set; } = string.Empty;

    /// <summary>Top-level window handle when the app is running; otherwise 0.</summary>
    public nint WindowHandle { get; set; }

    public bool IsRunning { get; set; }

    /// <summary>How many windows are grouped into this tile.</summary>
    public int WindowCount { get; set; } = 1;

    /// <summary>Unread badge count; 0 means no badge.</summary>
    public int BadgeCount { get; set; }

    /// <summary>Task progress 0..1, or -1 when the app reports none.</summary>
    public double Progress { get; set; } = -1;

    /// <summary>Stable key used to reconcile tiles across refreshes.</summary>
    public string MatchKey { get; set; } = string.Empty;
}
