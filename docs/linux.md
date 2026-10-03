# Linux x64 verification — 2026-10-03

The actual C#/Qt application was built and run on Debian 13.6 x86_64 with an
XFCE/X11 desktop. Linux now selects published
`QtGroup.Qt.Bridge.CSharp.linux-x64` **0.4.0-beta**. All other existing package
pins, including macOS arm64 **0.4.0.22-beta**, remain unchanged. This is a
platform-specific selection, not an assertion that the packages contain the
same source revision. NuGet's Linux index did not contain 0.4.0.22-beta.
The user explicitly approved the Linux-only version change.

The other verified versions remain .NET SDK **10.0.401**, runtime **10.0.12**,
`net10.0`, Qt **6.12.0** `linux_gcc_64`, aqtinstall **3.3.0**, CMake **4.4.3**,
Ninja Python package **1.13.2**, Python **3.12.14**, and GCC **14.2.0**.
Qt's downloaded archive identifies a RHEL 9.6 build; its required runtime
libraries resolved on this Debian host. This is one verified host, not a claim
that every Linux distribution is compatible.

## Provision and run

Install the pinned Microsoft .NET SDK and a C++ toolchain. On Debian/Ubuntu,
the relevant development/runtime prerequisites include `build-essential`,
`libgl-dev`, `libglx-dev`, `libopengl-dev`, `libegl-dev`, `libxkbcommon-dev`,
`libvulkan-dev` and `libxcb-cursor0`. Use an ordinary graphical desktop terminal.
Qt Bridge's build tasks need local process IPC; a restricted shell that denies
Unix sockets can fail with MSB4216 even when native C++ compilation succeeded.
That environment failure must not be worked around by suppressing Bridge tasks.

```sh
python3 -m venv "$HOME/.local/share/hdb-qt-toolchain"
"$HOME/.local/share/hdb-qt-toolchain/bin/pip" install \
  aqtinstall==3.3.0 ninja==1.13.2 cmake==4.4.3
export PATH="$HOME/.local/share/hdb-qt-toolchain/bin:$PATH"
aqt list-qt linux desktop --arch 6.12.0
aqt list-qt linux desktop --modules 6.12.0 linux_gcc_64
aqt install-qt linux desktop 6.12.0 linux_gcc_64 -O "$HOME/Qt" \
  -m qtlocation qtpositioning qtshadertools
export QtDir="$HOME/Qt/6.12.0/gcc_64"
"$QtDir/bin/qmake" -query QT_VERSION
ldd "$QtDir/plugins/platforms/libqxcb.so"
ldd "$QtDir/plugins/geoservices/libqtgeoservices_osm.so"
# No "not found" entries are expected.

dotnet build -m:1
dotnet test --no-build -m:1
dotnet build -c Release -m:1
dotnet test -c Release --no-build -m:1
python3 -m unittest discover -s tools -p 'test_*.py' -v
dotnet run -c Release --project src/HdbResale.App --no-build
```

If .NET is installed into a custom directory, add it to `PATH` and set
`DOTNET_ROOT` to that SDK/runtime installation. No Linux bundle target or manual
native-host replacement was needed; the official Bridge package emits
`src/HdbResale.App/bin/{Debug,Release}/net10.0/HdbResale.App`.

The verification environment used user-local toolchain directories. Because
system package installation was unavailable, signed Debian repository packages
were downloaded and extracted into a local development prefix exposed through
`CMAKE_PREFIX_PATH`. aqt's multiprocessing orchestration also needed a local
socket, so its unchanged checksum-verifying archive worker was invoked
sequentially, followed by its normal relocation step. Archive/hash validation
was retained. These are provisioning adaptations, not application patches.
The actual build, native gates and physical UI ran in the graphical desktop
session with ordinary process IPC; **no offscreen platform was used**.

## Executed checks

- Root Debug and Release solution builds passed. MSBuild reported zero warnings
  and errors. Native configure/deploy still printed the existing private-header
  coupling/optional TaskTree notices and warnings about unavailable translation
  locales; none were suppressed.
- **46 C# tests passed in each configuration**, no skips. The Python suite has
  **14 passing tests**, including actual MSBuild property evaluation for Linux,
  macOS x64/arm64 and Windows x64/arm64 package selection. That one test is
  explicitly skipped when no .NET SDK is on PATH; it ran here.
- Canonical Debug/Release native gates passed all ten ordered transitions and
  clean native/C# teardown. Full-corpus Debug/Release scale gates passed all
  ten transitions and exact sidebar/address-marker cardinalities, including
  the maximum-budget 241,920-row / 1,921-marker phase. Both native scenarios
  additionally assert the ComboBox index/current text agrees with C# TownIndex
  and JSON town labels, guarding the earlier presentation-transport workaround.
- Both deliberate `skip-empty` Release faults were rejected: canonical phase 5,
  scale phase 4, harness exit 1 and no pass marker. Deadlines were unchanged.
- All four public source snapshots matched the existing full-corpus hashes.
  The regenerated full input manifest byte-matched the checked-in manifest.
  Actual Release native-host `--scale` output is retained in
  [linux-full.json](scale/linux-full.json); every nonmeasurement field in all
  three iterations matches [final-full.json](scale/final-full.json).
- Canonical six-row data, frozen 416-row inputs, historical projection/reports,
  matching rules and renderer/provider were unchanged. Final native-host
  regeneration byte-matched the frozen 416 report and reproduced historical M5
  SHA-256 `d76614762c351eb121d3dd11ed03658e53daae1a5feeae36809a029b7e62d4be`.
  Existing differential
  and aggregation tests passed.

```sh
exe="$PWD/src/HdbResale.App/bin/Release/net10.0/HdbResale.App"
python3 tools/native_gate.py --executable "$exe" --log /tmp/linux-canonical.log
python3 tools/scale_gate.py --executable "$exe" \
  --data /absolute/path/to/hdb-scale-full --log /tmp/linux-full.log
HDB_GATE_FAULT=skip-empty python3 tools/native_gate.py \
  --executable "$exe" --log /tmp/linux-negative-canonical.log
HDB_GATE_FAULT=skip-empty python3 tools/scale_gate.py --executable "$exe" \
  --data /absolute/path/to/hdb-scale-full --log /tmp/linux-negative-full.log
# Repeat the positive gates with Debug; each negative command must exit 1.
```

Full-corpus native observations are in
[linux-native.json](scale/linux-native.json). The final Release run took 6,864 ms construction;
initial QML readiness 2,488 ms; reset 45 ms in C# and 2,834 ms in QML.
Increasing to the corpus maximum took 87 ms inclusive C# reset and 3,743 ms
QML readiness for all 241,920 rows / 1,921 markers. The complete process took
19.41 seconds. These are
single observations on this cloud desktop, not direct controlled comparisons
with the Mac, isolated bridge/GPU timing or cold-tile measurements. The existing
5-second state and 25-second process bounds were met.

## Physical Linux desktop evidence

The final Release app was launched via `dotnet run` with `HDB_DATA_DIRECTORY`
pointing to the exact 241,920-row input. CUA mouse/keyboard input and screenshots
verified:

- Real Singapore OSM tile pixels, visible contributor attribution and numbered
  address markers. Default counts: 236,791 sidebar transactions, 51,784 located
  transactions in 1,920 markers, 185,007 unlocated.
- Budget entry **S$1,728,000**, the corpus maximum, exposed **all 241,920 rows**,
  52,514 located transactions in 1,921 markers, and 189,406 unlocated. Reset
  restored S$1m. This completes the previously pending above-S$1m check.
- The final fixed-width details text wrapped within the sidebar. Drag scrolling
  reached all property/ACRA/footprint provenance and the end of the diagnostic
  list, including feature 11283. No sizing/binding loop appeared.
- Town entry ANG MO KIO showed 9,441 rows / 279 markers. S$200,000 combined
  filtering showed 22 rows, 21 located in eight markers and one unlocated;
  previously selected HDB-2 cleared. Zero budget showed empty list/map and
  cleared selection. Reset repopulated the full default model.
- Sidebar HDB-2 selection showed 406 ANG MO KIO AVE 10 and its provenance.
  Unlocated HDB-54563 at 174 ANG MO KIO AVE 4 remained selectable with
  `Unmatched` / `Missing`, without an invented marker.
- Physical marker tap selected HDB-133343 at 132 CLARENCE LANE; its orange
  count-six address marker rose above overlap. Drag panned the map; the + button
  changed zoom 11 to 12; Singapore restored 11 / 1.3521, 103.8198.
- Native titlebar close removed the window and `dotnet run` exited **0**.

## Beta/runtime observations and limits

No reproducible upstream Bridge defect was established. Linux's older published
package successfully compiled the existing model API and passed the gates.
The missing `org.freedesktop.portal.Desktop` service produced two host-theme
warnings; controls, tiles and teardown still worked. No error check was weakened.

The final run had `QT_QPA_PLATFORM` unset, `DISPLAY=:0`, no `WAYLAND_DISPLAY`,
`XDG_SESSION_TYPE=x11` and `XDG_CURRENT_DESKTOP=XFCE`. Qt plugin logging
confirmed `libqxcb.so` and xcb GLX integration loaded. Actual human wheel input
was not tested. Injected wheel attempts had no observable effect on either the map or the
text panels in this automation session. **Wheel behavior is not verified**;
this does not establish a Qt defect. Button zoom and drag scrolling were usable
workarounds, so this did not block the validated workflow. Physical pinch,
Wayland, Linux arm64, Windows, packaged distribution and cold tile/network
performance remain unverified. The final macOS pixel recheck is still unrun;
Linux verification does not retroactively certify those Mac pixels.

## Quantitative load and state results

Milliseconds from the actual Release native-host `--scale` run, same pinned
241,920 rows, three iterations in one process. Iteration 0 includes first-run/JIT
cost; later iterations can reuse OS caches and retain prior garbage. These are
observations, not statistical guarantees or cold-start comparisons.

| Operation | First | Later 1 | Later 2 |
| --- | ---: | ---: | ---: |
| property-evidence | 99.1 | 72 | 28.6 |
| postal-evidence | 79.8 | 23.8 | 17.9 |
| footprints | 1359.6 | 1016 | 1009.1 |
| evidence-index | 24.9 | 15.9 | 14.2 |
| transaction-csv | 5054.7 | 7844.5 | 4565.5 |
| validation-facts | 383.7 | 175.9 | 166.1 |
| matching-resolution | 288.8 | 119.7 | 125.5 |
| transaction-domain | 22.6 | 19.1 | 20.5 |
| state-construction | 20 | 14.5 | 12.4 |
| town-filter | 17 | 16.8 | 13.6 |
| budget-filter | 16.8 | 12.1 | 10.9 |
| combined-filter | 13.9 | 15.9 | 10.8 |
| selection | 0.6 | 0 | 0 |
| empty-hidden-selection | 8.6 | 9.5 | 6.1 |
| reset | 15.7 | 11.6 | 5.9 |
| map-aggregation | 77.5 | 433.7 | 345 |

The sum of the eight instrumented import stages was **7,313.2 / 9,286.9 /
5,947.4 ms**; it excludes measurement-snapshot overhead, so it is not claimed as
an independent wall-clock startup stopwatch. CSV parsing dominates the measured
load. All accepted transaction rows survive; aggregation is over the real
52,514 located transactions / 1,921 addresses, not a synthetic all-row map.

Maximum **sampled** managed memory by iteration was **611.0 / 826.0 / 715.5 MiB**;
maximum sampled working set **687.6 / 974.7 / 1,064.1 MiB**. End-of-iteration
managed snapshots were **611.0 / 638.8 / 641.0 MiB** and working sets
**687.6 / 794.4 / 796.2 MiB**. These are GC.GetTotalMemory(false) and
Environment.WorkingSet snapshots, not allocated bytes, continuously measured
peaks or proven retained heap. No forced collection was used in this Linux run.

Native C# reset timings include filtering, address aggregation and synchronous
Bridge begin/end-reset notifications. QML timings include its observer cadence,
control bindings and map delegates. **Bridge-only marshalling cost is not
isolated**, and these inclusive timings must not be attributed wholly to Bridge
or the GPU. The JSON retains each observed transition and cardinality.

For this measured desktop workload, keep the in-memory model: filters were
about 6–17 ms and there is no demonstrated storage/query bottleneck that a
SQLite migration would fix. Transient load memory and CSV work remain sensible
next profiling targets. Qt Location handled the validated marker workload;
it is **not a demonstrated blocker requiring a renderer change**. Some full map
resets took several seconds, so this is usable completion evidence rather than
a claim of universally instant interaction. Renderer/provider/database changes
remain outside M6.

## All footprint diagnostics classified

[The per-feature audit](scale/footprint-diagnostics.md) identifies all 11 source
features and exact rejection predicates. Ten are topologically valid MultiPolygon
features outside the current Polygon-only scope. The remaining valid Polygon
has a present numeric ENTITYID of zero, which violates the app's positive-ID
contract. No member is missing, no malformed geometry or duplicate source key
explains these diagnostics, and no accepted-type importer mishandling was found.
Therefore no behavior-changing parser fix, identity relaxation, geometry repair
or expanded geometry support was introduced. All diagnostics remain visible.

## M11 additional chart modules

M11's owner-approved GPL-3.0-or-later application includes a small Qt Graphs 2D selected-address trend. Add the matching official **Qt 6.12.0** `qtgraphs` and `qtquick3d` modules to the existing installation. The official Graphs binary links Quick3D/RuntimeRender/Utils even for 2D; the app adds no 3D flow. `qtquicktimeline` was provisioned as an official recommended supporting module, but only actual scanner/ELF dependencies are included in the private package. QtShaderTools was already part of the pinned setup.

Use the existing aqt 3.3.0 environment, preserving the base installation and package hash verification. The module archive version was `6.12.0-0-202609280346`; no system ICU or obsolete binary compatibility package was installed. See [M11 licensing/packaging](product-rc/licensing-packaging.md) for the exact local closure and redistribution limits.
