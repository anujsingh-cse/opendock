using System.Windows;
using System.Windows.Interop;
using OpenDock.Core.Services;
using OpenDock.Interop;

namespace OpenDock;

/// <summary>
/// Owns the v0.2 desktop experience: Launchpad, Spotlight, Exposé, the menu
/// bar, the stack popup, and the global hotkeys. Created once at startup by
/// App; windows are created lazily and toggled, never duplicated.
/// </summary>
public sealed class DesktopCoordinator : IDisposable
{
    private readonly SettingsService _settings;
    private readonly IconService _icons;
    private readonly WindowEnumerator _windows;
    private readonly AppEnumerator _apps;

    private HotkeyManager? _hotkeys;
    private MenuBarWindow? _menuBar;
    private LaunchpadWindow? _launchpad;
    private SpotlightWindow? _spotlight;
    private ExposeWindow? _expose;
    private StackPopup? _stackPopup;
    private DockWindow? _dock;
    private bool _disposed;

    public DesktopCoordinator(
        SettingsService settings,
        IconService icons,
        WindowEnumerator windows,
        AppEnumerator apps)
    {
        _settings = settings;
        _icons = icons;
        _windows = windows;
        _apps = apps;

        _settings.SettingsChanged += (_, _) => ApplySettings();
    }

    /// <summary>Called once the dock window exists; attaches hotkeys + menu bar.</summary>
    public void AttachDock(DockWindow dock)
    {
        _dock = dock;
        ApplySettings();
    }

    private void ApplySettings()
    {
        if (_dock is null)
            return;

        // Hotkeys: rebuild on every settings change (cheap, and picks up edits).
        _hotkeys?.Dispose();
        _hotkeys = new HotkeyManager();
        _hotkeys.Attach(_dock);
        _hotkeys.Register(_settings.Current.LaunchpadHotkey, ToggleLaunchpad);
        _hotkeys.Register(_settings.Current.SpotlightHotkey, ToggleSpotlight);
        _hotkeys.Register(_settings.Current.ExposeHotkey, ToggleExpose);

        // Menu bar visibility follows the setting.
        if (_settings.Current.MenuBarEnabled)
        {
            _menuBar ??= new MenuBarWindow(_settings, this);
            ExcludeFromEnumeration(_menuBar);
            if (!_menuBar.IsVisible)
                _menuBar.Show();
        }
        else
        {
            _menuBar?.Hide();
        }
    }

    private void ExcludeFromEnumeration(Window window)
    {
        try
        {
            _windows.ExcludeWindow(new WindowInteropHelper(window).EnsureHandle());
        }
        catch { /* window not ready yet; harmless */ }
    }

    // ------------------------------------------------------------------ toggles

    public void ToggleLaunchpad()
    {
        if (_launchpad is not null)
        {
            _launchpad.Close();
            _launchpad = null;
            return;
        }
        _launchpad = new LaunchpadWindow(_settings, _icons, _apps);
        ExcludeFromEnumeration(_launchpad);
        _launchpad.Closed += (_, _) => _launchpad = null;
        _launchpad.Show();
    }

    public void ToggleSpotlight()
    {
        if (_spotlight is not null && _spotlight.IsVisible)
        {
            _spotlight.Hide();
            return;
        }
        _spotlight ??= new SpotlightWindow(_settings, _icons, _apps);
        ExcludeFromEnumeration(_spotlight);
        _spotlight.ShowSearch();
    }

    public void ToggleExpose()
    {
        if (_expose is not null)
        {
            _expose.Close();
            _expose = null;
            return;
        }
        _expose = new ExposeWindow(_windows);
        ExcludeFromEnumeration(_expose);
        _expose.Closed += (_, _) => _expose = null;
        _expose.Show();
    }

    public void ShowStack(string folderPath, Point screenAnchor)
    {
        if (_stackPopup is null)
        {
            _stackPopup = new StackPopup(_settings, _icons);
            ExcludeFromEnumeration(_stackPopup);
        }
        _stackPopup.ShowFor(folderPath, screenAnchor);
    }

    // ------------------------------------------------------------------ misc

    public void OpenPreferences(Window owner)
    {
        var prefs = new PreferencesWindow(_settings) { Owner = owner };
        prefs.ShowDialog();
    }

    public void ShowAbout(Window owner)
    {
        MessageBox.Show(owner,
            "OpenDock 0.2 — an open-source macOS-style dock for Windows.\n" +
            "MIT licensed. Written clean-room; no commercial code.\n\n" +
            "Launchpad · Stacks · Menu bar · Spotlight · Trash · Exposé",
            "About OpenDock", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _hotkeys?.Dispose();
    }
}
