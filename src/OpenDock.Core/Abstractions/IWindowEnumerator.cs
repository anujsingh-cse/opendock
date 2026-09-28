namespace OpenDock.Core.Abstractions;

/// <summary>Enumerates the windows that deserve a dock tile.</summary>
public interface IWindowEnumerator
{
    IReadOnlyList<RunningWindowInfo> Enumerate();

    /// <summary>Every qualifying window individually, newest first.</summary>
    IReadOnlyList<WindowRef> EnumerateIndividual();
}

/// <summary>One individual top-level window, ungrouped (for Exposé).</summary>
public sealed record WindowRef(nint Handle, string Title, string ExePath, int ProcessId);

/// <summary>One running app, grouped by executable like the taskbar does.</summary>
/// <param name="Handle">A representative top-level window handle.</param>
/// <param name="Title">Title of the representative window.</param>
/// <param name="ExePath">Full path of the owning executable.</param>
/// <param name="ProcessId">Owning process id.</param>
/// <param name="WindowCount">How many windows were grouped into this tile.</param>
public sealed record RunningWindowInfo(nint Handle, string Title, string ExePath, int ProcessId, int WindowCount);
