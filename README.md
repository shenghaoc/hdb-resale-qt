# HDB resale native fixture explorer

Milestone 9 adds a complete full-corpus failure inventory, dataset-qualified
public ACRA address evidence, bounded historical first-hit assertions and narrowly
validated MultiPolygon support. [Coverage, source provenance and audit](docs/address-coverage/README.md)
separate public gains, weaker historical gains and new conflicts. The original
six-row default and frozen studies remain unchanged.

Milestone 7 introduced the retained OneMap raster basemap, visible official attribution, compact zoom-aware
markers and stable-address incremental map updates. The full-corpus Linux x64
measurements and verification are in [M7 map results](docs/map/verification.md).
Earlier milestones provide the reproducible coverage study, historical postal
experiment and native macOS arm64/Linux x64 checkpoints. M7 does not claim a new
macOS or Windows UI verification. This is the C#/Qt sibling of the
[web HDB resale visualizer](https://github.com/shenghaoc/hdb-resale-visualizer);
the web project is a product and behavior reference, not a translated frontend.

A small native C# / Qt Quick vertical slice. Six checked-in **real HDB** resale
transactions cover three towns, three flat types and four registration months.
All six have corroborated address matches and derived **BlockApproximation**
points from official HDB building footprints. Identity match quality and
coordinate quality are separate; neither establishes an exact flat location. See [data provenance and derivation](data/README.md).

C# owns the transactions, combined town/budget filtering, selection, summaries,
and the Qt item model. QML owns the native window, controls, marker presentation,
and map navigation. Filter changes update `MapItemView` by stable address key:
remove missing rows, insert new rows and notify changed roles while retaining
surviving delegates. The independently virtualized transaction list still uses
its reset notifications. Selection is retained
while visible and cleared when its transaction is filtered out.

Milestone5 local work adds a verified [historical first-hit cache experiment](docs/coverage/onemap/historical/README.md) on the unchanged benchmark. Its410experimental HDB-footprint points depend on explicitly weaker cached postal assertions, not exhaustive candidate evidence. Fresh Search tooling is separate. Only a minimized benchmark projection is included; the full export and display names remain private.

## Verified toolchain (macOS arm64)

- .NET SDK **10.0.401**, `net10.0`, runtime **10.0.12**; Microsoft macOS arm64 SDK.
- Qt **6.12.0**, macOS `clang_64` universal x86_64/arm64 binaries,
  installed under `$HOME/Qt/6.12.0/macos`.
- Qt Bridge `QtGroup.Qt.Bridge.CSharp.osx-arm64` **0.4.0.22-beta**.
- Official Bridge template `QtGroup.Qt.Bridge.CSharp.Templates` **0.4.0-beta**.
- aqtinstall **3.3.0**, Python **3.12.14**, Ninja Python package **1.13.2**
  (`ninja --version`: `1.13.2.git.kitware.jobserver-pipe-1`).
- CMake **4.4.3** and Xcode **27.0** / AppleClang **21.0.0.21000334**.
- Verified host: macOS **27.0.1**, Apple Silicon.
- Test dependencies: Microsoft.NET.Test.Sdk **17.14.1**, xUnit **2.9.3**,
  xunit.runner.visualstudio **3.1.4**.

Qt Bridge on macOS requires an external Qt installation. Install Xcode and its
command-line tools, CMake, and the Microsoft .NET 10 arm64 SDK first. The SDK
version is pinned in `global.json`; no .NET 11 prerelease is used.

### Qt provisioning with aqtinstall

This setup used `uv` and an isolated user-local virtual environment. Existing
Qt installations are preserved. Package installs require Internet access.

```sh
uv venv --python 3.12.14 "$HOME/.local/share/hdb-qt-toolchain"
uv pip install --python "$HOME/.local/share/hdb-qt-toolchain/bin/python" \
  aqtinstall==3.3.0 ninja==1.13.2
export PATH="$HOME/.local/share/hdb-qt-toolchain/bin:$PATH"

aqt list-qt mac desktop --spec '6.12'
aqt list-qt mac desktop --arch 6.12.0
aqt list-qt mac desktop --modules 6.12.0 clang_64
# Actual results: 6.12.0; clang_64; qtlocation, qtpositioning, qtshadertools available.
aqt install-qt mac desktop 6.12.0 clang_64 -O "$HOME/Qt" \
  -m qtlocation qtpositioning qtshadertools

export QtDir="$HOME/Qt/6.12.0/macos"
test -f "$QtDir/lib/cmake/Qt6/Qt6Config.cmake"
test -f "$QtDir/plugins/geoservices/libqtgeoservices_osm.dylib"
file "$QtDir/plugins/geoservices/libqtgeoservices_osm.dylib"
"$QtDir/bin/qmake" -query QT_VERSION
```

The base Qt archive includes Qt Quick, QML, Quick Controls, SVG, and tools.
Location/Positioning are additional queried modules; the OSM GeoServices plugin
was verified as a universal binary containing arm64. Qt Location 6.12 is a
**Technology Preview**, deliberately used here.

The project was based on inspected official template output:

```sh
dotnet new install QtGroup.Qt.Bridge.CSharp.Templates@0.4.0-beta
dotnet new qt -n BridgeProbe --SampleCode --Framework net10.0
```

These commands are provenance; do not generate another template over this
checkout. Its wildcard runtime version was replaced with an exact package pin.
Qt and Bridge have open-source/commercial license terms; the Bridge package
license identifies Qt Commercial or LGPL-3.0-only. No Qt Online Installer,
account sign-in, or interactive agreement acceptance was used.

## Build, test, run

From the repository root in each new terminal:

```sh
export PATH="$HOME/.local/share/hdb-qt-toolchain/bin:$PATH"
export QtDir="$HOME/Qt/6.12.0/macos"
dotnet build
dotnet test --no-build
dotnet run --project src/HdbResale.App/HdbResale.App.csproj --no-build
```

Alternatively pass `-p:QtDir="$HOME/Qt/6.12.0/macos"` to build/run. Ninja and
CMake must be on PATH. Close the window to exit. Run without `--no-build` when
changing source. The small macOS-only MSBuild target stages build output into
`src/HdbResale.App/obj/Debug/net10.0/HdbResale.app`; `dotnet run` launches its
native bridge host. This development bundle provides macOS app identity and
accessibility discovery. It uses the installed Qt frameworks and is **not** a
self-contained, signed, notarized distribution. Generated bridge code and bundle
output stay under ignored `obj`/`bin` directories.

## Structure

```text
src/HdbResale.Domain/  immutable facts/match/location records, CSV/GeoJSON importer, ExplorerState
src/HdbResale.App/     official-template csproj, Program, ResaleMapModel, LocatedMapModel, Main.qml, Info.plist
tests/HdbResale.Tests/ import/domain/evidence tests, independent of Qt
data/                 six-row correctness fixture and official evidence
docs/coverage/        separate 416-row study, source/hash pins, report and audit
tools/                offline sample/audit scripts and bounded native gate
```

The bridge singleton `Resales` derives from the documented `Qt.Bridge.Models.Model`,
exposes custom model roles and `INotifyPropertyChanged` properties, and handles
filter/selection methods called by QML. Matching begin/end notifications surround
structural C# model changes; map values use role-specific `DataChanged`. `Resales.mapPoints` is a second bridge model containing only
located visible address summaries; the sidebar retains all visible accepted transactions.
The model-valued property follows the official ColorPalette example.
No business filtering or selection logic lives in QML JS;
its JavaScript only forwards UI commands and manages map gestures/presentation.

## Map provider

The GeoServices plugin remains **osm**, using its documented CustomMap with
`https://www.onemap.gov.sg/maps/tiles/Default/` (standard 256px XYZ, zoom 11–19).
The visible official logo and linked “OneMap © contributors | Singapore Land
Authority” attribution remain at the bottom. OneMap map services do not require
a Search token. Provider lookup and tile prefetching are disabled; an identifying
user agent and separate `onemap-default-v1` Qt cache are used. Existing caches
are preserved. Internet access is needed for uncached tiles. No proxy, Search
acquisition, bulk downloader or offline tile bundle is included.
The unchanged official logo is bundled locally, so attribution does not depend
on a separate image request. See [provider contract, terms and model strategy](docs/map/README.md).

At zoom 11–12, all addresses remain as 14px dots without ordinary count labels;
at zoom 13+ they use 24px/count markers. Selection is always orange, raised and
28px, retaining its multi-transaction count label. This reduces low-zoom overlap without culling or clustering.
Drag includes movement crossing the activation threshold, so a single-move
automation gesture can pan as well as normal pointer movement.

## Verification

See [M7 verification](docs/map/verification.md) for current checks and limits;
[earlier verification](docs/verification.md) preserves the historical checkpoints.
Supported RID package selection from the template remains for Windows x64/arm64,
Linux x64, and macOS x64/arm64. Other desktop targets require their matching Qt
installation and toolchain; Linux x64 is additionally verified; see [Linux setup and results](docs/linux.md).
Windows has not been built or UI-tested here.
Keep build/run on the same Qt installation: the bridge uses Qt private headers
and fresh native configuration warns about coupling to that Qt build.

## Local import and diagnostics

At startup C# reads three small CSVs (`transactions.csv`, `address-evidence.csv`,
`postal-address-evidence.csv`) and `building-evidence.geojson` relative to
`AppContext.BaseDirectory`. MSBuild copies them into output and the development
bundle. No data fetching or geocoding occurs at runtime. Only the basemap needs
network access. See [data/README.md](data/README.md) for pinned source hashes,
row provenance, the exact linkage and normalization rules.

The Qt-free domain separates official facts from identity evidence
(`ExactAddress`, `NormalizedAddress`, `Ambiguous`, `Unmatched`) and coordinate
quality (`Missing`, `BlockApproximation`). Matching requires a unique explicit
HDB property block/street, agreeing official ACRA address/postal assertions,
and a unique HDB footprint on **both block and postal code**. Every agreeing
ACRA source row is retained; corporate entities are assertions, not building
candidates. Conflicting postals or multiple property/footprint records are
ambiguous with no winner. Unmatched and ambiguous valid transactions remain
filterable/selectable; they do not receive placeholder coordinates.

Only case/outer-space normalization, street whitespace collapsing and the
observed road aliases `AVE → AVENUE` / `CTRL → CENTRAL` immediately before a
numeric final suffix are allowed. No punctuation stripping, block-suffix
collapse, number rewriting, fuzzy matching or street-code inference occurs.
The retired M2 crosswalk and point table live in
[docs/milestone-2](docs/milestone-2/README.md); they are neither app inputs nor
fallbacks. C# computes approximate points from the retained original polygons.

The importer uses BCL `TextFieldParser` and `System.Text.Json`; no import or
spatial dependency was added. Structured results retain accepted transactions,
rejected records and file/row diagnostics. Invalid input rows are rejected
while readable valid rows survive. Explicit null geometry preserves a matched
identity with missing coordinates. Malformed geometry is rejected with a
feature-number diagnostic. Missing files, IO errors, malformed headers and
quoting are visible diagnostics.

The UI shows import and matching counts, mapped/unlocated counts, diagnostics,
registration month, local ID, both qualities and HDB/ACRA source references.
The canonical fixture produces **6 accepted, 6 matched (NormalizedAddress),
0 ambiguous, 0 unmatched, 0 rejected, 0 diagnostics; 6 mapped, 0 unlocated**.
The six original resale rows are unchanged. The formerly omitted block 510 now
uses its legitimate official polygon, not a manufactured missing case. Missing,
ambiguous and unmatched paths are covered by unit tests and controlled native
checks using only ignored bundle copies. The initial budget is S$1,000,000;
prices range from S$238,000 to S$620,000. Hidden selection clears and visible
selection survives reset.

ACRA registered addresses are official-record **corroboration**, not an
authoritative HDB identifier or proof of a historical transaction's building.
Local transaction IDs and CSV source rows identify pinned snapshots only.
Registration month is not an exact sale date. Polygon bounding-box midpoints
may fall outside irregular shapes and never locate a flat. Current registered
addresses/buildings can change; no historical certainty or full-data coverage
is claimed. The full-corpus path is an explicit offline experiment; no online geocoder or database exists.

The [bounded coverage study](docs/coverage/README.md) runs 416 stratified real
transactions through unchanged M3 matching: 22 ExactAddress, 48
NormalizedAddress, 1 Ambiguous and 345 Unmatched; 70 approximate points.
This is **ACRA B-only coverage under current conservative normalization**, not
general matcher accuracy. Missing corroboration does not prove wrong addresses.
Full source/derived hashes, every failure and town/time cross-tabs are retained;
all failure source references were mechanically checked, with detailed manual
review of 24 fixed-ranked missing representatives plus the one postal conflict.
Normal startup still loads only the original six-row fixture.

Milestone 6 measures the pinned 241,920-row corpus with unchanged conservative
matching and a C# address-marker projection. See [scale results and reproduction](docs/scale/README.md)
for cardinalities, stage timings, memory, native gates, the explicit offline data-directory
override and limitations. Default startup remains the six-row fixture. M7 separates
aggregation, notification and QML lifecycle/readiness observations and retains
Qt Location with smaller incremental updates. Transient loading memory, Windows
GUI validation and packaged distribution remain separate work.
[Linux verification](docs/linux.md) records the additional platform-specific Bridge pin.
The [bounded MapLibre spike](docs/map/maplibre-spike.md) includes small independent
experiment sources and a verified binary-loader blocker. It does not establish
rendering/performance or add a production dependency. Keep Qt Location for the
next milestone unless richer-layer requirements justify exact-Qt-6.12 plugin
qualification and a like-for-like real-data comparison; M8 has not started.

## Native runtime gate

With README's PATH and QtDir exports, execute Debug and Release checks:

```sh
dotnet build
dotnet test
dotnet build -c Release
dotnet test -c Release
python3 -m unittest discover -s tools -p 'test_*.py' -v
python3 tools/native_gate.py \
  --executable src/HdbResale.App/obj/Release/net10.0/HdbResale.app/Contents/MacOS/HdbResale.App \
  --log /tmp/hdb-release-gate.log
# Substitute Debug for Release to run the same gate against Debug.
# Expected negative self-check: exits 1, never reports acceptance.
HDB_GATE_FAULT=skip-empty python3 tools/native_gate.py \
  --executable src/HdbResale.App/obj/Release/net10.0/HdbResale.app/Contents/MacOS/HdbResale.App \
  --log /tmp/hdb-negative-gate.log
```

The standard-library Python harness opts into `HDB_RUNTIME_GATE=1` only for its
child process. A separate QML test component observes the actual C# bridge
properties, bound controls, ListView row count and Map mapItems after each
mutation. It requires mapReady/a usable map shell, town filter, selection,
hidden-selection clearing, empty results, reset/repopulation, zoom, pan,
recenter, ordered completion and clean native/C# shutdown. State deadlines are
5 seconds per phase; process/shutdown deadline is 25 seconds. Qt/QML error
signatures, nonzero native exits, missing/reordered steps and missing teardown
fail the command with its full log. No sleep/retry loop or pixel framework exists.
The explicit negative fault skips one test mutation and must time out at phase
5. The test component is inactive without the opt-in environment variable;
normal Release behavior/data are unchanged.

This is automated **QML-hook/model acceptance**, not evidence of physical input
or successful network tile rendering. The gate does not certify tile pixels.
Real Cocoa desktop inputs and screenshots were separately exercised, including
rendered OSM, selected orange pin, wheel/drag, unlocated selection and visible
startup diagnostics; details and limits are in [verification](docs/verification.md).
Those historical gates were verified on macOS arm64 and Linux x64; the current
M7 code is verified on Linux x64. The gate is inspired by the sibling
rowplay-qt runtime gate but implemented with the C# bridge and no sibling edits.

## Development workflow

This repository uses a lightweight rapid-development workflow:

- Use a meaningful feature or milestone branch and make logical checkpoint
  commits. Push once meaningful work exists and keep the branch reasonably
  current with normal pushes; local-only work is temporary.
- After a milestone is verified, promptly update `main`. Prefer a fast-forward
  when history is naturally linear; do not rewrite history to manufacture one.
- Keep `main` buildable and usable. Updating it is not a production-release gate.
  Use a PR when CI or review is useful; a PR is optional.
- Preserve existing work and history. No force pushes without a concrete,
  explicit request; no tags or releases unless asked. Never change global Git
  configuration for this repository's workflow.

## Official references

- [Qt Bridge C# README / examples](https://code.qt.io/cgit/qt/qtbridge-csharp.git/tree/)
- [Published Bridge runtime](https://www.nuget.org/packages/QtGroup.Qt.Bridge.CSharp.osx-arm64/0.4.0.22-beta)
- [Qt 6.12 Map and gesture documentation](https://doc.qt.io/qt-6.12/qml-qtlocation-map.html)
- [Qt 6.12 OSM plugin parameters](https://doc.qt.io/qt-6.12/location-plugin-osm.html)
- [OneMap raster tiles and attribution](https://www.onemap.gov.sg/docs/maps/)
- [OneMap Terms of Use](https://www.onemap.gov.sg/legal/termsofuse.html)
- [aqtinstall](https://aqtinstall.readthedocs.io/)

The Bridge README's linked snapshot documentation returned HTTP 404 during
setup; API declarations in the official C# source and official examples were
consulted instead. No APIs were inferred from the Rust/Python bridges.

## Milestone 8: startup and transient memory

[Measured raw-import improvements](docs/startup/README.md) preserve the M7 UI,
full source/evidence contracts and in-memory domain model. Opt-in allocated-byte
profiling separates CSV, footprints, domain work and native readiness; no startup
artifact, database or production forced collection is introduced. See the
[M8 verification record](docs/startup/verification.md) for measured results.


## M9: explicit full-corpus address coverage

The [M9 report](docs/address-coverage/README.md) inventories all 9,755 addresses /
241,920 transactions before changing evidence. With unchanged conservative
normalization, full public ACRA A–Z/Others plus HDB geometry locate 2,513 addresses /
66,256 transactions; the bounded, explicitly weaker M5 first-hit projection adds
288 / 9,481. Final experimental coverage is 2,801 / 75,737, while all 166,183
unlocated transactions remain filterable/selectable.

Additional contrary evidence withholds 60 previously located addresses / 1,122
transactions; it never selects a majority, first candidate or nearest footprint.
Every changed address is mechanically audited, with all conflicts/former-match
changes and a deterministic ordinary sample manually reviewed. New sources retain
dataset IDs and every source row; malformed public postals are retained as
unresolved evidence. Full raw exports and generated full inputs stay local.

MultiPolygon support spans all validated component exterior-ring bounds, adding
one currently evidenced ACRA-B address without relaxing positive ENTITYID checks.
M5's frozen experiment explicitly retains its original geometry policy. The
[reproduction commands](docs/address-coverage/README.md#regenerating-full-data)
produce separate public-only and public-plus-historical inputs; use an explicit
HDB_DATA_DIRECTORY to open them. No source/evidence fetching occurs at startup.
