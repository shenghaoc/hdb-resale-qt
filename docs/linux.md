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
  nine transitions and exact sidebar/address-marker cardinalities.
- Both deliberate `skip-empty` Release faults were rejected: canonical phase 5,
  scale phase 4, harness exit 1 and no pass marker. Deadlines were unchanged.
- All four public source snapshots matched the existing full-corpus hashes.
  The regenerated full input manifest byte-matched the checked-in manifest.
  Actual Release native-host `--scale` output is retained in
  [linux-full.json](scale/linux-full.json); every nonmeasurement field in all
  three iterations matches [final-full.json](scale/final-full.json).
- Canonical six-row data, frozen 416-row inputs, historical projection/reports,
  matching rules and renderer/provider were unchanged. Existing differential
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
[linux-native.json](scale/linux-native.json). Release construction took 6,458 ms;
initial QML readiness 2,709 ms; reset 59 ms in C# and 3,501 ms in QML. These are
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

Injected wheel attempts had no observable effect on either the map or the
text panels in this automation session. **Wheel behavior is not verified**;
this does not establish a Qt defect. Button zoom and drag scrolling were usable
workarounds, so this did not block the validated workflow. Physical pinch,
Wayland, Linux arm64, Windows, packaged distribution and cold tile/network
performance remain unverified. The final macOS pixel recheck is still unrun;
Linux verification does not retroactively certify those Mac pixels.
