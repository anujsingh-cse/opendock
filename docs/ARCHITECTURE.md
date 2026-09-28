# OpenDock Architecture

## Project map

```
src/OpenDock.sln
├── OpenDock.Core/                 net8.0, no Windows dependencies
│   ├── Models/
│   │   ├── DockItem.cs            tile data (kind, paths, hwnd, badges, progress)
│   │   └── AppSettings.cs         JSON-serializable settings + enums
│   ├── Services/
│   │   └── SettingsService.cs     load/save %APPDATA%/OpenDock/settings.json
│   ├── Layout/
│   │   └── DockLayoutEngine.cs    PURE magnification math (unit-testable)
│   └── Abstractions/
│       ├── IIconProvider.cs       platform image source behind an interface
│       └── IWindowEnumerator.cs   running-window enumeration behind an interface
└── OpenDock/                      net8.0-windows, WPF
    ├── App.xaml(.cs)              single-instance mutex, crash log, wiring
    ├── DockWindow.xaml(.cs)       the dock bar: Canvas + render loop
    ├── PreferencesWindow.xaml(.cs)
    ├── TrayIcon.cs                notify icon (H.NotifyIcon.Wpf) + menu
    ├── ViewModels/
    │   └── DockViewModel.cs       1-second reconcile of pins + windows
    ├── Interop/
    │   ├── WindowEnumerator.cs    EnumWindows → taskbar-style filtering → group by exe
    │   ├── IconService.cs         SHGetFileInfo → trim → cache (single funnel)
    │   ├── TaskbarManager.cs      hide/restore Shell_TrayWnd
    │   └── PreviewService.cs      DWM thumbnails into an opaque host window
    └── NativeMethods.txt          CsWin32 API list
```

## Data flow

```
Win32 (EnumWindows, 1/s timer)
        │
        ▼
DockViewModel.Reconcile()  ── keyed by DockItem.MatchKey ("pin:…" / "run:…")
        │                      updates the ObservableCollection IN PLACE
        │                      (insert/move/remove minimally — no UI churn,
        │                       magnification state survives refreshes)
        ▼
DockWindow.SyncVisuals()   ── creates/removes IconVisual tiles on a Canvas
        │
        ▼
CompositionTarget.Rendering── while the cursor is over the dock:
  GetCursorPos → PointFromScreen → DockLayoutEngine.ComputeLayout()
        │                      raised-cosine falloff around the cursor,
        │                      cumulative centered cells, bottom-anchored growth
        ▼
Canvas.SetLeft/Top per tile (direct positioning, no binding churn)
```

The render loop self-detaches on `MouseLeave` (one final unmagnified layout),
so the app idles at zero CPU when the cursor is elsewhere. Hover is computed
from the real cursor position every frame — WPF `MouseLeave` is unreliable
once tiles start moving under the pointer.

## Layout engine

`DockLayoutEngine.ComputeLayout` is pure: `(count, baseSize, cursorX?,
magnification, magnifiedSize, range) → per-tile (offset, size)`. Single pass
over resting tile centers; deterministic and unit-testable. Callers handle
bottom-anchored growth (tile top = content bottom − drawn size) and the
`IconFill` factor (artwork draws at 84% of its cell, like macOS).

## Interop notes

- **CsWin32 workflow** (`Microsoft.Windows.CsWin32`, `PrivateAssets=all`):
  add the API or struct name to `NativeMethods.txt`, build, then call it as
  `Windows.Win32.PInvoke.<Name>`. To see an exact generated signature, build
  with `-p:EmitCompilerGeneratedFiles=true` and read
  `obj/.../generated/Microsoft.Windows.CsWin32/`.
- **CsWin32 gotchas learned the hard way:**
  - Generated types are `internal` (fine — same assembly) but so are struct
    *fields*: `SHFILEINFOW.hIcon` can't be read back, so icon extraction is
    hand-rolled `DllImport` with our own struct.
  - Arch-specific APIs (`GetWindowLongPtrW`, `SHGetFileInfo`) are refused for
    AnyCPU (warning PInvoke005) — that's why the app targets **x64**.
  - Generated names don't always match the SDK: `PostMessage` (not
    `PostMessageW`), `GetWindowLongPtr` (not `...W`); `HTHUMBNAIL` is
    projected as `nint`.
  - `HWND`'s constructor and `.Value` are internal — convert via the public
    operators: `(HWND)(IntPtr)handle` and `(nint)(IntPtr)hwnd`.
  - Pointer-based signatures (e.g. `GetWindowThreadProcessId`) need
    `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` plus tiny `unsafe` helpers.
- **Hand-rolled DllImport** is used for APIs taking caller-allocated string
  buffers (`GetWindowTextW`, `GetWindowTextLengthW`, `FindWindowW`) and for
  trivial one-liners (`ShowWindow`) — the generated overloads for those are
  awkward, and a comment at each site says so.
- **DWM thumbnail gotcha:** DWM refuses to mirror a thumbnail into a layered
  window. The preview host (`PreviewService`) is therefore a deliberately
  *opaque* window (`AllowsTransparency=false`), unlike the dock itself.
  DWM thumbnails work for occluded and minimized windows, where a BitBlt
  would capture the occluder or nothing.
- **Icon funnel:** everything goes through `IconService` — `SHGetFileInfo`
  extraction, transparent-margin trimming (Windows icons disagree wildly
  about their margins; trimming keeps tiles visually equal), then a
  case-insensitive in-memory cache.
- **Taskbar safety:** `App.OnExit` always restores `Shell_TrayWnd`, even after
  a crash-adjacent shutdown path. The global exception handlers log to
  `%APPDATA%\OpenDock\crash.log` and keep the dock alive through non-fatal
  UI errors.
- **Single instance:** a named mutex (`OpenDock_SingleInstance`); a second
  launch exits immediately.
- **Own-window exclusion:** the dock's HWND is registered with
  `WindowEnumerator.ExcludeWindow` so OpenDock never tiles itself.

## Threading (v0.1)

Everything runs on the UI thread: the 1-second enumeration is cheap
(EnumWindows + cached exe paths), and icon extraction happens once per new
tile through the cache. If icon loading ever janks the render loop, the fix
is to move `IconService.Extract` onto a worker thread and freeze the
`BitmapSource` there (frozen `Freezable`s cross threads safely) — the funnel
is already isolated for exactly this.

## Theming

`DockTheme.System` reads
`HKCU\...\Themes\Personalize\AppsUseLightTheme`. The bar is a translucent
rounded `Border`; per-tile theme application goes through
`IconVisual.ApplyTheme`.
