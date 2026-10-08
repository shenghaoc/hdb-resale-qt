# Native acceptance batch 1: Stages 2 and 3

Runbook for the first batch under the [acceptance contract](native-acceptance.md).
One session per platform, same order on both. Results go into the batch 1 table
of the contract, one row per scenario per platform, as **passed**, **failed** or
**unverified**. Nothing from one platform, from an offscreen run or from an
XWayland window is carried into the other platform's column.

Head under test: `ui/details-inspector` (Stage 3, which contains Stage 2).
Record the commit hash with the results.

## 1. Build and automated native checks (both platforms)

The automated checks are the existing smoke modes of `tools/api_native_smoke.py`,
run in the real desktop session so the window is a native Cocoa or Wayland
window. They are functional regression checks that happen to run natively;
they say nothing about appearance, and the keyboard gate delivers its keys
in-process, not through the platform's input path. Expected: every mode prints
`PASS`, with no QML warning in its log.

```sh
dotnet build -c Release -m:1
dotnet test -c Release --no-build -m:1
case "$(uname)" in
  Darwin) # the staged bundle, so macOS and its accessibility tools see the application, not a bare host
    exe="$PWD/src/HdbResale.App/obj/Release/net10.0/HdbResale.app/Contents/MacOS/HdbResale.App" ;;
  *)      # Linux: the published host
    exe="$PWD/src/HdbResale.App/bin/Release/net10.0/HdbResale.App" ;;
esac
export QSG_INFO=1      # the scene graph logs its backend; record it (macOS must say Metal)
for mode in recorded keyboard high-zoom unreachable tile-failure production; do
  python3 tools/api_native_smoke.py --executable "$exe" --mode "$mode" --log "/tmp/batch1-$mode.log" || break
done
grep -h "rhi backend\|Using QRhi\|backend:" /tmp/batch1-recorded.log | head -3     # record the backend line
grep -Eil "warning|binding loop|TypeError|ReferenceError|Unable to assign|is not a type" /tmp/batch1-*.log   # expected: nothing
```

The `PASS` lines are the harness's; the logs also hold the gates' own
`qml: HDB_…` markers, which are not warnings.

The `keyboard` mode exists from Stage 2 onward (it is in `ui/address-search`
and so in the head under test); on `main` before Stage 2 merges the harness
rejects it. On Fedora, export `QT_QPA_PLATFORM=wayland` before this block
(section 3). Launch the interactive runs below from the same `$exe`.

## 2. Interactive runs (both platforms)

Two passes of F1 to F10. The numbered steps below are the **fixture pass**:
their addresses, counts and orderings are the recorded snapshot's. The
**production pass** repeats the same actions against the live API and checks
only the invariants in "Production pass" below, since production data moves.
F6's faults run on the fixtures only. Each terminal line below is one server;
launch the application from a second terminal with the printed URL.

```sh
python3 tools/api_fixture_server.py                       # F1–F10 fixture pass
python3 tools/api_fixture_server.py --refuse              # F6 unreachable API
python3 tools/api_fixture_server.py --fail-details 503    # F6 failed details
python3 tools/api_fixture_server.py --fail-tiles          # F6 tile failure (HDB_TILE_TEST=1, HDB_TEST_TILE_ENDPOINT)
HDB_API_BASE_URL=<url> "$exe"
```

Fixture addresses used by the steps: `748B BEDOK RESERVOIR CRES` (ten sales, a
chart with gaps, nearest MRT, postal code 472748), `727 ANG MO KIO AVE 6` (no
sale in the 24-month window, so an empty chart), `588B` and `588C` (hidden by
the default S$1,000,000 maximum).

### Stage 2: search, keyboard and focus

| # | Step | Expected |
|---|---|---|
| S2.0 | Launch against the fixture server; wait for the load | Six addresses in the list; the status bar's count reads six; healthy OSM basemap tiles render under six markers or their groups; no load error. The window has the platform's own chrome: on macOS the native title bar with traffic lights, on KDE the Plasma frame, both titled "HDB Resale Explorer". |
| S2.1 | Press ⌘F (macOS) or Ctrl+F (KDE); on macOS also Edit › Find Address… | Search field focused, existing text selected. |
| S2.2 | Type `bedok res` | Three results, 748A, 748B, 747A in that order; map shows the same three. |
| S2.3 | ↓ ↓ Return; then click another application's window, look, and click back | 748B selected once; details open; the cursor ring is visible in the active window; the row stays in view. While the window is inactive the cursor ring and focus rings are not painted and the selection uses the platform's inactive colour; both return on reactivation. |
| S2.4 | Type `e` (so `bedok rese`) | 748B stays selected and is not re-announced. |
| S2.5 | Type ` 747` | 748B hidden, selection and details clear, map highlight gone. |
| S2.6 | Escape, then Escape | First clears the search (six results, nothing reselected), second moves focus to the list. |
| S2.7 | With the list focused, type `4717` | Typing continues in the search field; results 748A and 747A (postal codes). |
| S2.8 | Replace with `588` | Empty state explains two matches hidden by the filters; Clear Search works. |
| S2.9 | ⌘L / Ctrl+L | Focus lands on the town filter; in a compact window the filters expand. |
| S2.10 | Open About, press ⌘F / Ctrl+F and then ⌘L / Ctrl+L, close About | Neither shortcut acts behind the dialog (search is not focused, the filters neither expand nor take focus); focus returns where it was. |
| S2.11 | Select 748B, then narrow the window below the breakpoint and widen it again | Map/Addresses toggles appear; focus moves to the pane shown; 748B stays selected throughout. |
| S2.12 | Screen reader on (VoiceOver, Orca): move through three rows, select one, type `b` | One name per row with address, town, median, sales and month; "Selected …" once; the result count announced after a pause. Then Escape until the search is empty and six addresses show. |
| S2.13 | Select 748B, then set town ANG MO KIO; Reset. Then each filter from the defaults (Reset between them): town ANG MO KIO; flat type 4 ROOM; maximum S$500,000; minimum S$500,000; minimum S$600,000 with maximum S$500,000; Latest 12 months; then Reset | The excluded selection clears from the list, the map highlight and the details together. Then counts, with the list, the map and the status bar agreeing each time: 1 (727); 5 (747A, 748B, 748A, 115, 39); 3 (115, 39, 727); 3 (747A, 748B, 748A); 0 with the reversed-range explanation; 4 (747A, 748B, 748A, 39: the window starts at 2025-11, and 115's latest sale is 2025-10); Reset restores six and every default. |
| S2.14 | Click a map marker (zoom in until 748B is an individual marker) | The list highlights and scrolls to 748B, the details open, and the marker is highlighted; "Show on map" from the details recentres on it. |
| S2.15 | Quit through the platform: macOS ⌘Q or the application menu's Quit; KDE the window frame's close button (and once more with Alt+F4) | The window closes at once, the process exits with status 0 in the launching terminal, no error is printed, and nothing is left running. Launch again afterwards for the Stage 3 steps. |

### Stage 3: inspector, chart, clipping, typography, accessibility

| # | Step | Expected |
|---|---|---|
| S3.1 | Select 748B, read the Sales group | Five facts at once and, when the details arrive, a sixth labelled "Middle half, all types" reading S$705,750–S$880,000 (the all-types interquartile range the contract's F5 requires); labels right-aligned in one column, values aligned, tabular figures; the note names the 24-month scope. |
| S3.2 | Set the flat type filter to 4 ROOM | Sales facts switch to the 4 ROOM cohort; "Middle half, all types" keeps the all-type range; the chart and registrations are unchanged. |
| S3.3 | Address group | Town, flat types, models, postal code, nearest MRT; "Nearest MRT" and "Postal code" on one line each. |
| S3.4 | Lease and Location groups | Commenced, "Remaining in 2026"; one block point; the two notes in the caption role. |
| S3.5 | Chart, in the light appearance (set it now if the session is dark) | Title, range caption, axis labels and the two month labels in the caption size; line and dots in the link colour; gaps for months without a sale. |
| S3.6 | Scroll the details so the chart is half out of view, in both directions | The chart is clipped at the details' edge; nothing is drawn over the list or the heading. **Record the result explicitly; this is the open question from the offscreen captures.** |
| S3.7 | Latest registrations | Heading, caption, rows separated by hairlines; headings demibold and tabular; details in the secondary colour. |
| S3.8 | Reset the flat type filter to All flat types, then select 727 ANG MO KIO AVE 6 | Chart replaced by the "No registrations … 24-month window" caption; the inspector still shows four groups. |
| S3.9 | Reselect 748B, then switch to the dark appearance or colour scheme | Every label, value, note, chart element and separator remains readable; no hard-coded light colour shows. |
| S3.10 | Raise the system text size or scale factor one step | Labels wrap rather than clip; the label column does not exceed two fifths of the pane; the chart captions scale. |
| S3.11 | Screen reader through the details | Each group reads as a named group with a heading; each fact reads once as "Label: value"; each registration reads once. |
| S3.12 | Scroll the list and the details by wheel, trackpad and keyboard (↑/↓, Page Up/Down, Home/End); drag a row with the mouse | Both scroll smoothly and stop at their ends; a mouse drag on the list selects or does nothing but never flicks it. |
| S3.13 | Against `--fail-details 503`: select two addresses, press Retry on one | Each selection shows the error and Retry; Retry re-requests; the inspector's four groups still show. |
| S3.14 | Against `--refuse`: launch, wait, press Retry | The load error says the API could not be reached and offers Retry; Retry re-attempts and the error remains; the window stays responsive, filters and About still open. |
| S3.15 | Against `--fail-tiles` with `HDB_TILE_TEST=1` and `HDB_TEST_TILE_ENDPOINT`: launch, select 748B, search `bedok` | The tile-failure notice appears over an empty map; the list, search, selection and details keep working; the notice stays legible in light and dark. |

### Production pass

Repeat S2.0 to S2.15 and S3.1 to S3.12 against production with these
invariants instead of the fixture values. S2.0: the launch completes without
a load error, the status bar's count matches the list and the map shows
markers. Search (F3): `ang mo kio ave` returns results whose addresses all
contain those words, ranked with exact-word matches first; `ang mo kio avenue`
returns the same results (abbreviation equivalence); the postal code read
from a selected address's Address group, searched on its own, returns that
address. Details (F5): selecting any result opens four populated groups whose
Sales note names the dataset's latest month; an address with sales in the
window shows a chart, one without shows the empty caption. Filters (F2): town,
price bounds and the registration window narrow the count or leave it
unchanged; a flat type may raise it, because the selected type's own median
is compared against the price cap; list, map and status bar agree after every
change, and Reset restores S2.0's default count (the default S$1,000,000
maximum still applies, so this is not every address the API holds). Selection (F4): a map marker
click selects the address it names. Exit (S2.15): as on the fixtures. Record
the production head's `generatedAt` from About.

### Captures

Take these as native screenshots of the active window, light and dark, and
keep them with the results (not committed): S2.3, S2.8, S2.11, S3.1, S3.5,
S3.6 (both scroll directions), S3.7, S3.9. On macOS note the scale; on KDE
note the scale factor and the Qt Quick Controls style that loaded.

## 3. Platform notes

### macOS (Cocoa, Metal)

- Build and run in the logged-in desktop session, not with the screen locked;
  window-level accessibility and input are otherwise blocked.
- `QSG_INFO=1` is exported in section 1; its backend line must name Metal.
  Record it with the scale (2 on Retina). Without that line the macOS
  platform row stays unverified.
- Light and dark: System Settings › Appearance; per-app dark mode also counts.
- VoiceOver for S2.12 and S3.11.

### Fedora KDE Plasma (Wayland)

- Runtime paths follow `docs/pr8-review-verification.md` (user-local .NET,
  Qt 6.12.0 `gcc_64`, venv CMake). Run `ldd "$QtDir/plugins/platforms/libqwayland.so"`
  first; no `not found` line is expected.
- `export QT_QPA_PLATFORM=wayland` for every launch in sections 1 and 2.
  Confirm the window is a Wayland client: KWin's window information (Alt+F3 ›
  More Actions › Configure Special Window Settings, or `qdbus-qt6 org.kde.KWin
  /KWin queryWindowInfo`; Fedora ships the Qt 6 tool under that name) shows no
  X11 window id. Anything through XWayland
  does not count; record it as unverified, not failed.
- Record the Qt Quick Controls style that loaded (`QT_QUICK_CONTROLS_STYLE` in
  the session, or `QT_LOGGING_RULES=qt.quick.controls*=true`).
- Light and dark: System Settings › Colours & Themes › Colours (Breeze Light,
  Breeze Dark). Scale factor: Display Configuration.
- Orca for S2.12 and S3.11, with AT-SPI enabled for Qt
  (`QT_LINUX_ACCESSIBILITY_ALWAYS_ON=1` if Orca does not see the window).

## 4. Recording

For each platform, fill the batch 1 table in `native-acceptance.md` from the
step tables above: a scenario is **passed** only when every step mapped to it
passed on that platform, **failed** with the failing step numbers, otherwise
**unverified** with the reason (environment unavailable, XWayland only,
screen reader not available). Add the commit hash, OS version, Qt style
loaded, scale, and the list of captures taken. Any failed step is a concrete
native defect to fix before Stage 4.

Scenario mapping: F1 S2.0 (section 1's `recorded` and `production` are
supporting evidence, never sufficient); F2 S2.8, S2.13, S3.2; F3 S2.1–S2.8;
F4 S2.3, S2.5, S2.14; F5 S3.1–S3.5, S3.7, S3.8; F6 S3.13, S3.14, S3.15; F7
S3.6, S3.12; F8 S3.5, S3.8, S3.9 (light and dark both observed); F9 S2.11;
F10 S2.10. Platform-specific items: S2.0 (window chrome, OSM tiles), S2.3
(inactive-window rings), S2.9, S2.12, S2.15 (clean exit through the
platform's own quit or close), S3.9–S3.11 and the platform notes. A scenario's production column needs the production pass too.
