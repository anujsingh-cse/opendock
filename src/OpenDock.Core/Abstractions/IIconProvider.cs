using OpenDock.Core.Models;

namespace OpenDock.Core.Abstractions;

/// <summary>
/// Supplies tile artwork. Returns a platform-specific image object
/// (a WPF <c>ImageSource</c> in the desktop app) so Core stays UI-agnostic.
/// </summary>
public interface IIconProvider
{
    object? GetIcon(DockItem item);

    void Invalidate(string iconKey);
}
