# M8: measured raw startup and transient memory

M8 starts from the clean, exact M7 checkpoint
`762a77099ddac301aee0764679868213b150e397`. The production change is confined to
raw import allocations. Domain remains Qt-free; accepted in-memory transactions
remain the truth. There is no database, new parser package, global string intern
pool, binary startup cache, renderer replacement or new evidence acquisition.

## Measurement contract

Same pinned 241,920-row corpus and four derived hashes as
[the M6 manifest](../scale/manifest.json). Linux x64, Debian 13.6/XFCE X11,
.NET SDK 10.0.401/runtime 10.0.12, Qt 6.12.0 and Linux Bridge 0.4.0-beta.
The macOS Bridge 0.4.0.22-beta pin is unchanged. These are Linux observations,
not macOS/Windows, cold-disk, cold-tile, GPU-frame or universal speed guarantees.

- `--startup-profile INPUT OUTPUT` runs three iterations in one process. The
  first includes JIT/pool initialization; later iterations reuse caches and pools.
- Stopwatch stages separate property/postal evidence, footprint file decoding,
  JSON parsing, geometry/identity creation, evidence indexing, transaction CSV,
  validation/fact allocation, matching, final transaction allocation, state,
  address aggregation, filtering and reset. Validation plus fact creation is one
  explicitly combined stage; no claim of separating their individual CPU cost.
- `ThreadAllocatedBytes` is a delta from `GC.GetAllocatedBytesForCurrentThread`;
  `ManagedThreadId` is recorded. The delta is null across a thread change.
  `ProcessAllocatedBytes` uses `GC.GetTotalAllocatedBytes(true)` and includes all
  managed threads. Neither includes native Qt allocations. Snapshot collection
  and diagnostic logging have small overhead, not subtracted from observations.
- `ManagedBytes` is `GC.GetTotalMemory(false)`, not cumulative allocation or a
  naturally settled live heap. `WorkingSetBytes` is `Environment.WorkingSet`,
  covering native libraries, render resources, committed resident GC pages, etc.
- After timed import/state/update work, the diagnostic command alone requests a
  full blocking compacting collection. `GC.KeepAlive` preserves the import,
  current ExplorerState and address summaries across it. Iterations have separate
  frames so previous imports are not intended roots. The measurement list stores
  scalars only. No collection is inserted into normal application startup/updates.
- Subsequent file-I/O (copy to `Stream.Null`), original TextFieldParser-tokenizer,
  unique-address discovery and normalization probes are explanatory, warm-cache
  measurements. They are **not additive** import stages. In particular, tokenizing
  without keeping every row has different GC behavior from the real importer.

[Baseline native-host managed stages](baseline-managed.json) and the final report
are separate from [.NET-only experiments](experiments.json). Do not mix their
wall-clock numbers as one controlled comparison. The experiments used identical
harness code, three-run/GC methodology and retained roots; host contention was
visible in the scalar-bounds trial, so that trial supports allocation, not speed,
claims. Final native observations use the same diagnostic code/provider/input.

## Evidence-led choices

The original tokenizer alone allocated about **3,509 MiB** over the full CSV;
whole transaction CSV materialization allocated **3,786 MiB**. Warm CSV file
copy was only a few milliseconds. Consequently raw disk I/O was not the measured
bottleneck, and replacing it with a database/cache was not the first intervention.

The stepwise .NET-only experiments found:

| Change | Relevant allocated bytes, MiB | Diagnostic retained managed, MiB |
|---|---:|---:|
| Original M7 importer | CSV 3,786; validation/facts 73 | about 231 |
| Shared per-file header map only | CSV 3,523 | about 231 |
| Guarded unquoted CSV path | CSV 209 | about 231 |
| Bounded exact strings + direct required-field checks | validation/facts 40 | about 192 |
| Borrowed UTF-8 footprint bytes | file decoding 54 instead of 218 | about 128 |
| Scalar exterior-ring bounds | geometry 9.5 instead of 55.6 | about 128 |

These are stage allocation deltas and separately labelled post-GC observations,
not a sum of retained transaction sizes. MiB means 1,048,576 bytes.

### CSV contract

Every file has one ordinal header-index dictionary instead of a dictionary per
row. The fast path reads ordinary UTF-8 lines, preserves all field text and empty
fields, checks the complete original header width, and skips the same whitespace
lines. It never normalizes a fact. Every line is checked for quotes; another BOM
encoding, a quote anywhere, or a line over 64 KiB abandons the provisional pass.
The same held file stream is rewound and the **existing TextFieldParser handles
the entire file**, including escaped/multiline quotes and malformed recovery.
Provisional rows/parse errors are discarded before fallback, so errors are not
duplicated. There is no stale pre-scan decision that can accept quoting as plain
text. Like M7, an I/O failure retains preceding readable rows with a diagnostic.

`LoadDirectory(referenceCsv: true)` keeps the original parser route available
for differential verification, not as a second production architecture. The
profile command selects it with `HDB_CSV_REFERENCE=1`. Read errors still precede
semantic-validation errors. Diagnostic row numbers preserve M7's pre-blank-skip
parser convention; transaction identity remains the explicit source_row value.
Validation order, canonical integer-ID reservation, invariant exact month/price
checks, source-row evidence and accepted surviving rows are unchanged.

A per-import ordinal string dictionary shares only town, block, street and flat
text. It accepts at most 8,192 entries and only strings of at most 128 UTF-16
characters; overflow/long strings simply remain unshared. Its dictionary dies
with import; immutable accepted facts retain the strings they use. It never uses
`string.Intern`, changes case/spacing, merges normalized keys or caches private
coordinates/names. Direct whitespace checks replace a per-row helper array.

### Footprints and retained roots

Valid UTF-8 JSON is read into one byte array and borrowed by `JsonDocument`,
instead of building full UTF-16 text and transcoding it into another pooled
byte buffer. UTF-8 BOM is stripped; other BOMs and invalid UTF-8 use the original
StreamReader decoding behavior before parsing. The document is disposed within
import, and no JsonElement or borrowed-byte view escapes. Accepted footprints
retain only copied identity strings/scalars and their derived point/provenance.

Exterior-ring min/max and first/last coordinates are accumulated in scalars,
removing the per-feature position list and four LINQ extrema passes. Validation,
closure/equality, rounding, ignored holes/extra ordinates and Polygon-only scope
stay the same. All ten unsupported MultiPolygon features and ENTITYID=0 rejection
remain, with their feature ordinals and complete raw diagnostic JSON.

The post-GC reduction combines approximately **38.5 MiB of repeated fact strings**
and **64 MiB associated with the removed UTF-8 JsonDocument pool rental**. The
remaining heap still includes runtime/parser pools: first JSON parse allocates
about 64 MiB while later parses reuse it. Therefore the roughly 128 MiB diagnostic
result is not a transaction-model-only size. These observations are consistent
with retained JsonDocument metadata buffers; they are not a heap-dump census of
individual objects. Unused CSV strings/arrays, staging lists, decoded text and
unreferenced evidence/indexes are collectible after import. Working set need not
fall by the same amount immediately after collection.

## Startup-artifact decision

**Keep raw startup.** CSV tokenization/allocation, not file reading, dominated
baseline work; the guarded raw path removes that measured cost without a new
stored representation. Evidence matching/indexing are relatively small, while
QML delegate creation remains separately measurable. The current raw importer
therefore does not justify a custom binary format or an additional JSON artifact
with duplication, source-hash/version invalidation, corruption recovery and
rebuild machinery. No artifact is generated, trusted or loaded, so there is no
new stale-cache correctness surface. A future artifact would require a new
measured need plus deterministic/version/corruption/source-hash tests first.

Also deferred: full streaming/fusing of import stages (would complicate error
ordering/profiling for smaller benefit), broader string/value interning, new
matching indexes, MultiPolygon support, database/EF adoption, and production GC
policies. No behavior was changed merely to improve a benchmark number.

## Native shell, map and bridge observations

`HDB_STARTUP_PROFILE=1` enables stage/allocation logs. With
`HDB_STARTUP_VIEW=qml-shell|map-shell|full`, diagnostic startup selects all prices,
constructs all 241,920 facts and 1,921 summaries, observes readiness and exits.

- `qml-shell` is a separately labelled **minimal diagnostic QML/control shell**,
  with no Map/provider and no list/marker roles. It is not the full UI minus a
  precisely isolated map cost, so subtracting it from other modes is misleading.
- `map-shell` uses the production layout/list and OneMap provider, with marker
  model disabled. It includes normal bindings/virtualized sidebar population.
- `full` uses the production layout, provider and all 1,921 map delegates.
- C# `map-model-population` measures only backing-list creation, not isolated
  Bridge marshalling. QML readiness and recorded sidebar/map role-read counts
  show subsequent lazy model consumption. These intervals include bridge,
  binding, layout and timer costs; native marshalling/GPU time is not isolated.
- Optional `HDB_STARTUP_HEAP=1` collects only after readiness for a separate live
  native-root observation. Ordinary map-update gates run without it.

A first diagnostic-only attempt to keep a Map with a null provider exited with
SIGSEGV before readiness. It was rejected and replaced with the separate shell;
this is not counted as a successful run or claimed as an upstream Qt defect.
Existing gate state/process deadlines and warning checks are unchanged.

## Reproduction

Use the pinned toolchain setup in [Linux instructions](../linux.md), absolute
paths, the exact manifest inputs and a real desktop session for native Qt:

```sh
dotnet build -c Release
exe="$PWD/src/HdbResale.App/bin/Release/net10.0/HdbResale.App"
"$exe" --startup-profile /absolute/hdb-scale-full /tmp/startup.json
"$exe" --import-digest /absolute/hdb-scale-full /tmp/import.sha256
HDB_STARTUP_PROFILE=1 HDB_STARTUP_VIEW=full HDB_STARTUP_HEAP=1 \
  HDB_DATA_DIRECTORY=/absolute/hdb-scale-full "$exe"
# Repeat separately with qml-shell and map-shell; inspect READY and clean EXIT.
```

`--import-digest` streams default System.Text.Json serialization of the complete
ImportResult into SHA-256. The original M7 assembly and optimized importer agree:
`94a65b7888615b71b049ed97dcfe52fdc88b2707e5e21b77811303febb6ea8f6`.
This covers every accepted public fact/match/provenance field and every rejected
raw record/diagnostic, not only summary counts. The hash command is not a timing
measurement. [Verification](verification.md) records final native/UI checks,
comparison observations and remaining platform limits.
