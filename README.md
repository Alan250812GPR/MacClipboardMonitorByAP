# MacClipboardMonitor — Clipboard History Manager for macOS

[![Version](https://img.shields.io/badge/version-1.0.6-blue)](MacClipboardMonitor/MacClipboardMonitor.csproj)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com)
[![Avalonia](https://img.shields.io/badge/Avalonia-11.3.6-8A2BE2)](https://avaloniaui.net)
[![Platform](https://img.shields.io/badge/platform-macOS%2013%2B-lightgrey)](https://www.apple.com/macos)
[![Licence](https://img.shields.io/badge/licence-MS--PL-green)](LICENCE)

**English** | [Español](README.es.md)

> The Windows `Win + V` experience, natively remade for macOS. Capture text, images and files automatically, organise them chronologically, and paste anywhere in one keystroke.

---

## Screenshots

<!-- TODO: Add your screenshots to docs/screenshots/ — placeholders below will render once images are added -->
| Main window | Full preview (right-click) | Image preview + Tag dialogue |
|---|---|---|
| ![Main window](docs/screenshots/main.png) | ![Full preview](docs/screenshots/preview.png) | ![Tag dialogue](docs/screenshots/tag-dialogue.png) |

*Place your captures in `docs/screenshots/`. Recommended size: 350×550 @2x.*

---

## Features

### Capture & History
- **Automatic capture via Rx polling (750 ms)** — detects clipboard format with priority `File > Image > Text`.
- **Chronological ordering** — `CreatedAt DESC` + `Id DESC` tie-breaker; the newest item is always on top. Duplicates are not discarded — they are promoted to the top (LIFO).
- **Smart deduplication**
  - Text: `Content` case-insensitive (`OrdinalIgnoreCase`)
  - Image: `SHA-256` hash (`ImageHash`)
  - File: exact `FilePaths` match
- **Universal retention**
  | Type | Deduplication | Expiry | Global limit |
  |---|---|---|---|
  | Text | `Content` ToLower | 48 hours | 100 |
  | Image | `ImageHash` | 1 hour | 100 |
  | File | `FilePaths` | 1 hour | 100 |
  | Encrypted | n/a | **never** | exempt from pruning |
- **Automatic purging** — in-memory and SQLite purge every 60 seconds; encrypted entries are never purged.

### Search & Organisation
- **Instant search** — debounced 250 ms, filters text and file names; encrypted entries are searchable by their custom tag/title rather than content.
- **Language detection** — heuristic identification of code snippets (`JSON`, `SQL`, `XML`, etc.) with badge display.
- **Tag/title for encrypted entries** — give encrypted items a human-readable label (`EncryptedTag`) for quick identification.

### Previews (read-only)
- **Full preview on right-click** — overlay with the complete content (no `60 px` truncation). Supports text, code, images (with `Ctrl + wheel` zoom 0.5×–4×) and file lists. **Encrypted entries are deliberately exempt** — right-click does nothing.
- **Image preview** — dedicated overlay with `LayoutTransformControl` zoom. Kept as a separate overlay for finer control.

### Actions
- **Single click** — copy to clipboard (encrypted items are decrypted on the fly; re-capture of the plain secret is suppressed via SHA-256 fingerprint).
- **Double-click** — copy and paste directly into the active application (`CGEvent Cmd+V`). Requires Accessibility permission.
- **Per-item delete** and **Clear history**.

### System Integration
- **Configurable global hotkey** — default `Ctrl + Cmd + V` via `SharpHook` (`TaskPoolGlobalHook`). Reconfigurable from Settings (⚙️) to any `Ctrl`/`Cmd` + letter or `F1–F12`. Persisted in `~/MacClipboardMonitor.config.json`.
- **Menu bar `TrayIcon`** — `NSStatusItem` template icon (`tray.png`), menu: Show/Hide, Clear history, Quit. Clicking the icon toggles the window.
- **Launch at login** — `LaunchAgent` plist via `AutoStartManager` (no `KeepAlive`, so the app can still be quit manually).
- **Frameless, topmost window** — `SystemDecorations=None`, `Topmost=True`, `CornerRadius=12`, drag via `BeginMoveDrag`, `LSUIElement=true` (no Dock icon).

---

## Requirements

- macOS 13 or later
- Accessibility permission (System Settings → Privacy & Security → Accessibility) — only for direct paste
- .NET 8 SDK — only if you wish to build from source

---

## Installation

### Option A — For users (recommended)

```bash
# Build the .app and .dmg (scripts provided)
./MacClipboardMonitor/build_installer.sh
# or
./MacClipboardMonitor/compiler.sh
./MacClipboardMonitor/CompilerJustMac.sh
```
The `.app` is published as `WinExe` (`LSUIElement=true`) — no Terminal window appears.

### Option B — From source

```bash
# Build
dotnet build MacClipboardMonitor.sln -c Release

# Run with Terminal (development)
dotnet run --project MacClipboardMonitor

# Run without Terminal (publishes and opens the .app)
./MacClipboardMonitor/run_app.sh
```

---

## Usage

1. Copy anything — text, an image, or files — it appears at the top of the history.
2. Press `Ctrl + Cmd + V` (or your custom hotkey) or click the menu bar icon to show/hide the window.
3. **Click** a card to copy · **Double-click** to paste directly · **Right-click** for a full read-only preview.
4. Click **🔒** on a text card to encrypt it — you will be prompted for a tag/title. Encrypted cards show the tag (or `••••••••` if untagged) and a `🔒 Encrypted` badge.
5. Click **✎** on an encrypted card to edit its tag.
6. Use the search field to filter the history. Type `⚙️` to change the hotkey, `🗑️` to clear all, `✕` to hide.

**Keyboard shortcuts**

| Key | Action |
|---|---|
| `↑` / `↓` | Navigate history |
| `Enter` | Copy selected item |
| `Esc` | Close tag dialogue → full preview → image preview → settings → hide window |
| `Ctrl + wheel` | Zoom image previews |

---

## Configuration & Data

| File | Location | Purpose |
|---|---|---|
| Database | `~/MacClipboardMonitor.db` | SQLite via EF Core 9 (`EnsureCreated` + idempotent `ALTER TABLE` — no migrations) |
| Configuration | `~/MacClipboardMonitor.config.json` | `HotkeyModifiers`, `HotkeyKey`, `LatestVersion` |

To reset: quit the app and delete either file — it will be recreated on next launch.

---

## Architecture

```
Program.cs (Main + single-instance guard)
 → App.axaml.cs (OnFrameworkInitializationCompleted)
     creates: MainWindow, AppDbContext, ClipboardRepository,
              PollingClipboardMonitorService, MainWindowViewModel
     wires: DataContext + TrayIcon + AutoStartManager + HotkeyChanged

PollingClipboardMonitorService (Rx, 750 ms)
 → IObservable<ClipboardCapture> ClipboardChanged
     detects: File > Image > Text
 → MainWindowViewModel subscribes
     → in-memory dedupe → Repository.AddItemAsync → SourceList<ClipboardItem> (History)

MainWindow (SharpHook global hook)
 → Ctrl+Cmd+V toggles window visibility

TrayIcon (App.axaml.cs)
 → Menu: Show/Hide, Clear history, Quit
```

> Full developer rules and file responsibilities: see [AGENTS.md](AGENTS.md).

---

## Tech Stack

| Area | Technology |
|---|---|
| UI | Avalonia 11.3.6 (XAML) + FluentTheme + Inter font |
| MVVM | ReactiveUI (`ReactiveObject`, `ReactiveCommand`, Rx) + DynamicData |
| Persistence | EF Core 9 + SQLite |
| Global hotkey | SharpHook 7.1.1 (`TaskPoolGlobalHook`) |
| Tray | Avalonia `TrayIcon` (`NSStatusItem`) |
| Target | `net8.0` (`WinExe`) |

---

## Project Structure

<details>
<summary>Click to expand</summary>

```
MacClipboardMonitor/
├── Program.cs
├── App.axaml / App.axaml.cs        # FluentTheme, ViewLocator, DI, TrayIcon
├── ViewLocator.cs
├── Views/MainWindow.axaml(.cs)     # Single window + global hook + drag
├── ViewModels/
│   ├── ViewModelBase.cs
│   └── MainWindowViewModel.cs      # All presentation logic (History, search, previews, encryption, hotkey)
├── Models/ClipboardItem.cs         # EF entity + enum + [NotMapped] UI props + EncryptedTag
├── Repositories/
│   ├── IClipboardRepository.cs
│   └── ClipboardRepository.cs      # Dedupe + expiry + 100-item limit + encrypted handling
├── Services/
│   ├── PollingClipboardMonitorService.cs
│   ├── MacPasteService.cs          # CGEvent Cmd+V
│   ├── CodeDetectionService.cs
│   ├── AppConfigService.cs
│   ├── EncryptionService.cs        # AES-256-CBC, PBKDF2
│   └── AutoStartManager.cs
├── Data/AppDbContext.cs
├── Assets/tray.png, avalonia-logo.ico
├── run_app.sh / compiler.sh / CompilerJustMac.sh / build_installer.sh
└── MacClipboardMonitor.csproj      # Version 1.0.6
```

</details>

---

## Permissions

**Accessibility** is required only for double-click direct paste. If the warning banner appears:

`System Settings → Privacy & Security → Accessibility → enable MacClipboardMonitor`

Without it, copy-on-click still works perfectly.

---

## Changelog

### 1.0.6
- Full preview overlay on right-click (read-only, encrypted exempt)
- Encrypted entries with custom tag/title + in-card `✎` editor + search by tag
- Stable `CreatedAt DESC, Id DESC` ordering
- Separate image and full previews
- Configurable global hotkey persisted to `~/MacClipboardMonitor.config.json`

See `MacClipboardMonitor.csproj` (`Version`/`AssemblyVersion`) for version history.

---

## Contributing

Contributions are welcome. Please:

1. Read [AGENTS.md](AGENTS.md) — strict MVVM, compiled bindings (`x:DataType`), single-window convention, `#RootWindow` binding, Fluent `DynamicResource` colours.
2. Keep logic in `MainWindowViewModel.cs`; `.axaml` is presentation only.
3. Run `dotnet build MacClipboardMonitor.sln -c Release` before submitting.

Report issues with macOS version, reproduction steps, and whether Accessibility is enabled.

---

## Licence

Licensed under the [Microsoft Public License (MS-PL)](LICENCE) — see `LICENCE` for details. Retain all copyright and attribution notices when distributing.

---

## Donations — Support the Project

MacClipboardMonitor is free, open-source, and has no telemetry. If it saves you time, please consider supporting its development.

<!-- TODO: Replace # with your actual URLs -->

- **PayPal:** [PayPal](#) <!-- e.g. https://paypal.me/yourname -->
- **Buy Me a Coffee:** [Buy Me a Coffee](#) <!-- e.g. https://buymeacoffee.com/yourname -->

Every donation — no matter the size — is genuinely appreciated and helps keep the project maintained.

Contact: [alan.2500gpr@gmail.com](mailto:alan.2500gpr@gmail.com)

---

## Acknowledgements

Built with [Avalonia UI](https://avaloniaui.net), [ReactiveUI](https://reactiveui.net), [DynamicData](https://github.com/RolandPheasant/DynamicData), [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/), and [SharpHook](https://github.com/TolikPylypchuk/SharpHook). Thanks to all contributors and testers.

---

*Made for macOS, inspired by Windows. Crafted with care in British English.*
