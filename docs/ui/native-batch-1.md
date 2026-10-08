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
exe="$PWD/src/HdbResale.App/bin/Release/net10.0/HdbResale.App"      # macOS: the bundle's executable
for mode in recorded keyboard high-zoom unreachable tile-failure production; do
  python3 tools/api_native_smoke.py --executable "$exe" --mode "$mode" --log "/tmp/batch1-$mode.log" || break
done
grep -il "warning\|qml" /tmp/batch1-*.log      # expected: nothing
```

On Fedora, export `QT_QPA_PLATFORM=wayland` before this block (section 3).

## 2. Interactive runs (both platforms)

Two passes of F1 to F10, first against the fixtures, then against production
(F6's faults on the fixtures only). Each terminal line below is one server;
launch the application from a second terminal with the printed URL.

```sh
python3 tools/api_fixture_server.py                       # F1–F10 fixture pass
python3 tools/api_fixture_server.py --refuse              # F6 unreachable API
python3 tools/api_fixture_server.py --fail-details 503    # F6 failed details
python3 tools/api_fixture_server.py --fail-tiles          # F6 tile failure (HDB_TILE_TEST=1, HDB_TEST_TILE_ENDPOINT)
HDB_API_BASE_URL=<url> dotnet run -c Release --project src/HdbResale.App --no-build
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
| S2.11 | Narrow the window below the breakpoint, then back | Map/Addresses toggles appear; focus moves to the pane shown; the selection survives. |
| S2.12 | Screen reader on (VoiceOver, Orca): move through three rows, select one, type a letter | One name per row with address, town, median, sales and month; "Selected …" once; the result count announced after a pause. |

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
| S3.8 | Select 727 ANG MO KIO AVE 6 | Chart replaced by the "No registrations … 24-month window" caption; the inspector still shows four groups. |
| S3.9 | Switch to the dark appearance or colour scheme | Every label, value, note, chart element and separator remains readable; no hard-coded light colour shows. |
| S3.10 | Raise the system text size or scale factor one step | Labels wrap rather than clip; the label column does not exceed two fifths of the pane; the chart captions scale. |
| S3.11 | Screen reader through the details | Each group reads as a named group with a heading; each fact reads once as "Label: value"; each registration reads once. |
| S3.12 | `--fail-details 503` | Each selection shows the error and Retry; Retry re-requests; the inspector's four groups still show. |

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
  More Actions › Configure Special Window Settings, or `qdbus org.kde.KWin
  /KWin queryWindowInfo`) shows no X11 window id. Anything through XWayland
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

Scenario mapping: F1 section 1 `recorded`/`production`; F2 S2.8, S3.2; F3
S2.1–S2.8; F4 S2.3, S2.5; F5 S3.1–S3.4, S3.8; F6 section 2 fault servers and
S3.12; F7 S3.6; F8 S3.5, S3.8, S3.9; F9 S2.11; F10 S2.10. Platform-specific
items: S2.9, S2.12, S3.9–S3.11 and the platform notes.
