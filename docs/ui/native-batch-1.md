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
# Linux: the published host.
exe="$PWD/src/HdbResale.App/bin/Release/net10.0/HdbResale.App"
# macOS: the staged bundle, so macOS and its accessibility tools see the application, not a bare host.
exe="$PWD/src/HdbResale.App/obj/Release/net10.0/HdbResale.app/Contents/MacOS/HdbResale.App"
for mode in recorded keyboard high-zoom unreachable tile-failure production; do
  python3 tools/api_native_smoke.py --executable "$exe" --mode "$mode" --log "/tmp/batch1-$mode.log" || break
done
grep -il "warning\|qml" /tmp/batch1-*.log      # expected: nothing
```

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
| S2.1 | Press ⌘F (macOS) or Ctrl+F (KDE); on macOS also Edit › Find Address… | Search field focused, existing text selected. |
| S2.2 | Type `bedok res` | Three results, 748A, 748B, 747A in that order; map shows the same three. |
| S2.3 | ↓ ↓ Return | 748B selected once; details open; the cursor ring is visible in the active window; the row stays in view. |
| S2.4 | Type `e` (so `bedok rese`) | 748B stays selected and is not re-announced. |
| S2.5 | Type ` 747` | 748B hidden, selection and details clear, map highlight gone. |
| S2.6 | Escape, then Escape | First clears the search (six results, nothing reselected), second moves focus to the list. |
| S2.7 | With the list focused, type `4717` | Typing continues in the search field; results 748A and 747A (postal codes). |
| S2.8 | Replace with `588` | Empty state explains two matches hidden by the filters; Clear Search works. |
| S2.9 | ⌘L / Ctrl+L | Focus lands on the town filter; in a compact window the filters expand. |
| S2.10 | Open About, press ⌘F / Ctrl+F, close About | The shortcut does nothing behind the dialog; focus returns where it was. |
| S2.11 | Select 748B, then narrow the window below the breakpoint and widen it again | Map/Addresses toggles appear; focus moves to the pane shown; 748B stays selected throughout. |
| S2.12 | Screen reader on (VoiceOver, Orca): move through three rows, select one, type a letter | One name per row with address, town, median, sales and month; "Selected …" once; the result count announced after a pause. |
| S2.13 | Filters: town ANG MO KIO; then All towns and flat type 4 ROOM; then a maximum of S$500,000; then a minimum above the maximum; then Latest 12 months; then Reset | List and map narrow together each time; the reversed range shows an empty, explained result; the status bar's count follows; Reset restores six addresses and every default. |
| S2.14 | Click a map marker (zoom in until 748B is an individual marker) | The list highlights and scrolls to 748B, the details open, and the marker is highlighted; "Show on map" from the details recentres on it. |

### Stage 3: inspector, chart, clipping, typography, accessibility

| # | Step | Expected |
|---|---|---|
| S3.1 | Select 748B, read the Sales group | Six facts, labels right-aligned in one column, values aligned, tabular figures; the note names the 24-month scope. |
| S3.2 | Set the flat type filter to 4 ROOM | Sales facts switch to the 4 ROOM cohort; "Middle half, all types" keeps the all-type range; the chart and registrations are unchanged. |
| S3.3 | Address group | Town, flat types, models, postal code, nearest MRT; "Nearest MRT" and "Postal code" on one line each. |
| S3.4 | Lease and Location groups | Commenced, "Remaining in 2026"; one block point; the two notes in the caption role. |
| S3.5 | Chart | Title, range caption, axis labels and the two month labels in the caption size; line and dots in the link colour; gaps for months without a sale. |
| S3.6 | Scroll the details so the chart is half out of view, in both directions | The chart is clipped at the details' edge; nothing is drawn over the list or the heading. **Record the result explicitly; this is the open question from the offscreen captures.** |
| S3.7 | Latest registrations | Heading, caption, rows separated by hairlines; headings demibold and tabular; details in the secondary colour. |
| S3.8 | Reset the flat type filter to All flat types, then select 727 ANG MO KIO AVE 6 | Chart replaced by the "No registrations … 24-month window" caption; the inspector still shows four groups. |
| S3.9 | Reselect 748B, then switch to the dark appearance or colour scheme | Every label, value, note, chart element and separator remains readable; no hard-coded light colour shows. |
| S3.10 | Raise the system text size or scale factor one step | Labels wrap rather than clip; the label column does not exceed two fifths of the pane; the chart captions scale. |
| S3.11 | Screen reader through the details | Each group reads as a named group with a heading; each fact reads once as "Label: value"; each registration reads once. |
| S3.12 | Scroll the list and the details by wheel, trackpad and keyboard (↑/↓, Page Up/Down, Home/End); drag a row with the mouse | Both scroll smoothly and stop at their ends; a mouse drag on the list selects or does nothing but never flicks it. |
| S3.13 | `--fail-details 503` | Each selection shows the error and Retry; Retry re-requests; the inspector's four groups still show. |

### Production pass

Repeat S2.1 to S2.14 and S3.1 to S3.12 against production with these
invariants instead of the fixture values: the status bar's count matches the
list; searching `ang mo kio ave` returns results whose addresses all contain
those words, ranked with exact-word matches first; selecting any result opens
details whose four groups are populated and whose Sales note names the
dataset's latest month; an address with sales in the window shows a chart,
one without shows the empty caption; every filter narrows the count or leaves
it unchanged, never raises it, and Reset restores the unfiltered count; a map
marker click selects the address it names. Record the production head's
`generatedAt` from About.

### Captures

Take these as native screenshots of the active window, light and dark, and
keep them with the results (not committed): S2.3, S2.8, S2.11, S3.1, S3.5,
S3.6 (both scroll directions), S3.7, S3.9. On macOS note the scale; on KDE
note the scale factor and the Qt Quick Controls style that loaded.

## 3. Platform notes

### macOS (Cocoa, Metal)

- Build and run in the logged-in desktop session, not with the screen locked;
  window-level accessibility and input are otherwise blocked.
- Confirm Metal in the log if `QSG_INFO=1` is set, and the scale (2 on Retina).
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

Scenario mapping: F1 section 1 `recorded`/`production`; F2 S2.8, S2.13, S3.2;
F3 S2.1–S2.8; F4 S2.3, S2.5, S2.14; F5 S3.1–S3.5, S3.7, S3.8; F6 section 2
fault servers and S3.13; F7 S3.6, S3.12; F8 S3.5, S3.8, S3.9; F9 S2.11; F10
S2.10. Platform-specific items: S2.9, S2.12, S3.9–S3.11 and the platform
notes. A scenario's production column needs the production pass too.
