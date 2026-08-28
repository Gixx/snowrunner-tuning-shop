#!/usr/bin/env python3
"""Extract UiText strings into JSON and refactor UiText.cs to use StringResources."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
UITEXT = ROOT / "src/SnowRunnerTuningShop/Localization/UiText.cs"
LOCALE_DIR = ROOT / "assets/localization"

CONST_RE = re.compile(
    r'^\s*public const string (\w+) = (?:"((?:[^"\\]|\\.)*)"|'
    r'(?:"((?:[^"\\]|\\.)*)"\s*\+\s*)+"((?:[^"\\]|\\.)*)");',
    re.MULTILINE,
)

# Simpler: match single-line const
SIMPLE_CONST_RE = re.compile(
    r'^(\s*)public const string (\w+) = "(.*)";\s*$',
    re.MULTILINE,
)

# Multi-line const starting with quote
MULTI_START_RE = re.compile(
    r'^(\s*)public const string (\w+) =\s*$',
    re.MULTILINE,
)


def unescape_csharp(s: str) -> str:
    return (
        s.replace('\\"', '"')
        .replace("\\n", "\n")
        .replace("\\r", "\r")
        .replace("\\t", "\t")
        .replace("\\\\", "\\")
    )


def extract_multiline_const(text: str, start: int) -> tuple[str, str, int]:
    """Return (name, value, end_index) for multiline const at start."""
    line_end = text.find("\n", start)
    header = text[start:line_end]
    m = re.match(r'\s*public const string (\w+) =', header)
    if not m:
        raise ValueError("Not a multiline const")
    name = m.group(1)
    pos = line_end + 1
    parts: list[str] = []
    while pos < len(text):
        line_end = text.find("\n", pos)
        if line_end == -1:
            line_end = len(text)
        line = text[pos:line_end]
        stripped = line.strip()
        if stripped.startswith('"'):
            chunk = stripped
            while chunk.endswith("+") and not chunk.rstrip().endswith('";'):
                chunk = chunk[:-1].strip()
                if chunk.endswith('+'):
                    chunk = chunk[:-1].strip()
                if chunk.startswith('"') and chunk.endswith('"'):
                    parts.append(chunk[1:-1])
                pos = line_end + 1
                line_end = text.find("\n", pos)
                if line_end == -1:
                    line_end = len(text)
                line = text[pos:line_end]
                stripped = line.strip()
                chunk = stripped
            if chunk.endswith('";'):
                inner = chunk[1:-2]
                parts.append(inner)
                return name, unescape_csharp("".join(parts)), line_end + 1
        pos = line_end + 1
    raise ValueError(f"Unterminated multiline const {name}")


def parse_uittext(content: str) -> tuple[dict[str, str], list[tuple[str, str, str, bool]]]:
    """Parse nested classes and const strings. Returns (keys->values, replacements)."""
    strings: dict[str, str] = {}
    class_stack: list[str] = []
    replacements: list[tuple[str, str, str, bool]] = []  # indent, key, value, is_multiline

    lines = content.splitlines(keepends=True)
    i = 0
    while i < len(lines):
        line = lines[i]
        class_match = re.match(r"(\s*)public static class (\w+)", line)
        if class_match:
            class_stack.append(class_match.group(2))
            i += 1
            continue
        if line.strip() == "}" and class_stack:
            class_stack.pop()
            i += 1
            continue

        simple = re.match(r'^(\s*)public const string (\w+) = "(.*)";\s*$', line.rstrip("\n"))
        if simple and class_stack:
            indent, name, raw = simple.groups()
            value = unescape_csharp(raw)
            key = f"{'.'.join(class_stack)}.{name}"
            strings[key] = value
            replacements.append((indent, key, value, False))
            i += 1
            continue

        multi_start = re.match(r'^(\s*)public const string (\w+) =\s*$', line.rstrip("\n"))
        if multi_start and class_stack:
            # collect multiline
            indent, name = multi_start.groups()
            pos = sum(len(lines[j]) for j in range(i))
            full = "".join(lines)
            abs_start = pos
            try:
                _, value, end_pos = extract_multiline_const(full, abs_start)
            except ValueError:
                i += 1
                continue
            key = f"{'.'.join(class_stack)}.{name}"
            strings[key] = value
            replacements.append((indent, key, value, True))
            # advance i past multiline block
            consumed = full[abs_start:end_pos]
            i += consumed.count("\n")
            continue

        i += 1

    return strings, replacements


def refactor_content(content: str, strings: dict[str, str]) -> str:
    """Replace const strings with StringResources.Get properties."""
    class_stack: list[str] = []
    out_lines: list[str] = []
    lines = content.splitlines(keepends=True)
    i = 0

    while i < len(lines):
        line = lines[i]
        class_match = re.match(r"(\s*)public static class (\w+)", line)
        if class_match:
            class_stack.append(class_match.group(2))
            out_lines.append(line)
            i += 1
            continue
        if line.strip() == "}" and class_stack:
            class_stack.pop()
            out_lines.append(line)
            i += 1
            continue

        simple = re.match(r'^(\s*)public const string (\w+) = "(.*)";\s*$', line.rstrip("\n"))
        if simple and class_stack:
            indent, name, raw = simple.groups()
            value = unescape_csharp(raw)
            key = f"{'.'.join(class_stack)}.{name}"
            fallback = json.dumps(value, ensure_ascii=False)
            out_lines.append(
                f'{indent}public static string {name} => StringResources.Get("{key}", {fallback});\n'
            )
            i += 1
            continue

        multi_start = re.match(r'^(\s*)public const string (\w+) =\s*$', line.rstrip("\n"))
        if multi_start and class_stack:
            indent, name = multi_start.groups()
            pos = sum(len(lines[j]) for j in range(i))
            full = "".join(lines)
            try:
                _, value, end_pos = extract_multiline_const(full, pos)
            except ValueError:
                out_lines.append(line)
                i += 1
                continue
            key = f"{'.'.join(class_stack)}.{name}"
            fallback = json.dumps(value, ensure_ascii=False)
            out_lines.append(
                f'{indent}public static string {name} => StringResources.Get("{key}", {fallback});\n'
            )
            consumed = full[pos:end_pos]
            i += consumed.count("\n")
            continue

        out_lines.append(line)
        i += 1

    result = "".join(out_lines)
    if "StringResources" not in result.split("namespace")[1].split("public static class")[0]:
        result = result.replace(
            "namespace SnowRunnerTuningShop.Localization;\n\nusing SnowRunnerTuningShop.Core;",
            "namespace SnowRunnerTuningShop.Localization;\n\nusing SnowRunnerTuningShop.Core;",
        )
    return result


def add_format_keys(content: str, en: dict[str, str]) -> str:
    """Add format string keys for common static methods (manual patterns)."""
    patterns = [
        (
            r'public static string VersionLabel => \$"Version \{AppInfo\.Version\}";',
            'Nav.VersionLabel',
            'Version {0}',
            'public static string VersionLabel => StringResources.Format("Nav.VersionLabel", "Version {0}", AppInfo.Version);',
        ),
        (
            r'public static string InstalledVersion => \$"Installed version: \{AppInfo\.Version\}";',
            'Settings.InstalledVersion',
            'Installed version: {0}',
            'public static string InstalledVersion => StringResources.Format("Settings.InstalledVersion", "Installed version: {0}", AppInfo.Version);',
        ),
    ]
    for old, key, template, new in patterns:
        en[key] = template
        content = re.sub(old, new, content)
    return content


def main() -> int:
    if not UITEXT.exists():
        print(f"Missing {UITEXT}", file=sys.stderr)
        return 1

    content = UITEXT.read_text(encoding="utf-8")
    strings, _ = parse_uittext(content)
    refactored = refactor_content(content, strings)
    refactored = add_format_keys(refactored, strings)

    LOCALE_DIR.mkdir(parents=True, exist_ok=True)
    en_path = LOCALE_DIR / "en.json"
    with en_path.open("w", encoding="utf-8") as f:
        json.dump(dict(sorted(strings.items())), f, ensure_ascii=False, indent=2)
        f.write("\n")

    UITEXT.write_text(refactored, encoding="utf-8")
    print(f"Wrote {len(strings)} keys to {en_path}")
    print(f"Refactored {UITEXT}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
