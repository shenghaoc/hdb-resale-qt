# M8 Linux verification, 2026-10-03

Production source checkpoint: `ad0827aa6365281b47aa23ceda18aa8d01b1fc75` on `milestone-8-startup-memory`, descended
from exact M7 `762a77099ddac301aee0764679868213b150e397`. Diagnostic-only baseline
uses `e65bec5` with the original M7 import algorithm and stage instrumentation.
Same Linux x64 desktop/toolchain/pins and full-input manifest as M7; no pushes,
remote authentication attempts, private export acquisition or M9 implementation.

## Executed automated checks

- Root Debug/Release solution builds passed, zero MSBuild warnings/errors.
  Existing native private-header/optional TaskTree/translation notices remain.
- Root C# suites: **94 passed per configuration**, zero failed/skipped.
  The 39 new cases include 35 full-result CSV reference-path comparisons and
  four exact-string lifetime/bounds plus GeoJSON decoding tests. They cover
  quotes, escaped/multiline content, malformed recovery, rollback of errors
  before late fallback, extra/reordered headers, blank/Unicode/control lines,
  CR/LF/CRLF, EOF/trailing fields, BOM encodings, invariant decimal validation,
  canonical source identity and preservation of raw spellings.
- Python unittest discovery: **19 passed**. Existing gate rejection checks,
  platform package pins and bundled OneMap logo hash/resource checks remain.
- Canonical, full classic, extended and reentrant native gates passed in **both
  Debug and Release**, including clean managed/native exit. State deadlines,
  25-second ordinary process bounds and 45-second extended bound are unchanged.
  No offscreen host, retry-on-failure acceptance or warning suppression was used.
- Four deliberate Release negatives rejected with harness exit 1 and no pass:
  canonical `skip-empty` phase 5, classic `skip-empty` phase 4, extended
  `skip-empty` phase 4 and reentrant `skip-burst` phase 6.
- All full scale reports retain **241,920 transactions, 9,755 raw addresses,
  52,514 located transactions and 1,921 located address markers**, 63 ambiguous
  transactions and 189,406 unlocated rows. The default S$1m state remains
  236,791 rows /1,920 markers. No source row was dropped.
- Four derived input hashes match the M6 manifest. Six fixture and frozen 416
  files/reports are unchanged; native 416 report byte comparison passed.
- Native historical M5 report is byte-identical with SHA-256
  `d76614762c351eb121d3dd11ed03658e53daae1a5feeae36809a029b7e62d4be`.
- Original M7 and optimized **complete** ImportResult serialization hashes match:
  `94a65b7888615b71b049ed97dcfe52fdc88b2707e5e21b77811303febb6ea8f6`.
  A streaming hash comparison covers every accepted fact/match/provenance field
  and every rejection/diagnostic. The final native `--import-digest` matches it.
- All eleven full-footprint diagnostics survive unchanged: ten unsupported
  MultiPolygons and one numeric ENTITYID=0 positive-identity rejection.
- Independent review found no concrete parser/footprint regression. Separate
  baseline-M7/current complete-result probes matched 32 footprint cases including
  signed zeros, coordinate extrema, ignored extra ordinates/holes, BOMs, invalid
  UTF-8, exact malformed-JSON messages, numeric overflow, missing/directory paths
  and existing geometry/identity rejections. Injected mid-read OS I/O failure was
  not independently simulated; preceding-readable-row handling remains explicit.

## Like-for-like native-host managed observations

Reports: [baseline](baseline-managed.json), [optimized](final-managed.json).
Each command ran three iterations, the same production import stages and the
same post-stage diagnostic collection/KeepAlive roots. Later means iterations
1–2, not a statistical distribution. Sequential host/cache effects remain.

| Measure | Original M7 | Optimized |
|---|---:|---:|
| First import, summed stages | 7,742 ms | 2,494 ms |
| Later import, summed stages | 5,361–5,514 ms | 1,236–1,245 ms |
| First import thread allocation | 4,411 MiB | 463 MiB |
| Later import thread allocation | 4,281 MiB | 399 MiB |
| Later transaction CSV | 4,088–4,102 ms /3,786 MiB | 212–261 ms /209 MiB |
| Later footprint file decoding | 157–253 ms /218 MiB | 18–19 ms /54 MiB |
| Later footprint JSON parse | 273–288 ms /pooled reuse | 262–270 ms /pooled reuse |
| Later geometry/footprint identity | 204–206 ms /55.6 MiB | 171–177 ms /9.5 MiB |
| Post-diagnostic-GC managed heap | 230.7–230.8 MiB | 128.2–128.3 MiB |

Other later stages remain separately visible; no all-stages-faster claim is made:

| Stage | Baseline ms /allocated MiB | Optimized ms /allocated MiB |
|---|---:|---:|
| Property evidence | 25–52 /40.9 | 4–37 /3.5 |
| Postal evidence | 16–24 /27.4 | 2–3 /2.1 |
| Evidence index | 42–47 /5.0 | 7–8 /5.0 |
| Validation + fact creation | 149–160 /72.9 | 334–378 /39.9 |
| Matching/location resolution | 127–128 /41.2 | 112–146 /41.2 |
| Final transaction objects | 267–268 /33.8 | 29–32 /33.8 |
| ExplorerState | 7–13 /3.7 | 8–18 /3.7 |
| Address aggregation | 31–77 /9.6 | 31–84 /9.6 |
| Town + budget | 13–17 /0.05 | 6 /0.05 |
| Reset | 6–8 /1.8 | 8–10 /1.8 |

Exact-string dictionary lookups add work in validation/fact construction; that
stage was slower in this native pair despite lower allocations. GC timing also
moves between stages (final transaction allocation bytes are unchanged). The
sharing decision accepts that bounded tradeoff for the measured roughly 38.5 MiB
retained-string reduction and faster overall raw startup. Unchanged aggregation
and reset code show ordinary timing variation, not algorithmic gains/regressions.
Warm explanatory file reads were about 3 ms CSV and 6–7 ms GeoJSON; distinct-address
normalization was 1–2 ms /1.9 MiB. These probes do not remove that work from the
real index/matching stages or separate CPU from every allocation/GC pause.

The roughly **91% reduction in later import allocations** and much shorter raw
CSV path are the main measured wins. Whole-file JSON parsing still exists; no
claim of streaming the complete GeoJSON is made. Post-GC totals include retained
runtime/parser pools, accepted facts/evidence, state and summaries, rather than
only transaction objects. The 64 MiB pool-related component and exact-string
lifetime are discussed in the [measurement contract](README.md).

Snapshots are not equivalent to either allocations or retained roots. For
example, later pre-collection managed snapshots after reset were 543 MiB before
and 480 MiB after; working sets were about 687–691 and 647–649 MiB. Diagnostic
post-GC working sets were about 622–634 and 565–574 MiB. Prior explanatory probes
and retained resident GC pages influence later runs. No leak or guaranteed
steady-state working-set number is inferred.

## Native layout, bridge consumption and full markers

[All raw native stages and ordinary gate events](linux-native.json) include two
separate-process observations per view, same provider/cache/full input and heap
opt-in. Every run has exactly one matching READY with 241,920 rows /1,921 summaries
and clean EXIT. External probe limit stays 25 seconds.

| View | Baseline total process s | Optimized total process s |
|---|---:|---:|
| Minimal diagnostic QML shell | 8.229 /8.250 | 3.715 /3.296 |
| Production layout + OneMap, no markers | 9.290 /8.492 | 3.430 /3.565 |
| Production layout + all 1,921 markers | 10.573 /9.865 | 5.358 /5.192 |

Total process includes diagnostic post-readiness collection and teardown. The
post-model QML readiness interval for full markers was **2,057–2,223 ms before**,
**1,962–2,020 ms after**. This part is broadly similar; startup savings come from
managed import, not a new renderer or isolated Bridge speedup. Full views read
13,447 marker roles (1,921×7) and 45 sidebar roles; the virtualized list does not
populate hundreds of thousands of sidebar delegates. Map-shell read 40–45
sidebar roles and zero map roles; minimal shell read neither.

Full-view pre-collection managed snapshots were 570 MiB before and 443–444 MiB
after, but working set remained substantial: **936–940 MiB before, 839–846 MiB
after**. After the diagnostic collection, managed heap was about 231 vs128.5 MiB,
with working set about 726–730 vs684–686 MiB. The native Qt process is not a
128 MiB application, and forced collection is not offered as a production fix.
No macOS memory extrapolation is made.

## Ordinary update preservation

Final classic total process: Debug 9.12s /Release 11.31s; extended 14.18s /14.79s;
reentrant 10.85s /11.75s. These include startup/teardown and no forced collection.
Every gate preserves all exact displayed marker fields and surviving QML object
identity, FIFO reentry draining, hidden selection clearing, density behavior and
full/subset/different/empty/reset/zoom/pan/recenter state transitions.

Release S$1m→all-price readiness was **189 ms**, inserting exactly one delegate
and destroying zero, alongside M7's prior 196 ms observation (same incremental
algorithm). This is not a statistically significant speedup claim; it confirms
that the measured startup change did not reintroduce the prior 1,920-delegate
reset churn. No new per-filter parsing, canonicalization or footprint work runs.

## Physical Release desktop check

On the final Release binary, real desktop screenshot/mouse/keyboard input showed:

- OneMap tile pixels, bundled logo and linked SLA/OneMap credit, all 241,920
  transactions /1,921 markers at the full budget, and the virtualized list.
- Sidebar HDB-2 selection displayed exact transaction/provenance details and its
  orange address marker (22 transactions). Selecting BEDOK produced 12,620 rows
  /25 markers and cleared the hidden selection.
- Zero budget produced empty list/map; Reset restored 236,791 rows /1,920 markers.
  Quick successive reset/town inputs settled to correct visible state. Popup/input
  delivery timing is not a deterministic reentrancy proof; the native injected
  rowsAboutToBeRemoved burst gate provides that separate evidence.
- Zoom buttons reached level 14 with density styling. Dragging changed the
  center to about 1.3573,103.8118; Singapore restored zoom 11 and 1.3521,103.8198.
- Full budget and HDB-2 selection were restored for the final screenshot. Native
  close exited 0. Logs contained only the two previously known Linux desktop
  portal notices, with no QML/runtime error signatures or AT-SPI warnings.

The verified final screenshot is delivered separately. Wheel/pinch hardware
input and macOS/Windows behavior remain unclaimed.

## Publication and limits

Final history audit is recorded after the documentation checkpoint. Generic
reachable-history exclusions are rerun; exact private display-name/coordinate
rescanning remains unavailable because the private reference was not transferred
or read. Earlier M6/M7 exact-reference evidence is inherited, not relabelled as a
new exact-reference audit. The full-history bundle, fresh-restore check and Library delivery are recorded
separately. No macOS/Windows execution, wheel/pinch hardware, cold network,
signed distribution or isolated GPU/Bridge benchmark is claimed.
