# HDB resale native fixture explorer

Milestone 4: a runnable native slice with a reproducible bounded coverage study
and automated native interaction gate, verified on macOS
arm64. This is the C#/Qt sibling of the
[web HDB resale visualizer](https://github.com/shenghaoc/hdb-resale-visualizer);
the web project is a product and behavior reference, not a translated frontend.

A small native C# / Qt Quick vertical slice. Six checked-in **real HDB** resale
transactions cover three towns, three flat types and four registration months.
All six have corroborated address matches and derived **BlockApproximation**
points from official HDB building footprints. Identity match quality and
coordinate quality are separate; neither establishes an exact flat location. See [data provenance and derivation](data/README.md).

C# owns the transactions, combined town/budget filtering, selection, summaries,
and the Qt item model. QML owns the native window, controls, marker presentation,
and map navigation. Changing a filter resets the bridge model, so both
`MapItemView` markers and the transaction list update. Selection is retained
while visible and cleared when its transaction is filtered out.

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
filter/selection methods called by QML. Begin/end reset notifications surround
C# model changes. `Resales.mapPoints` is a second bridge model containing only
located visible transactions; the sidebar retains all visible accepted rows.
The model-valued property follows the official ColorPalette example.
No business filtering or selection logic lives in QML JS;
its JavaScript only forwards UI commands and manages map gestures/presentation.

## Map provider

The GeoServices plugin is still **osm**. It uses Qt's documented custom-map mode
with `https://tile.openstreetmap.org/`, visible OSM contributor attribution, an
identifying `HdbResaleQt/0.1` user agent, default Qt tile caching, and
`NoPrefetching`. Only normal interactive viewing is supported; there is no bulk
or offline downloader. Internet access is needed for uncached tiles. The public
OSM tile service is best-effort and subject to its usage policy; choose an
appropriate provider before a wider production rollout.

The default Qt remote provider returned Thunderforest tiles with an API-key
watermark during verification. Explicit provider configuration fixes that;
this is a provider choice, **not an upstream Qt bug**. Map attribution stays
visible at the bottom. Drag includes movement crossing the activation threshold,
so a single-move automation gesture can pan as well as normal pointer movement.

## Verification

See [docs/verification.md](docs/verification.md) for executed checks and limits.
Supported RID package selection from the template remains for Windows x64/arm64,
Linux x64, and macOS x64/arm64. Other desktop targets require their matching Qt
installation and toolchain; Linux/Windows have not been built or UI-tested here.
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
is claimed. No full-history pipeline, online geocoder or database exists.

The [bounded coverage study](docs/coverage/README.md) runs 416 stratified real
transactions through unchanged M3 matching: 22 ExactAddress, 48
NormalizedAddress, 1 Ambiguous and 345 Unmatched; 70 approximate points.
This is **ACRA B-only coverage under current conservative normalization**, not
general matcher accuracy. Missing corroboration does not prove wrong addresses.
Full source/derived hashes, every failure and town/time cross-tabs are retained;
all failure source references were mechanically checked, with detailed manual
review of 24 fixed-ranked missing representatives plus the one postal conflict.
Normal startup still loads only the original six-row fixture.

Next milestone: investigate an authoritative address/postal source and narrowly
audited spelling rules against the fixed sample, preserving conflict handling
and reporting before/after results. Linux/Windows validation and packaged
distribution remain separate work.

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
The gate is currently verified on macOS arm64 only, inspired by the sibling
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
- [OSM tile usage policy](https://operations.osmfoundation.org/policies/tiles/)
- [aqtinstall](https://aqtinstall.readthedocs.io/)

The Bridge README's linked snapshot documentation returned HTTP 404 during
setup; API declarations in the official C# source and official examples were
consulted instead. No APIs were inferred from the Rust/Python bridges.
