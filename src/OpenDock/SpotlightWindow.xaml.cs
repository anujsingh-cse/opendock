using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OpenDock.Core.Models;
using OpenDock.Core.Search;
using OpenDock.Core.Services;
using OpenDock.Interop;

namespace OpenDock;

/// <summary>
/// Spotlight: a centered floating search overlay. Fuzzy-ranks installed
/// apps, pinned items, and files from common locations; Up/Down + Enter to
/// launch, Esc to dismiss. Ranking lives in OpenDock.Core (pure/testable).
/// </summary>
public partial class SpotlightWindow : Window
{
    private const int MaxResults = 9;
    private const int MaxFiles = 1500;

    private readonly SettingsService _settings;
    private readonly IconService _icons;
    private readonly AppEnumerator _apps;

    private sealed record Candidate(
        string Name, string Subtitle, string LaunchPath, string IconKey, ImageSource? Icon);

    private List<Candidate> _candidates = new();

    public SpotlightWindow(SettingsService settings, IconService icons, AppEnumerator apps)
    {
        _settings = settings;
        _icons = icons;
        _apps = apps;
        InitializeComponent();

        SearchBox.TextChanged += (_, _) => RefreshResults();
        SearchBox.KeyDown += OnSearchKeyDown;
        ResultsList.MouseDoubleClick += (_, _) => LaunchSelected();
        Deactivated += (_, _) => Hide();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Hide(); };
    }

    /// <summary>Shows the overlay centered near the top of the work area.</summary>
    public void ShowSearch()
    {
        BuildCandidates();
        SearchBox.Text = string.Empty;
        ResultsList.ItemsSource = null;

        var work = SystemParameters.WorkArea;
        Left = work.Left + (work.Width - Width) / 2;
        Top = work.Top + work.Height * 0.22;

        Show();
        Activate();
        SearchBox.Focus();
    }

    private void BuildCandidates()
    {
        var list = new List<Candidate>();

        foreach (var app in _apps.Apps)
        {
            list.Add(new Candidate(app.Name, app.LaunchPath, app.LaunchPath, app.IconKey,
                _icons.GetIcon(new DockItem { IconKey = app.IconKey }) as ImageSource));
        }

        foreach (var pin in _settings.Current.PinnedApps)
        {
            if (list.Any(c => string.Equals(c.LaunchPath, pin.Path, StringComparison.OrdinalIgnoreCase)))
                continue;
            list.Add(new Candidate(pin.Name, pin.Path, pin.Path, pin.Path,
                _icons.GetIcon(new DockItem { IconKey = pin.Path }) as ImageSource));
        }

        foreach (var file in ScanFiles())
        {
            list.Add(new Candidate(
                Path.GetFileNameWithoutExtension(file),
                file, file, file,
                _icons.GetIcon(new DockItem { IconKey = file }) as ImageSource));
        }

        _candidates = list;
    }

    /// <summary>
    /// Bounded scan of the places people actually look: Desktop, Documents,
    /// Downloads. Depth-limited and capped so it stays instant.
    /// </summary>
    private static IEnumerable<string> ScanFiles()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
        };

        int yielded = 0;
        foreach (string root in roots)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                continue;
            foreach (string file in ScanDepth(root, 0))
            {
                yield return file;
                if (++yielded >= MaxFiles)
                    yield break;
            }
        }
    }

    private static IEnumerable<string> ScanDepth(string dir, int depth)
    {
        if (depth > 3)
            yield break;

        string[] entries;
        try
        {
            entries = Directory.GetFileSystemEntries(dir);
        }
        catch
        {
            yield break;
        }

        foreach (string entry in entries)
        {
            FileAttributes attrs;
            try { attrs = File.GetAttributes(entry); }
            catch { continue; }
            if ((attrs & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                continue;

            if ((attrs & FileAttributes.Directory) != 0)
            {
                foreach (var nested in ScanDepth(entry, depth + 1))
                    yield return nested;
            }
            else
            {
                yield return entry;
            }
        }
    }

    private void RefreshResults()
    {
        string q = SearchBox.Text;
        if (q.Trim().Length == 0)
        {
            ResultsList.ItemsSource = null;
            return;
        }

        var ranked = SpotlightRanker.Rank(q, _candidates, c => c.Name, c => c.Subtitle + " " + c.Name);
        ResultsList.ItemsSource = ranked.Take(MaxResults).Select(r => r.Item).ToList();
        if (ResultsList.Items.Count > 0)
            ResultsList.SelectedIndex = 0;
    }

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down || e.Key == Key.Up)
        {
            int count = ResultsList.Items.Count;
            if (count == 0)
                return;
            int next = ResultsList.SelectedIndex + (e.Key == Key.Down ? 1 : -1);
            ResultsList.SelectedIndex = (next + count) % count;
            ResultsList.ScrollIntoView(ResultsList.SelectedItem);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            LaunchSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
        }
    }

    private void LaunchSelected()
    {
        if (ResultsList.SelectedItem is not Candidate candidate)
            return;
        try
        {
            Process.Start(new ProcessStartInfo(candidate.LaunchPath) { UseShellExecute = true });
            Hide();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Couldn't open {candidate.Name}:\n{ex.Message}", "OpenDock",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
