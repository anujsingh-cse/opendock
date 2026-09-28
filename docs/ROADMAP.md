# OpenDock Roadmap

## v0.1 — MVP dock

- [x] Dock bar: bottom-centered, translucent, magnification, auto-hide
- [x] Pinned apps + running-app tiles grouped by executable
- [x] Drag-and-drop pinning, window previews, running indicators
- [x] Taskbar hide/restore, tray icon, preferences window
- [x] JSON settings, crash log, CI (Core on Linux, solution on Windows)

## v0.2 — Full macOS-like experience (current)

- [x] **Launchpad**: full-screen paged app grid (Start Menu shortcuts, incl. UWP/Store
      apps via their Start Menu entries), search, drag-to-rearrange, page dots,
      app folders (drag one tile onto another), configurable hotkey
- [x] **Folder stacks**: pin a folder, fan/grid/list views on click, subfolder
      drill-in, drag files out, per-folder view persistence
- [x] Top **menu bar**: OpenDock menu (About/Preferences/Launchpad/Sleep/Lock/
      Log off/Restart/Shut down), active-app name, Spotlight pill,
      Control Center (real volume slider via CoreAudio), clock, show-desktop
- [x] **Spotlight search**: fuzzy-ranked overlay (pure ranking engine in Core),
      installed apps + pins + Desktop/Documents/Downloads files, keyboard-first
- [x] **Trash tile**: live Recycle Bin empty/full icon, open, empty
- [x] **Exposé-lite**: full-screen grid of live DWM thumbnails, click to switch
- [x] Global hotkeys for Launchpad / Spotlight / Exposé (configurable)
- [x] Preferences: menu bar toggle, hotkey fields, default stack view

## v0.3 — Depth

- [ ] True **genie / suck / scale** minimize warps via a D3D11 overlay
- [ ] Multi-monitor placement (dock follows the cursor's monitor)
- [ ] Left/right dock orientation
- [ ] Unread badges and task progress on tiles (badge count, download/copy bars)
- [ ] Rearranging inside Launchpad folders
- [ ] Jump lists / recent documents on right-click

## v0.4 — Desktop integration and polish

- [ ] **Weather widget** tile with live icon
- [ ] Window minimize-to-dock interception (hook minimize, animate, hide)
- [ ] Theme store (import/export JSON themes)
- [ ] Localization framework + community translations
- [ ] Settings search in Preferences
- [ ] Auto-update (GitHub Releases feed)
- [ ] Portable single-file publish profile in CI artifacts

## Non-goals

- macOS Finder clone — OpenDock is a dock, not a file manager.
- Copying any commercial dock's code or assets — clean-room only, always.
