# SnowRunner Tuning Shop — Desktop (Avalonia)

Cross-platform UI shell for **Linux** (and a Windows sidecar). The Windows product release remains the WPF app under `src/SnowRunnerTuningShop/`. Domain logic lives in `SnowRunnerTuningShop.Core`.

## Prerequisites

- .NET 10 SDK
- Linux GUI stack for Avalonia (CachyOS/Arch: `dotnet-sdk-10.0` plus usual desktop libs; if the window fails to open, install `icu` / font packages from your distro)

## Run

In Cursor: select **Avalonia Desktop (Linux)** in the Run and Debug dropdown, then press **F5** (build + debug) or use **Avalonia Desktop (run, no debugger)** if the C# debugger is unavailable. `Ctrl+Shift+B` builds the Desktop project.

From the repo root (prefer a native filesystem clone on Linux — avoid building from a Windows SMB share):

```bash
dotnet run --project src/SnowRunnerTuningShop.Desktop -c Debug
```

Windows (layout smoke without a VM):

```powershell
dotnet run --project src/SnowRunnerTuningShop.Desktop -c Debug
```

## Publish linux-x64 (self-contained)

```bash
dotnet publish src/SnowRunnerTuningShop.Desktop -c Release -r linux-x64 --self-contained true -o artifacts/linux-x64
./artifacts/linux-x64/SnowRunnerTuningShop.Desktop
```

Flatpak packaging comes in a later phase (`packaging/flatpak/`).

## Status

Phase 1: Home workspace (baseline, location, restore, refresh, reapply) plus sidebar navigation. General / Parts / Vehicles / Trailers / Photo Mode / Settings are placeholders. Flatpak packaging is later.
