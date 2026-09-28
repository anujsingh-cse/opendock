# OpenDock Roadmap

## v0.1 — MVP dock (this scaffold)

- [x] Dock bar: bottom-centered, translucent, magnification, auto-hide
- [x] Pinned apps + running-app tiles grouped by executable
- [x] Drag-and-drop pinning, window previews, running indicators
- [x] Taskbar hide/restore, tray icon, preferences window
- [x] JSON settings, crash log, CI (Core on Linux, solution on Windows)

## v0.2 — Depth

- [ ] **Launchpad**: full-screen app grid (Win32 + UWP/Store apps)
- [ ] **Folder stacks**: pin a folder, fan/grid out its contents on click
- [ ] True **genie / suck / scale** minimize warps via a D3D11 overlay
      (v0.1 plays a scale pulse on the tile)
- [ ] Multi-monitor placement (dock follows the cursor's monitor)
- [ ] Left/right dock orientation
- [ ] Unread badges and task progress on tiles (badge count, download/copy bars)

## v0.3 — Desktop integration

- [ ] Top **menu bar** (clock, tray-area widgets, spotlight-style search)
- [ ] **Weather widget** tile with live icon
- [ ] Jump lists / recent documents on right-click
- [ ] Window minimize-to-dock interception (hook minimize, animate, hide)

## v0.4 — Polish and community

- [ ] Theme store (import/export JSON themes)
- [ ] Localization framework + community translations
- [ ] Settings search in Preferences
- [ ] Auto-update (GitHub Releases feed)
- [ ] Portable single-file publish profile in CI artifacts

## Non-goals

- macOS Finder clone — OpenDock is a dock, not a file manager.
- Copying any commercial dock's code or assets — clean-room only, always.
