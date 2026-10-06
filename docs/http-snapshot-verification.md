# Local HTTP snapshot verification, 2026-10-06

Main contains the preserved ordered-source commits `4a8d63e` and `35337be`,
whole-year precision checkpoint `96e46c2`, independently checked reconciliation
documentation `56ec8ac`, and measured import allocation checkpoint `e1bcd92`.
The HTTP client is a concrete static pack consumer inside the existing Domain
project, with two App CLI entrypoints and one local preparation tool. It has
no database connector, provider interface, new production project or UI redesign.

Mac arm64, .NET SDK 10.0.401/runtime 10.0.12, Qt 6.12.0 and Bridge 0.4.0.22-beta.
Builds use the existing Python/ninja environment, Homebrew CMake and `QtDir` pin.
Main was clean before fast-forwarding the preserved commits. Remote main was
read-only checked at `4796d7d`; no fetch/merge of another owner's UI work was
needed. The separate UI worktree remains at `c0985ec`, untouched. No push, PR,
deployment, production snapshot publication, routing change or database action
was performed.

## Automated and native evidence

| Check | Result |
| --- | --- |
| Root Debug and Release builds | Passed, zero MSBuild warnings/errors; existing Qt deployment/private-header notices retained. |
| Full C# suites | 231 passed in each configuration, zero failures/skips. |
| Real loopback HTTP cases within those suites | 19, covering activation/reuse, corruption, incomplete transfers, cancellation, unsupported versions, wrong counts, changed current pointer, bounds, redirect/path rejection, cache locks and offline corruption detection. |
| Python 3.12 suite | 51 passed, including deterministic gzip objects, inventory/hashes, refusing an existing pack, source corruption and unsupported preparation inputs. |
| Existing native positive matrix | 24 passed across Debug/Release: canonical, strict 10k and exact expanded M11 classic/extended/reentrant/presentation/buyer cases, plus mixed-source buyer views. |
| Maximum captured completed positive state | 939 ms; existing ordinary 5-second and validated M11 10-second limits unchanged. |
| Native deliberate-failure matrix | 54 detected with `HDB_GATE_FAIL` and `HDB_GATE_EXIT`, including wrong source hashes and materially wrong whole-year expectations. |
| Historical native-host full/legacy import digests | Four exact matches; expected hashes unchanged. |
| Fresh-cache native HTTP to buyer views | Passed in both Debug/Release; each made 10 local GETs for seven files plus discovery/manifest/recheck, then completed all 15 buyer steps with zero snapshot HTTP calls on launch. |
| Native corrupt update | Both hosts exited 1 with a clear integrity error, preserved the old active pointer, then opened the previous complete snapshot successfully. |
| Forced native process interruption | Release killed mid-object (exit -9); old pointer byte-identical, one inactive orphan stage retained, retry activated a complete new generation and passed all 15 native buyer steps. |

Tests use the actual development bundle executable under
`obj/<configuration>/net10.0/HdbResale.app/Contents/MacOS/HdbResale.App`.
The native assertions cover C#/QML state, map/list projections, filters, selection,
trend series, displayed precision/provenance and reentry. They do not certify
hardware input, tile pixels, painted-frame latency, accessibility, packaging or
signing. The synthetic 18-row mixed-precision fixture remains explicitly a test
fixture derived from canonical rows. Its second generation changes fixture
prices only to verify update/rollback and a new raw-source hash.

The native CLI check exposed an initially uncaught `InvalidDataException` on
corrupt download. The final handlers catch integrity/URI failures, return exit 1
and preserve the prior cache. Fresh-cache runtime checks were repeated on the
corrected Debug and Release bundles. No failed attempt was counted as acceptance.

## Representative full-corpus local HTTP check

One retained 988,123-row mixed-source pack was prepared and served by a loopback
static server, then downloaded through the Release native CLI. No provider or
production endpoint was queried. It contained eleven files, **168,732,030 unpacked
bytes** including unchanged location evidence, and **33,407,288 gzip bytes**.
Preparation took 2.280 seconds and synchronization 3.632 seconds in this single
warm-cache observation. There were **14 ordinary local GETs**: current, manifest,
eleven objects, current recheck. The client verified every compressed/unpacked
hash and size, source descriptor inventory and all 988,123 declared transaction
occurrences before activation. The manifest hash is
`2b7e6e6e5fb557b785a4adb4a1ad814f6744868aa8b06a80aad21ac4f64a3125`.
This CLI check is not a native UI or statistical throughput measurement.

Native full-view startup on the same retained source/evidence cohort separately
reached 988,123 rows and 7,618 mapped addresses. [The memory report](startup/http-snapshot-memory.md)
distinguishes temporary import allocation, retained managed buyer state and
whole-process Qt memory. Removing one redundant resolved-row tuple list saved
about **80.1 MiB managed allocation**, while retained full buyer state stayed
about **570.9 MiB**. Native full-view peak RSS was about **1.68 GiB**; after
diagnostic GC its managed heap was about **573.6 MiB** and working set about
**728.9 MiB**. No forced GC was added to the production path, and process working
set minus managed heap is not claimed as isolated native allocation.

## Reproduction and evidence location

Run the root Debug/Release builds and tests and Python unittest discovery as
documented in [source import verification](source-import-verification.md).
`HttpSnapshotTests` uses actual `TcpListener` loopback responses, not an HTTP
provider mock. Use [the local HTTP commands](http-snapshots.md) for native sync
and cache selection. The existing buyer/native/scale/presentation tools retain
their state budgets and independent CSV expectations. The wrong-years negative
adds 24 months to the independent expectation, so whole-year display rounding
cannot conceal that deliberate error.

Evidence remains outside Git in task-3 `mvp-evidence/`: build logs, TRX files,
Python log, `native/positive.json`, `negative.json`, `digests.json`, individual
native logs, `runtime-http.json`, `kill-download.json`, `full-http.json`,
resource-counter logs, scope-separated profile JSON and `memory-summary.json`.
The one-off local HTTP/profile orchestration scripts and their fixture/cache
outputs remain there as test artifacts. Generated packs, raw source corpora,
private evidence and binaries are not committed.

The [occurrence reconciliation](source-import-verification.md#occurrence-reconciliation-checkpoint-2026-10-06)
is separately committed and independently hash-checked against the read-only web
owner's report and exact five original/prepared source pairs. It establishes
988,123 source occurrences plus exactly five approved unresolved retentions
for the recorded 988,128-row candidate, reordered active captures, and the 85
raw lease variants explaining different duplicate grains. It does not assert a
freshly queried candidate, global absence of data loss or complete object equality.

The remaining production decision is approval of a source/evidence cohort and
a public static host for the implemented manifest/object contract. The current
D1 API does not supply that pack. No backend extension, publication or Neon
acceptance was substituted for the authorized local MVP.
