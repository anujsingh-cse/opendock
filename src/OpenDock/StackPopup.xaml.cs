using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using OpenDock.Core.Models;
using OpenDock.Core.Services;
using OpenDock.Interop;

namespace OpenDock;

/// <summary>
/// A folder stack: click a folder tile on the dock and its contents open
/// in a macOS-style popup — fan, grid, or list. Files drag straight out of
/// the popup; subfolders drill in with a back button.
/// </summary>
public partial class StackPopup : Window
{
    private readonly SettingsService _settings;
    private readonly IconService _icons;
    private readonly Stack<string> _history = new();

    private string _folderPath = string.Empty;
    private StackViewMode _view;
    private string _sort = "Name"; // Name | Date | Type

    public StackPopup(SettingsService settings, IconService icons)
    {
        _settings = settings;
        _icons = icons;
        InitializeComponent();

        Deactivated += (_, _) => Hide();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Hide(); };
    }

    /// <summary>Opens the popup for a folder, anchored above a screen point.</summary>
    public void ShowFor(string folderPath, Point screenAnchor)
    {
        if (!Directory.Exists(folderPath))
            return;
        _history.Clear();
        _folderPath = folderPath;
        _view = _settings.Current.StackViews.TryGetValue(folderPath, out var saved)
            ? saved
            : _settings.Current.DefaultStackView;
        Render();
        // Position above the anchor; the window sizes to content.
        Left = screenAnchor.X - 200;
        Top = screenAnchor.Y - 480;
        if (Left < 0) Left = 8;
        if (Top < 0) Top = 8;
        Show();
        Activate();
    }

    private void Render()
    {
        TitleText.Text = _history.Count > 0
            ? Path.GetFileName(_folderPath)
            : Path.GetFileName(_folderPath.TrimEnd(Path.DirectorySeparatorChar));
        BackButton.Visibility = _history.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        var entries = ListEntries();
        ViewHost.Content = _view switch
        {
            StackViewMode.Fan => BuildFan(entries),
            StackViewMode.List => BuildList(entries),
            _ => BuildGrid(entries),
        };
    }

    private List<FileSystemInfo> ListEntries()
    {
        var dir = new DirectoryInfo(_folderPath);
        FileSystemInfo[] all;
        try
        {
            all = dir.GetFileSystemInfos();
        }
        catch
        {
            return new List<FileSystemInfo>();
        }

        IEnumerable<FileSystemInfo> q = all.Where(f => (f.Attributes & FileAttributes.Hidden) == 0);
        q = _sort switch
        {
            "Date" => q.OrderByDescending(f => f.LastWriteTime),
            "Type" => q.OrderBy(f => f is DirectoryInfo ? 0 : 1)
                        .ThenBy(f => Path.GetExtension(f.Name), StringComparer.OrdinalIgnoreCase)
                        .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase),
            _ => q.OrderBy(f => f is DirectoryInfo ? 0 : 1)
                  .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase),
        };
        return q.Take(200).ToList();
    }

    // ------------------------------------------------------------------ views

    private FrameworkElement BuildGrid(List<FileSystemInfo> entries)
    {
        var grid = new UniformGrid { Columns = 4, MaxWidth = 420 };
        foreach (var entry in entries)
            grid.Children.Add(BuildGridTile(entry));
        return grid;
    }

    private Button BuildGridTile(FileSystemInfo entry)
    {
        var stack = new StackPanel();
        stack.Children.Add(new Image
        {
            Source = _icons.GetIcon(new DockItem { IconKey = entry.FullName }) as ImageSource,
            Width = 48,
            Height = 48,
            Stretch = Stretch.Uniform,
        });
        stack.Children.Add(new TextBlock
        {
            Text = entry.Name,
            Foreground = Brushes.White,
            FontSize = 11,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 96,
        });

        var tile = new Button
        {
            Content = stack,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6),
            Cursor = Cursors.Hand,
            ToolTip = entry.FullName,
        };
        WireTile(tile, entry);
        return tile;
    }

    private FrameworkElement BuildList(List<FileSystemInfo> entries)
    {
        var panel = new StackPanel { MaxWidth = 420, MaxHeight = 420 };
        var scroll = new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 420,
        };
        foreach (var entry in entries)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new Image
            {
                Source = _icons.GetIcon(new DockItem { IconKey = entry.FullName }) as ImageSource,
                Width = 22,
                Height = 22,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(0, 0, 8, 0),
            });
            row.Children.Add(new TextBlock
            {
                Text = entry.Name,
                Foreground = Brushes.White,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 300,
            });
            if (entry is FileInfo fi)
            {
                row.Children.Add(new TextBlock
                {
                    Text = FormatSize(fi.Length),
                    Foreground = new SolidColorBrush(Color.FromArgb(0xAA, 0xFF, 0xFF, 0xFF)),
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0),
                });
            }

            var tile = new Button
            {
                Content = row,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6, 4, 6, 4),
                Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                ToolTip = entry.FullName,
            };
            WireTile(tile, entry);
            panel.Children.Add(tile);
        }
        return scroll;
    }

    private FrameworkElement BuildFan(List<FileSystemInfo> entries)
    {
        // An arc of tiles fanning upward from the dock tile, macOS-style.
        const double radius = 190;
        const double spread = 140; // degrees
        var shown = entries.Take(12).ToList();

        var canvas = new Canvas
        {
            Width = radius * 2 + 120,
            Height = radius + 130,
        };

        for (int i = 0; i < shown.Count; i++)
        {
            double t = shown.Count == 1 ? 0.5 : (double)i / (shown.Count - 1);
            double angleDeg = 90 - spread / 2 + t * spread;
            double angle = angleDeg * Math.PI / 180;
            double cx = canvas.Width / 2;
            double cy = canvas.Height - 40;
            double x = cx + radius * Math.Cos(angle) - 45;
            double y = cy - radius * Math.Sin(angle) - 45;

            var tile = BuildGridTile(shown[i]);
            tile.Width = 90;
            Canvas.SetLeft(tile, x);
            Canvas.SetTop(tile, y);
            canvas.Children.Add(tile);
        }
        return canvas;
    }

    // ------------------------------------------------------------------ behavior

    private void WireTile(Button tile, FileSystemInfo entry)
    {
        Point press = new();
        tile.PreviewMouseLeftButtonDown += (_, e) => { press = e.GetPosition(this); };
        tile.PreviewMouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed)
                return;
            var delta = e.GetPosition(this) - press;
            if (Math.Abs(delta.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(delta.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                // Drag the real file out: Explorer handles copy/move.
                DragDrop.DoDragDrop(tile,
                    new DataObject(DataFormats.FileDrop, new[] { entry.FullName }),
                    DragDropEffects.Copy | DragDropEffects.Move);
            }
        };
        tile.Click += (_, _) => OpenEntry(entry);
    }

    private void OpenEntry(FileSystemInfo entry)
    {
        try
        {
            if (entry is DirectoryInfo)
            {
                _history.Push(_folderPath);
                _folderPath = entry.FullName;
                Render();
                return;
            }
            Process.Start(new ProcessStartInfo(entry.FullName) { UseShellExecute = true });
            Hide();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Couldn't open {entry.Name}:\n{ex.Message}", "OpenDock",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return $"{size:0.#} {units[unit]}";
    }

    // ------------------------------------------------------------------ chrome

    private void SetView(StackViewMode view)
    {
        _view = view;
        _settings.Update(s => s.StackViews[_folderPath] = view);
        Render();
    }

    private void OnViewFan(object sender, RoutedEventArgs e) => SetView(StackViewMode.Fan);
    private void OnViewGrid(object sender, RoutedEventArgs e) => SetView(StackViewMode.Grid);
    private void OnViewList(object sender, RoutedEventArgs e) => SetView(StackViewMode.List);

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        if (_history.Count > 0)
        {
            _folderPath = _history.Pop();
            Render();
        }
    }
}
