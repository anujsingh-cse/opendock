# OpenDock

An open-source macOS-style dock for Windows 10/11. Pin your apps, watch
running windows gather as tiles, magnify icons on hover, and hide the Windows
taskbar for a clean desktop.

> **Clean-room project.** OpenDock is written from scratch and shares no code
> with any commercial dock software. It is inspired by the *idea* of a macOS
> dock on Windows, not by anyone's implementation.

<!-- TODO: replace with a real screenshot once the UI is polished: docs/screenshot.png -->
![OpenDock screenshot](docs/screenshot.png)

## Features (v0.1 MVP)

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
  minimize effect, auto-hide
- System-tray icon with Preferences and Quit
- Settings stored as JSON in `%APPDATA%\OpenDock\settings.json`
- Crash log at `%APPDATA%\OpenDock\crash.log`

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
  OpenDock.Core/        # portable: models, settings, layout math, abstractions (builds anywhere)
    Models/             # DockItem, AppSettings
    Services/           # SettingsService (JSON persistence)
    Layout/             # DockLayoutEngine (pure, unit-testable magnification math)
    Abstractions/       # IIconProvider, IWindowEnumerator
  OpenDock/             # WPF app (Windows only)
    DockWindow.*        # the dock bar: Canvas tiles + render loop
    PreferencesWindow.* # settings UI
    ViewModels/         # DockViewModel (1-second reconcile, no UI churn)
    Interop/            # WindowEnumerator, IconService, TaskbarManager, PreviewService
    TrayIcon.cs         # system-tray icon + menu
    NativeMethods.txt   # CsWin32 Win32 API list
docs/
  ARCHITECTURE.md       # how it all fits together
  ROADMAP.md            # v0.2 and beyond
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
