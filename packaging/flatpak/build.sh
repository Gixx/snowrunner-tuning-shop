#!/usr/bin/env bash
# Build a single-file Flatpak bundle for SnowRunner Tuning Shop (Linux Avalonia).
# Prerequisites: .NET 10 SDK, flatpak, flatpak-builder, org.freedesktop.{Platform,Sdk}//24.08
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
FLATPAK_DIR="$ROOT/packaging/flatpak"
PUBLISH_DIR="$FLATPAK_DIR/publish"
APP_ID="io.github.gixx.SnowRunnerTuningShop"
MANIFEST="$FLATPAK_DIR/$APP_ID.yml"

VERSION="$(sed -n 's/.*public const string Version = "\([^"]*\)".*/\1/p' \
  "$ROOT/src/SnowRunnerTuningShop.Core/AppInfo.cs" | head -n1)"
if [[ -z "$VERSION" ]]; then
  echo "Could not parse AppInfo.Version" >&2
  exit 1
fi
TAG="v${VERSION}"
NUMERIC="${VERSION%%-*}"
OUT_BUNDLE="${1:-$ROOT/artifacts/SnowRunnerTuningShop-${TAG}-linux-x64.flatpak}"

echo "==> Publishing Avalonia Desktop (linux-x64 self-contained, $VERSION)"
rm -rf "$PUBLISH_DIR"
mkdir -p "$PUBLISH_DIR"
dotnet publish "$ROOT/src/SnowRunnerTuningShop.Desktop/SnowRunnerTuningShop.Desktop.csproj" \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:Version="$NUMERIC" \
  -p:InformationalVersion="$TAG" \
  -p:DebugType=None \
  -p:DebugSymbols=false \
  -o "$PUBLISH_DIR"

if [[ ! -x "$PUBLISH_DIR/SnowRunnerTuningShop.Desktop" ]] && [[ ! -f "$PUBLISH_DIR/SnowRunnerTuningShop.Desktop" ]]; then
  echo "Publish output missing SnowRunnerTuningShop.Desktop" >&2
  exit 1
fi
chmod +x "$PUBLISH_DIR/SnowRunnerTuningShop.Desktop" || true

echo "==> Syncing AppStream release metadata to $VERSION"
python3 - <<PY
from pathlib import Path
import re
path = Path("$FLATPAK_DIR/io.github.gixx.SnowRunnerTuningShop.metainfo.xml")
text = path.read_text(encoding="utf-8")
text, n = re.subn(
    r'<release version="[^"]*" date="[^"]*"/>',
    f'<release version="$VERSION" date="$(date -u +%Y-%m-%d)"/>',
    text,
    count=1,
)
if n != 1:
    raise SystemExit("Could not update metainfo <release>")
path.write_text(text, encoding="utf-8")
print("Updated", path)
PY

if ! command -v flatpak-builder >/dev/null 2>&1; then
  echo "flatpak-builder is not installed. On Arch/CachyOS: sudo pacman -S flatpak-builder" >&2
  echo "Also install runtimes: flatpak install -y flathub org.freedesktop.Platform//24.08 org.freedesktop.Sdk//24.08" >&2
  exit 1
fi

echo "==> Building Flatpak ($APP_ID)"
cd "$FLATPAK_DIR"
rm -rf build repo .flatpak-builder
flatpak-builder --user --force-clean --repo=repo build "$MANIFEST"

mkdir -p "$(dirname "$OUT_BUNDLE")"
echo "==> Bundling → $OUT_BUNDLE"
flatpak build-bundle repo "$OUT_BUNDLE" "$APP_ID"
echo "Done: $OUT_BUNDLE"
echo "Install with: flatpak install --user $OUT_BUNDLE"
echo "Run with:     flatpak run $APP_ID"
