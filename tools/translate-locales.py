#!/usr/bin/env python3
"""Build locale overlay JSON files from en.json using Google Translate."""

from __future__ import annotations

import json
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LOCALE_DIR = ROOT / "assets/localization"

TARGETS = {
    "de": "de",
    "fr": "fr",
    "es": "es",
    "pt": "pt",
    "pt-BR": "pt",
    "pl": "pl",
    "ru": "ru",
    "uk": "uk",
}


def main() -> int:
    try:
        from deep_translator import GoogleTranslator
    except ImportError:
        print("Installing deep-translator...", file=sys.stderr)
        import subprocess

        subprocess.check_call([sys.executable, "-m", "pip", "install", "deep-translator", "-q"])
        from deep_translator import GoogleTranslator

    en_path = LOCALE_DIR / "en.json"
    if not en_path.exists():
        print(f"Missing {en_path}", file=sys.stderr)
        return 1

    with en_path.open(encoding="utf-8") as f:
        en: dict[str, str] = json.load(f)

    skip_prefixes = ("Engine.", "Gearbox.", "Suspension.", "Tires.", "Winch.")
    # Translate everything; placeholders must stay intact
    preserve_tokens = ("{0", "{1", "{2", "AppInfo", "Environment.NewLine", "N0", "0%")

    for locale, google_code in TARGETS.items():
        out_path = LOCALE_DIR / f"{locale}.json"
        if out_path.exists() and out_path.stat().st_mtime >= en_path.stat().st_mtime and len(json.loads(out_path.read_text(encoding="utf-8"))) >= len(en) - 5:
            print(f"Skip up-to-date {out_path.name}")
            continue

        print(f"Translating {len(en)} keys -> {locale} ({google_code})...")
        translator = GoogleTranslator(source="en", target=google_code)
        translated: dict[str, str] = {}
        items = sorted(en.items())
        batch_size = 40
        keys = [k for k, _ in items]
        values = [v for _, v in items]

        for start in range(0, len(values), batch_size):
            chunk_vals = values[start : start + batch_size]
            chunk_keys = keys[start : start + batch_size]
            joined = "\n|||SPLIT|||\n".join(chunk_vals)
            for attempt in range(3):
                try:
                    result = translator.translate(joined)
                    break
                except Exception as exc:
                    if attempt == 2:
                        raise
                    print(f"  retry after {exc}", file=sys.stderr)
                    time.sleep(2 * (attempt + 1))
            parts = result.split("\n|||SPLIT|||\n")
            if len(parts) != len(chunk_vals):
                # fallback: translate one by one
                parts = []
                for val in chunk_vals:
                    for attempt in range(3):
                        try:
                            parts.append(translator.translate(val))
                            break
                        except Exception:
                            time.sleep(1)
                    else:
                        parts.append(val)
            for key, val in zip(chunk_keys, parts, strict=True):
                translated[key] = (val or chunk_vals[chunk_keys.index(key)]).strip()
            time.sleep(0.4)

        with out_path.open("w", encoding="utf-8") as f:
            json.dump(translated, f, ensure_ascii=False, indent=2)
            f.write("\n")
        print(f"Wrote {out_path}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
