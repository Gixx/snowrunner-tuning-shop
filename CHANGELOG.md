# Changelog

All notable changes to **SnowRunner Tuning Shop** are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project follows [Semantic Versioning](https://semver.org/).

Releases are published from `v*` git tags via GitHub Actions ([Releases](https://github.com/Gixx/snowrunner-tuning-shop/releases)).

---

## [Unreleased]

---

## [1.3.0] — 2026-09-01

### Added
- **Trailers page:** new nav page (below Vehicles) listing every trailer from `initial.pak`, filtered by hitch (Scout, Standard, Saddle Low, Saddle High) plus Mission. Cards and the detail page use per-trailer photos from `assets/trailers` (hitch defaults remain as fallback).
- **Trailer XML tuning:** edit store price, unlock rank, store availability (GameData IsQuest), and existing fuel / water / repair / spare-wheel capacities (fields are not added if the trailer never had them). Global multipliers scale those values from the baseline; restore this trailer or all trailers from baseline. Trailer XML is included in the tuning profile reapply.
- **Mission trailers in the trailer store:** **Make mission trailers purchasable** sets `IsQuest` to false, including values inherited from a parent XML (rocket / wind-blade). Trains and other special hitches get an extra `InstallSocket Type="Trailer"` so they can appear in the regular trailer store. Per-trailer **Available in store** does the same for one XML.

### Fixed
- **Trailer store names:** Season 17's diesel locomotive no longer shares the DLC 3 "Diesel Locomotive" label (now "Diesel Locomotive (Season 17)"). The wind-turbine-blade semi no longer uses the assembled-rocket name; both get unique string-table keys so the in-game store matches the catalog.

---

## [1.2.2] — 2026-08-31

### Added
- **Photo Mode defaults:** new nav page (above Settings) to edit photo mode defaults stored in `initial.pak` — time, weather, exposure/contrast/hue/saturation, color grading, vignette, film grain, FOV, aperture, focus point, and depth-of-field span. Changes apply when you open photo mode or press **Restore default** in game.
- **Photo Mode baseline restore:** restore only photo-mode-related pak entries (`initial.cache_block` and sslbundles) from your configured baseline without resetting other tunings.

### Fixed
- **Photo Mode — cache_block encoding:** the game file embeds binary data; saving it as UTF-8 corrupted ~13M bytes and prevented SnowRunner from starting. Photo mode edits now use a byte-preserving Latin-1 round-trip.
- **Photo Mode — sslbundle:** compiled sslbundle patching is disabled for now (it crashed SnowRunner on boot). Apply writes slider and weather defaults to `initial.cache_block` only; the Time preset UI is not applied to the game yet.
- **Pak writes — in-place cache_block patch:** photo mode saves now overwrite the existing compressed `initial.cache_block` slot (same byte length, padded tail) so later pak entries stay at their original offsets. Re-splicing or Store compression shifted those offsets and crashed SnowRunner; Store also bloated the pak to ~44 MB.
- **Photo Mode — load:** weather preset parsing now targets the photo mode controller block (not an unrelated `presetUiNames` list); line-ending regex and sslbundle time marker detection fixed so current values load correctly.
- **Photo Mode — save:** pak was kept open while writing, causing *“The process cannot access the file because it is being used by another process”* on Apply/Restore; archives are closed before `initial.pak` is replaced.
- **Pak writes:** temp rebuild files go to `%TEMP%` instead of the game install folder; clearer error when SnowRunner or another process locks the pak.
- **Photo Mode — Time = Default:** ComboBox now selects index `0` correctly when loading settings.

### Notes
- Close SnowRunner before applying or restoring photo mode defaults.
- Standalone CLI patch script remains in `tools/photo-mode-defaults/`; the in-app page is the preferred workflow.

---

## [1.2.1] — 2026-08-28

### Fixed
- **Vehicles — detail view image load:** manufacturer logos (and one test asset) were WebP files saved with a `.png` extension; WPF could not decode them and raised `NotSupportedException` when opening vehicle details ([#3](https://github.com/Gixx/snowrunner-tuning-shop/issues/3)). Logos are now real PNG; `TryLoadImage` fails gracefully on unsupported formats; metadata rebuild converts WebP downloads automatically.

---

## [1.2.0] — 2026-08-28

### Added
- **Multilingual UI:** app chrome in English, German, French, Spanish, Portuguese, Brazilian Portuguese, Polish, Russian, and Ukrainian (`assets/localization/*.json`).
- **Installer language picker** (English default): writes `%LocalAppData%\SnowRunnerTuningShop\install-language.json` on first install; the app imports it into `config.json` on startup.
- **Settings → Language** selector with restart prompt (WPF `x:Static` bindings require a relaunch).
- **Global crash reporting:** unhandled UI/domain/task exceptions open a crash dialog instead of silently exiting — local log, clipboard copy, GitHub issue search by fingerprint, pre-filled new issue URL.
- Debug-only crash test buttons on Settings (Debug builds).

### Changed
- Game pak string language (`strings_*.str`) follows the selected UI culture (e.g. `pt-BR` → `brazilian`).

### Fixed
- App icon / favicon sizes for clearer display at small DPI.

### Notes
- Dynamic status messages (e.g. “Loaded successfully: …”) still use English fallbacks in code; static UI labels and buttons are translated.
- Locale strings were machine-translated — wording may be refined in later patches.
- Restart the app after changing language in Settings.

---

## [1.1.4] — 2026-08-25

### Added
- **Vehicles — store unlocks:** global release region lock / unlock-all-by-rank, plus per-truck region-free and unlock rank (0–30).
- **Vehicles — restore all:** restore every truck XML from baseline on the list page.
- **In-app update download:** progress dialog, then Update and restart after the installer finishes downloading.
- Collapsible global panels (Expander) on Parts and Vehicles list pages.
- Refreshed app icon / favicon.

### Changed
- Vehicle catalog metadata uses a single **Year**; Soviet-era UA/BY/RU plants before 1991 show as USSR (e.g. Gor BY-4).
- Default window size tuned for typical desktop layouts (DPI-aware logical size).

---

## [1.1.3] — 2026-08-25

### Added
- **Vehicles — store price:** global multiplier on the list page and per-truck **Store price** field (edits `GameData Price` in truck XML).
- **Safe-range hints:** numeric vehicle tuning fields show baseline, allowed range, and color-coded guidance (normal / atypical / extreme) while editing.

---

## [1.1.2] — 2026-08-24

### Added
- **In-app update check:** Home shows a banner when a newer GitHub release exists. Settings can check now, open the installer, or skip that version.

---

## [1.1.1] — 2026-08-24

### Fixed
- **Ignore ice** on tires wrote invalid self-closing `WheelFriction` XML (`… / IsIgnoreIce="true">`). That broke the in-game Truck Store and garage for most trucks ([#1](https://github.com/Gixx/snowrunner-tuning-shop/issues/1)).

---

## [1.1.0] — 2026-08-23

### Added
- **Game update detection:** Home shows a banner when `initial.pak` looks like a new vanilla file (no Tuning Shop marker, saved profile still present, working pak differs from the baseline).
- **Refresh baseline from game** (Home and Settings): copies the current working pak over the read-only baseline **without** clearing the saved profile.
- **Reapply saved changes** (Home and Settings): writes the saved profile back into the working pak, then reports applied / missing / failed files.
- Workspace health status on Home (banner + profile line) and Settings.

### Changed
- Full baseline restore still keeps the saved profile. The confirm/success text now points at **Reapply saved changes**.
- Saving while the working pak matches the baseline no longer wipes a waiting profile (so restore → refresh → reapply is safe).

### Notes
- After a game update, refresh the baseline **before** reapplying. Reapply is disabled until the working pak matches the baseline.
- Avoid saving new edits after restore/refresh until you reapply — a new save would replace the saved profile.

---

## [1.0.6] — 2026-08-23

### Added
- **Tuning profile** persistence: every save writes a diff of tuned pak entries (vs. your baseline) to `%LocalAppData%\SnowRunnerTuningShop\profiles\`.
- **Pak marker** (`[media]/_tuning_shop/marker.xml`) injected into the working `initial.pak` when edits exist — used later for game-update detection (v1.1.0).
- **Baseline and working fingerprints** (SHA-256, size, timestamp) stored per game edition in `config.json`.
- **Vehicles — global multipliers** for fuel tank and responsiveness; **front steer** uses three presets (Min 10°, Default/baseline, Max 60°).

### Notes
- Full baseline restore keeps the saved profile for future **Reapply** (coming in v1.1.0).
- Replacing the baseline clears the profile for that edition.

---

## [1.0.5] — 2026-08-23

### Fixed
- **Drive / AWD detection** now matches the in-game garage: `Torque="connectable"` alone is no longer treated as selectable AWD. Upgradeable / selectable AWD requires a **TransferBox** (or similar) addon socket. Trucks such as the Pacific P512 PF correctly show **RWD** when the game lists **AWD: No**.
- Clarified the vehicle Drive field hint to describe this behavior.

### Changed
- Vehicle detail: short note under **Country** — *Brand origin of the real-world basis.*
- Slightly taller default main window so the country hint does not clip.

---

## [1.0.4] — 2026-08-23

### Fixed
- Corrected **country of origin** for several vehicles based on the real-world *Based on* manufacturer (not heuristics alone), including:
  - Hendrickson / AVENHORN A15 → United States (was Australia)
  - Western Star family → Canada
  - Pacific P12 / P512 → Canada; Pacific M26/P16 remains United States (different company)
  - MZKT-based Kolob trucks → Belarus
  - KrAZ-6443 (Tayga 6436) → Ukraine
- Fixed Bandit *Based on* text (was a release date) to **P8WD GEOLKOM-PM**.
- Added Canada and Belarus flag assets and oval marks.

---

## [1.0.3] — 2026-08-23

### Added
- **Windows installer** (Inno Setup) built in the Release workflow: `*-win-x64-Setup.exe` with Start menu shortcut and uninstaller.
- Portable **zip** kept as an optional download alongside the installer.

### Changed
- Release notes and README prefer the Setup.exe over unzipping the self-contained runtime folder.

---

## [1.0.2] — 2026-08-22

### Added
- **Settings** page:
  - Theme: Dark / Light / System (persisted in `%LocalAppData%\SnowRunnerTuningShop\config.json`)
  - Restore full baseline
  - Links: project website, PayPal donate, GitHub issue tracker
- **GitHub Pages** landing site under `website/` (deploy workflow).
- App favicon / site icon aligned with the desktop app icon.

### Changed
- Funding / donate PayPal link set to the project maintainer (`paypal.me/GaborIvan`).

---

## [1.0.1] — 2026-08-22

### Added
- **Vehicles** page: per-truck tuning (fuel, front/rear steer, responsiveness, diff lock, drive layout) with save / restore from baseline.
- Enriched vehicle catalog metadata (based on, years, country, manufacturer logos).
- **General** page: camera collision (`ClipCamera`) and trail rock size controls.
- Open-source **MIT** license and repository funding metadata.

### Changed
- Parts / tires UI polish (including grouping identical tire rows that differ only by *Used by*).
- Broader UI finetuning across Parts and Vehicles.

---

## [1.0.0] — 2026-08-21

### Added
- Initial public release of **SnowRunner Tuning Shop** (WPF, self-contained win-x64).
- **Baseline** workflow: set from original `initial.pak`, restore full pak, remember store/edition paths.
- **Parts** tuning with global multipliers and per-row edits:
  - Winch, Engine, Gearbox, Suspension, Tires
  - *Used by* vehicle mapping, filters, category restore to baseline
- Fluent-style shell: Home, Parts, Vehicles (catalog), Settings placeholder; collapsible sidebar.
- CI and **Release** GitHub Actions (tag `v*` → self-contained zip).
- README and distribution license notes.

---

[Unreleased]: https://github.com/Gixx/snowrunner-tuning-shop/compare/v1.3.0...HEAD
[1.3.0]: https://github.com/Gixx/snowrunner-tuning-shop/releases/tag/v1.3.0
[1.2.2]: https://github.com/Gixx/snowrunner-tuning-shop/releases/tag/v1.2.2
[1.2.1]: https://github.com/Gixx/snowrunner-tuning-shop/releases/tag/v1.2.1
[1.2.0]: https://github.com/Gixx/snowrunner-tuning-shop/releases/tag/v1.2.0
[1.1.4]: https://github.com/Gixx/snowrunner-tuning-shop/releases/tag/v1.1.4
[1.1.3]: https://github.com/Gixx/snowrunner-tuning-shop/releases/tag/v1.1.3
[1.1.2]: https://github.com/Gixx/snowrunner-tuning-shop/releases/tag/v1.1.2
[1.1.1]: https://github.com/Gixx/snowrunner-tuning-shop/releases/tag/v1.1.1
[1.1.0]: https://github.com/Gixx/snowrunner-tuning-shop/releases/tag/v1.1.0
[1.0.6]: https://github.com/Gixx/snowrunner-tuning-shop/releases/tag/v1.0.6
[1.0.5]: https://github.com/Gixx/snowrunner-tuning-shop/releases/tag/v1.0.5
[1.0.4]: https://github.com/Gixx/snowrunner-tuning-shop/releases/tag/v1.0.4
[1.0.3]: https://github.com/Gixx/snowrunner-tuning-shop/releases/tag/v1.0.3
[1.0.2]: https://github.com/Gixx/snowrunner-tuning-shop/releases/tag/v1.0.2
[1.0.1]: https://github.com/Gixx/snowrunner-tuning-shop/releases/tag/v1.0.1
[1.0.0]: https://github.com/Gixx/snowrunner-tuning-shop/releases/tag/v1.0.0
