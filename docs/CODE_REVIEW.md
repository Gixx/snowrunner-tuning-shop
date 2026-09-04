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

### 6. Parts szolgáltatások másolása (Engine / Gearbox / Suspension / Tires / Winch)
**Hol:** `Core/{Engine,Gearbox,Suspension,Tires,Winch}/*Service.cs`  
**Miért:** Ugyanaz a pipeline (zip scan → regex → UsedBy → baseline multipliers → replace) százsoronként. Winch ráadásul `XDocument` (whitespace churn), a többi regex.  
**Javaslat:** Közös „part set” helper (`PartXmlHelpers` bővítése); egy XML stratégia.

### 7. Truck / Trailer God class
**Hol:** `TruckTuningService.cs` (~1.2k), `TrailerTuningService.cs` (~1k)  
**Miért:** Parse + global apply + store unlock + hitch/quest egy static típusban. Nehéz teszteni és review-zni.  
**Javaslat:** Szétválasztás: Load/Parse | Mutate | Persist; hitch/quest külön típus.

### 8. UI code-behind klónok
**Hol:** öt `*TuningView` (~400–520 sor), `VehiclesView` (~1100), `TrailersView` (~860)  
**Miért:** Write-gate, MessageBox, filter, row VM ismétlődik; könnyű elcsúszni (lásd `TryProceed(null)`).  
**Javaslat:** Nem kötelező teljes MVVM; elég közös `TuningGridController` / write-gate helper + session injection a Parts gyermekekbe.

### 9. `PakWriteUi.TryProceed(null)` a Parts tabokon — **kész (P0)**
Session átadás a PartsView-ból; lásd fent.
### 10. Zip entry név: Ordinal vs OrdinalIgnoreCase
**Hol:** `InitialPakWriter.ReadPakEntryNames` vs `PakRawZipReplacer`  
**Miért:** Ignore-case osztályozás után Ordinal lookup → „entry not found” vagy silent miss.  
**Javaslat:** Egy normalizálás + egy összehasonlító végig.

### 11. Pak I/O és restore tesztek hiánya
**Hol:** `tests/SnowRunnerTuningShop.Tests` (jelenleg locale + PakFileId + hitch)  
**Miért:** A legveszélyesebb kód (raw zip, in-place patch, baseline restore, process guard) nincs lefedve.  
**Javaslat:** Fixture mini-pak → ReplaceEntries round-trip; restore temp+move; hitch undo round-trip.

### 12. Vehicles/Trailers szinkron load a UI szálon
**Hol:** `VehiclesView` / `TrailersView` `Ensure*Loaded`  
**Miért:** Nagy pak → UI fagyás detail megnyitáskor. Parts már `Task.Run`.  
**Javaslat:** Ugyanaz az async + overlay minta.

### 13. `WorkspaceConfigStore` race + silent corrupt
**Hol:** `Core/Config/WorkspaceConfigStore.cs`  
**Miért:** Load-mutate-save lock nélkül; hibás JSON → üres config, user jelzés nélkül.  
**Javaslat:** Egyszerű lock; corrupt esetén backup + UI figyelmeztetés.

---

## P2 — közepes (UX, memória, éles élek)

### 14. MessageBox túlterhelés
Siker + hiba gyakran status **és** modal. Parts `StatusChanged` részben dead.  
**Javaslat:** Siker → status/banner; modal csak confirm + valódi error.

### 15. Írás közben nincs busy-disable
Hosszú Apply alatt újabb kattintás lehetséges.  
**Javaslat:** Cursor + gombok disable a művelet végéig.

### 16. Eager BitmapImage a katalógusokban
**Hol:** `VehicleCatalog` / `TrailerCatalog` + view LoadCatalog  
**Javaslat:** Lazy / virtualizálás; flag cache mintája a thumbökre is.

### 17. `assets/vehicles/_meta_build` bemegy az outputba
**Hol:** `SnowRunnerTuningShop.csproj` (`vehicles/**\*`)  
Trailers kizárja a `_meta_build`-et; vehicles nem.  
**Javaslat:** Exclude `assets\vehicles\_meta_build\**`.

### 18. `PakFileId` fuzzy suffix/prefix
Rövid közös végződés → rossz truck. Collisionnél `list[0]`.  
**Javaslat:** Catalogban kötelező egyedi `pakId`; fuzzy csak fallback + log; tesztek ambiguus id-kre.

### 19. Change location / első baseline tuned pakból
**Hol:** `PakBaselineService.ChangeLocation`  
Tuned fájl = „vanilla” baseline az editionnek.  
**Javaslat:** Erős UI figyelmeztetés + opcionális fingerprint/marker check.

### 20. Crash report tartalmazza a teljes pak pathot
**Hol:** `CrashReportBuilder`  
Privacy / support.  
**Javaslat:** Path truncálás vagy „…/SnowRunner/…/initial.pak” normalizálás a megosztott szövegben.

### 21. Photo Mode / General reload minden `GameRunningChanged`-re
Folyamat indul → slider állapot elveszhet.  
**Javaslat:** Csak a write gombok frissítése, ne teljes ReloadFromPak.

### 22. Locale tesztek csak en + de + zh-CN
Többi bundled locale nincs kulcs-mérve.  
**Javaslat:** Theory az összes shipped `*.json`-ra (hiány OK fallbackkal; parse kötelező).

### 23. CI nem buildeli az installert / nem publish smoke
**Hol:** `ci.yml` vs `release.yml`  
**Javaslat:** Legalább Inno compile dry-run vagy `dotnet publish` win-x64 CI-n (opcionális job).

### 24. README drift
Vehicles még „planned”; Trailers / General / tests / close-game hiányzik.  
**Javaslat:** README szinkron AGENT_CONTEXT / CHANGELOG szerint.

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
| ~~1~~ | ~~Atomikus full restore~~ | **kész** |
| ~~2~~ | ~~Marker remove a safe writer útvonalon~~ | **kész** |
| ~~3~~ | ~~Parts load cancellation~~ | **kész** |
| ~~4~~ | ~~`TryProceed(_session)` Parts-ben~~ | **kész** |
| ~~5~~ | ~~Release: test + AppInfo↔tag~~ | **kész** |
| ~~7~~ | ~~RemoveSupplementalStoreHitch szigorítás~~ | **kész** |
| 6 | Mini-pak I/O + hitch undo tesztek (bővebb I/O) | P1 regresszió |
| 8 | Entry-name casing egységesítés | P1 |
| 9 | Vehicles `_meta_build` exclude + README | P2 hygiene |
| 10 | Közös tuning write-gate / partial extract | P1 karbantartás |

---

## Kapcsolódó fájlok

- Termék térkép: `docs/AGENT_CONTEXT.md`  
- Változásnapló: `CHANGELOG.md` `[Unreleased]`  
- Tesztek: `tests/SnowRunnerTuningShop.Tests/`  
- CI: `.github/workflows/ci.yml`, `release.yml`

*Ez a dokumentum snapshot; frissítsd, ha a P0 tételek megvannak, vagy az architektúra változik.*
