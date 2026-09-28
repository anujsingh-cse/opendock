using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using OpenDock.Core.Services;
using OpenDock.Interop;
using OpenDock.ViewModels;

namespace OpenDock;

public partial class App : Application
{
    private Mutex? _instanceMutex;
    private TaskbarManager? _taskbarManager;
    private TrayIcon? _trayIcon;
    private DesktopCoordinator? _coordinator;

    protected override void OnStartup(StartupEventArgs e)
    {
        _instanceMutex = new Mutex(initiallyOwned: true, name: "OpenDock_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            // Another OpenDock is already running.
            Shutdown();
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, args) => LogCrash(args.ExceptionObject as Exception);
        DispatcherUnhandledException += (_, args) =>
        {
            LogCrash(args.Exception);
            args.Handled = true; // keep the dock alive through non-fatal UI errors
        };

        var settings = new SettingsService();
        settings.Load();

        _taskbarManager = new TaskbarManager();
        if (settings.Current.HideTaskbar)
            _taskbarManager.Hide();

        // Toggling "hide taskbar" in Preferences applies immediately.
        settings.SettingsChanged += (_, _) =>
        {
            if (settings.Current.HideTaskbar)
                _taskbarManager?.Hide();
            else
                _taskbarManager?.Show();
        };

        var enumerator = new WindowEnumerator();
        var icons = new IconService();
        var apps = new AppEnumerator();
        apps.Refresh();
        var viewModel = new DockViewModel(settings, enumerator, icons);

        // The v0.2 desktop experience: Launchpad, Spotlight, Exposé,
        // menu bar, stacks, and the global hotkeys behind them.
        _coordinator = new DesktopCoordinator(settings, icons, enumerator, apps);

        var dockWindow = new DockWindow(viewModel, settings, _coordinator);
        // Don't list our own window as a running app.
        enumerator.ExcludeWindow(new WindowInteropHelper(dockWindow).EnsureHandle());
        _coordinator.AttachDock(dockWindow);

        _trayIcon = new TrayIcon(settings, dockWindow, _coordinator);

        MainWindow = dockWindow;
        dockWindow.Show();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Always give the user their Windows taskbar back.
        try { _taskbarManager?.Show(); } catch { /* best effort */ }
        _trayIcon?.Dispose();
        _coordinator?.Dispose();
        try { _instanceMutex?.ReleaseMutex(); } catch { /* already released */ }
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }

    private static void LogCrash(Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(SettingsService.SettingsDirectory);
            File.AppendAllText(
                Path.Combine(SettingsService.SettingsDirectory, "crash.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}");
        }
        catch
        {
            // The crash logger must never throw.
        }
    }
}
