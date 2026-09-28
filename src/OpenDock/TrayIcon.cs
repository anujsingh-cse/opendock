using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using OpenDock.Core.Services;

namespace OpenDock;

/// <summary>
/// System-tray icon: the dock has no taskbar button of its own, so this is
/// how users reach Preferences and Quit.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly TaskbarIcon _icon;
    private readonly Bitmap _bitmap; // must stay alive while the HICON is in use
    private bool _disposed;

    public TrayIcon(SettingsService settings, DockWindow dockWindow)
    {
        _bitmap = CreateBitmap();
        _icon = new TaskbarIcon
        {
            ToolTipText = "OpenDock",
            Icon = Icon.FromHandle(_bitmap.GetHicon()),
            Visibility = Visibility.Visible,
        };

        var menu = new ContextMenu();

        var prefsItem = new MenuItem { Header = "Preferences…" };
        prefsItem.Click += (_, _) =>
        {
            var prefs = new PreferencesWindow(settings) { Owner = dockWindow };
            prefs.ShowDialog();
        };

        var quitItem = new MenuItem { Header = "Quit OpenDock" };
        quitItem.Click += (_, _) => Application.Current.Shutdown();

        menu.Items.Add(prefsItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(quitItem);
        _icon.ContextMenu = menu;
    }

    // Draws the tray glyph at runtime so the repo needs no binary .ico asset.
    private static Bitmap CreateBitmap()
    {
        var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var bar = RoundedRect(2, 19, 28, 10, 5))
        using (var brush = new SolidBrush(Color.FromArgb(45, 45, 48)))
            g.FillPath(brush, bar);

        g.FillEllipse(new SolidBrush(Color.FromArgb(0, 122, 204)), 6, 6, 7, 7);
        g.FillEllipse(new SolidBrush(Color.FromArgb(16, 124, 16)), 13, 6, 7, 7);
        g.FillEllipse(new SolidBrush(Color.FromArgb(204, 70, 70)), 20, 6, 7, 7);

        return bmp;
    }

    private static GraphicsPath RoundedRect(int x, int y, int w, int h, int r)
    {
        var path = new GraphicsPath();
        path.AddArc(x, y, r * 2, r * 2, 180, 90);
        path.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
        path.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
        path.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
        path.CloseFigure();
        return path;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _icon.Dispose();
        _bitmap.Dispose();
    }
}
