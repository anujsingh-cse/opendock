using System.Diagnostics;
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
/// Launchpad: a full-screen, paged grid of every installed app.
/// Drag a tile onto another to create/extend a folder, drag to empty
/// space to move it to the end of the page, search to filter, Esc or a
/// background click to dismiss. Order and folders persist in settings.
/// </summary>
public partial class LaunchpadWindow : Window
{
    private const int Columns = 7;
    private const int Rows = 5;
    private const int PageSize = Columns * Rows;

    private readonly SettingsService _settings;
    private readonly IconService _icons;
    private readonly AppEnumerator _apps;

    private readonly List<TileEntry> _entries = new();
    private LaunchpadFolder? _openFolder;
    private int _page;
    private Point _pressPoint;
    private TileEntry? _pressEntry;
    private bool _dragging;

    private sealed class TileEntry
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string LaunchPath { get; init; } = string.Empty;
        public string IconKey { get; init; } = string.Empty;
        public bool IsFolder { get; init; }
        public LaunchpadFolder? Folder { get; init; }
    }

    public LaunchpadWindow(SettingsService settings, IconService icons, AppEnumerator apps)
    {
        _settings = settings;
        _icons = icons;
        _apps = apps;
        InitializeComponent();

        SearchBox.GotFocus += (_, _) => SearchBox.SelectAll();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
            if (e.Key == Key.Left) ShowPage(_page - 1);
            if (e.Key == Key.Right) ShowPage(_page + 1);
        };
        // Clicking the dim background (not a tile) dismisses.
        PageHost.PreviewMouseLeftButtonDown += (_, e) => { _pressPoint = e.GetPosition(this); };
        PageHost.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (!_dragging && e.OriginalSource is not Button)
                Close();
            _dragging = false;
        };
        // Dropping a tile on empty space (or a page dot) moves it to the
        // end of that page instead of making a folder.
        PageHost.AllowDrop = true;
        PageHost.Drop += (_, e) =>
        {
            if (e.Data.GetData("OpenDock.LaunchpadId") is string draggedId)
                MoveToPageEnd(draggedId, _page);
            e.Handled = true;
        };

        Loaded += (_, _) => { RebuildEntries(); ShowPage(0); SearchBox.Focus(); };
    }

    // ------------------------------------------------------------------ data

    private void RebuildEntries()
    {
        _entries.Clear();
        var byId = new Dictionary<string, LaunchpadItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in _apps.Apps)
            byId[app.Id] = app;

        var folders = _settings.Current.LaunchpadFolders;
        var foldered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var folderEntries = new List<TileEntry>();
        foreach (var folder in folders)
        {
            var members = folder.MemberIds.Where(id => byId.ContainsKey(id)).ToList();
            if (members.Count == 0)
                continue;
            foreach (var id in members)
                foldered.Add(id);
            folderEntries.Add(new TileEntry
            {
                Id = "folder:" + folder.Id,
                Name = folder.Name,
                IsFolder = true,
                Folder = folder,
            });
        }

        var loose = byId.Values
            .Where(a => !foldered.Contains(a.Id))
            .Select(a => new TileEntry
            {
                Id = a.Id,
                Name = a.Name,
                LaunchPath = a.LaunchPath,
                IconKey = a.IconKey,
            })
            .ToList();

        // Saved order first, then anything new alphabetically.
        var order = _settings.Current.LaunchpadOrder;
        var ranked = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < order.Count; i++)
            ranked.TryAdd(order[i], i);

        var all = folderEntries.Concat(loose).ToList();
        all.Sort((a, b) =>
        {
            bool ao = ranked.TryGetValue(a.Id, out int ai);
            bool bo = ranked.TryGetValue(b.Id, out int bi);
            if (ao && bo) return ai.CompareTo(bi);
            if (ao) return -1;
            if (bo) return 1;
            return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
        });

        _entries.AddRange(all);
    }

    private void Persist()
    {
        _settings.Update(s =>
        {
            s.LaunchpadOrder = _entries.Select(e => e.Id).ToList();
            // Folders are mutated in place; nothing extra to write.
        });
    }

    // ------------------------------------------------------------------ paging

    private List<TileEntry> VisibleEntries()
    {
        string q = SearchBox.Text.Trim();
        if (_openFolder is not null)
        {
            var byId = _apps.Apps.ToDictionary(a => a.Id, StringComparer.OrdinalIgnoreCase);
            return _openFolder.MemberIds
                .Where(id => byId.ContainsKey(id))
                .Select(id => new TileEntry
                {
                    Id = id,
                    Name = byId[id].Name,
                    LaunchPath = byId[id].LaunchPath,
                    IconKey = byId[id].IconKey,
                })
                .Where(e => q.Length == 0 || e.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        if (q.Length == 0)
            return _entries;
        return _entries
            .Where(e => e.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private void ShowPage(int page)
    {
        var visible = VisibleEntries();
        int pageCount = Math.Max(1, (visible.Count + PageSize - 1) / PageSize);
        _page = Math.Clamp(page, 0, pageCount - 1);

        bool searching = SearchBox.Text.Trim().Length > 0 || _openFolder is not null;
        DotsPanel.Visibility = searching ? Visibility.Collapsed : Visibility.Visible;
        BackButton.Visibility = _openFolder is not null ? Visibility.Visible : Visibility.Collapsed;

        DotsPanel.Children.Clear();
        if (!searching)
        {
            for (int i = 0; i < pageCount; i++)
            {
                int index = i;
                var dot = new Button
                {
                    Width = 10,
                    Height = 10,
                    Margin = new Thickness(5, 0, 5, 0),
                    Background = new SolidColorBrush(i == _page ? Colors.White : Color.FromArgb(0x88, 0xFF, 0xFF, 0xFF)),
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                };
                dot.Click += (_, _) => ShowPage(index);
                dot.AllowDrop = true;
                dot.ToolTip = "Drop a tile here to move it to this page";
                dot.Drop += (_, e) =>
                {
                    if (e.Data.GetData("OpenDock.LaunchpadId") is string draggedId)
                        MoveToPageEnd(draggedId, index);
                    e.Handled = true;
                };
                DotsPanel.Children.Add(dot);
            }
        }

        PageHost.Children.Clear();
        var grid = new UniformGrid { Columns = Columns };
        foreach (var entry in visible.Skip(_page * PageSize).Take(PageSize))
            grid.Children.Add(BuildTile(entry));
        PageHost.Children.Add(grid);
    }

    // ------------------------------------------------------------------ tiles

    private Button BuildTile(TileEntry entry)
    {
        var stack = new StackPanel { Orientation = Orientation.Vertical };

        if (entry.IsFolder && entry.Folder is not null)
            stack.Children.Add(BuildFolderGlyph(entry.Folder));
        else
            stack.Children.Add(new Image
            {
                Source = _icons.GetIcon(new DockItem { IconKey = entry.IconKey }) as ImageSource,
                Width = 64,
                Height = 64,
                Stretch = Stretch.Uniform,
            });

        stack.Children.Add(new TextBlock
        {
            Text = entry.Name,
            Foreground = Brushes.White,
            FontSize = 12,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 110,
            Margin = new Thickness(0, 6, 0, 0),
        });

        var tile = new Button
        {
            Content = stack,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8),
            Cursor = Cursors.Hand,
            Tag = entry,
            AllowDrop = true,
        };

        tile.PreviewMouseLeftButtonDown += (_, e) =>
        {
            _pressPoint = e.GetPosition(this);
            _pressEntry = entry;
            _dragging = false;
        };
        tile.PreviewMouseMove += (_, e) =>
        {
            if (_pressEntry != entry || e.LeftButton != MouseButtonState.Pressed)
                return;
            var delta = e.GetPosition(this) - _pressPoint;
            if (!_dragging && (Math.Abs(delta.X) > SystemParameters.MinimumHorizontalDragDistance ||
                               Math.Abs(delta.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                _dragging = true;
                DragDrop.DoDragDrop(tile, new DataObject("OpenDock.LaunchpadId", entry.Id),
                    DragDropEffects.Move);
                _pressEntry = null;
            }
        };
        tile.Click += (_, _) =>
        {
            if (_dragging)
                return;
            ActivateEntry(entry);
        };
        tile.Drop += (_, e) =>
        {
            if (e.Data.GetData("OpenDock.LaunchpadId") is string draggedId && draggedId != entry.Id)
                OnTileDroppedOn(draggedId, entry);
            e.Handled = true;
        };

        return tile;
    }

    private Grid BuildFolderGlyph(LaunchpadFolder folder)
    {
        var grid = new Grid { Width = 64, Height = 64 };
        for (int i = 0; i < 2; i++)
            grid.RowDefinitions.Add(new RowDefinition());
        for (int i = 0; i < 2; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition());

        var byId = _apps.Apps.ToDictionary(a => a.Id, StringComparer.OrdinalIgnoreCase);
        int i2 = 0;
        foreach (string id in folder.MemberIds.Where(id => byId.ContainsKey(id)).Take(4))
        {
            var img = new Image
            {
                Source = _icons.GetIcon(new DockItem { IconKey = byId[id].IconKey }) as ImageSource,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(2),
            };
            Grid.SetRow(img, i2 / 2);
            Grid.SetColumn(img, i2 % 2);
            grid.Children.Add(img);
            i2++;
        }
        return grid;
    }

    private void ActivateEntry(TileEntry entry)
    {
        if (entry.IsFolder && entry.Folder is not null)
        {
            _openFolder = entry.Folder;
            ShowPage(0);
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(entry.LaunchPath) { UseShellExecute = true });
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Couldn't launch {entry.Name}:\n{ex.Message}", "OpenDock",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnTileDroppedOn(string draggedId, TileEntry target)
    {        if (_openFolder is not null)
            return; // rearranging inside folders is a TODO

        var dragged = _entries.FirstOrDefault(e => e.Id == draggedId);
        if (dragged is null)
            return;

        if (target.IsFolder && target.Folder is not null)
        {
            // Drop into an existing folder.
            _settings.Update(s =>
            {
                var folder = s.LaunchpadFolders.FirstOrDefault(f => "folder:" + f.Id == target.Id);
                if (folder is not null && !folder.MemberIds.Contains(draggedId, StringComparer.OrdinalIgnoreCase))
                    folder.MemberIds.Add(draggedId);
            });
        }
        else
        {
            // Drop on a plain tile: make a new folder holding both.
            _settings.Update(s =>
            {
                var folder = new LaunchpadFolder
                {
                    Name = "Folder",
                    MemberIds = new List<string> { target.Id, draggedId },
                };
                s.LaunchpadFolders.Add(folder);
            });
        }

        RebuildEntries();
        Persist();
        ShowPage(_page);
    }

    /// <summary>
    /// Plain rearrangement: the dragged tile is moved to the end of the
    /// given page and the new order persists.
    /// </summary>
    private void MoveToPageEnd(string draggedId, int page)
    {
        if (_openFolder is not null)
            return; // rearranging inside folders is a TODO

        var dragged = _entries.FirstOrDefault(e => e.Id == draggedId);
        if (dragged is null)
            return;

        _entries.Remove(dragged);
        int insertAt = Math.Min(_entries.Count, (page + 1) * PageSize);
        _entries.Insert(insertAt, dragged);
        Persist();
        ShowPage(page);
    }

    // ------------------------------------------------------------------ chrome

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => ShowPage(0);

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        _openFolder = null;
        ShowPage(0);
    }
}
