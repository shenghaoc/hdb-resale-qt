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

Status (2026-10-08, see [docs/packaging/linux.md](../packaging/linux.md)): the packages stage the Wayland and XCB
platform plugins, install a `.desktop` entry, SVG icon and AppStream metadata, and CI installs the DEB on Debian 13 and
the RPM on Fedora 43, launches them through `/usr/bin/hdb-resale-explorer` on X11 (Xvfb) against the recorded Worker API,
and checks uninstall. **Still unverified, by hand:** a Plasma Wayland session (the Wayland plugin is staged, never launched
under a compositor), menu launch and the icon in a real desktop, and the window/desktop-entry association on Wayland.

## Record

Each batch of native verification adds a dated section below. One row per
scenario per platform; "unverified" is a valid and expected entry.

### Batch 1 — Stage 2 and Stage 3 (macOS recorded 2026-10-09; KDE pending)

Runbook: [native-batch-1.md](native-batch-1.md).

| Scenario | macOS | KDE Plasma/Wayland |
|---|---|---|
| F1 Launch and load | passed | unverified (pending: KDE batch not run) |
| F2 Filters | passed | unverified (pending) |
| F3 Search | unverified: fixture and production native alphabetic typing/type-to-search and genuine production block ranking passed; quiet VoiceOver refinement remains unverified | unverified (pending) |
| F4 Selection sync | passed | unverified (pending) |
| F5 Details | passed | unverified (pending) |
| F6 Loading and errors | passed | unverified (pending) |
| F7 Scrolling | unverified: ordinary fixture list/details and production list/details wheel scrolling observed; prior keyboard and pointer evidence retained; genuine trackpad pinch/momentum remains manual | unverified (pending) |
| F8 Chart | passed | unverified (pending) |
| F9 Compact layout | unverified: fixture/production compact master/detail and Addresses/Back/list/filter focus observed; Map-specific native focus remains unverified | unverified (pending) |
| F10 About and modal isolation | passed | unverified (pending) |
| Platform-specific items | unverified: fixture/production system Light/Dark following and supported production scale check passed; VoiceOver and Map-specific compact focus remain unverified; genuine trackpad pinch/momentum remains manual | unverified (pending) |
| RPM/DEB packaging | — | unverified |

Nothing in the macOS column is carried into the KDE column.

#### macOS run

- **Head:** `ui/details-inspector` at `6ca00e5` (Stage 3, containing Stage 2
  at `c880920`). Both heads passed Codex review and CI.
- **Platform:** macOS 27.0.1 (26A434) on an Apple M5, built-in Retina
  display. Cocoa windows; `QSG_INFO` logged "Creating QRhi with backend
  Metal" and a CAMetalLayer at scale 2.00. The Qt Quick Controls style that
  loaded was `macOS`, with `QT_QUICK_CONTROLS_STYLE` unset. Keyboard
  navigation was at the macOS default (off), so Tab stops at text fields
  and lists: filters, search, list.
- **Appearance:** the system was dark throughout and was not changed. Light
  came from the per-app `NSRequiresAquaSystemAppearance` override; the
  window was dark again once the override was deleted.
- **Section 1:** all six smoke modes passed natively on the final head, with
  no QML warning.
- **Production:** pinned at `generatedAt` 2026-10-04T15:30:00.000Z, unchanged
  from the first fetch to the last, and the same value About showed. The
  default count was 9,297, as computed from the pinned summaries. Filter
  counts also matched: maximum S$479,999 gave 2,174; minimum S$438,001,
  7,700; the latest 12 months, 7,517; town Ang Mo Kio, 354; the maximum at its
  S$1,650,000 limit, 9,730. Each named exclusion lost its marker while a
  neighbour kept its own. For `ang mo kio 10`, all 103 markers were pressed,
  each selecting its own address, and the marker set equalled the list.
  Details, chart and all 20 registrations of 121 ANG MO KIO AVE 3 matched its
  saved response, including the full-history cohort note for 4 ROOM. 346
  JURONG EAST ST 31 showed the empty-window chart caption.
- **Defects found and fixed during the run**, each in its owning PR. The
  three keyboard fixes have keyboard-gate checks that fail without them. The
  pooling fix was verified natively before and after, by counting the rows
  the platform exposes after scrolling. The chart fix was verified with
  native captures before and after.
  - #12 `225e0f8`: Tab skipped the result list. A list needs its
    accessible role to be a Tab stop on macOS.
  - #12 `63f40ff`: Page Up/Down and Home/End did nothing in the list.
  - #12 `c880920`: rows reused from the list's pool dropped out of the
    accessibility tree after scrolling, so rows are no longer pooled.
  - #14 `e3822a3`: the chart's month labels sat about 38 points below the
    axis, and the y labels missed both bounds.
  - #14 `cb1d6a5`: the details could not be scrolled from the keyboard on a
    default Mac. A click now focuses them, Page Up/Down and Home/End scroll
    them, and focus leaves them when they hide.
- **How input was given:** keys and clicks were real events posted to the
  application. Latin text went through the accessibility text API, because
  the Mac's input source was Chinese Pinyin, which composes typed letters.
  The window was narrowed through its frame and widened through the
  accessibility window-size API. Announcements (S2.4, the count after
  typing) were observed as the platform's accessibility announcements, not
  through VoiceOver speech.
- **Notes for the runbook:**
  - S2.11: in the compact layout, Addresses shows the selected address's
    details, by design, with focus on "‹ All addresses". It does not show the
    list.
  - Production has no block 10 in Ang Mo Kio. Matches on "AVE 10" do not
    establish exact-block-before-prefix-block ranking. That production
    invariant remains unverified until rerun with a snapshot-derived case.
  - Section 1's backend grep misses Qt 6.12's wording; the line reads
    "Creating QRhi with backend Metal".
- **Captures** (light and dark, not committed): S2.3 (active, inactive and
  reactivated), S2.8, S2.11, S3.1, S3.5, S3.6 (both directions), S3.7,
  S3.8, S3.9 and S3.15, plus the chart before and after `e3822a3`.

#### Narrow correction sessions — 2026-10-09

This section supersedes the passed claims for F3/F9 and system-appearance
following above. The historical run remains recorded; its AX text assignments
and per-app light override do not establish native alphabetic keystrokes or
system-appearance following. No full batch was repeated and no Stage 2/3
product source was edited.

- **Provenance:** acceptance branch `docs/native-batch-1-macos` at
  `fc8d24ebfaf3f3e17ff946688638b27589c03895` before these corrections;
  existing Stage 3 Release bundle in
  `hdb-resale-qt-worktrees/batch1/src/HdbResale.App/obj/Release/net10.0/HdbResale.app`,
  source `6ca00e51038811c305b77ab2db32061cf4650871`, containing Stage 2
  `c880920f76604337070ecc2ea376f7e29baa0b55`. Apphost SHA-256
  `8a5b806b267918f4c87866e31b1dfbafc568328355cb6b18db25d600c34da86d`;
  native library `5c8724827730e93a683f4fdc06fb7b3f7582e52d4053815bf913b32de2e272ca`;
  managed DLL `aa0dc223f0156280b7df452105f60704b3be02e9dc7818d476ea621922638588`.
  Existing macOS 27.0.1 (26A434), M5, Retina scale 2 provenance applies.
- **Native input, fixture:** input source switched from Chinese Pinyin to ABC.
  Computer Use sent individual key events, without AX text assignment or paste.
  Command-F and `bedok res` yielded 748A, 748B, 747A, count three; Down,
  Down, Return selected 748B. Appending `e` retained it; appending
  ` 747` left 747A, count one, and cleared selection/details/highlight.
  Escape twice restored six and focused the list. Alphabetic type-to-search
  from that list repeated the three-result query. Numeric `4717` returned
  748A and 747A; `588` showed zero with two matches hidden by filters.
  Production keyboard paths were not rerun.
- **Ranking preparation only:** saved production summaries pinned at
  `generatedAt=2026-10-04T15:30:00.000Z` contain the real default-price-cap
  case `geylang 30`: exact block 30 CASSIA CRES and prefix blocks
  301, 302, 304, 305 UBI AVE 1. This was derived from summary block fields,
  not street-number words. The native search and live snapshot-coherence
  check remain pending; this derivation is not a native pass.
- **Compact:** native frame-drag attempts did not establish a below-breakpoint
  rerun. F9 remains unverified. Expected selected master/detail behavior:
  Addresses shows selected details and focuses “All addresses”; Map/Addresses
  switching retains selection. Back clears selection and returns focus to
  the list. After reselecting, Filters and widening preserve selection.
- **System appearance, fixture:** Computer Use selected Light and then Dark
  in System Settings. The same running app visibly changed its inspector
  and chart palette; labels, line and dots remained readable. Original
  appearance was Auto and was restored, visibly verified. Production
  system-appearance following remains pending.
- **Wheel and text:** ordinary native wheel scrolling exposed the fixture
  details chart and registrations. No wheel action establishes a trackpad
  gesture. System accessibility text size was Default, raised one step to
  13 pt; HDB was not listed among supported apps and its text did not visibly
  change. S3.10 remains unverified until a supported text/scale check.
  Default was restored and visibly verified before the initial disconnect.
- **VoiceOver:** initially visibly off. Opening VoiceOver Utility stalled;
  no successful on-toggle, caption/focus setting change or spoken reading
  check occurred. S2.12/S3.11 remain unverified. No exposed Computer Use API
  provides genuine pinch/magnify or momentum; those remain explicitly manual.
- **Cleanup and reconnection:** the first session lost its native transport
  and then host connection. On the authorized resume, shell inspection found
  no HDB app or fixture server still running; old sessions 42825/45740 must
  not be blindly signalled. System Settings again visibly showed VoiceOver
  off and Appearance Auto. A Control-Space input-source restoration attempt
  did not establish Pinyin; the subsequently read selected-source preference
  still named ABC. Pinyin restoration and Default-text-size re-verification
  remain required. The Keyboard Shortcuts sheet was open when native
  Computer Use failed again with `Transport closed`; reset also failed.
  No remaining native result can be inferred from that failure.


#### Native tool recovery and narrow rerun — 2026-10-09

This follow-up supersedes the pending production keyboard/ranking/appearance,
supported scale and restoration statements in the correction sessions above.
Compact master/detail behavior was subsequently rerun in both passes; Map-specific
native focus and screen-reader checks remain unverified.

- **Recovery and provenance:** this fresh Mac task had directly registered
  `cua_repl` controls. Native System Settings screenshots, AX clicks, individual
  keys and ordinary wheel actions worked. The docs worktree began clean at
  `9f5212c59f522c08cde239ee7e43cfb4428705eb`; the frozen Stage 3 bundle,
  Stage 2 ancestor and apphost hash are unchanged from the provenance above.
  The ordinary fixture and production launches logged Metal, scale 2.
- **Pinned production ranking — passed:** manifests fetched before summaries,
  after summaries and after the native reruns all reported
  `2026-10-04T15:30:00.000Z`, matching native About. The saved summaries give
  9,297 default-eligible addresses. They contain two exact block-30 matches,
  not just Cassia: 30 BALAM RD (S$325,888) and 30 CASSIA CRES (S$950,000).
  Native `geylang 30` returned those two first, then 304, 305, 301, 302 UBI
  AVE 1 (S$324,000, S$465,000, S$480,000, S$485,000), all GEYLANG. Both
  exact blocks precede every prefix even though 304 is cheaper than either.
- **Production native keyboard — functional paths passed:** with ABC selected,
  individual key events after Command-F entered `geylang 30`; the same query
  typed from the focused list entered search and gave the same six identities.
  Down, Down, Return selected 30 CASSIA CRES. Extending to `geylang 30 c`
  retained its selection, details and highlight; appending `zz` gave zero and
  cleared all three. Escape twice restored defaults and list focus. Numeric
  type-to-search `560121` returned and selected 121 ANG MO KIO AVE 3. No paste
  or AX text assignment supplied these queries. The earlier fixture native
  keystroke results and historical abbreviation/marker checks are retained;
  quiet VoiceOver refinement is still unverified, so F3 is not yet fully passed.
- **Production system appearance — passed:** Computer Use selected Light,
  then Dark in System Settings. The same running 121 inspector/chart changed
  palette; facts, notes, axis/month captions, line and dots remained readable.
  This used the system controls, not the historical per-app light override.
- **Supported scale, S3.10 — passed on production:** the built-in display was
  originally 1512 × 982 (Default). One step toward Larger Text selected
  1352 × 878. The running inspector kept aligned facts, wrapped the long MRT
  and lease text, and kept its label column below two fifths of the pane;
  the chart captions remained readable at the larger physical scale.
  Default resolution was restored and visibly verified. This establishes
  the runbook's scale alternative; it does not reinterpret the earlier
  unsupported accessibility-text-size attempt as a pass.
- **Ordinary wheel:** native wheel actions scrolled the fixture list and exposed its details chart
  and production chart/registrations; the production list moved from its
  initial 346 JURONG EAST ST 31 row into the Tanglin Halt/MacPherson rows and
  returned upward. An AX scrollbar assignment positioned the scaled chart
  capture separately and is not counted as wheel or keyboard evidence.
  No wheel or zoom-button action establishes genuine trackpad gestures.
- **Compact master/detail — functional paths passed, F9 still unverified:**
  raising the native window and dragging its right edge reduced the production
  and fixture window from 1,360 to approximately 721 logical points, visibly
  exposing Map/Addresses tabs and collapsed filters. With 121 ANG MO KIO AVE 3
  selected on production, and 748B BEDOK RESERVOIR CRES on fixtures, Addresses
  showed selected details and the Back button had native AX focus and a visible
  focus ring. Map/Addresses switching preserved those selections; Back cleared
  selection and showed the list with a focused row. Without a selection,
  Addresses returned to the list. Command-L expanded filters and focused Town;
  production widening restored the side-by-side layout while preserving its
  selected row, details and map highlight. In the fixture, Return after clearing
  selection selected the first address (727 ANG MO KIO AVE 6), as the source
  resets the keyboard index on deselection; Command-L then focused Town.
  The fixture reverse edge drags did not widen the window before exit, so that
  transition is established on production only. Map switching reports the
  standard window as the focused native AX element; it does not independently
  prove Map-specific focus. F9 therefore remains unverified for that criterion.
  Earlier failed corner/interior drags are superseded by the successful right
  edge drags; no hidden window-size protocol was used.
- **VoiceOver — unverified:** System Settings was visibly toggled on; Utility
  already had Show caption panel and Show VoiceOver cursor enabled, so those
  preferences were not changed. No readable caption or focus traversal was
  obtained. Native VoiceOver lookup timed out and the inventory reported it
  not running despite the enabled settings switch. S2.12/S3.11 and quiet
  refinement therefore remain unverified; AX names alone are not speech.
- **Restoration — verified:** Auto appearance, VoiceOver off, Default
  accessibility text size (slider 4) and Default display resolution were
  visually reverified. Control-Space restored Chinese Pinyin; actual `n`,
  `i`, Space produced `你` in the search field, then Escape twice cleared it.
  A read-only selected-source preference also named
  `com.apple.inputmethod.SCIM.ITABC`. All owned interactive app sessions exited
  with status 0 and the owned fixture server was stopped. The final Pinyin
  relaunch logged an `IMKCFRunLoopWakeUpReliable` mach-port diagnostic on exit;
  no QML failure or product defect was established by it.
- **Evidence:** native screenshots/AX observations are retained in the
  recovery task transcript, not committed or saved as standalone image files.
  Local snapshot JSON and result ledger are in the recovery task's
  `evidence/` directory. Only narrow acceptance corrections were run; no
  product source, production snapshot or deployment changed.
- **Remaining handoff:** manual Mac Map-specific compact focus and fixture compact-to-wide transition,
  VoiceOver captions/focus/announcements and genuine trackpad pinch/momentum.
  PR #18 remains held until these requested checks are complete. Fedora KDE
  is untouched: run the separate native Wayland batch from the same frozen
  Stage 3 head, prove Wayland rather than XWayland, record loaded style,
  Plasma scheme/font/scale and Orca results; carry no Mac pass into KDE.

- **Resumed validation:** domain build completed with zero warnings/errors;
  all 278 C# tests passed. Six frozen-bundle regression modes (keyboard,
  recorded, high-zoom, unreachable, tile-failure and production) exited 0;
  their logs had no matching QML warning/error patterns. These automated modes
  supplement the native observations and do not establish gestures or VoiceOver.
  Python 3.14 discovery ran 42 tests with one error:
  `FailDetailsTests.test_the_reserved_port_refuses_connections` expected
  `ConnectionRefusedError`, but the unchanged loopback socket call timed out.
  A focused repeat produced the same `TimeoutError`; its cause is undetermined.
  This is a failed local Mac check, not an all-green validation claim. No test
  or product source was altered to bypass it. The two final owned app sessions
  exited 0, the fixture server was stopped, and a filtered process check found
  no remaining HDB/fixture/tile-failure process. No system preference changed
  during these compact reruns.


#### Replacement-agent bounded checks — 2026-10-09

This section supersedes the outstanding fixture compact-to-wide item and the
undetermined PR-regression status of the Mac Python failure above. It does not
complete F3, F9 or screen-reader acceptance.

- **Current provenance:** docs head before this update was
  `53f42619cfece10de4ee78c94242d310181284d9`, clean. The frozen UI worktree was
  clean at `6ca00e51038811c305b77ab2db32061cf4650871`, with Stage 2
  `c880920f76604337070ecc2ea376f7e29baa0b55` verified as an ancestor. Apphost
  SHA-256 remained
  `8a5b806b267918f4c87866e31b1dfbafc568328355cb6b18db25d600c34da86d`.
  Direct native Computer Use screenshots and controls worked. No applicable
  `AGENTS.md` or `.agents/skills` was present in these checkouts.
- **Fixture compact-to-wide — passed:** the preceding agent's final native
  observation established Back/list focus, explicit 748B reselection, Command-L
  Town focus and widening with selection retained. The replacement also opened
  the already-running bundle through native Finder to foreground it, narrowed
  the left window frame from 3,024 to 1,424 screenshot pixels, and switched
  Addresses/Map with 748B retained. Addresses focused “Back to all addresses”.
  The exposed native “zoom the window” action restored 3,024 pixels; the
  screenshot showed the selected 748B row, inspector and highlighted marker
  together. This is native window sizing, not map zoom or a trackpad gesture.
- **Map-specific focus — unverified:** after switching from the focused Back
  button to Map, native AX reported only the standard window as focused.
  One Tab did not expose map-specific focus. The screenshot showed the map
  and retained highlight, but no independently identifiable map focus. The
  earlier production observation has the same limitation. Source focus calls
  and hidden-control disappearance are not promoted to native focus evidence.
- **VoiceOver — unverified:** original state was off. Original caption panel
  was on, font 22 pt (slider 3); those preferences were read and left unchanged.
  Native System Settings enabled VoiceOver and a read-only process check
  confirmed its reader running. VO-Shift-F4, VO-Right and VO-H produced no
  readable caption/focus sequence in the supported app observations, including
  a retry after HDB was foregrounded through Finder. Binding VoiceOver by its
  full system app path timed out. A Dock lookup also timed out after several
  minutes. No supported desktop-wide caption capture or genuine multitouch
  control was exposed. No custom native-control protocol or permission bypass
  was used. S2.12 row/selection/count announcements, S2.4 quiet refinement and
  S3.11 spoken detail traversal remain unverified in both passes; the fixture
  observation limit was not treated as a production pass or a product defect.
- **Text-size distinction:** S3.10's production display-scale alternative
  remains passed. The separate accessibility preferred-text-size mechanism
  remains unverified: the earlier Default→13 pt attempt caused no visible HDB
  text change. Display scaling does not establish that mechanism's response.
- **Genuine gestures — manual:** the exposed controls support pointer drag,
  ordinary wheel and keys, but no genuine trackpad pinch/magnify or inertial
  scroll gesture. S3.12/F7 and requested map pinch remain explicitly manual.
  Wheel and zoom-button results are not substituted.
- **Python diagnosis retained:** the held bound/non-listening port test at
  `tools/test_api_fixture_server.py:99` also timed out on unchanged base
  `9723df1bfc85f5e4d1cd1cb785a1558cc4757268`, with byte-identical helper/test
  files. Python 3.14.7 and Apple Python 3.9.6 reproduced it; controls refused
  immediately after the port closed and connected immediately when listening.
  Read-only firewall inspection found it disabled and stealth off. This is a
  pre-existing fixture assumption mismatch with this Mac, not a #18 regression;
  the lower-level cause remains undetermined. The failed local discovery result
  (42 tests, one error) is preserved; no tests or security settings were changed.
- **Cleanup:** both owned fixture app launches exited 0; the fixture server
  stopped with Ctrl-C (130). VoiceOver off was visually and natively verified,
  and caption panel on/22 pt was re-read. The filtered process check found no
  remaining HDB/fixture/VoiceOver reader. Chinese Pinyin, Auto appearance,
  Default accessibility text size and Default display scale were not changed;
  their prior restoration evidence remains applicable. Native screenshots/AX
  observations are retained in this replacement task transcript, and its
  `fixture-app.log` records Metal and Retina scale 2. No full batch, product
  change, deployment, production data mutation or Fedora operation occurred.

**Minimal remaining physical Mac checks, on this same frozen bundle:**

1. In fixture and production compact layout, select an address, switch
   Addresses→Map and observe the native keyboard/VoiceOver focus on the map.
2. Enable VoiceOver with its caption panel and use ABC for actual typing.
   On fixtures traverse three rows, select once, type `b`, and observe one name
   per row, one “Selected …”, settled five rather than stale six, then Escape
   back to six. Select 748B from `bedok res` and append `e`: no repeated
   selection announcement. On production repeat with counts equal to the
   visible status bar and a retained-selection refinement (the verified
   `geylang 30`→`geylang 30 c` case retains Cassia).
3. On each selected inspector, traverse the named groups/headings, each
   “Label: value” once, and each latest registration once.
4. Physically pinch the map and perform inertial trackpad scrolling in the
   list/details on both passes, recording the actual response. Restore Pinyin
   and VoiceOver off afterward, preserving the original caption preference.

PR #18 stays unmerged pending those substantive focus/VoiceOver observations
or an explicit owner decision about the remaining gaps. Pinch/momentum remains
the separately allowed manual tool limitation. KDE/Wayland acceptance and
RPM/DEB packaging remain unverified; no Mac pass carries over.
