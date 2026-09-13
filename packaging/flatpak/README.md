# Flatpak (Linux Avalonia Desktop)

App ID: **`io.github.gixx.SnowRunnerTuningShop`**

Bundles the existing self-contained `linux-x64` Avalonia publish (same binary as the portable zip). Flatpak grants home filesystem access so Steam/Proton `initial.pak` paths under `~/.local/share/Steam` and `~/.steam` work; use Flatseal for custom library folders outside `$HOME`.

Avalonia uses the X11 backend (XWayland on Wayland desktops), so the manifest grants `--socket=x11` (not only `fallback-x11`). Already-installed builds that still crash with `XOpenDisplay failed` can be fixed until reinstall with:

```bash
flatpak override --user --socket=x11 io.github.gixx.SnowRunnerTuningShop
```

Crash logs under Flatpak land in `~/.var/app/io.github.gixx.SnowRunnerTuningShop/data/SnowRunnerTuningShop/logs/`.

## Local build

```bash
# Once per machine
flatpak remote-add --if-not-exists --user flathub https://dl.flathub.org/repo/flathub.flatpakrepo
flatpak install -y --user org.freedesktop.Platform//24.08 org.freedesktop.Sdk//24.08
# Arch/CachyOS:
#   sudo pacman -S flatpak flatpak-builder

./packaging/flatpak/build.sh
# → artifacts/SnowRunnerTuningShop-vX.Y.Z-linux-x64.flatpak
```

Install / run:

```bash
flatpak install --user artifacts/SnowRunnerTuningShop-v*-linux-x64.flatpak
flatpak run io.github.gixx.SnowRunnerTuningShop
```

## Layout

| File | Role |
|------|------|
| `io.github.gixx.SnowRunnerTuningShop.yml` | Manifest |
| `*.desktop` / `*.metainfo.xml` | Desktop entry + AppStream |
| `snowrunner-tuning-shop.sh` | `/app/bin` launcher |
| `icons/hicolor/…` | App icons (from WPF `Assets/app-icon.png`) |
| `publish/` | Local publish output (gitignored; filled by `build.sh`) |

Release CI builds the same `.flatpak` and attaches it to the GitHub Release alongside the portable zip.
