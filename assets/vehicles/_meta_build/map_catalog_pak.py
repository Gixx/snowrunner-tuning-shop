# Maps wiki catalog cards to pak XML file names (stable ids, not display names).
from __future__ import annotations

import json
import pathlib
import re

DUMP = pathlib.Path(__file__).resolve().parent / "pak-trucks.json"
CATALOG = pathlib.Path(__file__).resolve().parents[1] / "catalog.json"

# Wiki slugs / store aliases that do not match the XML file name.
MANUAL = {
    "gmh9500": "gmc_9500",
    "cct680": "cat_ct680",
    "fleetstar": "international_fleetstar_f2070a",
    "wws4964": "ws_4964_white",
    "a67096a": "azov_670963n",
    "c745c": "cat_745c",
    "pp12": "pacific_p12w",
    "smfk816e": "sleiter_mfk816",
    "western_star_47x_nf_1424": "western_star_nf1424",
    "western_star_47x_nf_1430": "western_star_nf1430",
    "ws6900ts": "ws_6900xd_twin",
    "z612h": "zikz_612h_mastodont",
    "mba3332": "mercedes_benz_actros_6x6",
    "mbz": "mercedes_benz_zetros_6x6",
    "mr230": "mercer_6x6r_230",
    "cth357": "cat_th357",
    "chevrolet-apache": "chevy_apache",
    "veh_c770g_alt": "cat_770g",
    "ank_mk38_civilian": "ank_mk38",
}


def norm(value: str | None) -> str:
    return re.sub(r"[^a-z0-9]+", "", (value or "").lower())


def load_trucks() -> dict[str, tuple[str, str]]:
    rows = json.loads(DUMP.read_text(encoding="utf-8-sig"))
    return {row["id"]: (row.get("uiNameKey") or "", row.get("englishName") or "") for row in rows}


def resolve(catalog_id: str, display_name: str, trucks: dict[str, tuple[str, str]]) -> tuple[str | None, str]:
    if catalog_id in MANUAL:
        pak_id = MANUAL[catalog_id]
        return (pak_id, "manual") if pak_id in trucks else (None, "manual-missing")

    id_key = norm(catalog_id)
    name_key = norm(display_name)
    by_file = {norm(truck_id): truck_id for truck_id in trucks}

    if id_key in by_file:
        return by_file[id_key], "exact-id"
    if name_key in by_file:
        return by_file[name_key], "exact-name"

    english_exact = [
        truck_id
        for truck_id, (_, english) in trucks.items()
        if name_key and norm(english) == name_key
    ]
    if len(english_exact) == 1:
        return english_exact[0], "english-name"

    if len(id_key) >= 3:
        suffix = [
            truck_id
            for truck_id in trucks
            if norm(truck_id).endswith(id_key) and len(norm(truck_id)) > len(id_key)
        ]
        if len(suffix) == 1:
            return suffix[0], "unique-suffix"

    prefixes = [
        (len(norm(truck_id)), truck_id)
        for truck_id in trucks
        if len(norm(truck_id)) >= 5
        and (id_key.startswith(norm(truck_id)) or name_key.startswith(norm(truck_id)))
    ]
    if prefixes:
        prefixes.sort(reverse=True)
        best_len = prefixes[0][0]
        best = [truck_id for length, truck_id in prefixes if length == best_len]
        if len(best) == 1:
            return best[0], "longest-prefix"

    return None, "miss"


def main() -> None:
    catalog = json.loads(CATALOG.read_text(encoding="utf-8-sig"))
    trucks = load_trucks()
    miss: list[tuple[str, str]] = []
    for row in catalog:
        pak_id, how = resolve(row["id"], row["displayName"], trucks)
        print(f"{how:16} {row['id']:32} -> {pak_id or '-'}")
        if not pak_id:
            miss.append((row["id"], row["displayName"]))
            continue
        row["pakId"] = pak_id

    if miss:
        raise SystemExit(f"unmapped catalog cards: {miss}")

    CATALOG.write_text(json.dumps(catalog, indent=4, ensure_ascii=False) + "\n", encoding="utf-8")
    print("wrote", CATALOG)


if __name__ == "__main__":
    main()
