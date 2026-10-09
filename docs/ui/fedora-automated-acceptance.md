# Fedora app-level acceptance tooling — 2026-10-09

This tooling branch overlays frozen Stage 3
`6ca00e51038811c305b77ab2db32061cf4650871` (tree
`29a15748f8e272ccc1a424fa34ef8f1547e6b856`), containing Stage 2
`c880920f76604337070ecc2ea376f7e29baa0b55`. It does not change either product
branch or fix product behavior. It supplements, rather than completes, the
physical acceptance recorded in draft PR #19. The merged record/helper ref is
`f5cfc0f6a8fe1855a09d87762509663adcd32880`; its helper blob is
`1d5d1205aa8400bc012a982a0adefc81e10fe484`. The fixture tree in both is
`ea671adbe6c0c3122f2daa8f3978c6ba44dacaea`.

## Instrumentation boundary

No new general-purpose test framework, Worker change, native input injector,
OS preference change or native Computer Use bypass is involved.

- `Main.qml`: stable `objectName` selectors; opt-in loaders for
  `acceptance-poc`, `acceptance-fixture`, `acceptance-production`; exclude
  those modes from the older smoke timer. Existing shortcuts, button handlers,
  focus policies, layout, model, accessibility behavior and rendering remain
  unchanged. Normal launches never load either new QtTest gate.
- `BuyerTrendChart.qml` and `InspectorSection.qml`: selectors only.
- Project file: include the two new QML files in the unchanged Bridge build.
- `AcceptanceGate.qml`: small proof using the existing QtTest types.
- `AcceptanceSuite.qml`: bounded phase gate following `KeyboardGate.qml`'s
  pattern. QtTest delivers keys, pointer clicks, wheel events and a mouse drag
  inside the application; width/height assignments exercise compact layout.
  No test calls these compositor-delivered input or frame resizing.
- `tools/native_acceptance/run.py`: local lifecycle/evidence orchestration;
  real GET-only production snapshots; a fresh external AT-SPI reader at each
  checkpoint; independent counts/names checked against saved API summaries.
- `atspi_probe.py`: system Python GI/AT-SPI2 reads the owned application's
  actual external accessibility tree, selected states, roles and names. It
  scopes discovery by process ancestry. It does not set QML Accessible values,
  invoke accessibility actions, enable Orca or infer speech. Its application
  root disables the reader cache; snapshots also clear individual cached
  entries. Whole trees are saved only on assertion failure.
- `verify_captures.py`: Pillow reads the unedited Qt item PNGs and checks
  opacity, a clipping sentinel, actual chart-boundary intersection and rendered
  link-colour pixels. These checks cover app rendering, not KWin decoration,
  compositor clipping, monitor crispness or every chart pixel.

Only the inspector Pane is captured with `QQuickItem.grabToImage`, including
its Qt style background. Map/OneMap tiles and native frames are outside that
subtree. No PNG, production JSON, log or snapshot is committed. QtTest's
`TestResult.grabImage(item)` initially returned the wrong framebuffer region;
that attempt is retained locally and is not a deliverable or accepted capture.

## Reproduce

Use the real logged-in Fedora KDE Wayland session and the existing pinned
installation. No `AGENTS.md` or applicable `.agents/skills` exists in these
checkouts. This session used Fedora 45 KDE prerelease, KWin 6.7.5, scale 1,
BreezeLight, Noto Sans 10; .NET SDK 10.0.401/runtime 10.0.12, official Qt
6.12.0 gcc_64 with Location/Positioning/ShaderTools/Graphs/Quick3D, Linux x64
Bridge 0.4.0-beta. Wayland plugin dependencies resolve. The session selected
no style override; Fusion loaded, with OpenGL scene graph rendering.

```sh
export DOTNET_ROOT=/home/sheng/.local/share/hdb-qt-toolchain/dotnet
export QtDir=/home/sheng/Qt/6.12.0/gcc_64
export PATH="$DOTNET_ROOT:/home/sheng/.local/share/hdb-qt-toolchain/venv/lib/python3.14/site-packages/cmake/data/bin:/home/sheng/.local/share/hdb-qt-toolchain/venv/bin:$PATH"
export QT_QPA_PLATFORM=wayland QSG_INFO=1
# Preserve the inherited QT_QUICK_CONTROLS_STYLE (unset on this host).
dotnet build -c Release -m:1
dotnet test -c Release --no-build -m:1
/usr/bin/python3 -m unittest discover -s tools -p 'test_*.py' -v

exe="$PWD/src/HdbResale.App/bin/Release/net10.0/HdbResale.App"
helper=/home/sheng/Documents/Codex/2026-10-09/task/hdb-fedora-record/tools/api_fixture_server.py
# Each output directory must be new; evidence is never overwritten.
/usr/bin/python3 tools/native_acceptance/run.py --executable "$exe" \
  --fixture-helper "$helper" --mode poc --output /tmp/hdb-poc-new
/usr/bin/python3 tools/native_acceptance/run.py --executable "$exe" \
  --fixture-helper "$helper" --mode fixture --output /tmp/hdb-fixture-new
/usr/bin/python3 tools/native_acceptance/run.py --executable "$exe" \
  --fixture-helper "$helper" --mode production --output /tmp/hdb-production-new
/usr/bin/python3 tools/native_acceptance/verify_captures.py /tmp/hdb-fixture-new
/usr/bin/python3 tools/native_acceptance/verify_captures.py /tmp/hdb-production-new
```

System `/usr/bin/python3` supplies Fedora's GI/AT-SPI and Pillow; the toolchain
venv's Python lacks GI. The runner sets `QT_LINUX_ACCESSIBILITY_ALWAYS_ON=1`
only for its child app and preserves the session's style selection. It logs
and asserts **`Qt.platform.pluginName === "wayland"`** inside the running app,
not merely the requested environment value. It starts one loopback fixture
server at a printed ephemeral port and stops only its own process groups.
Snapshots use the runbook's curl GET client: urllib requests returned HTTP
403/code 1010, whereas ordinary curl succeeded. No service policy was changed.

The current production expectations are snapshot-specific. Manifest reads
before/after summaries and after the last request must agree; the app's dataset
summary must contain that same timestamp. Derive new test values/cases before
running against a changed snapshot. Hardcoded production price/type/window
counts and chart values below are not universal invariants.

Gate verdicts are explicit: a failure dumps the QML tree and exits; known
independent contract findings emit `HDB_ACCEPTANCE_DEFECT` and allow subsequent
checks to run. `HDB_ACCEPTANCE_COMPLETE` means the sequence completed, not that
it passed. The Python runner returns **1** for any defect, failed AT-SPI
checkpoint, missing capture/marker, snapshot mismatch or failed app exit.
The two expanded suites therefore currently return 1. Nothing is marked
expected-failure or suppressed to manufacture an all-green result.

## Proof checkpoint

The four requested components ran before suite expansion:

1. Existing keyboard gate: exit 0, `HDB_API_KEYBOARD_PASS`.
2. A QtTest wheel event moved the real inspector's `contentY` from 0 to 193;
   the test did not assign a scroll offset.
3. Qt Quick rendered and saved the selected inspector subtree.
4. Independent AT-SPI2 read the actual app PID 983872: one
   `Matching addresses` list and the exact
   `748B BEDOK RESERVOIR CRES, BEDOK, median S$837,500, 10 sales, latest 2026-09`
   list item, selected and showing. The gate completed and exited 0.

The final implementation's `--mode poc` repeats all four successfully. An
inspector capture from the expanded proof is in the user's Library
(`inspector-top.png`); its private identifier is returned in the task. It
contains no map imagery. Initial timer/selector/reader-environment mistakes are retained
as failed harness attempts, not product findings.

## Final coverage and results

Production remained pinned at `2026-10-04T15:30:00.000Z`: 9,730 summaries and
9,297 default-eligible rows. Exact blocks 30 BALAM RD and 30 CASSIA CRES preceded
all 301/302/304/305 UBI AVE 1 prefix matches. Inspector/chart candidate is
121 ANG MO KIO AVE 3, postal 560121. The ordinary source APIs and saved detail
response were used; no Worker/database/snapshot publication occurred.

| App-level check | Fixture | Production |
|---|---|---|
| Actual runtime QPA `wayland`, load and expected count | PASS | PASS |
| Ctrl+F, typed search/order, postal and list type-to-search | PASS | PASS |
| Arrow/Return selection; retained refinement; hidden selection clearing; Escape | PASS | PASS |
| Town, flat type, minimum/maximum, reversed range, latest 12 months, Reset | PASS | PASS |
| Ctrl+L and compact filter focus; Back clears selection and focuses list; widening retains selection | PASS | PASS |
| Compact Map toggle moves focus to map | FAIL | FAIL |
| Compact Addresses toggle moves focus to Back | FAIL | FAIL |
| About shortcut isolation and Escape preserve query/results | PASS | PASS |
| About button close returns to prior list | FAIL | FAIL |
| Four inspector sections, fact geometry, 40% label cap, no text truncation | PASS | PASS |
| “Nearest MRT” on one line at default layout | FAIL | FAIL |
| Inspector wheel down/up, Home/End/PageDown, list Home/End/PageUp, no-flick mouse drag | PASS | PASS |
| Large-list wheel actually changes content position | — (six rows fit) | PASS |
| Chart series/counts/bounds/gaps, current palette link binding and secondary contrast | PASS | PASS |
| Opaque Qt captures; chart crosses both clipping edges; heading sentinel unchanged | PASS | PASS |
| External AT-SPI selected row, 4 named groups/headings, 14 facts, 20 registrations exactly once | PASS | PASS |
| External AT-SPI rows: initial, end, page-up, home | PASS | PASS |
| External AT-SPI rows after wheel | PASS | FAIL |

Rendered palette: window `#eff0f1`, text `#232629`, secondary `#606365`, link
`#2980b9`; Noto Sans 10. Pixel checks found 300 fixture and 133 production
link-colour pixels in the visible chart crop; the fixed heading sentinel above
the scroll area was byte-identical across both clipping captures. Data/series
checks and this pixel evidence do not independently prove every gap's raster
geometry or every platform appearance transition.

### Remaining findings and owning-stage proposals (not implemented)

1. **Stage 2 compact toggle focus (two assertions, both datasets).** At
   640 × 600 with a selection, click Map using QtTest. The map is visible and
   selection remains, but `activeFocusItem` is `mapToggle`, and map
   `activeFocus` is false. Click Addresses: focus stays on `addressesToggle`
   rather than Back. `ButtonGroup.onClicked` changes the index; deferred
   `keepFocusVisible` keeps the still-visible focused toggle. Proposed Stage 2
   correction: route explicit pane-toggle actions through the existing
   `showView` focus command, with its selected/unselected Addresses behavior.
   Test the same paths on each platform before changing that owning branch.
2. **Stage 2 About return focus (both datasets).** Focus the list with query
   `748`/`560121`, click the status-bar About button, send Ctrl+F/Ctrl+L then
   Escape. The modal correctly blocks the shortcuts and retains search/count,
   but focus returns to `aboutButton`. This differs from S2.10's prior-list
   contract. Proposed decision: preserve the originating content-region focus
   around toolbar About activation, or have the owner explicitly revise that
   contract if button-return is intended. Do not remove keyboard accessibility
   from About merely to make the assertion pass.
3. **Stage 3 label wrapping (both datasets).** At normal 1360 × 900, scale 1,
   Noto Sans 10/Fusion, the selected inspector's `Nearest MRT` fact label has
   width 79 and `lineCount=2`. The image shows “Nearest” above “MRT”. It is
   readable and not truncated, but fails S3.3's one-line expectation.
   Proposed Stage 3 investigation: compare the actual Label's effective font
   and width with the TextMetrics used by `measureLabels`, including rounding,
   then correct measurement/layout without breaking the 40% cap and wrapping
   contract. No guessed width or font override is included here.
4. **Stage 2/Qt external row exposure after large-list wheel (production).**
   Clear search after the modal case, focus the full list, Home, then QtTest
   wheel delta -480. At the held checkpoint, `40 TANGLIN HALT RD` is a QML row
   at viewport y=468 with height 54, well inside the list. Its exact expected
   name comes from pinned summaries, but it is absent from external AT-SPI
   list items; later rows are absent too. The failure reproduced with a fresh
   independent reader process and disabled reader caching, so it is not only
   an old polling-client cache. End/PageUp/Home checkpoints each expose their
   14 visible rows correctly. Investigate Stage 2 delegate lifetime and Qt's
   AT-SPI child registration/notifications; `reuseItems` is already false.
   This record does not attribute the lower-level cause or prescribe a pooling
   change. Preserve the missing-row failure until an owning-stage fix is
   independently verified. It does not establish an Orca speech failure.

## Validation and local evidence

Final Release build: zero MSBuild warnings/errors (Qt deployment still notes
missing translation locales). 298 C# tests, 38 Python tests and 2 native C++
tests passed. Python's first invocation used system dotnet and failed the five
platform-pin subcases; rerunning with the pinned PATH passed without changing
tests. All six original readiness modes pass on the instrumentation build.
The final PoC passes. Fixture suite completes with four contract assertions
failed and all six AT-SPI checkpoints passed; production completes with those
four assertions plus the wheel exposure failure (five of six external
checkpoints passed). Both pixel checks pass. These deliberately red acceptance
results must not be described as full acceptance.

Local workspace: `/home/sheng/Documents/Codex/2026-10-09/task/`.
Evidence: `evidence/acceptance-poc-final`, `evidence/acceptance-fixture-final`,
`evidence/acceptance-production-final`, `evidence/tooling-smokes`, plus
`tooling-build-final.log`, `tooling-tests-release.log`,
`tooling-python-final.log`, `tooling-ctest.log`, `tooling-sha256.txt`.
The two expanded runs each contain `inspector-top.png`,
`chart-bottom-clip.png`, `chart-top-clip.png`, capture geometry/palette metadata,
external AT-SPI results, failure-only tree dumps and app logs. Earlier attempts
remain separately named. Screenshots/data remain outside Git.

The frozen UI and record worktrees are preserved, as are unrelated work and
oxpinyin lanes. App/helper processes created by the runners are cleaned up;
no system appearance, scale, reader, security or input-source setting changed.
`QT_LINUX_ACCESSIBILITY_ALWAYS_ON` was a child environment variable only.

## Still unverified

KWin's client-category inspection and xlsclients negative check; compositor
keyboard/pointer delivery and window-frame resizing/close; inactive-window
appearance; Dolphin visual consistency; system Breeze Light↔Dark following
and scale/text-size changes; Orca announcements, settled-count speech and quiet
spoken refinement; genuine pinch/momentum and hardware crispness; map marker
pointer identity and map tile pixels; every latest-registration field compared
to the saved detail; full injected interactive Retry/recovery scenarios; and
RPM/DEB packages. Existing readiness fault modes are not those interactive
scenarios. No Mac result is carried into Fedora, and no app-level assertion
promotes a physical acceptance row to PASS.
