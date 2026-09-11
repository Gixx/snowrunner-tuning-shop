# Code review — SnowRunner Tuning Shop

**Nézőpont:** külső senior developer, 2026-09-04  
**Scope:** `src/SnowRunnerTuningShop`, `src/SnowRunnerTuningShop.Core`, `tests/`, CI/release, README  
**Nem cél:** feature-lista bővítése; Linux/Avalonia terv; gitignored `docs/plan/`

A termék működő, jól célzott fan tool: a Core pak-kezelés nagy része (temp + raw zip replace, in-place `cache_block`, `pakId` szabály) tudatos válasz korábbi crash-ekre. Az alábbiak **prioritizált adósság és kockázat**, nem „újrakezdés” indítvány.

---

## Összkép

| Erősség | Gyengeség |
|---------|-----------|
| Nyelvfüggetlen azonosítók (`pakId` / XML `Name`) | Parts / Vehicles / Trailers code-behind klónozás |
| Védett pak replace útvonal (temp → Move) | Nem atomikus full restore; Update-mode zip ágak |
| Crash report angol; InvariantCulture számok | Majdnem nincs tesz a veszélyes I/O-ra |
| Game-running write gate (UI + Core) | Vehicles/Trailers sync load; kevés I/O teszt |
| Locale kulcskatalógus + en lefedettség | README elavult (Vehicles „planned”) |

---

## P0 — először ezek (adatvesztés / játék törés / félrevezető UI)

> **Státusz (2026-09-04):** mind az öt P0 tétel implementálva az Unreleased ágon. Lásd CHANGELOG `[Unreleased]`.

### 1. `RemoveEntries` / `AddEntries` in-place `ZipArchiveMode.Update` — **kész**
**Hol:** `Core/Pak/InitialPakWriter.cs` (+ `PakRawZipReplacer.RemoveEntries` / `AddEntries`)  
Temp + verbatim local-record rebuild; marker remove/add már nem Update-módban nyitja a live pakot.

### 2. Full baseline restore nem atomikus — **kész**
**Hol:** `Core/Backup/PakBaselineService.RestorePakFromBaseline`  
Temp másolat + méretellenőrzés + `File.Move` overwrite.

### 3. Trailer „Available in store” undo törölhet nem-supplemental `Trailer` socketet — **kész**
**Hol:** `Core/Trailers/TrailerTuningService.RemoveSupplementalStoreHitch`  
Baseline Trailer socketek megmaradnak; csak a pótlólag beszúrtakat veszi ki. Tesztek: `TrailerStoreAvailabilityTests`.

### 4. Parts tab async race — **kész** (+ `TryProceed(_session)`)
**Hol:** `Views/PartsView.xaml.cs` + `*TuningView`  
`CancellationToken` a load előtt/után; session átadás a Parts gyermekekbe.

### 5. Release workflow nem futtat tesztet, nem ellenőrzi `AppInfo.Version` ↔ tag — **kész**
**Hol:** `.github/workflows/release.yml`  
`dotnet test` + `AppInfo.Version` assert a publish előtt.

---

## P1 — magas értékű karbantartás / regresszió

> **Státusz (2026-09-11):** P1 tételek lezárva inkrementális extractekkel. Teljes „egy osztály minden parts service” még nincs (domain rewrite külön marad), de a pak I/O / XML / UI klóncsökkentés megvan.

### 6. Parts szolgáltatások másolása — **kész (pipeline)**
`PartPakPipeline` (`BuildBaselineReplacements` / `ValidateMultiplier` / `CommitReplacements`) + `PartXmlHelpers.ReadEntryUtf8`. Mind a hat `*Service` ApplyGlobal ezen megy át. Domain parse/rewrite (Engine regex vs Crane XDocument) továbbra is per-service — szándékos.

### 7. Truck / Trailer God class — **kész (következő extract)**
`TrailerHitchXml` + `TruckDiffLockXml` + `VehicleGameDataXml` (közös GameData/TruckData attribute I/O). Truck/Trailer orchestration a service-ekben marad.

### 8. UI code-behind klónok — **kész (Parts helpers)**
`PartsTuningUiHelpers` (write-button states, grid CommitEdit, Clear) az összes Parts `*TuningView`-n. Teljes `TuningGridController` / Vehicles–Trailers spinner unifikáció nincs (P2).

### 9. `PakWriteUi.TryProceed(null)` — **kész (P0)**

### 10. Zip entry név casing — **kész**
`PakEntryNameMap` + IgnoreCase lookup, canonical casing a raw write-hoz (`InitialPakWriter` / `PakRawZipReplacer`).

### 11. Pak I/O és restore tesztek — **kész**
`tests/.../PakIoTests.cs`: ReplaceEntries round-trip, case-insensitive replace, RemoveEntries, CopyEntriesFromPak, hitch undo.

### 12. Vehicles/Trailers szinkron load — **kész**
Async `Ensure*Loaded` + DetailPanel loading overlay (Parts minta).

### 13. `WorkspaceConfigStore` race + silent corrupt — **kész**
Lock; atomikus save; corrupt → `.corrupt.bak` + `ConsumeCorruptConfigWarning` a MainWindow-on.

---

## P2 — közepes (UX, memória, éles élek)

> **Státusz (2026-09-11):** P2 nyitott tételek (14–16, 18–23) lezárva. 17 és 24 korábban kész.

### 14. MessageBox túlterhelés — **kész**
Siker → status/banner (`PartsView.StatusText` + `StatusChanged`; Vehicles/Trailers/General/PhotoMode status mezők). Modal: confirm + error.

### 15. Írás közben nincs busy-disable — **kész**
`PakWriteUi.BeginBusyWrite`: Wait cursor + write gombok disable Apply/Save/Restore handlereken.

### 16. Eager BitmapImage a katalógusokban — **kész**
Vehicles/Trailers `LoadCatalog` csak path; thumb decode lazy + path cache (flag cache mintája). Detail kép selection-re marad.

### 17. `assets/vehicles/_meta_build` bemegy az outputba — **kész**
Vehicles Content Include kizárja a `_meta_build`-et (trailers mintájára).

### 18. `PakFileId` fuzzy suffix/prefix — **kész**
Ambiguus fuzzy → `null`; exact + DLC tie-break változatlan. Tesztek: `PakFileIdTests`.

### 19. Change location / első baseline tuned pakból — **kész**
`HomeView.ActivateFromBrowse`: marker + hiányzó edition baseline → Yes/No figyelmeztetés mielőtt `ChangeLocation` létrehozza a baseline-t.

### 20. Crash report tartalmazza a teljes pak pathot — **kész**
`CrashReportBuilder.SanitizePathForReport` (utolsó 3 szegmens); BugReport ugyanezt használja.

### 21. Photo Mode / General reload minden `GameRunningChanged`-re — **kész**
Csak `RefreshWriteGates` (gomb `IsEnabled`); teljes reload `PakChanged` / `BaselineChanged`-en.

### 22. Locale tesztek csak en + de + zh-CN — **kész**
`LocaleKeyCatalogTests`: `[MemberData]` az összes shipped `assets/localization/*.json`-ra (`keys.json` / `catalog.json` kihagyva).

### 23. CI nem buildeli az installert / nem publish smoke — **kész**
`ci.yml`: `dotnet publish` win-x64 self-contained → `publish/smoke` a tesztek után.

### 24. README drift — **kész**
Vehicles szekció frissítve (per-vehicle tuning + global multipliers).

---

## P3 — alacsony / polish

- Hardcoded chrome: MainWindow title, ☰ (márka OK lehet).  
- `TrailerStoreUiFix` sok nyelvi string a Core-ban.  
- `PakCacheBlockLayoutGuard` hardcode next entry (`strings_brazilian_portuguese.str`).  
- `SnowRunnerProcessGuard` fix process nevek; access-denied → „nem fut”.  
- `LocaleKeyCatalog` üres keys.json → gap check no-op.  
- `InternalsVisibleTo` PhotoModeLoadTest — nincs a solutionben (orphan).  
- `tools/` gitignore, de pár script tracked; Dependabot / CODEOWNERS hiány.  
- Accessibility: kevés `AutomationProperties`.  
- Coverlet a teszten van, coverage gate nincs.  
- Release: unpinned Chocolatey Inno; nincs Authenticode (dokumentált).

---

## Ami jól van megoldva (ne romboljátok szét)

1. **Katalog → pak soha display name alapján** (`pakId`, `PakFileId`).  
2. **ReplaceEntries:** temp copy + raw local replace + Move.  
3. **Photo Mode:** Latin-1, in-place `cache_block`, sslbundle Apply kikapcsolva.  
4. **Crash UI angol** (`GetEnglish`).  
5. **InvariantCulture** numerikus I/O.  
6. **Game running gate** (banner + Core `ThrowIfRunning`) — jó irány.  
7. **Workspace marker + health** (Refresh baseline tiltás marker esetén).  
8. **Új Core tesztek + CI `dotnet test`** — jó alap, bővítsétek a P0 I/O-ra.

---

## Javasolt sorrend (4–6 sprintnyi „adósság”)

| # | Tétel | Típus |
|---|--------|--------|
| ~~1–5, 7 hitch, P0~~ | ~~P0 biztonság + hitch undo~~ | **kész** |
| ~~10~~ | ~~Entry-name casing~~ | **kész** |
| ~~11~~ | ~~Mini-pak I/O tesztek~~ | **kész** |
| ~~12~~ | ~~Vehicles/Trailers async~~ | **kész** |
| ~~13~~ | ~~WorkspaceConfigStore lock/corrupt~~ | **kész** |
| ~~6~~ | ~~Parts `PartPakPipeline` + ReadEntryUtf8~~ | **kész** |
| ~~7~~ | ~~`TruckDiffLockXml` + `VehicleGameDataXml`~~ | **kész** |
| ~~8~~ | ~~`PartsTuningUiHelpers`~~ | **kész** |
| ~~14–16, 18–23~~ | ~~MessageBox / busy / lazy / PakFileId / ChangeLocation / crash path / GameRunning / locale tests / CI publish~~ | **kész** |
| — | Vehicles `_meta_build` exclude + README | **kész (hygiene)** |

---

## Kapcsolódó fájlok

- Termék térkép: `docs/AGENT_CONTEXT.md`  
- Változásnapló: `CHANGELOG.md` `[Unreleased]`  
- Tesztek: `tests/SnowRunnerTuningShop.Tests/`  
- CI: `.github/workflows/ci.yml`, `release.yml`

*Ez a dokumentum snapshot; frissítsd, ha a P0 tételek megvannak, vagy az architektúra változik.*
