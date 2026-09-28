using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace OpenDock.Interop;

/// <summary>
/// Global hotkeys (RegisterHotKey) bound to a window's message loop.
/// Hotkeys are configured as strings like "Ctrl+Alt+L" and parsed here;
/// registration failures (e.g. the combo is taken by Windows) are
/// swallowed — the feature degrades to its button, never a crash.
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    // Hand-declared: tiny signatures, and failure must be silent.
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);

    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;

    private readonly Dictionary<int, Action> _actions = new();
    private HwndSource? _source;
    private int _nextId = 1;
    private bool _disposed;

    /// <summary>Hooks the window's message loop to receive WM_HOTKEY.</summary>
    public void Attach(Window window)
    {
        nint hwnd = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(hwnd);
        _source?.AddHook(WndProc);
    }

    /// <summary>
    /// Registers "Ctrl+Alt+L"-style hotkey text. Returns false when the text
    /// doesn't parse or Windows refuses the combo.
    /// </summary>
    public bool Register(string hotkeyText, Action action)
    {
        if (_source is null || !TryParse(hotkeyText, out uint modifiers, out uint vk))
            return false;

        int id = _nextId++;
        try
        {
            if (!RegisterHotKey(_source.Handle, id, modifiers, vk))
                return false;
        }
        catch
        {
            return false;
        }

        _actions[id] = action;
        return true;
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            try { action(); } catch { /* hotkey actions must never throw into the pump */ }
            handled = true;
        }
        return nint.Zero;
    }

    public static bool TryParse(string text, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
            return false;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            modifiers |= parts[i].ToLowerInvariant() switch
            {
                "ctrl" or "control" => MOD_CONTROL,
                "alt" => MOD_ALT,
                "shift" => MOD_SHIFT,
                "win" or "windows" => MOD_WIN,
                _ => 0,
            };
        }

        if (modifiers == 0)
            return false;

        string keyName = parts[^1];
        if (!Enum.TryParse<Key>(keyName, ignoreCase: true, out var key))
            return false;

        try
        {
            vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        }
        catch
        {
            return false;
        }
        return vk != 0;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_source is not null)
        {
            foreach (int id in _actions.Keys)
            {
                try { UnregisterHotKey(_source.Handle, id); } catch { /* best effort */ }
            }
            _source.RemoveHook(WndProc);
            _source = null;
        }
        _actions.Clear();
    }
}
