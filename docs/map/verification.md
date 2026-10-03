# M7 Linux verification — 2026-10-03

Source checkpoint: `a9234c05776fa94284901d329760a3d2b5a8f46f`, descended from
M6 `820532a670c03618572db2ee2a0b1efd984b0cb8` on `milestone-7-map`.
Native execution used the same Debian 13.6 x86_64 / XFCE X11 desktop, .NET
10.0.401/10.0.12, Qt 6.12.0 and published Linux Bridge 0.4.0-beta as M6.
The macOS 0.4.0.22-beta pin is unchanged. No M7 macOS or Windows build/UI claim
is made. Builds and native gates ran through the graphical terminal with normal
IPC; the restricted shell cannot run Bridge's IPC-dependent build tasks.
No offscreen platform, alternate native host or warning suppression was used.

## Executed automated checks

- Root Debug and Release solution builds: passed, zero MSBuild warnings/errors.
  Existing native private-header/optional TaskTree/translation notices remain.
- Root C# tests: **55 passed in each configuration**, no skips/failures. Five new
  pure presentation-planner tests cover exact/repeated 1,921-address updates,
  minimal inserts/removals, duplicate/order validation and changed role selection.
  Four additional FIFO tests cover reentrancy, composed filter intents,
  intermediate selection clearing and failure recovery. A Python test pins the
  unchanged official logo and its local Qt-resource wiring.
- Python unittest discovery: **19 passed**, including actual platform-pin
  evaluation and rejection of incomplete/incorrect classic and extended gates.
- Canonical Debug/Release native gates: all ten transitions and clean C# exit.
- Full-data classic Debug/Release gates: exact list/map cardinalities and every
  displayed marker field against the complete target projection (not a partially
  updated backing list), plus retained QML object identity. Both passed the
  original **5-second state /25-second process** acceptance bounds.
- Full-data extended Debug/Release gates: all sixteen ordered transitions,
  including all-price full→town subset→different town→empty→reset→all-price
  full→selection→hide selected town→full and repeated filters, zoom/pan/recenter.
  Every surviving key remains the same QML object; no duplicate/stale delegates
  or selection survived incorrectly. Same 5-second state bound; separate
  45-second process bound for the longer scenario.
- Native reentrant Debug/Release gates: a real `rowsAboutToBeRemoved` callback
  enqueued four UI intents while Bridge synchronized the outer removal. All
  intents drained in order, empty-state selection cleared, and the final full
  241,920-row /1,921-marker state was exact. A separate step hid the selected
  transaction while retaining its address and verified that the very same QML
  object lost its highlight. Both gates passed 5-second states/25-second process.
- Deliberate Release `skip-empty` faults: canonical phase 5, classic phase 4 and
  extended phase 4 all rejected with harness exit 1 and no pass marker. The new
  `skip-burst` reentrant negative also rejected at phase 6, with no burst or pass.
- Classic whole-process times: Debug **14.91s**, Release **14.55s**. Extended:
  Debug **22.70s**, Release **21.90s**. Reentrant: Debug **15.84s**, Release **16.63s**. All include import/startup and teardown.
- Four full-input derived SHA-256 hashes matched M6. The final native-host
  `--scale` report reproduced every nonmeasurement value in all three iterations:
  **241,920 transactions, 9,755 addresses, 52,514 located transactions,
  1,921 located address markers**, 63 ambiguous transactions and 189,406
  unlocated transactions. No accepted transaction was dropped.
- Six-row fixture, frozen 416 inputs/reports, domain/parser/matching/geometry,
  historical projection and SDK pins are unchanged from M6. Native 416 report
  byte comparison passed. Native M5 report byte comparison passed with SHA-256
  `d76614762c351eb121d3dd11ed03658e53daae1a5feeae36809a029b7e62d4be`.
- All eleven M6 footprint diagnostics remain: ten valid unsupported MultiPolygons
  and one positive-ID contract rejection for present numeric ENTITYID=0.

## Like-for-like map measurements

[Machine-readable observations](linux-measurements.json) retain stage and
lifecycle events. The pair below used **the same final Release code, OneMap
provider/cache, compact marker styling, controls, full corpus, identity assertions
and lifecycle instrumentation**. Only the scale-gate-only reset/incremental
strategy switch differs. Runs were sequential single observations with previously
used tile caches, not cold-network tests, randomized trials or guarantees.

| Transition | Full reset QML ms | Incremental QML ms | Incremental created / destroyed |
|---|---:|---:|---:|
| Initial default load (1,920 markers) | 2,609 | 2,101 | 1,920 / 0 |
| Town (279 markers) | 750 | 773 | 0 / 1,641 |
| Town + S$500k (264 markers) | 394 | 383 | 0 / 15 |
| Empty | 263 | 227 | 0 / 264 |
| Empty → default reset | 1,327 | 1,893 | 1,920 / 0 |
| S$1m → S$1.728m, all 1,921 markers | 1,971 | 196 | 1 / 0 |

The strongest evidence is the **eliminated delegate churn**: all-price expansion
previously destroyed 1,920 and created 1,921 delegates. It now inserts one,
updates 149 retained address summaries and preserves all 1,920 existing QML
objects. Town filtering retains 279 objects. Budget filtering retains 264,
removes 15 and updates 198 summaries. Empty→full still requires all delegates;
its different observed duration is not claimed as an algorithmic improvement.
Town/reset did not become faster in this pair. There is no universal speedup
claim, and startup differences cannot be attributed to incremental updates.

For the final all-price pair, detailed C# stages were:

| Stage, ms | Full reset | Incremental |
|---|---:|---:|
| Filtering | 7.015 | 11.119 |
| Address aggregation | 51.736 | 56.817 |
| Edit planning | 0.004 | 2.503 |
| Synchronous map notifications + backing-list mutations | 14.079 | 42.015 |
| C# through models, before property notifications | 76 | 116 |
| C# inclusive property/revision notifications | 94.670 | 135.422 |
| QML revision observer, from transition start | 108 | 147 |
| Last delegate creation, from transition start | 1,940 | 154 |
| QML completed-state assertion, from transition start | 1,971 | 196 |

Aggregation code is unchanged; its timing difference is observational. The
incremental notification stage can be more expensive than one reset signal,
but avoids re-creating the map objects afterwards. This supports the small
stable-key update strategy without changing renderer or database architecture.
The prior M6 all-price observation (87ms C# /3,743ms QML) used OSM, larger pins
and older instrumentation, so it is context, **not a controlled comparison**.

C# `after-csharp-ms` in the raw observer event is measured from a timestamp set
after state/model and primary property notifications, immediately before the
revision notification; it is not the managed method-return time. QML timestamps
include the 25ms observer cadence, JavaScript assertions, control bindings and
lifecycle hooks. Notification timing includes synchronous Bridge work and list
mutation; it does not isolate marshalling. No GPU frame time, tile paint latency
or renderer-only duration was measured.

### Baseline failures and instrumentation limits

The first unchanged-reset OneMap run missed the original 5-second initial
readiness bound. A second missed reset with only 1,587/1,920 delegates created.
MapReady was true, error zero, and inspected marker fields matched. These failed
runs were retained as failures, not accepted by increasing the ordinary deadline.
Explicit diagnostic `--measurement` uses 10-second states /45-second process to
capture completion; it is never reported as ordinary acceptance.

Before density/control refinements, the diagnostic baseline measured all-price
QML **5,210ms**, C# pre-property **111ms**, with 1,921 created /1,920 destroyed.
A subsequent lifecycle-hooks-off run measured **4,929ms**; default reset was
4,300ms with hooks and 3,033ms without. Sequential cache/machine-load differences
confound that pair, so it does not isolate callback overhead. Callbacks contain
only counter/timestamp updates; identity scans happen after cardinalities agree,
not per creation. Final paired measurements above use the same hooks and code.

An early extended test timed out during its expected-count oracle: a computed
alternate-town label was evaluated inside every transaction predicate. This
introduced O(n²) test bug was fixed by evaluating it once and caching immutable
town labels. Both final extended gates passed without deadline changes. These
were application/test issues; no reproducible upstream Qt/Bridge bug is asserted.

### Review-driven correctness fixes

An independent review reproduced a stale removal-range exception by reentering
`SetMaximumPrice(0)` during a `BeginRemoveRows` callback. Bridge's synchronized
notifications can process QML events, so an unguarded precomputed edit plan was
unsafe under overlapping inputs even though sequential gates passed. The fix
serializes complete UI mutation intents in a FIFO, through all notifications.
Town/Price values are composed when each intent executes; intermediate empty
states still clear selection. The real native callback test above verifies that
fix rather than relying only on a mocked planner. Exceptions are propagated.
The independent post-fix harness also passed seven real-Bridge notification
hooks: sidebar begin/end reset, map begin/end remove, map begin/end insert, and
Town property notification. Each injected an ordered seven-action burst and
verified composed filters, independently expected keys/IDs/counts and selection.

Review also identified the hotlinked logo's network/cache weakness. It is now
bundled unchanged with source/hash provenance, rendered through `qrc:` and
required to reach `Image.Ready` in both native scenarios. No offline tile bundle
was added, and no offline GUI/network benchmark is claimed.

## Reproduce on the native Linux desktop

Use [the pinned Linux setup](../linux.md), an ordinary graphical terminal and
an absolute path to the unchanged full-data directory. Unset old gate/fault
variables before ordinary use.

```sh
dotnet build -m:1
dotnet test --no-build -m:1
dotnet build -c Release -m:1
dotnet test -c Release --no-build -m:1
python3 -m unittest discover -s tools -p 'test_*.py' -v
exe="$PWD/src/HdbResale.App/bin/Release/net10.0/HdbResale.App"
python3 tools/native_gate.py --executable "$exe" --log /tmp/m7-canonical.log
python3 tools/scale_gate.py --executable "$exe" --data /absolute/full-data --log /tmp/m7-classic.log
python3 tools/scale_gate.py --executable "$exe" --data /absolute/full-data --extended --log /tmp/m7-extended.log
python3 tools/scale_gate.py --executable "$exe" --data /absolute/full-data --reentrant --log /tmp/m7-reentrant.log
# Repeat positive gates with the Debug native host. Each negative must exit 1.
HDB_GATE_FAULT=skip-empty python3 tools/native_gate.py --executable "$exe" --log /tmp/m7-negative-canonical.log
HDB_GATE_FAULT=skip-empty python3 tools/scale_gate.py --executable "$exe" --data /absolute/full-data --log /tmp/m7-negative-classic.log
HDB_GATE_FAULT=skip-empty python3 tools/scale_gate.py --executable "$exe" --data /absolute/full-data --extended --log /tmp/m7-negative-extended.log
HDB_GATE_FAULT=skip-burst python3 tools/scale_gate.py --executable "$exe" --data /absolute/full-data --reentrant --log /tmp/m7-negative-reentrant.log
# Explicit diagnostic comparison, not acceptance-deadline substitution.
HDB_MAP_UPDATE=reset python3 tools/scale_gate.py --executable "$exe" --data /absolute/full-data --measurement --log /tmp/m7-reset.log
HDB_MAP_UPDATE=incremental python3 tools/scale_gate.py --executable "$exe" --data /absolute/full-data --measurement --log /tmp/m7-incremental.log
HDB_DATA_DIRECTORY=/absolute/full-data dotnet run -c Release --project src/HdbResale.App --no-build
```

## Physical Release desktop verification

Actual CUA screenshot/mouse/keyboard checks used the pinned full corpus and the
native Release application. The final post-review build was rechecked after
FIFO serialization and local logo bundling; native close exited **0**.

- Actual OneMap Singapore tile pixels, the bundled official logo and linked
  exact attribution were visible. Zoom 11 showed compact ordinary dots; zoom 13
  showed larger count markers. A selected address remained orange, raised and
  labelled at low zoom.
- S$1,728,000 displayed all **241,920 transactions /1,921 markers**. HDB-2 sidebar
  selection showed its address/provenance and orange count-22 address marker.
  Selecting ANG MO KIO at that full budget retained HDB-2, **9,745 transactions
  /279 markers**. Zero budget cleared selection and map; Reset restored
  **236,791 /1,920**. Returning to full budget and selecting HDB-2 worked again.
- Earlier ordinary checks on the same incremental/density implementation verified
  a physical map tap selecting latest HDB-170137 at 338 ANG MO KIO AVE 1 (count 2),
  wrapped provenance through its final footprint caveat, BEDOK hiding selection,
  selectable unlocated HDB-58 without an invented marker, S$300k BEDOK yielding
  1,718 rows/zero markers, repeated town/filter/reset inputs, panning, button zoom
  and recenter. The later native reentrant gate separately covers overlapping
  inputs; deliberate physical clicks alone are not that proof.
- The final screenshot's SHA-256 is
  `95c4be2b1da90c1e1ec92feeb2227c64dc0c3d7c884894ce68fa535a680ff436`.
  Screenshots are separate deliverables rather than embedded in repository history.

An earlier accessibility-tree observation session logged 3,546 AT-SPI
“Could not find accessible on path” warnings around delegate removals. Ordinary
visual/input behavior still worked. The final screenshot/input-only session
logged none, only the two existing missing-desktop-portal theme warnings. This
points to observation-context dependence, but does not prove the underlying
cause or certify screen-reader behavior. No warning pattern was suppressed and
no upstream defect is claimed.

Actual human wheel input was unavailable; earlier injected wheel attempts remain
inconclusive. Physical pinch, Wayland, Linux arm64, macOS/Windows M7 GUI,
packaged distribution, full accessibility and cold tile/network performance are
unverified. Button zoom, drag pan and recenter were physically verified.

## Bounded MapLibre experiment and recommendation

The [MapLibre spike](maplibre-spike.md) includes a small separate C++ loader/QML
prototype, deterministic synthetic-geometry generator and auditable evidence.
The exact Qt 6.12 host compiled; the official stable plugin binary built for Qt
6.7.3 failed loading because ICU 56 is unavailable. No unsupported symlinks,
downgrades, native-core build or production dependency was introduced. No
MapLibre rendering, real-HDB polygon behavior or performance comparison was
established. This is a binary-dependency blocker, not proof that the source
cannot support Qt 6.12. Keep the verified Qt Location path for the next milestone
unless richer layers justify separate exact-version qualification and real-data
comparison. M8 has not started.

## History and publication audit

M7 starts from exact M6 `820532a`, preserving both unpublished Linux commits.
`main` and remote refs were not changed; no push/authentication retry occurred.
Local M7 commits and the final full-history bundle remain publication-pending.

The final audit traverses every reachable commit tree/blob, checking raw-export
hash/header, credential-shaped content, prohibited local/build/binary paths,
large blobs, unchanged historical-projection field whitelist, M6 ancestry and
excluded `843df64` unreachability. Every new public source, official logo,
prototype and documentation/evidence blob is reviewed. Canonical/frozen/domain
content is unchanged. The audit inherits the M6 audit; an exact private
cache-display-name/full-precision-coordinate rescan is **not rerun**, because
the private raw reference was not transferred. No raw export, cached names,
coordinates from that export, secrets, downloaded plugin binaries or generated
synthetic geometry are included. The machine-readable final audit accompanies
the handoff; the previously preserved M6 bundle is untouched.
