# SnowRunner Tuning Shop

Windows desktop app for fine-tuning **SnowRunner** `initial.pak` values (engines, gearboxes, suspensions, tires, winches, and more).

[![Version](https://img.shields.io/github/v/release/Gixx/snowrunner-tuning-shop?style=flat-square&label=version&color=38bdf8)](https://github.com/Gixx/snowrunner-tuning-shop/releases/latest)
[![C#](https://img.shields.io/badge/language-C%23-239120?style=flat-square&logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![WPF](https://img.shields.io/badge/WPF-.NET-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://learn.microsoft.com/dotnet/desktop/wpf/)
[![Windows](https://img.shields.io/badge/platform-Windows-0078D4?style=flat-square&logo=windows&logoColor=white)](https://github.com/Gixx/snowrunner-tuning-shop/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/Gixx/snowrunner-tuning-shop/total.svg?style=flat-square&label=downloads&color=e11d48)](https://github.com/Gixx/snowrunner-tuning-shop/releases)
[![Website](https://img.shields.io/badge/website-GitHub%20Pages-38bdf8?style=flat-square&logo=githubpages&logoColor=white)](https://gixx.github.io/snowrunner-tuning-shop/)
[![License](https://img.shields.io/github/license/Gixx/snowrunner-tuning-shop?style=flat-square)](LICENSE)

See the full [Changelog](CHANGELOG.md) for features and fixes per release.

---

## What it is

SnowRunner stores a lot of truck and part balance data inside `initial.pak` (a ZIP archive of XML files). This tool lets you load that pak, change tuning values with a simple UI, and write the changes back — while keeping a read-only **baseline** of your original file so you can restore later.

It is aimed at players who want stronger engines, longer winches, stickier tires, etc., without hand-editing XML.

> **Not affiliated with** Saber Interactive, Focus Entertainment, or SnowRunner. Use at your own risk; modifying game files can break multiplayer fairness, updates, or installs.

---

## Features

### Workspace / baseline
- Set a baseline from your **unmodified** original `initial.pak` (Steam, GOG, Epic, Xbox, …)
- Remembers the working pak path per game edition/location
- Restore the full pak from baseline
- Detects when the game replaces `initial.pak`, refresh the baseline from the new vanilla file, then reapply saved tunings
- Switch between store/install locations

### Parts tuning
Global multipliers and per-row edits for:

| Tab | What you can change |
|-----|---------------------|
| **Winch** | Length, strength, autonomous |
| **Engine** | Torque, fuel, damage capacity, responsiveness |
| **Gearbox** | Fuel, idle modifier, AWD penalty |
| **Suspension** | Height, strength, damping, damage (front/rear) |
| **Tires** | On-road / off-road / mud friction, ignore ice |

Also:
- **Used by** — which trucks use each part set
- Filter/search within each list
- Restore that part category to baseline values
- Loading overlay while large lists are read

### Vehicles
- Catalog browser with images and categories (highway, scout, etc.)
- Per-vehicle deep tuning is planned

### UI
- Collapsible hamburger sidebar (optional pin)
- Dark/light Fluent-style theme follows Windows
- **Languages:** English, Deutsch, Français, Español, Português, Português (Brasil), Polski, Русский, Українська — chosen in the installer or **Settings** (restart to apply)
- **Crash reports:** unexpected errors show a dialog with copy report / open GitHub issue (deduplicated by fingerprint)

---

## Requirements

- Windows 10/11
- A legitimate SnowRunner install with `initial.pak`
- For building from source (developers): .NET SDK matching the project (`net10.0-windows`)

---

## How to use (players)

1. Run **SnowRunner Tuning Shop**.
2. On **Home**, choose **Set baseline from original…** and pick your untouched `initial.pak`.
3. Open **Parts**, pick a tab (Winch, Engine, …).
4. Use global multipliers **Apply**, and/or edit rows and **Save individual changes**.
5. If something goes wrong, use **Restore … to baseline** on that tab, or **Restore full baseline** on Home. After a game update, use **Refresh baseline from game**, then **Reapply saved changes**.

**Tip:** Keep a copy of your original pak outside the game folder as well. Updates may replace `initial.pak`.

---

## Project layout (developers)

```
src/
  SnowRunnerTuningShop/          # WPF UI
  SnowRunnerTuningShop.Core/     # Pak I/O, parsers, tuning services
assets/vehicles/                 # Vehicle catalog images + JSON
website/                         # Landing page (GitHub Pages)
```

The site is published by `.github/workflows/pages.yml`. In the repo: **Settings → Pages → Source: GitHub Actions**. Live URL: `https://gixx.github.io/snowrunner-tuning-shop/`

Build:

```bash
dotnet build src/SnowRunnerTuningShop/SnowRunnerTuningShop.csproj -c Release
```

---

## Disclaimer

- Editing `initial.pak` is unsupported by the game publisher.
- Always back up your game files before tuning.
- Online / multiplayer use of modified data may violate game or platform rules — you are responsible for how you use the tool.
- SnowRunner, related names, and assets belong to their respective owners. Vehicle images in this repo may come from community/wiki sources for catalog display only.

---

## License

This project is licensed under the [MIT License](LICENSE).

SnowRunner and related trademarks are property of their respective owners.
This project is an unofficial fan tool and is not affiliated with or endorsed
by the game's publishers or developers.

---

## Releases (GitHub Actions)

See **[CHANGELOG.md](CHANGELOG.md)** for what changed in each tagged version.

CI builds on every push/PR to `main`.

To publish a release (installer + portable zip):

```bash
git tag v1.0.0
git push origin v1.0.0
```

That runs the **Release** workflow: self-contained `win-x64` publish → **Inno Setup installer** (+ portable zip) → GitHub Release assets.

Prefer the `*-Setup.exe` installer (Start menu shortcut + uninstall). The zip is optional for portable use.

Tag format must be `vMAJOR.MINOR.PATCH` (e.g. `v1.0.0`). Code signing is not included yet.
