# Vehicle store images

Source: [SnowRunner/Vehicles](https://spintires.fandom.com/wiki/SnowRunner/Vehicles) (Fandom / Spintires wiki).

Downloaded as PNG for local app UI. Game artwork remains © Saber Interactive / Focus Entertainment; wiki images are used as fan-asset references. Do not redistribute commercially without rights clearance.

## Files

| File | Purpose |
|------|---------|
| `catalog.json` | Vehicle list for the app (`id`, `displayName`, `category`, `imageFile`) |
| `manifest.json` | Download provenance (wiki image URLs) |
| `metadata.json` | Per-vehicle facts: manufacturer, Based on, production years, country |
| `manufacturers/` | Manufacturer logos from the wiki (UI shows them on a dark plate for light theme) |
| `flags/` | Country flags (`us.png`, `ru.png`, `su.png` for USSR, …) |

Country is inferred from the wiki **Based on** analogue. If the lineage is Russian and `yearFrom` is before **1991**, the displayed country is **USSR** (`SU`) instead of Russia. Rebuild helpers live in `_meta_build/` (not required at runtime).
