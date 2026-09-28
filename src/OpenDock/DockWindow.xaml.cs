using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using OpenDock.Core.Layout;
using OpenDock.Core.Models;
using OpenDock.Core.Services;
using OpenDock.Interop;
using OpenDock.ViewModels;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace OpenDock;

/// <summary>
/// The dock itself: a borderless, topmost, transparent window whose tiles
/// live on a Canvas and are repositioned every frame while the cursor is
/// over the dock. Hover position is read from the real cursor
/// (GetCursorPos → PointFromScreen) rather than WPF mouse events, which
/// are unreliable once tiles start moving under the pointer.
/// </summary>
public partial class DockWindow : Window
{
    private const double BarPadding = 10;
    private const double IconFill = 0.84; // artwork draws smaller than its cell, like macOS
    private const int HoverPreviewDelayMs = 400;
    private const uint WM_CLOSE = 0x10;

    // Hand-declared: trivial signatures, zero CsWin32 overload ambiguity.
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    private const int SW_MINIMIZE = 6;
    private const int SW_RESTORE = 9;

    private readonly DockViewModel _viewModel;
    private readonly SettingsService _settings;
    private readonly PreviewService _previews = new();
    private readonly Dictionary<Guid, IconVisual> _visuals = new();
    private readonly DispatcherTimer _previewTimer;
    private readonly DispatcherTimer _autoHideTimer;
    private IconVisual? _previewTarget;
    private bool _renderLoopAttached;

    public DockWindow(DockViewModel viewModel, SettingsService settings)
    {
        _viewModel = viewModel;
        _settings = settings;
        InitializeComponent();

        ApplyTheme();
        PositionDock();

        _viewModel.Items.CollectionChanged += (_, _) => SyncVisuals();
        SyncVisuals();

        MouseEnter += (_, _) => AttachRenderLoop();
        MouseLeave += (_, _) =>
        {
            DetachRenderLoop();
            _previews.Hide();
        };
        Drop += OnDrop;
        DragOver += (_, e) =>
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        };
        _settings.SettingsChanged += (_, _) =>
        {
            ApplyTheme();
            PositionDock();
            AttachRenderLoop(); // one frame so the new sizes take effect
        };

        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(HoverPreviewDelayMs) };
        _previewTimer.Tick += (_, _) =>
        {
            _previewTimer.Stop();
            ShowPreview();
        };

        _autoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _autoHideTimer.Tick += (_, _) => UpdateAutoHide();
        _autoHideTimer.Start();

        Loaded += (_, _) => PositionDock();
        Closed += (_, _) => _previews.Dispose();
    }

    // ------------------------------------------------------------------ layout

    private void AttachRenderLoop()
    {
        if (_renderLoopAttached)
            return;
        _renderLoopAttached = true;
        CompositionTarget.Rendering += OnRendering;
    }

    private void DetachRenderLoop()
    {
        if (!_renderLoopAttached)
            return;
        _renderLoopAttached = false;
        CompositionTarget.Rendering -= OnRendering;
        LayoutIcons(cursorX: null); // settle back to unmagnified
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!PInvoke.GetCursorPos(out System.Drawing.Point cursorScreen))
            return;
        Point cursor = PointFromScreen(new Point(cursorScreen.X, cursorScreen.Y));
        LayoutIcons(cursor.X - BarPadding);
    }

    private void LayoutIcons(double? cursorX)
    {
        var s = _settings.Current;
        int n = _viewModel.Items.Count;
        if (n == 0)
            return;

        var layouts = DockLayoutEngine.ComputeLayout(
            n, s.IconSize, cursorX, s.MagnificationEnabled, s.MagnifiedSize, s.MagnificationRange);

        double total = DockLayoutEngine.TotalWidth(layouts);
        double targetWidth = total + BarPadding * 2;
        if (Math.Abs(Width - targetWidth) > 0.5)
        {
            Width = targetWidth;
            PositionDock(); // keep centered while the bar breathes
        }

        double contentHeight = s.MagnifiedSize; // fixed; tiles overflow upward, bottom-anchored
        IconCanvas.Width = total;
        IconCanvas.Height = contentHeight;

        for (int i = 0; i < n; i++)
        {
            var vm = _viewModel.Items[i];
            if (!_visuals.TryGetValue(vm.Model.Id, out var visual))
                continue;
            var layout = layouts[i];
            double draw = layout.Size * IconFill;
            Canvas.SetLeft(visual.Root, layout.Offset + (layout.Size - draw) / 2);
            Canvas.SetTop(visual.Root, contentHeight - draw);
            visual.Root.Width = draw;
            visual.Root.Height = draw;
        }
    }

    private void PositionDock()
    {
        var s = _settings.Current;
        Height = s.MagnifiedSize + BarPadding * 2;

        // TODO: multi-monitor — place on the monitor containing the cursor.
        // TODO: Left/Right orientation rotates the bar (settings value exists).
        var work = SystemParameters.WorkArea;
        Left = work.Left + (work.Width - Width) / 2;
        Top = work.Bottom - Height - 4;
    }

    private void ApplyTheme()
    {
        bool dark = _settings.Current.Theme switch
        {
            DockTheme.Dark => true,
            DockTheme.Light => false,
            _ => IsSystemDark(),
        };
        Bar.Background = new SolidColorBrush(dark
            ? Color.FromArgb(0x66, 0x28, 0x28, 0x2E)
            : Color.FromArgb(0x66, 0xF2, 0xF2, 0xF2));
        Bar.BorderBrush = new SolidColorBrush(dark
            ? Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)
            : Color.FromArgb(0x40, 0x00, 0x00, 0x00));
        foreach (var visual in _visuals.Values)
            visual.ApplyTheme(dark);
    }

    private static bool IsSystemDark()
    {
        try
        {
            object? value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1);
            return Convert.ToInt32(value) == 0;
        }
        catch
        {
            return false;
        }
    }

    // ------------------------------------------------------------------ visuals

    private void SyncVisuals()
    {
        var seen = new HashSet<Guid>();
        foreach (var vm in _viewModel.Items)
        {
            seen.Add(vm.Model.Id);
            if (!_visuals.TryGetValue(vm.Model.Id, out var visual))
            {
                visual = new IconVisual(vm);
                visual.Root.MouseLeftButtonUp += (_, _) => Activate(vm);
                visual.Root.MouseRightButtonUp += (_, e) => ShowContextMenu(visual, vm);
                visual.Root.MouseEnter += (_, _) => BeginHoverPreview(visual);
                visual.Root.MouseLeave += (_, _) => CancelHoverPreview(visual);
                IconCanvas.Children.Add(visual.Root);
                _visuals[vm.Model.Id] = visual;
            }
            visual.Sync(vm);
        }

        foreach (var id in _visuals.Keys.Where(k => !seen.Contains(k)).ToList())
        {
            IconCanvas.Children.Remove(_visuals[id].Root);
            _visuals.Remove(id);
        }

        AttachRenderLoop(); // one frame to settle the new layout
    }

    /// <summary>One tile: the icon plus a running indicator dot.</summary>
    private sealed class IconVisual
    {
        public Grid Root { get; } = new() { Background = Brushes.Transparent };
        public Image IconImage { get; } = new() { Stretch = Stretch.Uniform };
        public Ellipse RunningDot { get; } = new()
        {
            Width = 5,
            Height = 5,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, -9),
        };

        public DockItemViewModel Vm { get; private set; }

        public IconVisual(DockItemViewModel vm)
        {
            Vm = vm; // set directly: CS8618 can't see the assignment inside Sync()
            Root.Children.Add(IconImage);
            Root.Children.Add(RunningDot);
            Sync(vm);
        }

        public void Sync(DockItemViewModel vm)
        {
            Vm = vm;
            IconImage.Source = vm.Icon;
            RunningDot.Visibility = vm.IsRunning ? Visibility.Visible : Visibility.Collapsed;
            Root.ToolTip = vm.WindowCount > 1
                ? $"{vm.DisplayName} ({vm.WindowCount} windows)"
                : vm.DisplayName;
        }

        public void ApplyTheme(bool dark)
        {
            RunningDot.Fill = new SolidColorBrush(dark ? Colors.WhiteSmoke : Colors.DimGray);
        }
    }

    // ------------------------------------------------------------------ interaction

    private void Activate(DockItemViewModel vm)
    {
        var m = vm.Model;

        if (m.Kind == DockItemKind.PinnedApp && !m.IsRunning && !string.IsNullOrEmpty(m.TargetPath))
        {
            try
            {
                Process.Start(new ProcessStartInfo(m.TargetPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Couldn't launch {m.DisplayName}:\n{ex.Message}", "OpenDock",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            return;
        }

        if (m.WindowHandle == nint.Zero)
            return;

        var hwnd = (HWND)(IntPtr)m.WindowHandle;
        if ((nint)(IntPtr)PInvoke.GetForegroundWindow() == m.WindowHandle)
        {
            // Already focused → minimize, macOS-style.
            ShowWindow(m.WindowHandle, SW_MINIMIZE);
            if (_visuals.TryGetValue(m.Id, out var visual))
                PlayMinimizeEffect(visual);
        }
        else
        {
            if (PInvoke.IsIconic(hwnd))
                ShowWindow(m.WindowHandle, SW_RESTORE);
            PInvoke.SetForegroundWindow(hwnd);
        }
    }

    private void PlayMinimizeEffect(IconVisual visual)
    {
        // v0.1: a smooth scale pulse on the tile. True genie/suck warps need a
        // D3D overlay (roadmap); the setting picks the feel of the pulse.
        var (durationMs, easing) = _settings.Current.MinimizeEffect switch
        {
            MinimizeEffect.Genie => (260, (IEasingFunction)new CubicEase { EasingMode = EasingMode.EaseOut }),
            MinimizeEffect.Suck => (140, (IEasingFunction)new QuadraticEase { EasingMode = EasingMode.EaseIn }),
            _ => (200, (IEasingFunction)new QuadraticEase { EasingMode = EasingMode.EaseInOut }),
        };

        visual.Root.RenderTransformOrigin = new Point(0.5, 0.5);
        var transform = new ScaleTransform(1, 1);
        visual.Root.RenderTransform = transform;

        var duration = TimeSpan.FromMilliseconds(durationMs);
        var animX = new DoubleAnimation(1, 0.6, duration) { EasingFunction = easing, AutoReverse = true };
        var animY = new DoubleAnimation(1, 0.6, duration) { EasingFunction = easing, AutoReverse = true };
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, animX);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, animY);
    }

    private void ShowContextMenu(IconVisual visual, DockItemViewModel vm)
    {
        var m = vm.Model;
        var menu = new ContextMenu();

        var header = new MenuItem { Header = m.DisplayName, IsEnabled = false };
        menu.Items.Add(header);
        menu.Items.Add(new Separator());

        var openItem = new MenuItem { Header = m.IsRunning ? "Show" : "Open" };
        openItem.Click += (_, _) => Activate(vm);
        menu.Items.Add(openItem);

        if (m.Kind == DockItemKind.PinnedApp)
        {
            var removeItem = new MenuItem { Header = "Remove from Dock" };
            removeItem.Click += (_, _) =>
            {
                _settings.Update(s => s.PinnedApps.RemoveAll(p =>
                    string.Equals(p.Path, m.TargetPath, StringComparison.OrdinalIgnoreCase)));
                _viewModel.Refresh();
            };
            menu.Items.Add(removeItem);
        }

        if (m.IsRunning && m.WindowHandle != nint.Zero)
        {
            var closeItem = new MenuItem { Header = "Close Window" };
            closeItem.Click += (_, _) => PInvoke.PostMessage(
                (HWND)(IntPtr)m.WindowHandle, WM_CLOSE, (WPARAM)(nuint)0, (LPARAM)(nint)0);
            menu.Items.Add(closeItem);
        }

        menu.PlacementTarget = visual.Root;
        menu.IsOpen = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)
            return;

        bool changed = false;
        _settings.Update(s =>
        {
            foreach (string f in files)
            {
                if (s.PinnedApps.Any(p => string.Equals(p.Path, f, StringComparison.OrdinalIgnoreCase)))
                    continue;
                s.PinnedApps.Add(new PinnedApp
                {
                    Name = System.IO.Path.GetFileNameWithoutExtension(f.TrimEnd('\\')),
                    Path = f,
                });
                changed = true;
            }
        });

        if (changed)
            _viewModel.Refresh();
        e.Handled = true;
    }

    // ------------------------------------------------------------------ hover previews

    private void BeginHoverPreview(IconVisual visual)
    {
        if (!_settings.Current.ShowWindowPreviews || visual.Vm.Model.WindowHandle == nint.Zero)
            return;
        _previewTarget = visual;
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private void CancelHoverPreview(IconVisual visual)
    {
        if (_previewTarget == visual)
        {
            _previewTimer.Stop();
            _previewTarget = null;
        }
        _previews.Hide();
    }

    private void ShowPreview()
    {
        if (_previewTarget?.Vm.Model is not { WindowHandle: not 0 } model)
            return;
        Point anchor = _previewTarget.Root.PointToScreen(
            new Point(_previewTarget.Root.Width / 2, 0));
        _previews.Show(model.WindowHandle, anchor);
    }

    // ------------------------------------------------------------------ auto-hide

    private void UpdateAutoHide()
    {
        if (!_settings.Current.AutoHide)
        {
            if (Visibility != Visibility.Visible)
                Visibility = Visibility.Visible;
            return;
        }

        if (!PInvoke.GetCursorPos(out System.Drawing.Point cursorScreen))
            return;

        var work = SystemParameters.WorkArea;
        // TODO: per-position trigger edges when Left/Right orientation lands.
        bool nearEdge = cursorScreen.Y >= work.Bottom - 4;
        bool overDock = Visibility == Visibility.Visible &&
            new Rect(Left, Top, Width, Height).Contains(new Point(cursorScreen.X, cursorScreen.Y));

        Visibility = nearEdge || overDock ? Visibility.Visible : Visibility.Hidden;
    }
}
