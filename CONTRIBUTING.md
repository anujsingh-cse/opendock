# Contributing to OpenDock

Thanks for wanting to help! A few ground rules keep the project healthy.

## Clean-room policy

OpenDock is written from scratch. **Do not copy code** from commercial dock
software or from other open-source dock projects — not even "just a helper
function". Architecture ideas and public API knowledge (e.g. "DWM thumbnails
need an opaque window") are fine; implementations must be your own words.
PRs that look copied will be rejected.

## Building

You need Windows 10/11 and the .NET 8 SDK.

```powershell
dotnet build src/OpenDock.sln -c Release
```

`OpenDock.Core` is a plain `net8.0` library with no Windows dependencies — it
builds on Linux/macOS too, and CI checks that on every push.

## Code style

- File-scoped namespaces, `Nullable` enabled — no warnings.
- `LangVersion latest`, implicit usings on.
- XML doc comments on public Core APIs; plain comments where the *why* isn't
  obvious (interop especially).
- Win32 interop: prefer CsWin32 — add the API name to `NativeMethods.txt`.
  Only hand-roll `DllImport` for APIs with awkward generated overloads
  (caller-allocated string buffers), and say why in a comment.
- Keep the layout math in `OpenDock.Core` pure and UI-free so it stays
  unit-testable.

## Pull requests

1. Fork, branch from `main`, keep the change focused.
2. Describe what you changed and how you tested it (this project is visually
   tested by humans — say what you clicked and what you saw).
3. CI must be green: Core builds on Linux, the full solution builds on Windows.

## Issues

Bug reports: Windows version, .NET SDK version, steps to reproduce, and the
contents of `%APPDATA%\OpenDock\crash.log` if the app crashed. Feature
requests: check [docs/ROADMAP.md](docs/ROADMAP.md) first — it may already be
planned.

## No CLA/DCO

No contributor license agreement and no sign-off required. Your PR being
merged is the whole deal; the MIT license covers the rest.
