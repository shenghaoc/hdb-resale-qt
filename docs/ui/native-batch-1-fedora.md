# Fedora batch 1 — readiness completed, native acceptance blocked

Date: 2026-10-09. This is a new Fedora run of the
[batch 1 runbook](native-batch-1.md), not a continuation of the Mac results.
**F1–F10 and every native platform row remain unverified.** Automated readiness
passed, but this task could not observe or operate native windows. No product
defect is established by that limitation. RPM/DEB acceptance was not attempted.

## Source and executable provenance

Separate checkouts under `/home/sheng/Documents/Codex/2026-10-09/task/`:

- `hdb-fedora-ui`: detached frozen Stage 3
  `6ca00e51038811c305b77ab2db32061cf4650871`, tree
  `29a15748f8e272ccc1a424fa34ef8f1547e6b856`. Stage 2
  `c880920f76604337070ecc2ea376f7e29baa0b55` was verified as an ancestor.
- `hdb-fedora-record`: documentation branch
  `docs/native-batch-1-fedora-20261009`, based on the merged record ref
  `f5cfc0f6a8fe1855a09d87762509663adcd32880`. The ordinary fixture server
  came from this checkout, whose helper blob is
  `1d5d1205aa8400bc012a982a0adefc81e10fe484`.
- Both refs' `tests/fixtures/worker-api` trees equal
  `ea671adbe6c0c3122f2daa8f3978c6ba44dacaea`.
- The record ref is not the frozen UI source. Its smoke script lacks keyboard
  mode; all six smoke modes used the frozen UI's script. Its enhanced helper
  was used for the ordinary fixture launch, not copied into the frozen tree.
- No applicable `AGENTS.md` or `.agents/skills` was found in the checkout
  paths or their applicable parent paths. The original repo and stack
  worktree were inspected and preserved; unrelated work and oxpinyin lanes
  were not changed. The new clone has its own Git storage and detached UI
  worktree. Frozen tracked files remain clean.

Release executable:
`hdb-fedora-ui/src/HdbResale.App/bin/Release/net10.0/HdbResale.App`.
SHA-256 values from this build:

| File | SHA-256 |
|---|---|
| `HdbResale.App` | `eba3a53826afdb2253117239f04fea9b65eeab87346aa77d65b2de7d19dc2fc1` |
| `HdbResale.App.dll` | `9406da1723bcdc2ef0485ccfa50a8372b13eadfbe8c267b096324ad94fe90070` |
| `hdb_resale_app` | `b7e731e2326e3faad036d6d21c14c7f3429dcb1201fba3b020a4ebdfba0e52b1` |
| `libhdb_tile_status.so` | `0ae5533ded4187ca6d65704ba85a69ad42a3573131050c2a3177033460016e84` |

## Host and readiness

- Fedora Linux 45 KDE Plasma Desktop Edition Prerelease, x86-64;
  kernel `7.2.9-300.fc45.x86_64`. KWin `6.7.5` reports
  `Operation Mode: Wayland`; shell has `XDG_SESSION_TYPE=wayland`,
  `WAYLAND_DISPLAY=wayland-0`, `DISPLAY=:0`.
- Pinned toolchain: `DOTNET_ROOT=/home/sheng/.local/share/hdb-qt-toolchain/dotnet`,
  SDK `10.0.401`, runtime `10.0.12`, `QtDir=/home/sheng/Qt/6.12.0/gcc_64`.
  The Linux x64 Bridge asset resolves to `0.4.0-beta`. Pins and Bridge tasks
  were unchanged. The installed Qt module metadata and shared libraries
  identify Location, Positioning, ShaderTools, Graphs and Quick3D as `6.12.0`
  in the matching official Qt installation. `ldd` on `libqwayland.so` has no
  unresolved libraries.
- PATH prepends the pinned .NET directory,
  `/home/sheng/.local/share/hdb-qt-toolchain/venv/lib/python3.14/site-packages/cmake/data/bin`
  and that venv's `bin`. Python discovery used system Python `3.15.0rc3`.
- All app launches set `QT_QPA_PLATFORM=wayland`, `QSG_INFO=1` and
  `QT_LOGGING_RULES=qt.quick.controls*=true`. The inherited
  `QT_QUICK_CONTROLS_STYLE` was unset and remained unset. Logs explicitly
  resolve **Fusion** and report `Creating QRhi with backend OpenGL`.
- Read-only session queries report **BreezeLight**, **Noto Sans 10**, and
  eDP-1 **1920 × 1080 at 144 Hz, scale 1**. These configuration values do not
  prove visual consistency with Dolphin or correct palette/font following.

Commands ran in the frozen UI checkout unless stated otherwise:

```sh
dotnet build -c Release -m:1
dotnet test -c Release --no-build -m:1
python3 -m unittest discover -s tools -p 'test_*.py' -v
ctest --test-dir src/HdbResale.App/obj/Release/net10.0/qt/native/build --output-on-failure
# Also run Python discovery in the record/helper checkout.
for mode in keyboard recorded high-zoom unreachable tile-failure production; do
  python3 tools/api_native_smoke.py --executable "$exe" --mode "$mode" \
    --log "$evidence/smoke-$mode.log"
done
```

The first sandbox build failed at Bridge task-host IPC and reported NuGet
network/audit failures. With approved host access, Release build exited 0
with four NU1900 audit warnings retained; Qt deployment also emitted missing
translation-locale warnings. A subsequent `dotnet restore --force` with host
network access succeeded; the final Release build reported **zero MSBuild
warnings/errors**, with identical apphost, managed DLL, native executable and
tile-library hashes. The initial logs and Qt translation-locale diagnostics
remain retained; no audit or test was disabled. Tests: **298 C#
passed, zero skipped; 38 frozen-UI Python passed; 42 record/helper Python
passed; 2 native C++ tests passed**. Initial sandbox Python errors were socket
permission failures; the host-access rerun passed, including the reserved-port
refusal test. The Mac timeout does not carry into this Fedora result.

| Readiness mode | Exit | Evidence |
|---|---:|---|
| keyboard | 0 | `HDB_API_KEYBOARD_PASS` |
| recorded | 0 | shell, data, map and chart readiness markers |
| high-zoom | 0 | `HDB_API_HIGH_ZOOM_PASS` |
| unreachable | 0 | `HDB_API_UNREACHABLE_PASS` |
| tile-failure | 0 | `HDB_API_TILE_NOTICE_PASS`; 121 tile requests answered 503 |
| production | 0 | shell, data, map and chart readiness markers |

Each mode has an independent log/result and exit entry. The runbook's QML
warning/error scan, excluding `qml: HDB_` markers, returned no matches.
These are readiness checks: keyboard input is in-process; map/chart markers
are not pixel evidence, and none establishes physical native acceptance.

## Ordinary launches and native-tool blockers

Two bounded ordinary launches used the same Release host: healthy fixture
at the enhanced helper's printed `http://127.0.0.1:18719/`, then production
with all API/tile overrides removed. Neither set `HDB_PACKAGE_SMOKE` nor
`HDB_API_GATE`. Both remained alive for 12 seconds. `WAYLAND_DEBUG=1` logs
show binding `xdg_wm_base`, creating an `xdg_surface`/`xdg_toplevel`, and
`set_title("HDB Resale Explorer")`, alongside Fusion/OpenGL diagnostics.
This is direct Wayland protocol evidence, but it does not replace the
runbook's required KWin console and xlsclients observations.

`qdbus-qt6 org.kde.KWin /KWin showDebugConsole` returned 0 during the fixture
launch. The console's Windows tab could not be viewed, so an HDB entry under
Wayland rather than X11 was **not observed**. `xlsclients` is absent from
PATH/the expected system executable location, so its negative-client check
was **not performed**. No package was installed to alter the host.

The task's supported Computer Use configuration explicitly states **native
computer APIs are disabled**. Its actual `cua.getState()` inventory returned
`apps: []` and browser tabs only. Generic documentation describes native
methods, but that does not enable this task's disabled native surface.
No browser/cloud desktop, custom input injector, alternate screenshot
protocol or hidden accessibility control was substituted. Native screenshots,
window-frame actions, keyboard, pointer, wheel, System Settings and Orca
speech therefore could not be observed. Genuine pinch/momentum also needs
physical input or a supported real gesture API, never synthetic wheel.

Both ordinary app process groups and the fixture server were stopped by
this task's scoped SIGTERM cleanup. The app exits were `-15`, **not** clean
native-close acceptance. The process check afterwards found no process using
these checkouts. The requested KWin console was opened; its visibility/close
could not be verified through native tools and an operator may need to close
it. No appearance, scale, input-source, reader or security setting was changed;
no restoration of those settings was necessary. An AT-SPI reader-state query
returned an absent object/method and did not establish Orca's current state.
No reader toggle or accessibility permission change was attempted.

## Production preparation, not a native pass

GET-only responses are retained locally, not committed or published. Manifest
reads before summaries, after summaries and after the selected detail all
report `2026-10-04T15:30:00.000Z`. Saved summaries contain **9,730 total** and
**9,297 default-eligible** (`medianPrice <= 1000000`). Native About was not
observable, so the required match between the app and pinned snapshot remains
unverified and must be established on resume.

The current summary block fields give `geylang 30`: exact **30 BALAM RD** and
**30 CASSIA CRES**, versus prefix **301/302/304/305 UBI AVE 1**. This establishes
an available test case, not observed UI order. Selected detail candidate:
`ang-mo-kio-121-ang-mo-kio-ave-3`, postal `560121`, with a qualifying 4 ROOM
cohort, MRT and model. Its saved trend over 2024-11–2026-10 has 11 sales in
8 observed months, interior gaps, first dot 2024-12/S$400,000, last
2026-09/S$390,000, highest 2025-09/S$425,444, lowest 2026-04/S$360,000;
expected y axis 300–500 thousand. No registrations or chart were visually
compared. Preparation yields price-cap 500,000 count 2,569; minimum 500,000
with default cap count 6,787; latest 12 months count 7,517. Re-pin the snapshot
before the native pass instead of relying on this dated preparation.

## Per-step acceptance ledger

**U = UNVERIFIED** in the following tables. Every row needs the missing native
screen/input surface and required backend observations. More specific gaps
are listed to prevent the readiness results from becoming implied passes.

| Step | Fixture | Production | Outstanding observation |
|---|---|---|---|
| S2.0 | U | U | Native frame, list/status/map identities, healthy basemap; production About snapshot |
| S2.1 | U | U | Actual Ctrl+F focus and selected text |
| S2.2 | U | U | Native typed query, ordering and marker identity |
| S2.3 | U | U | Native selection, active/inactive ring and palette |
| S2.4 | U | U | Retained selection and quiet Orca refinement |
| S2.5 | U | U | Hidden selection clears list/details/map together |
| S2.6 | U | U | Escape sequence and focus |
| S2.7 | U | U | Native type-to-search and postal identity |
| S2.8 | U | U | Hidden-by-filter explanation and Clear Search |
| S2.9 | U | U | Ctrl+L town focus and compact expansion |
| S2.10 | U | U | About, modal shortcut isolation and focus restoration |
| S2.11 | U | U | Frame resizing, Map-specific focus, master/detail/Back/filter focus, widening preservation |
| S2.12 | U | U | Actual Orca row, selection and settled-count speech |
| S2.13 | U | U | Each filter, counts, exclusions, map identities and Reset |
| S2.14 | U | U | Pointer marker selection and Show on map |
| S2.15 | U | U | Frame close and Alt+F4, separate exit-0 launches |
| S3.1 | U | U | Sales facts, IQR, labels, alignment and note |
| S3.2 | U | U | Cohort facts change, all-types IQR/chart/registrations stay |
| S3.3 | U | U | Address facts and one-line labels |
| S3.4 | U | U | Lease/current-year figure, coordinates and captions |
| S3.5 | U | U | Light palette, crispness, chart values and interior gaps |
| S3.6 | U | U | Chart clipping in both scroll directions |
| S3.7 | U | U | All 20 registrations in order against saved details |
| S3.8 | U | U | Empty chart with four inspector groups |
| S3.9 | U | U | Native Breeze Dark following and readable link/secondary colours |
| S3.10 | U | U | One native scale step, wrap/clipping/crispness and restoration |
| S3.11 | U | U | Spoken groups/headings, Label:value once, registrations once |
| S3.12 | U | U | Real wheel, keyboard, pointer drag and trackpad/momentum |
| S3.13 | U | — | Ordinary delayed detail-failure selections, Retry loading and new request |
| S3.14 | U | — | Ordinary refusing endpoint Retry and recovery on the same port |
| S3.15 | U | — | Ordinary tile failure plus search/selection/details and both palettes |

The three injected interactive fault sessions were not run without a usable
native interaction surface; the corresponding readiness gates do not cover
Retry or continued interactive operation.

| Scenario | Fixture | Production |
|---|---|---|
| F1 Launch and load | UNVERIFIED | UNVERIFIED |
| F2 Filters | UNVERIFIED | UNVERIFIED |
| F3 Search | UNVERIFIED | UNVERIFIED |
| F4 Selection sync | UNVERIFIED | UNVERIFIED |
| F5 Details | UNVERIFIED | UNVERIFIED |
| F6 Loading and errors | UNVERIFIED | UNVERIFIED (healthy-load criterion only) |
| F7 Scrolling | UNVERIFIED | UNVERIFIED |
| F8 Chart | UNVERIFIED | UNVERIFIED |
| F9 Compact layout | UNVERIFIED | UNVERIFIED |
| F10 About and modal isolation | UNVERIFIED | UNVERIFIED |

| Native platform item | Result |
|---|---|
| KWin Wayland category plus absent xlsclients entry | UNVERIFIED; protocol evidence available, required observations missing |
| Session style, palette/font consistency beside Dolphin | UNVERIFIED; Fusion logged, visual comparison missing |
| Ctrl+F/Ctrl+L/About/native modal isolation | UNVERIFIED |
| Plasma frame resizing and compact focus | UNVERIFIED |
| Real Wayland keyboard/pointer/wheel/trackpad | UNVERIFIED |
| OSM imagery, markers and highlight | UNVERIFIED |
| Breeze Light/Dark and one scale step | UNVERIFIED |
| AT-SPI/Orca speech | UNVERIFIED |
| Native title/frame and clean close | UNVERIFIED; protocol title alone insufficient |
| RPM/DEB packages | UNVERIFIED; separate milestone |

## Evidence and resume handoff

Local evidence directory:
`/home/sheng/Documents/Codex/2026-10-09/task/evidence/`.
It holds build/test logs, module/dependency and executable provenance,
independent smoke logs/results/exits, ordinary Wayland protocol logs, the
scoped launch/cleanup script, original display/appearance queries and saved
production JSON/derived cases. No screenshot was captured, so none of the
required light/dark S2.3/S2.8/S2.11/S3.1/S3.5/S3.6 in both directions/S3.7/S3.9
captures exists for this run. No screenshot or snapshot is committed.

Resume on this host with an enabled supported native screen/input surface or
an operator; obtain the xlsclients prerequisite. With an ordinary app running,
inspect KWin's Windows tab under Wayland and run the negative xlsclients
check. Then execute the full step ledger in fixture and freshly pinned
production, including all three fixture fault sessions using the enhanced
helper's exact printed endpoints. Record original settings before native
Breeze Light/Dark and scale changes, compare beside Dolphin, obtain the
required captures, and restore settings. Actual Orca announcements and real
pinch/momentum require direct observation; if new security/accessibility
permissions are needed, obtain specific approval. Readiness completion is
not permission to mark these rows passed.

No UI source, toolchain pin, Bridge task, deployment, database or published
snapshot changed. This record is intended for a draft documentation PR;
no review request or merge is authorized.
