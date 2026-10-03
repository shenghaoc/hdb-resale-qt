# Milestone 3 verification — 2026-10-03

Executed on the unchanged macOS arm64 toolchain in README. Branch
`milestone-3-address-matching`, based on clean M2 SHA
`48bcb75143abf24b174eb2e0e2c5dc9afbee6a0a`. No push or PR.

From repository root with toolchain PATH and QtDir exports:

```sh
dotnet build
dotnet test --no-build
dotnet run --project src/HdbResale.App/HdbResale.App.csproj --no-build
```

Build: **0 warnings, 0 errors**. Tests: **25 passed, 0 failed, 0 skipped**.
Tests pin the six direct official resale/property matches, every expected
postal/assertion count and original footprint IDs, source provenance and
polygon midpoints; preserve the original M2 point; check conservative aliases
and distinct similar addresses; reject postal conflicts/multiple candidates
without a winner; retain unmatched/ambiguous transactions; distinguish matched
identity from missing coordinates. Parser regressions cover quoted fields,
decimal prices, invalid month/price/required fields, malformed quoting/header,
field count, duplicate IDs, file IO, malformed GeoJSON/geometry and invalid
postals, with readable valid records surviving. C# state tests exercise combined
inclusive town/budget filters, clearing hidden selection, retaining visible
selection/reset, empty results and invalid-budget integrity.

Final offline audit verified all four full-source hashes, the six unchanged
resale rows, 76 property row references, all 30 ACRA assertions with actual CSV
line numbers, and six original polygon feature objects. Each canonical address
also has one property record and one block/postal footprint in the full source,
not merely in the bounded extract.

Actual native desktop accessibility actions and screenshots verified:

- Singapore OSM basemap and attribution render. Canonical import: **6 accepted,
  6 matched (NormalizedAddress), 0 ambiguous/unmatched/rejected/diagnostics**;
  six rows and six map delegates, with close pairs overlapping at city zoom.
- Selecting 509 shows its matching HDB property row, all five ACRA B source
  rows, OBJECTID 937499 / ENTITYID 7861 / postal 560509, independent qualities
  and approximate-point/corroboration wording.
- ANG MO KIO gives 2 visible / 2 mapped. S$238,000 gives 1 visible / 1 mapped
  and clears selected 509. Selecting remaining 510 displays property row 8652,
  ACRA row 49610, OBJECTID 946126 / ENTITYID 4194 / postal 560510.
- Reset restores 6 mapped and retains visible 510. Budget zero gives empty
  map/list, no-matches message and cleared selection; reset restores controls.
- Drag changes center 1.3521,103.8198 → 1.4211,103.7508. Singapore recenters;
  plus/minus change zoom 11 → 12 → 11. Wheel zoom reaches 14.3 with anchored
  center 1.3191,103.7720; screenshot shows two separated Clementi pins and
  resolved OSM tiles. No arbitrary sleeps/retry loops were used.
- Controlled error run changed **only ignored built-bundle copies**: duplicate
  509 footprint, null 510 geometry, remove 461 postal assertions, append one
  invalid-price transaction. UI showed **6 accepted, 4 matched, 1 ambiguous,
  1 unmatched, 1 rejected, 1 diagnostic; 3 mapped, 3 unlocated** and the visible
  transactions.csv:8 price error. Selectable 509 shows Ambiguous / Missing and
  no winner; 510 shows NormalizedAddress / Missing plus the original footprint
  identity; 461 shows Unmatched / Missing and lack of ACRA corroboration.
  Screenshots show rendered basemap and selected unlocated row with no
  placeholder marker. Stale ignored M2 locations.csv was present during this
  check but supplied no fallback. Canonical copies were restored and
  byte-compared to source before final build/test/normal launch.
- Canonical and diagnostic runtime stdout/stderr logs were empty. Source data
  remained unchanged by controlled UI testing. Final normal run restored the
  canonical zero-diagnostic six-point state.

Limitations: the official ACRA register supplies corroborating registered
addresses, not authoritative HDB IDs or historical identity certainty. All
assertions in this one pinned B extract were retained; expanding coverage needs
fresh scrutiny of conflicts/coverage. Approximate bbox points may be exterior
and never locate a flat. Physical pinch, Linux/Windows and packaged distribution
remain untested. Qt Location 6.12 is deliberate Technology Preview; Bridge
0.4.0.22-beta uses the matching external Qt installation. No credible upstream
Qt Bridge/Location defect was found or externally reported. No dependency
versions changed for M3.

---

## Historical Milestone 2 and Milestone 1 verification

# Milestone 2 verification — 2026-10-03

Executed on the same macOS arm64 toolchain recorded in README. Local branch:
`milestone-2-data-pipeline`, based on published `42f1d24`. No push or PR.

From repository root with toolchain PATH and QtDir exports:

```sh
dotnet build
dotnet test --no-build
dotnet run --project src/HdbResale.App/HdbResale.App.csproj --no-build
```

Build: **0 warnings, 0 errors**. Tests: **24 passed, 0 failed, 0 skipped**.
Coverage includes real fixture acceptance, decimal price/quoted CSV, invalid
month, malformed/wrong-count fields, required values, bad/nonpositive prices,
duplicate ID, coordinate bounds/nonfinite values, missing/unknown quality,
source consistency, missing files/structural headers, preservation of valid
rows, imported filtering, inclusive budget, selection clearing/reset, and the
five pinned official footprint/code/quality/derived-point matches.

Desktop verification used the actual native app through accessibility actions
and screenshots, not launch alone:

- Initial Singapore OSM basemap renders with attribution and linked licence.
  C# import summary: 6 accepted, 0 rejected, 0 diagnostics. Six sidebar rows;
  five map delegates (close points overlap at city zoom) and one missing row.
- Mapped selection shows orange pin and selected list row, registration month,
  local ID, BlockApproximation and footprint OBJECTID/code/postal/inferred join.
- Town ANG MO KIO: 2 visible, 1 mapped, 1 unlocated. Budget S$238,000: 1 visible,
  0 mapped, 1 unlocated; previously mapped selection clears. Select HDB-1188
  from sidebar: Missing/reason displayed, no map marker or placeholder.
- Reset: 6 visible, 5 mapped, 1 unlocated; still-visible selection retained.
  Budget zero: 0 visible/map rows and empty message; selection clears. Reset
  restores all records and controls.
- Drag changes center from 1.3521,103.8198 to 1.4211,103.7508. Singapore button
  restores initial center/zoom. Plus/minus: 11 → 12 → 11. Wheel zoom: 11 → 14.3,
  anchored center 1.3191,103.7720. Clementi points visibly separate; selecting
  449 CLEMENTI AVE 3 shows OBJECTID 942992 and orange marker. Tiles resolve after
  ordinary asynchronous loading; no retry/sleep loop.
- Controlled error-path check modified **only ignored built-bundle copies**:
  missing locations.csv plus one appended invalid-price transaction. UI stays
  running and shows 6 accepted, 1 rejected, 2 diagnostics; 0 mapped, 6 unlocated.
  File IO and transactions.csv row 8 price messages are visible. Both copies
  were restored and byte-compared with checked-in CSVs before final normal run.
- Normal and diagnostic runtime stdout/stderr logs were empty. Source data
  stayed untouched by the error-path test.

Limits: inferred street-code crosswalk is supported by unique complete block-set
agreement, not an authoritative mapping; bounding-box midpoint may be outside
an irregular footprint and never identifies a flat. Current polygons do not
prove historical exact locations. Missing fixture entry is intentional, not a
claim about official coverage. No live data/geocoding path or database.
Physical multi-touch pinch and Linux/Windows remain untested. Basemap needs
uncached network tiles. No credible upstream Bridge/Location defect was found;
ordinary namespace/import/data-source corrections were application work. Qt
Location remains deliberate 6.12 Technology Preview; Bridge is beta and tied
to the external matching Qt installation. The macOS bundle is development-only,
not packaged/signed/notarized. No dependency versions were changed for M2.

---

## Historical Milestone 1 verification

# Verification — 2026-10-03

Executed on macOS 27.0.1 arm64 with the exact toolchain in README.
Target checkout was initially an empty directory with no Git repository or
repository-specific instructions. The existing web repository was read only as
a product reference and was not edited.

## Build and domain tests

```sh
export PATH="$HOME/.local/share/hdb-qt-toolchain/bin:$PATH"
export QtDir="$HOME/Qt/6.12.0/macos"
dotnet build
dotnet test tests/HdbResale.Tests/HdbResale.Tests.csproj --no-build \
  --logger 'console;verbosity=normal'
dotnet run --project src/HdbResale.App/HdbResale.App.csproj --no-build
```

- Build passed: **0 warnings, 0 errors** in the final application build.
- **2 tests passed**, 0 failed: combined inclusive town/budget filtering,
  hidden-selection clearing, rejecting hidden selections, preserving visible
  selections, reset, empty results, and invalid-budget state integrity.
- `Qt6Config.cmake` exists under the selected Qt prefix.
- `qmake -query QT_VERSION` reports **6.12.0**.
- `file plugins/geoservices/libqtgeoservices_osm.dylib` confirms x86_64 and arm64.
- Native bridge C++ output compiles; genuine C# roles/notifications reach QML.

## Observed native UI checks

Used the desktop UI tools on the actual running `HdbResale.app`, including
screenshots and accessibility-tree reads after interactions. Process launch
alone was not used as rendering evidence.

| Check | Observed result |
|---|---|
| Initial map | Singapore OSM basemap rendered; six blue markers and six C# rows; visible attribution |
| Marker tap | Selection displayed TP-2, 201 Tampines Street 21, 3 ROOM, S$420,000; marker turned orange and matching row highlighted |
| List selection | Selecting TP-1 row displayed its address, 4 ROOM, S$580,000 |
| Town | Tampines reduced model and map to two points |
| Combined filter | Tampines + S$450,000 reduced map/list to TP-2 only |
| Hidden selection | Selected TP-1 cleared when applying S$450,000 |
| Accessible marker | Native accessibility activation of remaining marker selected TP-2 |
| Empty result | S$0 yielded zero markers/rows, cleared selection, and showed no-matches text |
| Reset | Restored All towns, S$1,000,000, and six rows/markers |
| Pan | Drag changed center from 1.3521, 103.8198 to 1.4554, 103.6821; tiles and markers moved together |
| Recenter | Singapore button restored center 1.3521, 103.8198 and zoom 11 |
| Zoom buttons | Zoom in/out changed 11 → 12 → 11 with basemap/markers scaling |
| Wheel | Final scroll changed zoom 11.0 → 12.7, with anchored center 1.4005, 103.7882 |

Screenshots of the basemap, one-point combined filter/selection, pan, and zoom
were captured in the task's desktop-tool output. No separate native Library
attachment was created; this did not gate implementation.

## Ordinary issues resolved and remaining limitations

- Fixed QML map/sidebar sizing revealed by the first screenshot.
- Configured the documented custom OSM HTTPS endpoint because the default Qt
  provider returned key-watermarked Thunderforest tiles. This is provider
  configuration, not a Qt defect.
- Drag trace showed active/inactive transitions but no translation update for
  the automation's single-move gesture. Handling initial threshold-crossing
  movement made it pan correctly. No upstream defect was inferred.
- Fixed ordinary app use of `WheelEvent.position`: QML WheelEvent supplies
  `x`/`y`. Verified wheel zoom after correction, with gentler sensitivity and
  OSM tile zoom capped at 19. No runtime errors remained in the final check log.
- Qt Bridge's template emits an unbundled macOS host. A minimal MSBuild staging
  target gives `dotnet run` a discoverable development `.app` identity. Qt's
  native deployment step reports it skips self-contained deployment for the
  original non-bundle target; the staging target intentionally uses installed
  Qt frameworks. Signing/notarization/distribution remain outside this slice.
- Fresh native configuration may print Qt's private-Core-header compatibility
  warning. It was not suppressed. Keep the same Qt build for compile and run.
- Bridge's README linked snapshot documentation returned HTTP 404. Official
  template output, C# source API declarations, and examples were inspected.
- Qt Location 6.12 is deliberately Technology Preview; Bridge is 0.4 beta.
- Public OSM tiles require Internet for cache misses and offer no SLA. Default
  caching remains enabled; prefetch is disabled and no bulk download exists.
- Trackpad pinch is implemented using documented Qt gesture APIs but was not
  separately synthesized; wheel, buttons, and pointer pan were exercised.
- Linux/Windows builds and UI, production datasets, offline maps, and packaged
  distribution were not tested. No credible upstream defect was found, and no
  external bug report was filed.
