using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using OpenDock.Core.Services;
using OpenDock.Interop;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace OpenDock;

/// <summary>
/// The macOS-style top menu bar: OpenDock menu + active app name on the
/// left; Spotlight pill, Control Center, clock, and show-desktop on the
/// right. Translucent; independent of the Windows taskbar hide feature.
/// Volume is real (CoreAudio); Wi-Fi/Bluetooth deep-link to Windows
/// Settings; brightness is honestly out of reach from here.
/// </summary>
public partial class MenuBarWindow : Window
{
    private readonly SettingsService _settings;
    private readonly DesktopCoordinator _coordinator;
    private readonly DispatcherTimer _timer;
    private bool _volumeUpdating;

    public MenuBarWindow(SettingsService settings, DesktopCoordinator coordinator)
    {
        _settings = settings;
        _coordinator = coordinator;
        InitializeComponent();

        PositionBar();
        Loaded += (_, _) => PositionBar();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        Tick();
    }

    private void PositionBar()
    {
        var work = SystemParameters.WorkArea;
        Left = work.Left;
        Top = 0;
        Width = work.Width;
    }

    private void Tick()
    {
        ClockText.Text = DateTime.Now.ToString("ddd d MMM   HH:mm");
        AppNameText.Text = ActiveAppName();
    }

    private static string ActiveAppName()
    {
        try
        {
            nint hwnd = (nint)(IntPtr)PInvoke.GetForegroundWindow();
            if (hwnd == nint.Zero)
                return "OpenDock";
            uint pid = GetWindowPid((HWND)(IntPtr)hwnd);
            var process = Process.GetProcessById((int)pid);
            string? path = process.MainModule?.FileName;
            if (!string.IsNullOrEmpty(path) &&
                path.Contains("OpenDock", StringComparison.OrdinalIgnoreCase))
                return "OpenDock";
            if (!string.IsNullOrEmpty(path))
            {
                string? desc = FileVersionInfo.GetVersionInfo(path).FileDescription;
                if (!string.IsNullOrWhiteSpace(desc))
                    return desc;
            }
            return process.ProcessName;
        }
        catch
        {
            return "OpenDock";
        }
    }

    private static unsafe uint GetWindowPid(HWND hwnd)
    {
        uint pid = 0;
        PInvoke.GetWindowThreadProcessId(hwnd, &pid);
        return pid;
    }

    // ------------------------------------------------------------------ menu

    private void OnMenuClick(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();

        var about = new MenuItem { Header = "About OpenDock" };
        about.Click += (_, _) => _coordinator.ShowAbout(this);
        menu.Items.Add(about);
        menu.Items.Add(new Separator());

        var prefs = new MenuItem { Header = "Preferences…" };
        prefs.Click += (_, _) => _coordinator.OpenPreferences(this);
        menu.Items.Add(prefs);

        var launchpad = new MenuItem { Header = "Launchpad" };
        launchpad.Click += (_, _) => _coordinator.ToggleLaunchpad();
        menu.Items.Add(launchpad);

        var spotlight = new MenuItem { Header = "Spotlight Search" };
        spotlight.Click += (_, _) => _coordinator.ToggleSpotlight();
        menu.Items.Add(spotlight);
        menu.Items.Add(new Separator());

        var sleep = new MenuItem { Header = "Sleep" };
        sleep.Click += (_, _) => SystemPower.Sleep();
        menu.Items.Add(sleep);

        var lockItem = new MenuItem { Header = "Lock Screen" };
        lockItem.Click += (_, _) => SystemPower.Lock();
        menu.Items.Add(lockItem);

        var logoff = new MenuItem { Header = "Log Off…" };
        logoff.Click += (_, _) => ConfirmAnd("Log off now?", () => { SystemPower.LogOff(); });
        menu.Items.Add(logoff);
        menu.Items.Add(new Separator());

        var restart = new MenuItem { Header = "Restart…" };
        restart.Click += (_, _) => ConfirmAnd("Restart now?", SystemPower.Restart);
        menu.Items.Add(restart);

        var shutdown = new MenuItem { Header = "Shut Down…" };
        shutdown.Click += (_, _) => ConfirmAnd("Shut down now?", SystemPower.ShutDown);
        menu.Items.Add(shutdown);

        menu.PlacementTarget = MenuButton;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private static void ConfirmAnd(string question, Action action)
    {
        if (MessageBox.Show(question, "OpenDock",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            action();
    }

    // ------------------------------------------------------------------ right side

    private void OnSpotlightClick(object sender, RoutedEventArgs e) => _coordinator.ToggleSpotlight();

    private void OnControlCenterClick(object sender, RoutedEventArgs e)
    {
        int? volume = VolumeControl.GetVolume();
        _volumeUpdating = true;
        VolumeSlider.Value = volume ?? 50;
        VolumeSlider.IsEnabled = volume.HasValue;
        MuteButton.IsEnabled = volume.HasValue;
        bool? muted = VolumeControl.GetMute();
        MuteButton.Content = muted == true ? "Unmute" : "Mute";
        _volumeUpdating = false;
        ControlCenterPopup.IsOpen = !ControlCenterPopup.IsOpen;
    }

    private void OnVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_volumeUpdating)
            return;
        VolumeControl.SetVolume((int)e.NewValue);
    }

    private void OnMuteClick(object sender, RoutedEventArgs e)
    {
        bool muted = MuteButton.Content as string == "Mute";
        VolumeControl.SetMute(muted);
        MuteButton.Content = muted ? "Unmute" : "Mute";
    }

    private void OnWifiSettings(object sender, RoutedEventArgs e) =>
        SystemPower.OpenSettingsPage("ms-settings:network-wifi");

    private void OnBluetoothSettings(object sender, RoutedEventArgs e) =>
        SystemPower.OpenSettingsPage("ms-settings:bluetooth");

    private void OnShowDesktopClick(object sender, RoutedEventArgs e)
    {
        // The shell's own "show desktop": minimizes everything, restores on
        // a second press — exactly what the taskbar corner does.
        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType is null)
                return;
            dynamic shell = Activator.CreateInstance(shellType)!;
            shell.MinimizeAll();
        }
        catch { /* best effort */ }
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
    }
}
