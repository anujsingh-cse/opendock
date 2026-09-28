namespace OpenDock.Core.Layout;

/// <summary>Where one tile goes: left edge of its cell and the cell's width.</summary>
/// <param name="Offset">Left edge of the cell, measured from the content's left edge.</param>
/// <param name="Size">Width (and height) of the cell.</param>
public sealed record DockItemLayout(double Offset, double Size);

/// <summary>
/// Pure macOS-style dock layout math. No UI, no platform calls, fully
/// unit-testable: given the tile count, base size, cursor position and
/// magnification settings, it returns each tile's cell.
/// Tiles grow bottom-anchored — callers pin each tile's bottom edge.
/// </summary>
public static class DockLayoutEngine
{
    /// <param name="itemCount">Number of tiles.</param>
    /// <param name="baseSize">Resting tile size in device-independent pixels.</param>
    /// <param name="cursorOffset">Cursor X relative to the content's left edge, or null when the cursor is away.</param>
    /// <param name="magnificationEnabled">Master magnification toggle.</param>
    /// <param name="magnifiedSize">Tile size at the cursor.</param>
    /// <param name="magnificationRange">Falloff distance in pixels; tiles beyond it stay at base size.</param>
    public static IReadOnlyList<DockItemLayout> ComputeLayout(
        int itemCount,
        double baseSize,
        double? cursorOffset,
        bool magnificationEnabled,
        double magnifiedSize,
        double magnificationRange)
    {
        var layouts = new DockItemLayout[itemCount];
        if (itemCount == 0 || baseSize <= 0)
            return layouts;

        double[] sizes = new double[itemCount];
        for (int i = 0; i < itemCount; i++)
        {
            double scale = 1.0;
            if (magnificationEnabled && cursorOffset.HasValue && magnificationRange > 0 && magnifiedSize > baseSize)
            {
                // Distance from the cursor to this tile's resting center.
                // Single pass on resting centers: cheap, deterministic, and
                // visually indistinguishable from the iterative solution.
                double center = i * baseSize + baseSize / 2.0;
                double distance = Math.Abs(cursorOffset.Value - center);
                if (distance < magnificationRange)
                {
                    double falloff = 0.5 * (1.0 + Math.Cos(Math.PI * distance / magnificationRange));
                    scale = 1.0 + (magnifiedSize / baseSize - 1.0) * falloff;
                }
            }
            sizes[i] = baseSize * scale;
        }

        double x = 0;
        for (int i = 0; i < itemCount; i++)
        {
            layouts[i] = new DockItemLayout(x, sizes[i]);
            x += sizes[i];
        }
        return layouts;
    }

    public static double TotalWidth(IReadOnlyList<DockItemLayout> layouts)
    {
        double width = 0;
        foreach (var layout in layouts)
            width += layout.Size;
        return width;
    }
}
