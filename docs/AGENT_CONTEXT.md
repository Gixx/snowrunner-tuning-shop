# SnowRunner Tuning Shop — agent context

Persistent knowledge map for AI assistants. Prefer this over rediscovering the repo.
Update this file when architecture or hard rules change.

Repo: https://github.com/Gixx/snowrunner-tuning-shop  
Site: https://gixx.github.io/snowrunner-tuning-shop/  
Version source of truth: `src/SnowRunnerTuningShop.Core/AppInfo.cs` (`AppInfo.Version`)

---

## What it is

Windows desktop fan tool (MIT) that loads SnowRunner `initial.pak` (ZIP of XML/binary), edits balance values via UI, writes changes back, and keeps a read-only **baseline** for restore / game-update recovery.

- Closing the game before pak writes avoids file locks. The UI disables Apply/Save/Restore while a SnowRunner process is running (`SnowRunnerProcessGuard` / `GameRunningMonitor`); Core write paths throw if the game is still open.

---

## Stack and layout

| Item | Detail |
|------|--------|
| UI (Windows) | WPF, `net10.0-windows` — `src/SnowRunnerTuningShop/` |
| UI (Linux / cross) | Avalonia, `net10.0` — `src/SnowRunnerTuningShop.Desktop/` (in progress) |
| Domain | `net10.0` — `src/SnowRunnerTuningShop.Core/` |
| Solution | `SnowRunnerTuningShop.slnx` (full, incl. WPF); Linux IDE: `SnowRunnerTuningShop.Linux.slnx` via `.vscode/settings.json` `dotnet.defaultSolution` |
| Assets | `assets/vehicles`, `trailers`, `localization`, `general` |
| Installer | `installer/SnowRunnerTuningShop.iss` (+ Chinese ISL under `installer/languages/`) |
| CI | `.github/workflows/ci.yml` (Windows + Linux Desktop publish smoke), `release.yml` (`v*` tags), `pages.yml` |
| Docs | `CHANGELOG.md` (Keep a Changelog), `README.md` |

**Gitignored (do not treat as product source):** `.plan/`, `docs/plan/`, `artifacts/`, `tools/`, `example.data/`, `*.baseline`.

Build (Release, Windows WPF):  
`dotnet build src/SnowRunnerTuningShop/SnowRunnerTuningShop.csproj -c Release`

Build (Avalonia Desktop):  
`dotnet build src/SnowRunnerTuningShop.Desktop/SnowRunnerTuningShop.Desktop.csproj -c Release`

Tests: `tests/SnowRunnerTuningShop.Tests` — `dotnet test tests/SnowRunnerTuningShop.Tests/SnowRunnerTuningShop.Tests.csproj`  
(locale keys vs `en.json`, `PakFileId`, trailer store hitch rules).

Senior review snapshot (prioritized debt): `docs/CODE_REVIEW.md`.

If Debug build fails with DLL lock, the app or debugger is still running — close it or build to another `-o` folder.

---

## UI pages (nav order)

Shared session: `AppSession`. Shell: `MainWindow.*`.

| Page | View / Core | Role |
|------|-------------|------|
| Home | `Views/HomeView.*` | Baseline, install location, restore, refresh after game update, reapply profile, workspace health, updates |
| General | `Views/GeneralView.*`, `Core/General/GeneralService.cs` | Camera clip; trail rock scale (mod assets in `assets/general/no-stones`) |
| Parts | `Views/PartsView.*` + Winch/Engine/Gearbox/Suspension/Tire/Add-ons/Crane tabs | Global multipliers + per-row edits; Used-by; restore category |
| Vehicles | `Views/VehiclesView.*`, `Core/Trucks/TruckTuningService.cs`, `TruckSteerXml` | Catalog + per-truck edit (per-axle steer) + global vehicle multipliers / unlocks |
| Trailers | `Views/TrailersView.*`, `Core/Trailers/TrailerTuningService.cs` | Catalog + capacities/price/quest; global multipliers |
| Photo Mode | `Views/PhotoModeView.*`, `Core/PhotoMode/*` | Defaults in `initial.cache_block`; Time from sslbundle is **read-only** (Apply must not write sslbundle) |
| Settings | `Views/SettingsView.*` | Theme, UI language, locale downloads, links, updates |

Sidebar also has **Report a bug** (`Views/BugReportWindow.*`, `Core/Diagnostics/BugReportService.cs` + `MailtrapEmailClient.cs`) — sends via Mailtrap API. From: `BugReportSecrets.FromEmail`; To: `AppInfo.BugReportEmail`; token in gitignored `BugReportSecrets.Local.cs`. Release workflow injects `secrets.MAILTRAP_API_TOKEN` before publish.

---

## Workspace / pak model

- **Working file:** game `initial.pak` (path stored per edition).
- **Edition:** `GameEditionDetector` — Steam / GOG / Epic / Xbox from path; else `custom_<hash>`.
- **AppData root:** `%LocalAppData%\SnowRunnerTuningShop\`
  - `config.json` — editions, theme, UI culture, sidebar, skipped update (`WorkspaceConfigStore`)
  - `baselines/initial.baseline.{editionId}.pak`
  - `profiles/tuning-profile.{editionId}.json`
  - `photo-mode/photo-mode.{editionId}.json` (excluded from main reapply)
  - `localization/` — downloaded overlays
  - `logs/` — crash reports
- **Marker in working pak:** `[media]/_tuning_shop/marker.xml` (`TuningProfileMarker`) for update detection / reapply (`WorkspaceHealthService`).
- **Path helpers:** `Core/Constants/PakPaths.cs` — `[media]/`, trucks, engines, winches, etc.
- **Writes:** close archives before replace; temp rebuild under `%TEMP%`. Photo `cache_block` is patched **in place** (same compressed size) — never move it to ZIP end (`PakInPlaceZipPatcher`, `PakCacheBlockLayoutGuard`).

Steam pak example (dev machine may vary):  
`…/SnowRunner/preload/paks/client/initial.pak`

---

## Identity rules (critical)

### Vehicles / trailers

- Catalog **display names are UI only**. Never match or save by localized names.
- Vehicles `assets/vehicles/catalog.json`:
  - `id` — wiki/UI slug (may differ from XML)
  - **`pakId`** — XML file stem in pak (language-independent)
  - Example: `ank_mk38_civilian` → `pakId: "ank_mk38"`
- Match: `TruckTuningService.FindByCatalog(trucks, catalogId, pakId)` → `PakFileId.Find` on `TruckId` / `EntryPath`.
- Trailers `assets/trailers/catalog.json`: `id` is already the file id; `TrailerTuningService.FindByCatalog` → `PakFileId`.
- Helper docs/tools: `assets/vehicles/README.md`, `assets/vehicles/_meta_build/map_catalog_pak.py`, `dump-pak-trucks.ps1`.

### Parts (engines, winches, gearboxes, suspensions, tires, cranes)

- Stable key = XML **`Name`** (and set file id where relevant). Display = game strings only.
- Services under `Core/{Engine,Winch,Gearbox,Suspension,Tires,Crane}/`; shared pak apply via `PartPakPipeline`.
- Cranes are truck addons (`/classes/trucks/addons/`) with `AddonType` `Crane` / `LogCrane`; identity = file stem. Tunables: arm `Motor Force` + `ControlledIK` movement coeffs.
- Truck/trailer XML helpers: `TruckDiffLockXml`, `VehicleGameDataXml`, `TrailerHitchXml`.
- Parse/format numbers with **`CultureInfo.InvariantCulture`**. Float XML attributes use shared `XmlNumericFormatting` (max **2** decimal places) so scaled presets like 1/3 do not write repeating fractions.

### Localization priority

1. Correct data binding / pak I/O  
2. Invariant numeric handling  
3. UI translation last  

---

## Localization

Two layers:

1. **UI:** `assets/localization/{culture}.json` + `keys.json` → `StringResources` / `UiText`
2. **Game names:** pak `[strings]/strings_{lang}.str` → `GameStringsReader`; language from `LanguageOption.GameLanguage`

**Bundled** (copied to output): en, de, fr, es, pt, pt-BR, pl, ru, uk, zh-CN, zh-TW.  
**Repo-only downloadables** (not in installer output): hu, it, fi — fetched via Settings → Add or Update languages (`LocalePackUpdateService` from GitHub `main`).

English must contain every `keys.json` key; others fall back to English. Debug: **DEBUG (keys)** culture.

**Crash report UI and GitHub text are always English** (`StringResources.GetEnglish` / `FormatEnglish` on `UiText.CrashReport.*`). Report body builder in `CrashReportBuilder` is English prose.

---

## Release process

1. Bump `AppInfo.Version` (stable `X.Y.Z` or beta `X.Y.Z-beta.N`)
2. Move `[Unreleased]` notes into `## [x.y.z] — YYYY-MM-DD` in `CHANGELOG.md` + footer links
3. Commit when asked
4. Tag `vMAJOR.MINOR.PATCH` or `vMAJOR.MINOR.PATCH-beta.N` → `release.yml` builds **Windows** (win-x64 + Inno) and **Linux** Avalonia Desktop (portable zip + Flatpak `io.github.gixx.SnowRunnerTuningShop`) → GitHub Release (beta tags → prerelease).

Do **not** commit unless the user explicitly asks. Do **not** invent features or expand scope.

---

## Planned Linux port (in progress on main)

Tracked work notes may also live under gitignored `docs/plan/Linux-Avalonia-plan.md`.

- UI: Avalonia shell (`src/SnowRunnerTuningShop.Desktop/`) beside WPF; shared Core
- Work on **main** (not a long-lived `linux` branch)
- Package target: **Flatpak** (`packaging/flatpak/`, app-id `io.github.gixx.SnowRunnerTuningShop`) plus portable zip; sandbox grants `home` so Steam/`initial.pak` under `~` works; grants `--socket=x11` (Avalonia X11/XWayland) plus Wayland
- Smoke VM: CachyOS Hyper-V (plus Ubuntu when available); not WSL
- Phase 0 done when: empty Avalonia window runs on Windows + Linux with Core reference
- Phase 1 (current): Home + General + Parts + Settings + Vehicles + Trailers list+detail on Avalonia; Photo Mode is Windows-only (omitted from Linux nav)
- See `src/SnowRunnerTuningShop.Desktop/README.md` and `packaging/flatpak/README.md` for run / publish / Flatpak
- IDE on Linux: open `SnowRunnerTuningShop.Linux.slnx` (set via `.vscode/settings.json`) so Roslyn skips the WPF Windows TFM

---

## Key files (lookup table)

| Area | Paths |
|------|--------|
| Version / URLs | `Core/AppInfo.cs` |
| Updates | `Core/Updates/AppUpdateService.cs`, `AppSemVersion.cs`, `AppUpdateChannels.cs` (unset channel derives Beta from prerelease `AppInfo.Version`) |
| Workspace | `Core/Config/WorkspaceConfigStore.cs`, `GameEditionDetector.cs` |
| Baseline / health | `Core/Backup/PakBaselineService.cs`, `Core/Profile/WorkspaceHealthService.cs`, `TuningProfile*.cs` |
| Pak I/O | `Core/Pak/InitialPakReader.cs`, `InitialPakWriter.cs`, `PakFileId.cs`, `PakInPlaceZipPatcher.cs`, `PakVanillaText.cs`, `PakCacheBlockLayoutGuard.cs` |
| Vehicles / trailers | `Core/Trucks/TruckTuningService.cs`, `Core/Trailers/TrailerTuningService.cs`; UI `Vehicles/VehicleCatalog.cs`, `Trailers/TrailerCatalog.cs` |
| Parts | `Core/{Winch,Engine,Gearbox,Suspension,Tires,AddonCapacity,Crane}/*Service.cs`, `Core/Models/*Definition.cs` |
| General / Photo | `Core/General/GeneralService.cs`, `Core/PhotoMode/*` |
| Strings | `Core/Strings/GameStringsReader.cs`; UI `Localization/StringResources.cs`, `UiText.cs`, `LanguageService.cs` |
| Crash | `Core/Diagnostics/CrashReport*.cs`, `Views/CrashReportWindow.*`, `GlobalExceptionHandler.cs` |
| Shell (WPF) | `App.xaml.cs`, `MainWindow.*`, `AppSession.cs`, `AppPaths.cs`, `ThemeService.cs` |
| Shell (Avalonia) | `src/SnowRunnerTuningShop.Desktop/` (`Program.cs`, `MainWindow.axaml*`, `Views/{Home,General,Parts,Vehicles,Trailers,Settings}View.*`, `Vehicles/*`, `Trailers/*`) |
| Flatpak | `packaging/flatpak/` (`io.github.gixx.SnowRunnerTuningShop.yml`, `build.sh`) |

---

## Known pitfalls

- Wiki vehicle `id` ≠ pak XML name → always use `pakId` / `PakFileId`.
- German/Chinese UI culture must not drive float parsing.
- Photo Mode: only `initial.cache_block` is written; sslbundle Time apply stays disabled; treat binary as Latin-1/bytes.
- Global multipliers: if baseline lacks a new DLC entry, scale from working file (`PakVanillaText`).
- Photo-mode profile is **not** part of main “Reapply saved changes”.
- Installer Chinese languages are vendored under `installer/languages/` (Chocolatey Inno lacks them).
- User communicates in Hungarian often; product strings and crash reports stay English unless localizing UI keys.
