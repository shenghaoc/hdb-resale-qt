# Native UI acceptance: macOS and Linux KDE

The native-first UI redesign is accepted on **two platforms with equal standing**:
macOS and Linux KDE Plasma on Wayland. The application was first developed and
tested on Fedora KDE; the redesign emphasises macOS. Neither is secondary.

Acceptance is recorded per platform. A scenario that passed on one platform
is not accepted on the other, and the record says so. Three outcomes exist for
every scenario on every platform: **passed**, **failed**, **unverified**.

## What establishes acceptance

- A real desktop session on the platform: Cocoa on macOS, a KDE Plasma
  Wayland session on Linux. Interaction through the real menu bar, keyboard and
  pointer.
- The platform's own Qt style: the `macOS` style on macOS, the style KDE
  selects (Breeze through the platform theme) on Plasma. Neither platform's
  styling is forced onto the other, and no custom control is introduced to make
  the two look alike.
- Offscreen, Xvfb and headless runs are regression checks. They run in the
  web coding environment and in CI, and they catch functional regressions. They
  establish neither macOS nor KDE/Wayland acceptance.

## Shared functional scenarios

The same scenarios run on both platforms, against the recorded Worker API
fixtures (`tools/api_native_smoke.py --mode recorded`) and the production API.

| # | Scenario | Expected on both platforms |
|---|---|---|
| F1 | Launch and load | Addresses load from the Worker API; the status bar shows the result count; the map shows markers. |
| F2 | Filters | Town, flat type, price bounds and registration window narrow the list and the map together; Reset restores the defaults; a reversed price range is empty and explained. |
| F3 | Search (Stage 2) | Typing narrows the list and the map locally; ranking, abbreviations and postal codes behave as in PR #12; clearing restores the list without restoring a hidden selection. |
| F4 | Selection sync | List, map marker and details show the same address; a selection hidden by a search or filter clears everywhere. |
| F5 | Details | The inspector shows the address's figures, lease, chart and latest registrations; "Middle half of all sales" appears once the details arrive. |
| F6 | Loading and errors | An unreachable API reports and offers Retry; a failed detail request offers Retry; tile failures show the notice without blocking the list. |
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

- Qt's platform theme selects KDE's style and colour scheme; controls look like Breeze, not like macOS.
- Ctrl+F focuses search; Ctrl+L focuses Filters; About is in the status bar.
- Window resizing, including the compact breakpoint, works through the Plasma window frame; focus follows the pane shown.
- Keyboard and pointer input, including wheel scrolling and trackpad, behave under Wayland (not XWayland).
- Map tiles render through the OSM plugin; markers and the selection highlight are visible.
- Light and dark colour schemes follow Plasma's scheme, including the chart.
- Accessibility: the application is visible to AT-SPI (Orca or Accerciser) with row names, selection state and inspector facts.
- Desktop integration: application menu entry and icon, window title, and clean exit from the window frame's close.

## Linux packaging acceptance (separate milestone)

RPM and DEB installation and launch are accepted separately from the UI stages:

- Install the package on a clean system (Fedora for RPM, Debian or Ubuntu for DEB).
- Launch from the application menu and from the terminal; the Worker API loads; the chart renders (Qt Graphs and its Quick 3D runtime are bundled or depended on).
- Uninstall leaves no application files behind.

This milestone is unverified until recorded here.

## Record

Each batch of native verification adds a dated section below. One row per
scenario per platform; "unverified" is a valid and expected entry.

### Batch 1 — Stage 2 and Stage 3 (pending)

| Scenario | macOS | KDE Plasma/Wayland |
|---|---|---|
| F1–F10 | unverified | unverified |
| Platform-specific items | unverified | unverified |
| RPM/DEB packaging | — | unverified |

Captures of inactive macOS windows for Stage 2 are described in PR #12; they
do not stand in for an interaction session.
