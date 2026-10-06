# Snapshot import memory, Mac arm64, 2026-10-06

The 988,123-row import has a large temporary allocation footprint and retains
all immutable facts for buyer filtering. Removing the redundant resolved-row
tuple list saves **80.1 MiB of managed allocation** without changing accepted
order, provenance, facts, matches, location quality or diagnostics. Resolution
now directly creates the final accepted transaction objects. Diagnostic stage
labels remain, but `matching-resolution` now includes domain object creation;
`transaction-domain` no longer represents a separate materialization pass.

This is the only import allocation change. CSV rows and validated facts are
still materialized. There is no new streaming database, process-global string
pool or forced production GC. HTTP download/decompression is bounded and
streamed; its importer validation runs in the separate sync command and exits
before the UI process imports the selected cache.

## Measured scopes

The retained mixed-source descriptor is
`ea36ce53fd727cb7bf76e4b90a9af572f59a86aae58323cb4752c133b6f96f5a`.
All runs accepted 988,123 transactions with one existing geometry rejection and
198 diagnostics. Toolchain: .NET 10.0.401/10.0.12, Qt 6.12.0, Bridge 0.4.0.22-beta.
Baseline import binary is preserved commit `35337be`; current import removes
the staging list and carries the precision/client additions. Fresh-process
managed probes use the same corpus and diagnostic-only forced GC after import
and after full buyer state. The probe adds no duplicate grouping or provenance
audit arrays. Its project/logs are outside the repository, under task-3
`mvp-evidence/profile`, with raw measurements under `mvp-evidence/`.

| Scope | Baseline | Current |
| --- | ---: | ---: |
| Import managed allocation | 2,242,632,080 bytes | 2,158,590,512 bytes |
| Import time, warm filesystem | 3,208.1 ms | 3,305.9 ms |
| Import-only process maximum RSS | 1.57 GiB | 1.53 GiB |
| Import retained managed heap after diagnostic GC | 542.8 MiB | 541.9 MiB |
| Full buyer-state retained managed heap after diagnostic GC | 570.8 MiB | 570.9 MiB |

These are single paired observations, not a statistical speed/peak-RSS claim.
The allocation reduction is the measured removal of an unnecessary staging
container; retained state remains essentially unchanged. Import-only and
import-plus-state RSS can vary with collection timing and OS page accounting.

Real Release Qt startup on the same local corpus reached full-view readiness at
approximately 5,088.1 ms of recorded stages,
including 7,618 mapped addresses projected into 23 viewport clusters. Its process
maximum RSS was **1.68 GiB**. After the opt-in diagnostic
collection, managed heap was **573.6 MiB** and process
working set **728.9 MiB**. Working set includes native Qt,
runtime/code, heap pages and caches; subtracting managed heap does not isolate
native allocation. No such attribution is claimed. Readiness is a native state
assertion, not a hardware-input, painted-frame or tile-pixel latency result.

The previously reported 1.80 GiB process peak included independent duplicate
grouping, source/provenance validation arrays, buyer aggregation and diagnostic
GC. It was not an isolated import or native-Qt memory measurement. These new
scope-separated probes explain the retained versus temporary costs; the numbers
must not be compared as an identical-workload performance benchmark.

## Verification and limits

The complete C# suites, source/reference/indexed import comparisons and native
buyer flows pass. All four historical full/legacy M11 native-host hashes remain
exact; no expected hash was rewritten. The current full HTTP synchronization
also verifies every declared source occurrence before activation. Raw profile
JSON, process `/usr/bin/time -l` output and the native startup log are retained
outside Git. No binaries or data cache are committed.

The full immutable fact corpus still needs roughly half a GiB of retained
managed memory and a substantially larger startup working footprint. Compacting
that representation would be a separate, evidence-led change. This checkpoint
does not promise low-memory hardware acceptance, power-loss durability,
packaging/signing, accessibility or general physical UI acceptance.
