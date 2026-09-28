using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenDock.Core.Abstractions;
using OpenDock.Core.Models;
using OpenDock.Core.Services;

namespace OpenDock.ViewModels;

/// <summary>View model for one dock tile.</summary>
public sealed partial class DockItemViewModel : ObservableObject
{
    public DockItem Model { get; }

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private ImageSource? _icon;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private int _windowCount = 1;

    public DockItemViewModel(DockItem model, ImageSource? icon)
    {
        Model = model;
        SyncFrom(model, icon);
    }

    public void SyncFrom(DockItem model, ImageSource? icon)
    {
        Model.Kind = model.Kind;
        Model.DisplayName = model.DisplayName;
        Model.TargetPath = model.TargetPath;
        Model.IconKey = model.IconKey;
        Model.WindowHandle = model.WindowHandle;
        Model.IsRunning = model.IsRunning;
        Model.WindowCount = model.WindowCount;
        Model.BadgeCount = model.BadgeCount;
        Model.Progress = model.Progress;

        DisplayName = model.DisplayName;
        Icon = icon;
        IsRunning = model.IsRunning;
        WindowCount = model.WindowCount;
    }
}

/// <summary>
/// Owns the tile list. Reconciles pinned apps with the live window
/// enumeration once per second, updating the collection in place
/// (keyed by <see cref="DockItem.MatchKey"/>) so the UI never churns
/// and magnification state survives refreshes.
/// </summary>
public sealed partial class DockViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly IWindowEnumerator _windows;
    private readonly IIconProvider _icons;
    private readonly DispatcherTimer _refreshTimer;

    public ObservableCollection<DockItemViewModel> Items { get; } = new();

    public DockViewModel(SettingsService settings, IWindowEnumerator windows, IIconProvider icons)
    {
        _settings = settings;
        _windows = windows;
        _icons = icons;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _refreshTimer.Tick += (_, _) => Reconcile();
        _refreshTimer.Start();

        Reconcile();
    }

    public void Refresh() => Reconcile();

    private void Reconcile()
    {
        List<RunningWindowInfo> running;
        try
        {
            running = _windows.Enumerate().ToList();
        }
        catch
        {
            return; // never let enumeration kill the refresh loop
        }

        var runningByExe = new Dictionary<string, RunningWindowInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var w in running)
            runningByExe.TryAdd(w.ExePath, w);

        var desired = new List<DockItem>();
        var pinnedExes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pin in _settings.Current.PinnedApps)
        {
            var item = new DockItem
            {
                Kind = DockItemKind.PinnedApp,
                DisplayName = pin.Name,
                TargetPath = pin.Path,
                IconKey = pin.Path,
                MatchKey = "pin:" + pin.Path.ToLowerInvariant(),
            };
            if (runningByExe.TryGetValue(pin.Path, out var w))
            {
                item.IsRunning = true;
                item.WindowHandle = w.Handle;
                item.WindowCount = w.WindowCount;
                item.DisplayName = string.IsNullOrWhiteSpace(w.Title) ? pin.Name : w.Title;
            }
            pinnedExes.Add(pin.Path);
            desired.Add(item);
        }

        foreach (var w in running)
        {
            if (pinnedExes.Contains(w.ExePath))
                continue;
            desired.Add(new DockItem
            {
                Kind = DockItemKind.RunningWindow,
                DisplayName = string.IsNullOrWhiteSpace(w.Title)
                    ? Path.GetFileNameWithoutExtension(w.ExePath)
                    : w.Title,
                TargetPath = w.ExePath,
                IconKey = w.ExePath,
                WindowHandle = w.Handle,
                IsRunning = true,
                WindowCount = w.WindowCount,
                MatchKey = "run:" + w.ExePath.ToLowerInvariant(),
            });
        }

        var existing = new Dictionary<string, DockItemViewModel>(StringComparer.Ordinal);
        foreach (var vm in Items)
            existing[vm.Model.MatchKey] = vm;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < desired.Count; i++)
        {
            var item = desired[i];
            seen.Add(item.MatchKey);
            if (existing.TryGetValue(item.MatchKey, out var vm))
            {
                vm.SyncFrom(item, _icons.GetIcon(item) as ImageSource);
                int current = Items.IndexOf(vm);
                if (current != i)
                    Items.Move(current, i);
            }
            else
            {
                Items.Insert(i, new DockItemViewModel(item, _icons.GetIcon(item) as ImageSource));
            }
        }

        for (int i = Items.Count - 1; i >= 0; i--)
        {
            if (!seen.Contains(Items[i].Model.MatchKey))
                Items.RemoveAt(i);
        }
    }
}
