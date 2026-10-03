# M9 Linux verification, startup and map cost

Measured source is `06d84af` (subsequent documentation commits do not change app
behavior). Platform: Linux x64, Debian 13.6/XFCE X11; .NET SDK 10.0.401/runtime
10.0.12; Qt 6.12.0; Linux Bridge 0.4.0-beta. The separate macOS
0.4.0.22-beta pin is unchanged. No new macOS/Windows execution is claimed.

[Machine-readable results](linux-results.json), [managed startup stages](managed-profile.json)
and [full scale stages](scale-profile.json) retain exact counts and intervals.
Measurements are sequential observations, not randomized estimates or promises
for other machines. Disk/tile caches were not forced cold. The C# notification
interval includes Qt/Bridge callbacks, delegate creation and event processing;
it is not isolated marshalling, GPU or pure filter time.

## Final executed checks

- Root Debug and Release builds: passed; root tests: **134 passed in each**.
- Python suite under the pinned environment: **26 passed, no skips**.
- Canonical six-row native gates in Debug and Release: passed.
- Expanded classic, sixteen-transition extended and deterministic reentrant/FIFO
  gates in both configurations: **all six passed** under the explicitly approved
  expanded-workload budget below.
- Original full input, now **1,922 all-price / 1,921 default-budget markers** because
  of the separately audited one-address geometry gain: classic/extended/reentrant
  in both configurations passed the original **5-second state limits**, and
  original 25/45-second process limits. The exact 1,921-marker M8 domain baseline
  is preserved separately; this is not a claim that the geometry change vanished.
- Seven failure-sensitivity checks returned the expected exit 1: canonical
  `skip-empty`; expanded classic/extended `skip-empty` and reentrant `skip-burst`;
  the same three original-input faults at the original budgets. They fail for
  actual missing transitions, not by suppressing warnings or changing assertions.
- Frozen 416 report: byte-identical. Frozen M5 report SHA-256 remains
  `d76614762c351eb121d3dd11ed03658e53daae1a5feeae36809a029b7e62d4be`.
- Final accepted corpus: 241,920 transactions, 9,755 addresses, 188,573 located
  transactions, 7,618 all-price markers, 4,305 ambiguous transactions, one rejected
  footprint and 198 diagnostics (197 retained malformed assertions plus ENTITYID 0).
- Full import digest:
  `88118bdb3a9a68f59ea47cddae27568623f136cb7b7d5320a376f8f752dc6cc5`.
- Final runtime QML was compared to source after normal builds. All investigative
  generated-C++ and managed trace patches were removed. No renderer, database,
  package, coordinate source, sleep/retry acceptance policy or source pin changed.

## Explicit expanded correctness budget

The user approved a separate `--expanded-coverage` acceptance mode after observing
real large-model costs. It permits **10 seconds per completed state**, with
**45 seconds per classic/reentrant process and 60 seconds per extended process**.
The tool requires the explicit normalization profile and hash-identical full
transaction corpus; canonical/small/relabelled-small inputs cannot use the flag.
Without this flag, all existing normal gates keep their original 5-second state
and 25/45-second process limits. `--measurement` remains separately labelled
non-acceptance diagnostics and cannot be combined with expanded acceptance.

All state/cardinality/role/identity/selection/order/lifecycle/FIFO/shutdown/error
assertions are unchanged. A discovered harness gap was tightened: the original
callback-entry deadline check could pass a completed 5,523 ms state. Every
advance now also checks elapsed time after identity validation and snapshot
work. That earlier exit 0 is **not** accepted as meeting a 5-second budget.
A redundant second identity scan introduced while adding the lifetime regression
was removed, leaving the same assertions once per normal readiness check.

**A longer correctness deadline is not a performance fix.** Final Release
repopulation of 7,421 markers from a 197-marker town took 6,878.780 ms inside
notifications, **7,236.253 ms for the synchronous C# update call**, and
**8,030 ms to the fully validated state**. This exceeds the former 5-second
budget and can visibly block input/redraw. No claim of responsive large rebuilds
is made merely because the expanded gate passes.

| Final scenario | Debug process / maximum state | Release process / maximum state |
| --- | ---: | ---: |
| Expanded classic | 16.79 s / 3,448 ms | 16.80 s / 3,785 ms |
| Expanded extended | 37.48 s / **6,076 ms** | 41.28 s / **8,030 ms** |
| Expanded reentrant | 24.36 s / **5,237 ms** | 25.36 s / **6,262 ms** |
| Original-input classic | 7.42 s / 920 ms | 8.38 s / 1,026 ms |
| Original-input extended | 10.65 s / 1,047 ms | 10.56 s / 1,068 ms |
| Original-input reentrant | 8.44 s / 1,707 ms | 8.14 s / 1,360 ms |

Before approval, Debug extended genuinely failed at a 5,128 ms C# call even
though its final roles/counts were correct. Repeated diagnostic-only runs measured
completed states up to 7,155 ms and synchronous calls up to 6,510 ms. Final results
above are later observations and supersede those maxima; variance is visible.
Raw C# transaction filtering is not the multi-second bottleneck. Large delegate
repopulation/notification work dominates these observed transitions.

Initial synchronous construction can happen before the gate's first timer
callback. Therefore a `loaded` phase number alone is not a wall-clock startup
bound. External process timing and the separate QML-ready startup interval are
reported below; timer starvation is not hidden by relabelling a phase pass.

## Native model lifetime and construction correction

The expanded workload exposed a real app lifetime failure. With the original
binding, counts could reach 7,615 while every marker role was undefined. Both
ordinary 5-second and diagnostic 10-second gates failed. The seven native role
names were independently confirmed correct. Bounded Index/Data tracing showed
no callbacks in one failure, rather than just an invalid-index counter artefact.

The pinned Bridge creates a parentless JavaScript-owned wrapper for the managed
MapPoints object. Generated-native destructor tracing observed the actual wrapper
being destroyed **before READY**, dropping its model-change listener. Keeping the
managed C# object alive did not retain that separate native wrapper. A root
`readonly property var` now retains the exact same wrapper for the window
lifetime, and MapItemView binds through it. The model, role schema, stable-address
incremental update plan and FIFO mutation queue remain the same.

Relevant primary sources: [Qt object ownership](https://doc.qt.io/qt-6/qjsengine.html#ObjectOwnership-enum),
[Bridge's ownership introduction](https://code.qt.io/cgit/qt/qtbridge-csharp.git/commit/?id=03c20d16da4ee9d69b48883029980848c520e994),
and [Bridge wrapper creation](https://code.qt.io/cgit/qt/qtbridge-csharp.git/tree/include/qdotnetconvert.h?id=7019264f1a771a1f44ec55c33aa748f693faba75).
The evidence supports an application retention correction under the pinned
ownership contract. A bounded official issue/source search found no verified
matching issue; direct Qt tracker query was unavailable. No known upstream bug,
package-upgrade fix or upstream filing is claimed.

With the anchor and default asynchronous incubation, wrapper lifetime was correct,
but only 3,790/7,615 delegates existed at five seconds and the startup probe timed
out at ten seconds. The final input uses synchronous MapItemView incubation to
complete the larger population deterministically, retaining every point and all
existing update notifications. This does not remove the measured synchronous
rebuild cost. `incubateDelegates` is a QML-exposed `Q_PROPERTY` with revision 5.12
in the pinned Qt 6.12 implementation header. It is **omitted from the public
[MapItemView documentation](https://doc.qt.io/qt-6/qml-qtlocation-mapitemview.html)**.
The app does not call a private C++ symbol, but this is version-coupled behavior;
no portability guarantee beyond the verified pinned Linux stack is made.

A gate-only [QML garbage collection](https://doc.qt.io/qt-6/qtqml-javascript-memory.html)
check runs once after initial identity readiness, then requires normal filters,
role updates and retained delegates to work. Normal UI and startup probes never
force QML GC. Managed retained-heap diagnostics remain separately opt-in.

Rejected/limited investigative attempts are preserved honestly:
- Retaining the role-name Dictionary did not repair the failure and was reverted.
- Synchronous creation without the lifetime anchor produced initial roles but
  lost removal notifications; it was not accepted as a fix.
- A CMake-only diagnostic host initially lacked the normal SDK apphost patch and
  exited 253. This was a diagnostic setup failure, not an app regression.
- An early alleged anchor comparison accidentally used stale filesystem QML.
  It was discarded; actual runtime/source hashes were then checked explicitly.
- A conditional small-fixture `drop-model-anchor` fault did not reproduce the
  natural large-workload collection failure. It was removed, not counted as a
  successful negative regression. The original fault tests remain intact.

Selected diagnostic logs in this directory support the failure sequence. Startup
READY checks **cardinality**, and once reported success after wrapper destruction;
those exploratory READY timings are not functional acceptance. Final acceptance
comes from complete-role/identity and repeated-transition gates, with native
wrapper destruction observed only after exit in the corrected trace.

## Managed import and native startup

The transaction corpus and original property/footprint bytes are unchanged, but
postal evidence grows from 4,754 ACRA-B assertions to 311,110 all-partition
assertions. The workload therefore differs materially from M8. The original
UTF-8 footprint/scalar-bounds, guarded CSV fallback, bounded exact-string sharing
and accepted in-memory transaction design are preserved.

| Managed profile observation | M8 | M9 expanded |
| --- | ---: | ---: |
| First import | 2,494 ms | 3,931 ms |
| Later imports (two runs) | 1,236 / 1,245 ms | 2,899 / 2,648 ms |
| Later process allocations during import | 398.6 / 398.6 MiB | 795.8 / 803.0 MiB |
| Diagnostic retained managed heap | about 128.3 MiB | about 215.1 MiB |

These are separate sequential native-host observations, not a controlled speedup
benchmark. Allocations are managed counters, not native Qt bytes; post-GC retained
snapshots explicitly collect while keeping the import/state/summaries alive.
Working-set snapshots need not fall when managed objects are reclaimed. The old
`probe-distinct-normalization` explanatory probe still exercises legacy rules;
it is not presented as a timing of the expanded matcher.

Final native views all load 241,920 facts and 7,618 address summaries:

| View | External process seconds | QML-ready interval | Map role reads |
| --- | ---: | ---: | ---: |
| Minimal QML shell | 4.891 | 126.8 ms | 0 |
| Production map/list, no marker model | 5.341 | 334.7 ms | 0 |
| Full map | 8.437 | 2,896.2 ms | 53,326 (= 7 × 7,618) |

External seconds include diagnostic managed collection and teardown; the QML
interval includes bindings, layout and observer cadence, not isolated rendering.
The full view measured about 327.9 MiB managed / 1,133.9 MiB working set at
readiness, and about 215.4 MiB managed / 1,033.3 MiB working set after a separately
labelled diagnostic collection. This is not a low-memory-device guarantee.

## Reproduction

Use the pinned toolchain and a real Linux desktop session for the native host:

```sh
dotnet build -c Debug && dotnet test -c Debug --no-build
dotnet build -c Release && dotnet test -c Release --no-build
python3 -m unittest discover -s tools -p 'test_*.py' -v
exe="$PWD/src/HdbResale.App/bin/Release/net10.0/HdbResale.App"
python3 tools/native_gate.py --executable "$exe" --log /tmp/canonical.log
python3 tools/scale_gate.py --executable "$exe" --data /absolute/hdb-scale-full --extended --log /tmp/original-5s.log
python3 tools/scale_gate.py --executable "$exe" --data /absolute/hdb-m9-expanded --expanded-coverage --extended --log /tmp/expanded-10s.log
python3 tools/scale_gate.py --executable "$exe" --data /absolute/hdb-m9-expanded --expanded-coverage --reentrant --log /tmp/reentrant-10s.log
HDB_GATE_FAULT=skip-empty python3 tools/scale_gate.py --executable "$exe" --data /absolute/hdb-m9-expanded --expanded-coverage --extended --log /tmp/negative.log
# The negative must exit1. Repeat all positive scenarios in Debug.
"$exe" --startup-profile /absolute/hdb-m9-expanded /tmp/managed-profile.json
HDB_STARTUP_PROFILE=1 HDB_STARTUP_VIEW=full HDB_STARTUP_HEAP=1 HDB_DATA_DIRECTORY=/absolute/hdb-m9-expanded "$exe"
```

## Product limitation and next milestone

Visible blocking during large map rebuilds remains. The next milestone should
profile and compare the roughly 7,400-delegate rebuild: model population,
retained delegate/density strategies, clustering and, only with equivalent-workload
measurements, renderer alternatives. A database would not remove this measured
Qt delegate cost. No such redesign, loading animation or next milestone starts
in M9. Mac/Windows, hardware wheel/pinch, cold network/tile performance, isolated
GPU timing and signed distribution remain unverified.

## Physical Linux interaction and final screenshot

The Release expanded app was exercised with desktop mouse/keyboard input. Full
budget showed 241,920 rows, 188,573 located transactions and 7,618 markers. The
OneMap basemap tiles, local logo and linked credit were visible. Selecting sidebar
HDB-2 showed its exact transaction and the orange 22-transaction address marker.
BEDOK showed 12,620 rows / 4,607 located transactions / 197 markers and cleared
the hidden selection. Budget zero produced an empty state; reset restored
236,791 rows / 7,615 default-budget markers. Pan, zoom 12 and Singapore recenter
to zoom 11 / 1.3521, 103.8198 were checked, followed by clean exit.

Blocking was physically observed rather than dismissed: after reset plus queued
zoom input, one capture still showed the old empty BEDOK state; a later capture
showed the full markers and applied zoom. Full budget plus selection similarly
lagged one capture before reaching the correct state. Tool round-trips do not
supply precise human-response latency; the measured synchronous intervals above
quantify the separate performance limitation. Injected reentrant gates, not these
physical timings, prove the deterministic FIFO ordering contract.

After the final approved-profile build, full counts, OneMap presentation, HDB-2
selection, reset/recenter and clean exit were rechecked. Only the two previously
known Linux desktop portal notices appeared; no QML/runtime error signatures or
new warnings were accepted. The separately delivered final screenshot is
295,337 bytes, SHA-256
`ece79c8163a48bfa69e6abd0c0917ce32e9bb1112dda2eaaa98e5bbcc6170dfb`.
No human wheel/pinch hardware interaction or additional OS validation is claimed.

## Reachable-history and publication audit

The all-ref audit at documentation checkpoint
`af1537e958f58fb963d7ae8a7a5c9adf76bf675a` traversed **36 commits, 351 unique
blobs and 193 paths**, including all **101 new blobs** since exact M8
`a27657099623c211bf207e1e4cc4b057ead4c953`, with zero issues. Its
[machine-readable record](history-audit.json) checks raw-export hash/header,
credential-shaped content, prohibited private/build/binary paths, oversized
blobs, minimized historical-field whitelist, exact ancestry, and excluded
`843df64` unreachability. Canonical/frozen inputs and SDK properties remain
byte-identical. Prior milestone branches/remote-tracking refs are preserved.
No push, authentication retry, new remote publication or next milestone occurred.

This generic scan inherits M8's audit and its inherited M7/M6/prior exact-private-
reference checks. A new exact private display-name/coordinate-pair rescan is
**unavailable and not claimed**: the private 10,333-row cache was never transferred,
read, fetched or reconstructed in M9. Only approved minimized historical fields,
public address assertions, original HDB-derived points and evidence reports were
added. The all-ref scan is rerun on the final audit-record commit; that final
machine-readable result accompanies the handoff. Full-history bundle creation,
fresh restoration/fsck and Library delivery are separately recorded.
