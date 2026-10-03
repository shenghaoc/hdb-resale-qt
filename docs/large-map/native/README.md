# Final M10 Linux verification

Production source: `567e608ddcf708cfb0d2a0d5a293a974b32ec36d`.
Subsequent source/evidence and documentation commits do not change production
behavior. Platform: Linux x64 Debian 13.6/XFCE X11, .NET SDK 10.0.401/runtime
10.0.12, Qt 6.12.0 and Linux Bridge 0.4.0-beta. The macOS 0.4.0.22-beta pin is
unchanged; no new macOS/Windows execution, hardware wheel/pinch or signed
packaging claim is made.

[Exact run records](results.json), [managed import stages](managed-profile.json)
and [full scale stages](scale-profile.json) retain counts and timing boundaries.
Measurements are sequential observations with unforced caches and variable host
load. They are not randomized estimates, cold-network results, GPU frame timings
or isolated Bridge/marshalling costs.

## Root builds, tests and native positives

- Root Debug and Release builds passed with zero warnings/errors.
- Root C# tests: **144 passed in each configuration**.
- Python suite: **30 passed, no skips**.
- Canonical six-row native gate passed in Debug and Release.
- Original-input and expanded classic/extended/reentrant gates passed in both
  configurations, retaining their exact transaction/mapped-address oracles,
  complete role/key/selection/control/lifetime/FIFO assertions and clean exit.
- The separate 22-state production-presentation gate passed on original and
  expanded inputs in both configurations. It verifies the complete C# truth
  against native Qt viewport projection, exact partition accounting, cell/zoom
  rules, original individual coordinates/IDs, offscreen selection, domain-hidden
  selection clearing, rapid viewport changes and FIFO reentry.

The original input retains the original **5-second completed-state** deadlines,
25-second classic/reentrant process and 45-second extended process limits.
Only the explicit pinned expanded profile uses **10-second completed states**
and 45/60-second process limits. No timeout was increased in M10. Every gate
checks elapsed time after identity/viewport validation and snapshot work. Any
state exceeding five seconds remains a performance warning even if expanded
correctness would allow it; no final positive state exceeded five seconds.

| Scenario | Debug process / maximum state | Release process / maximum state |
| --- | ---: | ---: |
| Original classic | 5.23 s / 333 ms | 5.81 s / 369 ms |
| Original extended | 8.01 s / 589 ms | 8.19 s / 551 ms |
| Original reentrant | 6.70 s / 759 ms | 6.02 s / 651 ms |
| Expanded classic | 8.54 s / 414 ms | 7.37 s / 345 ms |
| Expanded extended | 10.42 s / 533 ms | 9.93 s / 367 ms |
| Expanded reentrant | 8.28 s / 881 ms | 7.96 s / 686 ms |
| Original presentation | 9.46 s / 675 ms | 9.62 s / 737 ms |
| Expanded presentation | 15.93 s / 944 ms | 20.69 s / 1,235 ms |

The additional presentation gate includes repeated independent Qt projection of
all 7,618 truth addresses and stronger camera/group assertions. Its completion
interval includes that test work and must not be described as UI/GPU frame time.
The native gate's 25 ms observer cadence and asynchronous incubation also remain
part of completed-state timings. Ordinary UI does not run these scans or force
QML/managed garbage collection.

## Unchanged data and import semantics

Final full import: 241,920 transactions, 9,755 addresses, 188,573 located
transactions, 7,618 mapped addresses and 53,347 unlocated transactions. The
address inventory remains **897 ExactAddress / 6,721 NormalizedAddress /
209 Ambiguous / 1,928 Unmatched**, and **7,618 BlockApproximation / 2,137 Missing**.
The full import digest is byte-identical to M9:

`88118bdb3a9a68f59ea47cddae27568623f136cb7b7d5320a376f8f752dc6cc5`.

This digest, unchanged Domain/importer/evidence bytes and full scale assertions
preserve the complete M9 quality inventory. The frozen 416 report is byte-equal.
The frozen M5 report remains byte-equal with SHA-256
`d76614762c351eb121d3dd11ed03658e53daae1a5feeae36809a029b7e62d4be`.
Canonical six-row files, all source hashes/provenance and source pins are
unchanged. No address-evidence acquisition, importer normalization, matching or
coordinate change is part of M10.

Import timing is not hidden by the renderer improvement. The final separate
managed profile measured 4,676 ms first import and **3,892 / 3,151 ms** later
imports, outside the historical M9 2.65–2.90-second interval. The preceding quiet
alternating baseline/candidate samples varied substantially for both identical
importer implementations (including M9 4,057 / 3,524 ms and candidate
2,703 / 2,509 ms). They do not establish a stable regression, but neither justify
a guarantee that every warm import meets the older interval. No importer change
was made to optimize a different workload or mask the variation.

## Native startup and memory

| Final view | External process | QML-ready interval | Map role reads |
| --- | ---: | ---: | ---: |
| Minimal QML shell | 5.128 s | 144.7 ms | 0 |
| Map/list shell without marker model | 5.701 s | 479.6 ms | 0 |
| Production full map | 5.302 s | 436.0 ms | 184 (= 8 × 23) |

Full view represents all **7,618** mapped addresses with **23** low-zoom objects,
not 23 located addresses. At full-map readiness the working set was 850,145,280
bytes, then 699,793,408 bytes after the separately labelled diagnostic collection;
retained managed heap was 226,890,816 bytes. Reproduced M9's comparable post-GC
working set was 1,121,075,200 bytes, about **402 MiB higher**. The full-map external
process includes managed import, diagnostic collection and teardown; it is not a
cold first-frame promise. The retained data/managed heap remains essentially the
same; native presentation population is what changes.

M10 uses public default incubation. The QML model wrapper is still explicitly
anchored for window lifetime, and gate-only QML GC is followed by normal updates.
The exact Main.qml source and deployed Debug/Release hash is
`ac6cdc2596cb44b268e75dffb4c4fe58e6cafac78560c58c86ccfdd0eace07e7`.

## Reproduction

Use the pinned toolchain and actual native desktop session documented in
[Linux setup](../../linux.md). Root tests and Python checks are required, not just
focused projection tests:

```sh
dotnet build -c Debug && dotnet test -c Debug --no-build
dotnet build -c Release && dotnet test -c Release --no-build
python3 -m unittest discover -s tools -p 'test_*.py' -v
exe="$PWD/src/HdbResale.App/bin/Release/net10.0/HdbResale.App"
python3 tools/native_gate.py --executable "$exe" --log /tmp/canonical.log
python3 tools/scale_gate.py --executable "$exe" --data /absolute/hdb-scale-full --extended --log /tmp/original.log
python3 tools/scale_gate.py --executable "$exe" --data /absolute/hdb-m9-expanded --expanded-coverage --reentrant --log /tmp/expanded.log
python3 tools/presentation_gate.py --executable "$exe" --data /absolute/hdb-m9-expanded --expanded-coverage --log /tmp/presentation.log
HDB_GATE_FAULT=drop-presentation python3 tools/presentation_gate.py --executable "$exe" --data /absolute/hdb-m9-expanded --expanded-coverage --log /tmp/negative.log
# Negative must exit1 with a real state assertion failure. Repeat modes/configurations.
```

## Failure sensitivity

All **26** final negative cases returned the expected exit 1 through actual
state assertions, without missing-library failures or crashes:

- The original seven cases in each configuration: canonical `skip-empty`, and
  original/expanded classic + extended `skip-empty` plus reentrant `skip-burst`.
- Three presentation faults on both workloads in both configurations:
  `drop-presentation` fails phase 0 with `identities=false`; `skip-empty` fails
  phase 6; `skip-burst` fails phase 19.

The projection fault removes an address group from both the C# model and its
serialized expectation, yet fails the independently derived Qt viewport oracle.
This demonstrates that the new accounting test cannot pass merely because two
views of the same incorrect presentation plan agree. Original 5-second and
expanded 10-second bounds remain intact.

## Physical Release desktop verification

Mouse/keyboard checks on the final expanded Release application verified:

- All 241,920 transactions / 188,573 located transactions / 7,618 mapped addresses,
  OneMap tiles, official local logo and complete linked attribution.
- A low-zoom group click changes zoom 11→13. “Show selected address” moves to
  individual-marker zoom 16. Clicking an individual selected its latest
  transaction (HDB-223951), with matching address/details.
- Sidebar HDB-2 remains selected when panned outside the viewport; the explicit
  outside-view notice and return control agree. Returning restores its highlighted
  address. BEDOK filtering clears that domain-hidden selection.
- Budget zero produces a genuinely empty map/list. Reset restores the default
  domain filter; rapid successive zoom input and recenter settle to correct
  counts/camera. Singapore returns to zoom 11 / 1.3521, 103.8198.
- Normal titlebar exit returns 0. Only the two previously known Linux desktop
  portal notices appear; no QML/runtime error signatures are accepted.

Brief asynchronous marker overlap/removal can be visible immediately after rapid
changes, then settles; this is not claimed to be an instantaneous visual update.
No persistent wrong state was observed. The deterministic injected gates, rather
than desktop tool round-trip timing, establish the FIFO and exact-state contracts.

The final screenshot is delivered separately: 316,195 bytes, SHA-256
`1adbd93b06731004d5d57c6b5ccd7c92077b452f977f3841c87d67e1c9be10e7`.
The stable captured full view shows all 7,618 addresses accounted for by 23 groups
plus the selected individual address, with the exact selected HDB-2 transaction
and source-quality details. No new hardware wheel/pinch or other-platform result
is inferred from these tests.


## Reachable history and publication scope

The [all-ref generic audit](history-audit.json) at documentation checkpoint
`8e4d07c84eb3a486c516f41f38dc327376b132f9` traversed **43 commits, 436 unique blobs
and 267 paths**, including all **83 new blobs** since exact M9, with zero issues.
It checks excluded raw-export hash/header, credential-shaped content, prohibited
local/build/binary paths, oversized blobs, minimized historical fields, unchanged
Domain/canonical/frozen evidence, source pins and excluded `843df64` reachability.
The source/evidence MapLibre pack contains no native binaries, dependency trees
or generated GeoJSON. Its local path/editorial adaptations have regenerated
`SHA256SUMS`; all 54 listed files verify.

The scanner is reproducible as `tools/audit_reachable_history.py`. A new exact
private display-name/full-precision-coordinate-pair rescan is **not available or
claimed**: the private raw reference was never transferred, read, fetched or
reconstructed. This deliberately inherits M9/M8 and earlier exact-reference
limits. All newly used coordinates remain the original approved public-derived
HDB points; no coordinate source or private cache was introduced.

The same generic all-ref scan is rerun after this audit-record commit and its
final result accompanies the handoff. Prior milestone branches, remote-tracking
refs and ancestry are preserved. No push or authentication retry occurred.
Full-history bundle creation, fresh restoration/fsck and Library delivery are
recorded separately. No M11, buyer-workflow, database, chart or packaging work
was started.
