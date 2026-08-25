# Changelog

All notable changes to **SnowRunner Tuning Shop** are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project follows [Semantic Versioning](https://semver.org/).

Releases are published from `v*` git tags via GitHub Actions ([Releases](https://github.com/Gixx/snowrunner-tuning-shop/releases)).

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
