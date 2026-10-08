# Native UI acceptance: macOS and Linux KDE

The native-first UI redesign is accepted on **two platforms with equal standing**:
macOS and Linux KDE Plasma on Wayland. The owner develops on Fedora KDE; the
checked-in Linux records are a Debian 13 XFCE/X11 run ([docs/linux.md](../linux.md))
and a Fedora KDE run through XWayland ([docs/pr8-review-verification.md](../pr8-review-verification.md)).
No native KDE/Wayland acceptance has been recorded yet. The redesign emphasises
macOS. Neither platform is secondary.

Acceptance is recorded per platform. A scenario that passed on one platform
is not accepted on the other, and the record says so. Three outcomes exist for
every scenario on every platform: **passed**, **failed**, **unverified**.

## What establishes acceptance

- A real desktop session on the platform: Cocoa on macOS, a KDE Plasma
  Wayland session on Linux. Interaction through the real menu bar, keyboard and
  pointer.
- The platform's own Qt style, with nothing set by the application: the
  `macOS` style on macOS; on Plasma, whatever the session selects for Qt Quick
  Controls applications. Plasma exports `QT_QUICK_CONTROLS_STYLE=org.kde.desktop`
  when qqc2-desktop-style is installed, which draws the controls with Breeze;
  without it Qt's Linux default is Fusion with Plasma's palette. The record
  names the style that loaded. Neither platform's styling is forced onto the
  other, and no custom control is introduced to make the two look alike.
- Offscreen runs are regression checks: the smoke modes of
  `tools/api_native_smoke.py` run offscreen in the web coding environment, by
  hand, before each push. CI (`.github/workflows/domain.yml`) builds and runs
  the C# and Python unit tests only; it does not build or run the application.
  Neither establishes macOS or KDE/Wayland acceptance.

## Shared functional scenarios

The same scenarios run on both platforms. F1 to F5 and F7 to F10 run twice,
against the recorded Worker API fixtures and against the production API. F6's
injected failures run against the fixture backend only, since they replace
the API with local endpoints; the production run of F6 covers nothing beyond
a healthy load. For the fixtures, start `python3 tools/api_fixture_server.py`
(it prints its URL; `--port` fixes the port) and launch the application with
`HDB_API_BASE_URL` set to that URL; this is an ordinary interactive run. (`tools/api_native_smoke.py --mode recorded` is a readiness check: it sets
`HDB_PACKAGE_SMOKE=1`, selects an address itself and exits, so it cannot be used
for these scenarios.)

| # | Scenario | Expected on both platforms |
|---|---|---|
| F1 | Launch and load | Addresses load from the Worker API; the status bar shows the result count; the map shows markers. |
| F2 | Filters | Town, a tightened price bound and the registration window narrow the list and the map together, or leave them unchanged; relaxing the default S$1,000,000 maximum widens them, as may a flat type, because the selected type's own median is compared with the price bounds; the status bar count follows; Reset restores the defaults; a reversed price range is empty and explained. |
| F3 | Search (Stage 2) | Typing narrows the list and the map locally; ranking, abbreviations and postal codes behave as in PR #12; clearing restores the list without restoring a hidden selection. |
| F4 | Selection sync | List, map marker and details show the same address; a selection hidden by a search or filter clears everywhere. |
| F5 | Details | The inspector shows the address's figures, lease, chart and latest registrations; the all-types interquartile range ("Middle half, all types" from Stage 3) appears once the details arrive. |
| F6 | Loading and errors | Three states, each an ordinary interactive launch so Retry can be exercised. Unreachable API: `python3 tools/api_fixture_server.py --refuse` reserves a loopback port that refuses every connection for as long as it runs and prints it; launch with `HDB_API_BASE_URL` set to that URL, and the app reports and offers Retry. Failed details: `python3 tools/api_fixture_server.py --fail-details 503` with `HDB_API_BASE_URL` pointing at it; the list loads and every selection shows the error and Retry. Tile failures: `python3 tools/api_fixture_server.py --fail-tiles` prints a tile endpoint; launch with `HDB_TILE_TEST=1` and `HDB_TEST_TILE_ENDPOINT` set to it; the notice appears and the list stays usable. (The `unreachable` and `tile-failure` smoke modes exit on detection and are regression checks only.) |
| F7 | Scrolling | The list and details scroll by wheel, trackpad and keyboard; a mouse drag does not flick the list on desktop. |
| F8 | Chart | The 24-month median chart renders with gaps for months without a sale, in the platform palette, in light and dark appearance. |
| F9 | Compact layout | Narrowing below the breakpoint switches to Map/Addresses toggles; focus moves to the pane shown. |
| F10 | About and modal isolation | Window commands wait while About is open; focus returns afterwards. |

## Platform-specific expectations

### macOS

- Native Qt Quick Controls `macOS` style; Cocoa window with native title bar and traffic lights.
- The application menu holds About and Quit; Edit › Find Address… is ⌘F; Filters is ⌘L.
- Metal rendering; Retina scale 2 is crisp, including the chart and map markers.
- Light and dark appearance follow the system, including per-app dark mode.
- Focus rings and the keyboard cursor ring show only in the active window.
- Accessibility: VoiceOver reads result rows as one name each, announces selection and result counts, and reaches the inspector's facts.

### Linux KDE Plasma (Wayland)

Prerequisite: the build must run as a native Wayland client. The official Qt 6.12
`linux_gcc_64` install carries `plugins/platforms/libqwayland.so`, and the
documented Linux setup checks its dependencies resolve; the local RC package
stages only XCB, so until the packaging milestone stages the Wayland plugin,
this acceptance runs from the source build. Launch with `QT_QPA_PLATFORM=wayland`
so a missing plugin fails loudly instead of falling back to XWayland, and
confirm the window is a Wayland client (KWin's window information shows no X11
window id). A run through XWayland does not satisfy any row below.

- The controls are drawn by the style the session selected, as recorded (`org.kde.desktop`, or Fusion with Plasma's palette); the window uses Plasma's colour scheme and fonts, and nothing looks like macOS.
- Ctrl+F focuses search; Ctrl+L focuses Filters; About is in the status bar.
- Window resizing, including the compact breakpoint, works through the Plasma window frame; focus follows the pane shown.
- Keyboard and pointer input, including wheel scrolling and trackpad, behave under Wayland (not XWayland).
- Map tiles render through the OSM plugin; markers and the selection highlight are visible.
- Light and dark colour schemes follow Plasma's scheme, including the chart.
- Accessibility: the application is visible to AT-SPI (Orca or Accerciser) with row names, selection state and inspector facts.
- Window title, and clean exit from the window frame's close. (The application menu entry and icon arrive with the package; see the packaging milestone.)

## Linux packaging acceptance (separate milestone)

RPM and DEB installation and launch are accepted separately from the UI stages:

- The package stages the Wayland and XCB platform plugins with their runtime dependencies, so a Plasma Wayland session runs it natively.
- Install the package on a clean system (Fedora for RPM, Debian or Ubuntu for DEB).
- The package installs a `.desktop` entry and an application icon; the application appears in the menu with that icon.
- Launch from the application menu and from the terminal; the Worker API loads; the chart renders (Qt Graphs and its Quick 3D runtime are bundled or depended on).
- Uninstall leaves no application files behind.

This milestone is unverified until recorded here.

## Record

Each batch of native verification adds a dated section below. One row per
scenario per platform; "unverified" is a valid and expected entry.

### Batch 1 — Stage 2 and Stage 3 (pending)

Runbook: [native-batch-1.md](native-batch-1.md).

| Scenario | macOS | KDE Plasma/Wayland |
|---|---|---|
| F1 Launch and load | unverified | unverified |
| F2 Filters | unverified | unverified |
| F3 Search | unverified | unverified |
| F4 Selection sync | unverified | unverified |
| F5 Details | unverified | unverified |
| F6 Loading and errors | unverified | unverified |
| F7 Scrolling | unverified | unverified |
| F8 Chart | unverified | unverified |
| F9 Compact layout | unverified | unverified |
| F10 About and modal isolation | unverified | unverified |
| Platform-specific items | unverified | unverified |
| RPM/DEB packaging | — | unverified |

Captures of inactive macOS windows for Stage 2 are described in PR #12; they
do not stand in for an interaction session.
