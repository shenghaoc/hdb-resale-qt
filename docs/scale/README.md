# Milestone 6: pinned full-corpus scale

The corpus is **241,920 real transactions**, distinct from the much smaller
map-ready subset. This experiment uses the same 2026-10-03 official snapshot as
M4. It neither fetches a new snapshot nor extends the private first-hit postal
cache to the full corpus. All six canonical rows, the 416-row / 378-address M4
inputs, and the minimized M5 projection remain unchanged. The historical M5
result still has 22 ExactAddress, 388 NormalizedAddress, 1 Ambiguous, 5 Unmatched,
410 BlockApproximation and 6 Missing; its report SHA remains
`d76614762c351eb121d3dd11ed03658e53daae1a5feeae36809a029b7e62d4be`.

## Reproducible input

[manifest.json](manifest.json) records source dataset IDs, URLs, retrieval date,
all four full-source hashes and all derived hashes. Resale source SHA-256 is
`3c3d8bf9b12adb88919fa74869922fe05cdb144f5f69d93a0de4345d047cc7d4`.
Sources are HDB resale / property / existing-building datasets and **ACRA B only**.
Attribution and licensing remain as in [data/README](../../data/README.md).
Current download endpoints need not return the pinned bytes.

`prepare_scale.py` verifies every full-source hash before writing. It preserves
resales in source order, adds their physical ending CSV line as source_row,
projects only property block/street and corporate block/street/postal/source-row
fields matching corpus normalized addresses, and retains the complete original
building GeoJSON. Corporate entity/person/unit fields are excluded. Full raw
and generated data stay outside Git; only the manifest and small measurements
are committed. Generated input sizes: about 24 MiB transactions, 231 KiB property
addresses, 162 KiB postal assertions, and 54 MiB original building polygons.
No Git LFS or database is needed.

```sh
# Supply the exact four pinned snapshots; paths below are examples.
python3 tools/prepare_scale.py \
  --resales /tmp/hdb-resales-official.csv \
  --properties /tmp/hdb-property-official.csv \
  --postals /tmp/hdb-m3-acra-c.csv \
  --buildings /tmp/hdb-buildings-official.geojson \
  --output /tmp/hdb-scale-full
# Note: the historically named hdb-m3-acra-c.csv file is the pinned ACRA B extract.
# Add --limit 1000, 5000 or 10000 for source-order prefixes in separate output dirs.
export PATH="$HOME/.local/share/hdb-qt-toolchain/bin:$PATH"
export QtDir="$HOME/Qt/6.12.0/macos"
dotnet build -c Release
dotnet run -c Release --project src/HdbResale.App --no-build -- \
  --scale /tmp/hdb-scale-full /tmp/full-scale-report.json
HDB_DATA_DIRECTORY=/tmp/hdb-scale-full \
  dotnet run -c Release --project src/HdbResale.App --no-build
```

`HDB_DATA_DIRECTORY` is an explicit offline input override. Without it, the
original six-row fixture still starts. Developer commands need **absolute input
paths**: dotnet run uses the native development bundle's working directory.
No geocoder, private cached coordinate, database or runtime data download is
involved. Only interactive OSM tiles use network access.

## Cardinalities and conservative coverage

| Full corpus measure | Count |
| --- | ---: |
| Accepted transactions | 241,920 |
| Raw town/block/street identities | 9,755 |
| Distinct block labels, not globally unique buildings | 2,785 |
| Towns / flat types | 26 / 7 |
| Located transactions / located addresses | 52,514 / 1,921 |
| Unlocated transactions / wholly unlocated addresses | 189,406 / 7,834 |
| Ambiguous transactions / addresses | 63 / 3 |
| Unmatched transactions | 189,343 |

Transactions per address: minimum 1, median 22, 95th percentile 54, maximum 183.
The full histogram is retained in the report. Located + unlocated conserves all
241,920 transactions; ambiguous rows are part of unlocated, not discarded.
This is conservative ACRA B-only linkage, not address accuracy or authoritative
historical identity. No assertion majority resolves a conflict.

Eleven full-footprint features are explicitly rejected: ten unsupported
MultiPolygon geometries and one invalid/missing required identity field. All
transactions survive. These are importer/source-format limits, not Qt issues.
The footprint parser remains deliberately Polygon-only. This milestone does
not silently convert geometry, alter matching, or replace missing locations.

The S$1,000,000 default budget exposes 236,791 transactions, 51,784 located
transactions in 1,920 map markers and 185,007 unlocated transactions. The budget
control now permits the actual corpus maximum, so the other 5,129 transactions
can be reached. Reset preserves the established S$1,000,000 default.

## Measurements and model decision

Environment: macOS 27.0.1 (26A434), arm64; .NET SDK 10.0.401 / runtime 10.0.12,
Qt 6.12.0, Qt Bridge 0.4.0.22-beta, CMake 4.4.3,
Ninja 1.13.2.git.kitware.jobserver-pipe-1, Python 3.9.6. These pins are unchanged.
Three iterations run in one process: iteration 0 includes first-run/JIT costs;
1 and 2 are later runs, not a statistical benchmark. Stopwatch intervals are
rounded to 0.1 ms (0.0 means below that reporting resolution). Managed bytes are
GC.GetTotalMemory(false), **not allocated-byte counts or proven retained heap**;
working set is Environment.WorkingSet. Neither GC nor disk/tile cache is forced
cold. Repeated runs can include previous unreclaimed allocations.

Original importer plus instrumentation, before optimization:
[1,000](baseline-1000.json), [10,000](baseline-10000.json).
At 10,000 rows, later validation/domain/matching cost 758.4 / 712.6 ms.
The small optimization builds evidence lookups once and reuses a match for the
same raw town/block/street. It passes all candidate assertions in original order
to the **unchanged AddressMatcher**; raw spelling stays in the memo key so exact
versus normalized quality is preserved. No fuzzy rules or evidence overrides
were introduced. [Indexed 10,000](indexed-10000.json) has later costs
22.8 / 16.2 ms plus 7.1 / 4.3 ms indexing. Differential tests serialize every
field from indexed and unindexed imports of the six and frozen 416 rows.
`HDB_SCALE_UNINDEXED=1` runs the original scan semantics in the data harness;
it is a bounded comparison opt-in, not recommended for the full corpus.

[Initial full-data report](indexed-full.json) retains the combined stage for
before/after context. [Final staged report](final-full.json) separates CSV
read/parse, validation/fact creation, matching/location resolution and final
transaction creation. Evidence CSV parsing/validation and polygon midpoint
creation have their own phases. State construction, town/budget/combined
filtering, selection, hidden-selection invalidation/empty, reset and map
aggregation are separate intervals. Flat-type filtering is not implemented in
the current explorer and is not claimed as measured. All seven types are counted.

Original map rows were per located transaction. Native source-order prefix
measurements used the existing per-transaction map at 416, 1,000, 5,000 and
10,000 transactions. At 10,000 / S$1m: 9,980 sidebar rows, 2,060 map delegates;
construction 1,919 ms, QML loaded readiness 372 ms, C# reset 3 ms, QML reset
readiness 119 ms. At 5,000: 1,016 delegates and 79 ms reset readiness.
These observations plus 52,514 full-corpus repeated points justified trying a
small address projection. **A 52k-delegate baseline was intentionally not
launched**; no measured full-baseline freeze or renderer failure is claimed.

C# `BlockSummaries` now projects located filtered rows into one marker per raw
address, preserving a deterministic latest transaction (month then ordinal ID),
count, min/max and median price. All underlying accepted transactions remain
in memory and the virtualized sidebar. Unlocated/ambiguous rows remain visible
and selectable. Every filter recomputes summaries over the filtered rows;
count sums conserve located transactions. Inconsistent coordinates at one
address throw rather than choose a point. Marker taps select that address's
latest visible transaction, sidebar taps select the exact transaction, and
selection colors the address marker and raises it above overlap. The default
six distinct addresses still produce six markers.

The full native gate exposes about 236k sidebar model rows but only a handful
of ListView delegates are visible; inspection found scrolling usable without
a block-only sidebar redesign. C# filter/reset is measured separately from QML
readiness. Grouped 10k retained 1,040 delegates; full data retains 1,920 under
the default budget. There is no clustering, spatial index, heatmap, chart port,
renderer change or additional project split.

The data harness initially measured full loading around 1.9–2.3 seconds and
later filters at 1.4–8.5 ms. Repeated harness managed snapshots reached roughly
814–820 MiB, with working-set snapshots roughly 557–727 MiB. Native constructor
snapshots are separately in [native-measurements.json](native-measurements.json).
The final staged harness has later managed snapshots around 841–870 MiB;
these include prior unreclaimed runs and temporary CSV/GeoJSON work, not a
single-app steady-state or allocation counter. A separate live single-app gate
logged 699,250,888 managed bytes / 853,884,928 working-set bytes after transitions.
An explicitly opted-in diagnostic full collection reduced these to 242,305,544
managed bytes / 641,712,128 working-set bytes in 125 ms (about 231 / 612 MiB).
This happened after timed UI transitions, while all models remained live. It is
not normal app behavior, natural steady-state, or a guarantee for smaller Macs.
Use `HDB_SCALE_HEAP=1` with the scale gate to reproduce this separate experiment.
No runtime forced collection is proposed. Memory remains a significant profiling
target; current workload completed without OOM or an observed freeze. Retain
in-memory storage for now; SQLite would not remove QML delegate or network costs and
there is no measured storage/query bottleneck justifying it. The large temporary
CSV dictionaries and full GeoJSON loading are sensible future profiling targets.

Qt reset measurements include synchronous C# filtering/aggregation and begin/end
model notifications; they **do not isolate bridge-only marshalling time**. QML
readiness includes the 25 ms observer cadence and control/map transitions, not
an isolated GPU render benchmark. The scale oracle is precomputed once and its
cost is logged separately to avoid repeatedly aggregating all rows in the
observer. Tiles may be cached; no tile/network timing or cold-cache claim is made.
No layer is blamed without evidence.

## Verification and remaining limits

[Verification record](verification.md) lists final executed checks and physical
inputs. Canonical ten-transition gates retain all existing checks, including
the deliberate skip-empty fault rejection. The scale gate additionally asserts
C# expected sidebar and map cardinalities at full startup, town, nonempty
combined budget, selected, hidden-selection/empty, reset, zoom, pan, recenter
and clean teardown. Five-second phase and 25-second process deadlines are
bounded; no arbitrary sleep, retries or deadline relaxation.

```sh
dotnet build
dotnet test
dotnet build -c Release
dotnet test -c Release
python3 -m unittest discover -s tools -p 'test_*.py' -v
python3 tools/native_gate.py \
  --executable src/HdbResale.App/obj/Release/net10.0/HdbResale.app/Contents/MacOS/HdbResale.App \
  --log /tmp/canonical.log
python3 tools/scale_gate.py \
  --executable src/HdbResale.App/obj/Release/net10.0/HdbResale.app/Contents/MacOS/HdbResale.App \
  --data /tmp/hdb-scale-full --log /tmp/full-native.log
# Substitute Debug for the same two Debug gates.
# Negative checks must exit 1:
HDB_GATE_FAULT=skip-empty python3 tools/native_gate.py \
  --executable src/HdbResale.App/obj/Release/net10.0/HdbResale.app/Contents/MacOS/HdbResale.App \
  --log /tmp/negative-canonical.log
HDB_GATE_FAULT=skip-empty python3 tools/scale_gate.py \
  --executable src/HdbResale.App/obj/Release/net10.0/HdbResale.app/Contents/MacOS/HdbResale.App \
  --data /tmp/hdb-scale-full --log /tmp/negative-full.log
```

A scoped GitHub Actions workflow builds/tests the Qt-free test/domain projects
in Release and runs the Python suite. It uses pinned official checkout/setup-dotnet
commits and SDK 10.0.401. It does not install Qt or assert Linux GUI support.
Publication and exact-head CI status are recorded separately after pushes.

No credible upstream Qt Bridge or Location defect has been demonstrated.
The string-array indexOf failure and self-referential ScrollView sizing loop
occurred in new application code and were fixed without suppressing warnings:
JSON is only presentation transport for town labels, TownIndex is C# state;
fixed-width Flickables keep full diagnostics/details scrollable. Qt Location
6.12 remains Technology Preview and Bridge's existing private-header coupling
requires the same Qt build. OSM service/network limits still apply.

M7 should first profile transient CSV/GeoJSON memory and loading on the adopted
in-memory/address-marker workload, add narrowly tested MultiPolygon support if
those excluded features affect needed coverage, and evaluate map interaction
at the 1,921-address workload. A renderer proof-of-concept, if separately
requested, should compare identical map-ready data and controls against this
baseline; full-corpus size alone does not prove a renderer problem. Address
coverage remains an independent evidence task. Linux/Windows GUI validation,
pinch, signed distribution and cold tile timing remain unrun.

## Additional Linux x64 verification

The user subsequently requested Linux portability and native verification. See
[Linux setup and evidence](../linux.md) for the approved Linux-only Bridge pin,
full-data gates, physical final-layout/above-S$1m checks and remaining limits.
The Mac measurements above are preserved as Mac measurements.
