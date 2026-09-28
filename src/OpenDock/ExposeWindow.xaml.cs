using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using OpenDock.Core.Abstractions;
using OpenDock.Interop;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace OpenDock;

/// <summary>
/// Exposé-lite: a dimmed full-screen grid of LIVE DWM thumbnails of every
/// visible window. Click a tile to switch to that window, Esc to dismiss.
/// Each cell hosts its own HWND (ThumbnailCell) because DWM only mirrors
/// into real windows — the overlay itself stays a transparent WPF window.
/// </summary>
public partial class ExposeWindow : Window
{
    private const int MaxCells = 16;
    private const int SW_RESTORE = 9;

    private readonly IWindowEnumerator _windows;

    // Hand-declared: trivial one-liner, same as TaskbarManager/DockWindow.
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    public ExposeWindow(IWindowEnumerator windows)
    {
        _windows = windows;
        InitializeComponent();

        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        // Clicking the dim background (not a cell) dismisses. Cells are
        // wrapped in Borders, so anything without a Border ancestor is
        // background. The preview-tunnel fires before the cell's own
        // click handler, hence the ancestor check instead of a plain close.
        RootGrid.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject source &&
                FindAncestor<Border>(source) is null)
                Close();
        };
        Loaded += (_, _) => BuildCells();
    }

    private static T? FindAncestor<T>(DependencyObject? node) where T : DependencyObject
    {
        while (node is not null)
        {
            if (node is T match)
                return match;
            node = VisualTreeHelper.GetParent(node);
        }
        return null;
    }

    private void BuildCells()
    {
        var windows = _windows.EnumerateIndividual().Take(MaxCells).ToList();
        if (windows.Count == 0)
        {
            Close();
            return;
        }

        int cols = (int)Math.Ceiling(Math.Sqrt(windows.Count));
        var grid = new UniformGrid { Columns = cols };

        foreach (var window in windows)
        {
            var cell = new Grid { Margin = new Thickness(10), Cursor = Cursors.Hand };
            cell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            cell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var thumb = new ThumbnailCell(window.Handle)
            {
                MinHeight = 120,
            };
            Grid.SetRow(thumb, 0);
            cell.Children.Add(thumb);

            var label = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(window.Title)
                    ? System.IO.Path.GetFileNameWithoutExtension(window.ExePath)
                    : window.Title,
                Foreground = Brushes.White,
                FontSize = 12,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 6, 0, 0),
            };
            Grid.SetRow(label, 1);
            cell.Children.Add(label);

            nint hwnd = window.Handle;
            cell.MouseLeftButtonUp += (_, _) =>
            {
                try
                {
                    if (PInvoke.IsIconic((HWND)(IntPtr)hwnd))
                        ShowWindow(hwnd, SW_RESTORE);
                    PInvoke.SetForegroundWindow((HWND)(IntPtr)hwnd);
                }
                catch { /* best effort */ }
                Close();
            };

            var border = new Border
            {
                Child = cell,
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)),
                Background = new SolidColorBrush(Color.FromArgb(0x66, 0x20, 0x20, 0x28)),
                Padding = new Thickness(8),
            };
            grid.Children.Add(border);
        }

        CellsHost.Children.Add(grid);
    }
}
