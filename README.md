# OpenDock

An open-source macOS-style dock for Windows 10/11. Pin your apps, watch
running windows gather as tiles, magnify icons on hover, and hide the Windows
taskbar for a clean desktop.

> **Clean-room project.** OpenDock is written from scratch and shares no code
> with any commercial dock software. It is inspired by the *idea* of a macOS
> dock on Windows, not by anyone's implementation.

<!-- TODO: replace with a real screenshot once the UI is polished: docs/screenshot.png -->
![OpenDock screenshot](docs/screenshot.png)

## Features (v0.2)

**The dock:**
- macOS-style dock bar: bottom-centered, translucent, rounded
- Icon magnification on hover (raised-cosine falloff, adjustable size and range)
- Pinned apps + live running-app tiles, grouped by executable like the taskbar
- Drag-and-drop files, folders, and exes onto the dock to pin them
- Hover window previews via DWM thumbnails
- Running indicators under active apps
- Click a focused app's tile to minimize it; click again to restore
- Auto-hide with edge trigger
- Optional Windows taskbar hiding (always restored on exit)
- Preferences window: icon size, magnification, theme (light/dark/system),
  minimize effect, auto-hide, menu bar toggle, hotkeys, default stack view
- System-tray icon with Launchpad, Spotlight, Preferences, and Quit
- Settings stored as JSON in `%APPDATA%\OpenDock\settings.json`
- Crash log at `%APPDATA%\OpenDock\crash.log`

**The macOS-like experience:**
- **Launchpad** — full-screen paged grid of every installed app (Start Menu
  shortcuts, including UWP/Store apps via their Start Menu entries), with
  search, page dots, drag-to-rearrange, cross-page moves, and app folders
  (drag one tile onto another)
- **Folder stacks** — pin any folder to the dock; click to fan/grid/list its
  contents, drill into subfolders, drag files straight out, per-folder view
  mode remembered
- **Top menu bar** — OpenDock menu (About, Preferences, Launchpad, Sleep,
  Lock, Log off, Restart, Shut down), live active-app name, Spotlight pill,
  Control Center with a real volume slider (CoreAudio), clock/date, and
  show-desktop — works alongside taskbar hiding
- **Spotlight search** — centered overlay with fuzzy-ranked results across
  installed apps, pins, and your Desktop/Documents/Downloads files;
  keyboard-first (↑↓ Enter Esc)
- **Trash tile** — live Recycle Bin icon (empty/full), open on click,
  "Empty Trash" on right-click
- **Exposé-lite** — full-screen grid of *live* DWM window thumbnails;
  click one to switch, Esc to dismiss
- Global hotkeys for Launchpad / Spotlight / Exposé, configurable in
  Preferences (defaults: `Ctrl+Alt+L`, `Ctrl+Space`, `Ctrl+Alt+E`)

Honest limitations: Wi-Fi/Bluetooth in Control Center deep-link to Windows
Settings (no direct toggle API); brightness isn't reachable from here;
rearranging inside Launchpad folders is not yet supported; UWP apps are
found through their Start Menu shortcuts rather than package enumeration.

## Quick start

Prerequisites: Windows 10/11 and the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```powershell
git clone https://github.com/<your-org>/opendock.git
cd opendock
dotnet build src/OpenDock.sln -c Release
# run it:
src\OpenDock\bin\Release\net8.0-windows\OpenDock.exe
```

Portable single-file publish (self-contained, no install needed):

```powershell
dotnet publish src/OpenDock/OpenDock.csproj -c Release -r win-x64 --self-contained
```

## Project structure

```
src/
  OpenDock.sln
  OpenDock.Core/        # portable: models, settings, layout math, search, abstractions (builds anywhere)
    Models/             # DockItem, AppSettings (+ StackViewMode, LaunchpadItem/Folder)
    Services/           # SettingsService (JSON persistence)
    Layout/             # DockLayoutEngine (pure, unit-testable magnification math)
    Search/             # SpotlightRanker (pure, unit-testable fuzzy ranking)
    Abstractions/       # IIconProvider, IWindowEnumerator (+ WindowRef)
  OpenDock/             # WPF app (Windows only)
    DesktopCoordinator.cs # owns Launchpad/Spotlight/Exposé/menu bar/stacks/hotkeys
    DockWindow.*        # the dock bar: Canvas tiles + render loop
    MenuBarWindow.*     # top menu bar + Control Center
    LaunchpadWindow.*   # paged app grid, folders, drag-rearrange
    StackPopup.*        # folder stacks: fan/grid/list
    SpotlightWindow.*   # fuzzy search overlay
    ExposeWindow.*      # live DWM-thumbnail window overview
    PreferencesWindow.* # settings UI
    ViewModels/         # DockViewModel (1-second reconcile, no UI churn)
    Interop/            # WindowEnumerator, AppEnumerator, ShellLink, IconService,
                        # TaskbarManager, PreviewService, ThumbnailCell, HotkeyManager,
                        # RecycleBin, SystemPower, VolumeControl
    TrayIcon.cs         # system-tray icon + menu
    NativeMethods.txt   # CsWin32 Win32 API list
docs/
  ARCHITECTURE.md       # how it all fits together
  ROADMAP.md            # v0.2 done, v0.3+ plans
```

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the data flow and the
interop notes (including the DWM-thumbnail opaque-window gotcha), and
[docs/ROADMAP.md](docs/ROADMAP.md) for what's next: Launchpad, folder stacks,
top menu bar, weather widget, themes, and localization.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). All contributions must be original
code — no copying from commercial or other open-source dock projects.

## License

MIT — see [LICENSE](LICENSE).
