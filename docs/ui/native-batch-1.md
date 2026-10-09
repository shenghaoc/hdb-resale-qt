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
if dotnet build -c Release -m:1 && dotnet test -c Release --no-build -m:1; then   # the rest runs only on a fresh, tested build
case "$(uname)" in
  Darwin) # the staged bundle, so macOS and its accessibility tools see the application, not a bare host
    exe="$PWD/src/HdbResale.App/obj/Release/net10.0/HdbResale.app/Contents/MacOS/HdbResale.App" ;;
  *)      # Linux: the published host
    exe="$PWD/src/HdbResale.App/bin/Release/net10.0/HdbResale.App" ;;
esac
export QSG_INFO=1      # the scene graph logs its backend; record it (macOS must say Metal)
modes="recorded high-zoom unreachable tile-failure production"
python3 tools/api_native_smoke.py --help | grep -q keyboard && modes="keyboard $modes"   # Stage 2 adds the keyboard gate
failed=
for mode in $modes; do
  python3 tools/api_native_smoke.py --executable "$exe" --mode "$mode" --log "/tmp/batch1-$mode.log" || failed="$failed $mode"
done
echo "failed modes:${failed:- none}"     # expected: none; a failed mode does not stop the others
grep -h "rhi backend\|Using QRhi\|backend:" /tmp/batch1-recorded.log | head -3     # record the backend line
# QML diagnostics ("QML Anchors: …", "file:…/Main.qml:123: …") and warnings; the gates' own "qml: HDB_…" lines are excluded.
grep -Ei "warning|binding loop|TypeError|ReferenceError|Unable to assign|is not a type|QML [A-Za-z]+:|\.qml:[0-9]+" /tmp/batch1-*.log | grep -v "qml: HDB_"   # expected: nothing
else echo "build or tests failed: nothing was launched, so no smoke result exists for this head"; fi
```

The `PASS` lines are the harness's; the logs also hold the gates' own
`qml: HDB_…` markers, which are not warnings.

The `keyboard` mode exists from Stage 2 onward (it is in `ui/address-search`
and so in the head under test); the block adds it only when the harness lists
it, so on `main` before Stage 2 merges the loop runs the other five and
Stage 2's rows stay unverified. On Fedora, export `QT_QPA_PLATFORM=wayland` before this block
(section 3). Launch the interactive runs below from the same `$exe`.

## 2. Interactive runs (both platforms)

Two passes of F1 to F10. Run the `case` block from section 1 in the launch
terminal too (or `export exe` there and open the second terminal from it), so
`"$exe"` is defined where the application is launched. The numbered steps
below are the **fixture pass**:
their addresses, counts and orderings are the recorded snapshot's. The
**production pass** repeats the same actions against the live API; where a
step's expectation depends on the fixture's data (addresses, counts, orderings,
figures) the invariants in "Production pass" replace it, and every other
expectation (appearance, scrolling, clipping, compact layout, modal isolation,
exit) applies as written, so F7 to F10 are judged on production too.
F6's faults run on the fixtures only. Each terminal line below is one server;
launch the application from a second terminal with the printed URL.

```sh
python3 tools/api_fixture_server.py                       # F1–F10 fixture pass
python3 tools/api_fixture_server.py --refuse              # F6 unreachable API
python3 tools/api_fixture_server.py --fail-details 503 --delay 3   # F6 failed details, held 3 s so the loading state is visible
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
| S2.1 | Press ⌘F (macOS) or Ctrl+F (KDE). On macOS then click a list row so focus leaves the search field, and choose Edit › Find Address… | Each time the search field takes focus with its text selected; the menu item does so from the list, not from an already focused field. |
| S2.2 | Type `bedok res` | Three results, 748A, 748B, 747A in that order; map shows the same three. |
| S2.3 | ↓ ↓ Return; then click another application's window, look, and click back | 748B selected once; details open; the cursor ring is visible in the active window; the row stays in view. While the window is inactive the cursor ring and focus rings are not painted and the selection uses the platform's inactive colour; both return on reactivation. |
| S2.4 | Type `e` (so `bedok rese`) | 748B stays selected and is not re-announced. |
| S2.5 | Type ` 747` | 748B hidden, selection and details clear, map highlight gone. |
| S2.6 | Escape, then Escape | First clears the search (six results, nothing reselected), second moves focus to the list. |
| S2.7 | With the list focused, type `4717` | Typing continues in the search field; results 748A and 747A (postal codes). |
| S2.8 | Replace with `588` | Empty state explains two matches hidden by the filters; Clear Search works. |
| S2.9 | ⌘L / Ctrl+L | Focus lands on the town filter; in a compact window the filters expand. |
| S2.10 | Narrow the window below the breakpoint with the filters collapsed; type `748` in the search (748A and 748B) and focus the list; open About from the platform's surface (the application menu on macOS, the status bar's About button on KDE); press ⌘F / Ctrl+F, then ⌘L / Ctrl+L, then Escape; widen the window again | No shortcut acts behind the dialog: the search field is not focused and the collapsed filters neither expand nor take focus; Escape closes About and nothing else, so the search still reads `748` with its two results and the status bar count unchanged (Escape clearing the search behind the dialog fails F10); on closing, focus returns to the list; a further Escape, with About closed, now clears the search. |
| S2.11 | Select 748B, then narrow the window below the breakpoint; click Map, then Addresses; activate “All addresses”; select 748B again; press ⌘L / Ctrl+L; widen again | Map/Addresses toggles appear. Map shows the map and focuses it; Addresses shows the selected address’s details and focuses “All addresses”. Switching panes retains selection. “All addresses” clears selection and returns focus to the list. After reselecting, Filters expands the collapsed filters and focuses the town picker; selection survives Filters and widening. With no selection, Addresses shows and focuses the list. |
| S2.12 | Screen reader on (VoiceOver, Orca): move through three rows, select one, type `b` | One name per row with address, town, median, sales and month; "Selected …" once; after a pause the result count announced is five (the Bedok matches 747A, 748B, 748A, 115, 39), not the stale six; on production the spoken value equals the visible status bar count. Then Escape until the search is empty and six addresses show. |
| S2.13 | Select 748B, then set town ANG MO KIO; Reset. Then each filter from the defaults (Reset between them): town ANG MO KIO; flat type 4 ROOM; maximum S$500,000; minimum S$500,000; minimum S$600,000 with maximum S$500,000; maximum raised to its upper limit; Latest 12 months; then Reset | The excluded selection clears from the list, the map highlight and the details together. Then counts, with the list, the map and the status bar agreeing each time: 1 (727); 5 (747A, 748B, 748A, 115, 39); 3 (115, 39, 727); 3 (747A, 748B, 748A); 0 with the reversed-range explanation; 11 (relaxing the default S$1,000,000 cap widens the list: 58, 46, 588B, 10D and 588C appear on the list and the map); 4 (747A, 748B, 748A, 39: the window starts at 2025-11, and 115's latest sale is 2025-10); Reset restores six and every default. |
| S2.14 | Click a map marker (zoom in until 748B is an individual marker) | The list highlights and scrolls to 748B, the details open, and the marker is highlighted; "Show on map" from the details recentres on it. |
| S2.15 | Quit through the platform, twice with a relaunch between: macOS the application menu's Quit item, then ⌘Q; KDE the window frame's close button, then Alt+F4 | The window closes at once, the process exits with status 0 in the launching terminal, no error is printed, and nothing is left running. Launch again afterwards for the Stage 3 steps. |

### Stage 3: inspector, chart, clipping, typography, accessibility

| # | Step | Expected |
|---|---|---|
| S3.1 | Select 748B, read the Sales group | Five facts at once, from 748B's recorded summary: Registrations 10; Latest 2026-09; Median price S$837,500; Median per m² S$9,832.82/m²; Floor area 67.0–105.0 m²; and, when the details arrive, a sixth labelled "Middle half, all types" reading S$705,750–S$880,000 (the all-types interquartile range the contract's F5 requires); labels right-aligned in one column, values aligned, tabular figures; the note reads "Sales of all flat types in the 24 source months to 2026-10." |
| S3.2 | Set the flat type filter to 4 ROOM | Sales facts switch to the 4 ROOM cohort: Registrations 6; Latest 2026-02; Median price S$850,000; Median per m² S$9,770.11/m²; Floor area 87.0 m²; the note begins "4 ROOM sales"; "Middle half, all types" keeps S$705,750–S$880,000; the chart and registrations are unchanged. |
| S3.3 | Address group, with 748B selected | Town BEDOK; Flat types 3 ROOM, 4 ROOM, 5 ROOM; Models DBSS (the summary's models, not the detail's); Postal code 472748; Nearest MRT BEDOK NORTH MRT STATION · 317 m, about 4 min walk; "Nearest MRT" and "Postal code" on one line each. |
| S3.4 | Lease and Location groups | Commenced 2014; "Remaining in <the current year>" reading "about <99 − (year − 2014)> years of a 99-year lease", so "about 87 years" in 2026 (the label and figure follow the clock; record the year); one block point reading exactly 1.33629, 103.92124 (748B's summary coordinates to five decimals); the two notes in the caption role. |
| S3.5 | Chart, in the light appearance (set it now if the session is dark) | At the display's scale (2 on Retina) the line, dots, text and the map's markers are crisp, with no pixelation or blur. Title, range caption, axis labels and the two month labels in the caption size; line and dots in the link colour; gaps for months without a sale. Values, from 748B's recorded `monthlyTrend` over the window 2024-11 to 2026-10: the caption reads "2024-11–2026-10 · 10 sales in 10 observed months", the month labels read 2024-11 and 2026-10, the y axis runs 600 to 1,150 (thousands), ten dots, the highest at 2026-05 (S$1,060,000) and the lowest at 2026-06 (S$653,000), the first at 2024-12 (S$850,000) and the last at 2026-09 (S$663,000); 2026-03 is an interior gap between the 2026-02 and 2026-04 dots, with no segment across it. |
| S3.6 | Scroll the details so the chart is half out of view, in both directions | The chart is clipped at the details' edge; nothing is drawn over the list or the heading. **Record the result explicitly; this is the open question from the offscreen captures.** |
| S3.7 | Latest registrations, with the flat type reset to All flat types | Heading, caption, 20 rows separated by hairlines, newest first: the first reads 2026-09 · 3 ROOM · S$663,000 with the detail lines "67.0 m² · S$9,895.52/m² · storey 13 TO 15", "DBSS · lease start 2014" and "Source remaining lease at resale application: 86 years 11 months"; the second 2026-06 · 3 ROOM · S$653,000 with "67.0 m² · S$9,746.27/m² · storey 04 TO 06", "DBSS · lease start 2014" and "… 87 years 03 months"; months never increase down the list, and every row's heading and detail lines match the fixture's `recentTransactions` (`tests/fixtures/worker-api/details/bedok-748b-bedok-reservoir-cres.json`) in order; headings demibold and tabular; details in the secondary colour. |
| S3.8 | Reset the flat type filter to All flat types, then select 727 ANG MO KIO AVE 6 | Chart replaced by the "No registrations … 24-month window" caption; the inspector still shows four groups. |
| S3.9 | Reselect 748B, then switch to the dark appearance or colour scheme | Every label, value, note, chart element and separator remains readable; no hard-coded light colour shows; the chart's line and dots take the dark appearance's link colour (compare with a link in the platform's own dialog or settings, as S3.5 did in light) and the captions the dark secondary text colour. |
| S3.10 | Raise the system text size or scale factor one step | Labels wrap rather than clip; the label column does not exceed two fifths of the pane; the chart captions scale. |
| S3.11 | Screen reader through the details | Each group reads as a named group with a heading; each fact reads once as "Label: value"; each registration reads once. |
| S3.12 | Scroll the list and the details by wheel, trackpad and keyboard (↑/↓, Page Up/Down, Home/End); drag a row with the mouse | Both scroll smoothly and stop at their ends; a mouse drag on the list selects or does nothing but never flicks it. |
| S3.13 | Against `--fail-details 503 --delay 3`: select two addresses, press Retry on one | Each selection shows the error and Retry; pressing Retry shows the loading state for about three seconds (the server holds every response that long) before the error returns, and the server's terminal logs a new `/api/details/…` request for that address (the recorded handler logs every request); the inspector's four groups still show. |
| S3.14 | Against `--refuse`: launch, wait, press Retry; then stop the server, start `python3 tools/api_fixture_server.py --port <the refused port>` and press Retry again | The load error says the API could not be reached and offers Retry; the first Retry returns the same error (the refusing port rejects at once and cannot log, so the loading state may be too brief to see and is not required; the recovery below is the evidence that Retry re-requests); the window stays responsive, filters and About still open; the second Retry, against the healthy server on the same port, loads the six addresses and the healthy server's terminal logs the requests. |
| S3.15 | Against `--fail-tiles` with `HDB_TILE_TEST=1` and `HDB_TEST_TILE_ENDPOINT`: launch, select 748B, search `bedok` | The tile-failure notice appears over an empty map; the list, search, selection and details keep working; the notice stays legible in light and dark. |

### Production pass

Repeat S2.0 to S2.15 and S3.1 to S3.12 against production. The invariants
below replace the fixture's data-dependent values; S2.9, S2.15, S3.6 and
S3.10 to S3.12 keep their expectations as written; S2.11 selects any address
from the pinned production results instead of 748B, and S3.9 reselects the
address chosen under Chart (F8) below instead of 748B; S2.10 prepares
its search from the pinned summaries instead of `748`: type the postal code
of a listed address (one result) and require that text, that one identity
and the count of one to be unchanged after Escape behind About; S3.5 keeps
its expectations for the address chosen under Chart (F8) below. S3.7 on
production, with the flat type reset to All flat types: the first row's month
is the address's latest month shown in the list (the all-types month), and
months never increase down the list. Registration rows carry no address, so
ownership is checked against the Worker API directly: with the address's
`addressKey` read from `/api/block-summaries` (its `town`, `block` and
`streetName` identify it), fetch
`https://hdb-resale-visualizer.shenghaoc.workers.dev/api/details/<addressKey>`
with `curl` and compare its `recentTransactions` with the rows shown: the
heading (month, flat type, price) and every detail line (`floorAreaSqm`,
`pricePerSqm`, `storeyRange`, `flatModel`, `leaseCommenceDate`,
`remainingLease`) of every row in order; all must match before F5 is
recorded as passed. S3.8 on production: pick an address whose list row shows a
latest month older than the 24-month window (search by town and sort by eye,
or use the Any time window); if none exists on the day, record S3.8 as
unverified for production. S2.0: the launch completes without
a load error, the status bar's count matches the list and the map shows
markers; the count also equals the default filter applied to the API's own
summaries (every town and flat type, no window, median at most S$1,000,000):
pin the snapshot first, because the Worker's dataset can refresh under a
running session: `base=https://hdb-resale-visualizer.shenghaoc.workers.dev; curl -s $base/api/manifest > manifest.json; curl -s $base/api/block-summaries > summaries.json; python3 -c "import json; print(json.load(open('manifest.json'))['generatedAt'])"`,
and the printed `generatedAt` must equal the one About shows (if not,
relaunch the application and fetch again until they agree); every API
comparison in this pass then reads the saved `summaries.json`, and the detail
fetches of S3.7 are saved beside it. The endpoints are not version-addressed,
so the set is proved coherent afterwards: fetch the manifest again after the
summaries and again after the last detail fetch, and if its `generatedAt`
differs from the pinned value at either point, discard the saved responses
and restart the production pass from this step. The count equals
`python3 -c "import json; print(sum(1 for a in json.load(open('summaries.json')) if a['medianPrice'] <= 1000000))"`;
a lower count in the window means a partial load and fails F1. Search (F3): the map must hold exactly the result list's addresses,
and below zoom 15 the map groups results while above it shows only the
viewport, so counts prove nothing; identity is checked on the two small
queries instead: for `ang mo kio 10`, zoom to the Ang Mo Kio results until
each is an individual marker, click every marker and confirm each selects an
address on the result list and that no listed address is without a marker;
for the postal-code query, the map holds one marker and clicking it selects
that address; and because the map draws only the viewport above zoom 15,
stale markers are checked away from the results too: before each query note
one marker outside Ang Mo Kio (the S2.0 selection's location serves), and
after the query zoom to that location and confirm no marker remains there.
A marker that selects an address not on the list, a listed address without
one, or a marker surviving outside the results, fails F3. `ang mo kio ave` returns results whose addresses all
contain those words. For exact-block-before-prefix-block ranking, derive a
town and block-number query from the pinned summaries under the active
filters: there must be an eligible exact `block` and eligible blocks whose
`block` begins with it. The exact block must precede the prefix blocks, with
no result outside the named town. A street word such as `AVE 10` is not an
exact block and cannot substitute. In the 2026-10-04T15:30:00.000Z snapshot,
`geylang 30` provides exact 30 CASSIA CRES and prefix 301, 302, 304, 305
UBI AVE 1 under the default price cap. Derive the case again if the snapshot
changes; if no eligible case exists, record ranking as unverified. The
`ang mo kio 10` marker-membership check above is separate from ranking;
`ang mo kio avenue` `ang mo kio avenue`
returns the same results (abbreviation equivalence); the postal code read
from a selected address's Address group, searched on its own, returns that
address. Quiet refinement (S2.4): select a result of `ang mo kio ave`, then
extend the query with the next character of that address's street name so it
still matches; the selection, its map highlight and its details stay, and
with the screen reader on no second "Selected …" is announced. Details (F5): one address serves S3.1 to S3.9, so every detail
comparison reads one saved response. Choose it so S3.2 can act on it (its
`flatTypeCohorts` in `summaries.json` must hold a `4 ROOM` entry and its
`medianPriceByFlatType["4 ROOM"]` must not exceed the active maximum, or the
correct filtering logic clears the selection at S3.2), so the Address group
has every fact the pass asserts (its entry must carry a `postalCode`, a
`nearestMrt` and a non-empty `flatModels`; the inspector omits a fact whose
value is absent, so an entry without them cannot fail those facts) and so S3.5 can show a
gap (its detail response must satisfy the interior-gap rule under Chart (F8)
below); fetch its detail response once, before S3.1, and save it beside
`summaries.json`. Selecting it opens four populated groups whose
Sales group matches that address's `/api/block-summaries` entry
(`transactionCount`, `latestMonth`, `medianPrice`, `pricePerSqmMedian`,
`floorAreaRange`; with a flat type selected, that type's `flatTypeCohorts`,
`medianPriceByFlatType` and `medianPricePerSqmByFlatType` entries), whose
Address group shows that address's own town, flat types, postal code and
nearest MRT as the same entry gives them (`town`, `flatTypes`, `postalCode`,
`nearestMrt`, and Models from that entry's `flatModels` with All flat types
or from `flatTypeCohorts[<type>].flatModels` with a type selected; the detail
response's `summary.flatModels` is not the source and may list more), whose Lease group shows its `leaseCommenceRange` and
99 minus the years since it, whose Location group shows its `coordinates`
to five decimals, and whose
Sales note names the dataset's latest month, and once the details arrive
the Sales group shows "Middle half, all types" whose two values equal the
detail response's `summary.priceIqr` (the S3.7 curl); an address with sales in the
window shows a chart, one without shows the empty caption. Chart (F8, S3.5):
the details address's saved response must have a `monthlyTrend` (the list
shows at most 20 registrations, so it cannot be read off the
list) that lacks at least one month inside the 24-month window that is bracketed by
populated months on both sides (a missing month before the first sale or
after the last has no points to connect across, so it proves nothing); an
address with a sale in every month cannot show a gap. Confirm the line is
discontinuous between the two populated months around each such interior
gap, instead of a connecting segment; and confirm the values against the
same `monthlyTrend`: the caption's sales and observed-month counts are the
sums over the window's entries, the month labels are the window's first and
last months, the first and last dots sit at the window's first and last
populated months, and the highest and lowest dots sit at the months with the
highest and lowest `medianPrice`; the y axis runs from
floor(min ÷ 50,000) × 50 − 50 to ceiling(max ÷ 50,000) × 50 + 50 thousand
(min and max over the window's `medianPrice`, the lower bound never below 0),
so a chart keeping the fixture's 600 to 1,150 scale for a differently priced
address fails; and the highest and lowest dots each sit at the proportional
height (value − lower bound) ÷ (upper bound − lower bound) of the plot area,
judged against the axis labels (no ordering between their two margins is
implied; the bounds round outward independently). Filters
(F2): choose each bound from the live list so it must act: a maximum just
below a listed address's median excludes that address, a minimum just above
another's median excludes it, and a registration window shorter than the
time since a listed address's latest month excludes it; each change must
remove the named address and lower the count (a control that leaves the
count unchanged fails); the count after each change must equal the one
computed from `summaries.json` (maximum M: entries with `medianPrice <= M`;
minimum m: `m <= medianPrice <= 1000000`; window of N months: entries with
`medianPrice <= 1000000` and `latestMonth` at or after the dataset's latest
month minus N − 1 months), and three addresses from the computed set, spread
through it, must be on the list (the complete membership of a live list of
hundreds is not compared by hand: the predicate over the summaries is what
`AddressExplorerTests` prove exhaustively, and this pass proves the native
list, map and status bar follow it on the count, the named exclusion and the
samples); and for each of the three excluded addresses (the maximum's, the
minimum's and the window's), zooming to its location shows no marker there
while a listed neighbour keeps its marker; town narrows to its own addresses; for the flat type,
choose a type that one listed address advertises and a neighbouring listed
address does not (read `flatTypes` off the list's addresses or the summaries):
after selecting it the first address stays, the second leaves the list and
its location shows no marker, and every remaining address checked (at least
five, spread through the list) advertises the type; the count may rise
rather than fall, because the selected type's own median
is compared against the price cap, and raising the maximum above its
S$1,000,000 default widens the count (record the count at the upper limit,
which must exceed S2.0's); list, map and status bar agree after every
change; after the town filter, map membership is checked by identity, as for
F3: note two addresses from other towns that were listed before the filter,
zoom to each location and confirm no marker remains there, and confirm a
listed address in the chosen town has a marker whose click selects it; after
Reset the two markers are back. Hidden-by-filters (S2.8): with the maximum
set just below a listed address's median, search for that address by its
postal code (unique, unlike a block number, which also matches longer
blocks); the list shows the empty state explaining that a match is
hidden by the filters, and Clear Search works. Reset restores S2.0's default count (the default S$1,000,000
maximum still applies, so this is not every address the API holds). Selection (F4): a map marker
click selects the address it names; then, with that address selected, type a
search that does not match it (for example the postal code of another listed
address) and confirm the list selection, the map highlight and the details
pane clear together; clear the search, select the address again, pick a town
other than its own and confirm the same three clear together; Reset restores
the default count with nothing selected. Exit (S2.15): as on the fixtures. Record
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
- The Qt Quick Controls style must be `macOS` with `QT_QUICK_CONTROLS_STYLE`
  unset for the acceptance launch (the variable overrides the bundle's own
  style selection, so a forced `macOS` is diagnostic evidence only and does
  not satisfy the row); `QT_LOGGING_RULES=qt.quick.controls*=true` on one
  launch names the style that loaded. Record it; Fusion or another style
  fails the platform row.
- S3.5's crispness check is the Retina observation: no pixelated or blurred
  chart line, dots, text or map markers at scale 2.
- Light and dark: System Settings › Appearance for S3.5 and S3.9. Then,
  separately, the per-app appearance override macOS supports for any bundle
  (there is no supported per-app dark override, and the application reads no
  appearance argument of its own): with the system dark, run
  `defaults write io.github.shenghaoc.hdb-resale-qt NSRequiresAquaSystemAppearance -bool YES`,
  launch, check the controls and the chart follow the light appearance while
  other applications stay dark, then quit, run
  `defaults delete io.github.shenghaoc.hdb-resale-qt NSRequiresAquaSystemAppearance`
  and relaunch to confirm the window is dark again.
- VoiceOver for S2.12 and S3.11.

### Fedora KDE Plasma (Wayland)

- Runtime paths follow `docs/pr8-review-verification.md` (user-local .NET,
  Qt 6.12.0 `gcc_64`, venv CMake). Run `ldd "$QtDir/plugins/platforms/libqwayland.so"`
  first; no `not found` line is expected.
- `export QT_QPA_PLATFORM=wayland` for every launch in sections 1 and 2.
  Confirm the window is a Wayland client with a protocol-aware source, since
  KWin's window information reports the same fields for XWayland windows:
  open KWin's debug console (`qdbus-qt6 org.kde.KWin /KWin showDebugConsole`;
  Fedora ships the Qt 6 tool under that name), and on its Windows tab the
  application must be listed under the Wayland windows, not the X11 windows;
  as a second signal, `xlsclients` (from xorg-x11-utils) must not list it.
  Record both. Anything through XWayland
  does not count; record it as unverified, not failed.
- Record the Qt Quick Controls style that loaded (`QT_QUICK_CONTROLS_STYLE` in
  the session, or `QT_LOGGING_RULES=qt.quick.controls*=true`).
- Observe, with System Settings › Colours & Themes and a Plasma application
  such as Dolphin open beside the window, that the window uses the session's
  colour scheme (window, text, highlight and link colours match) and its
  general font (same family and size as Dolphin's), and that no control looks
  like a macOS control (no traffic lights, no macOS toggle or check box
  shapes). Record the scheme and font names.
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
F4 S2.3, S2.5, S2.13 (clearing through a filter), S2.14; F5 S3.1–S3.5, S3.7, S3.8; F6 S3.13, S3.14, S3.15 and, for its production column, S2.0 on production
(the healthy load); F7
S3.6, S3.12; F8 S3.5, S3.8, S3.9 (light and dark both observed); F9 S2.11;
F10 S2.10. Platform-specific items: S2.0 (window chrome, OSM tiles), S2.1 (the
platform's Find command), S2.3 (inactive-window rings), S2.9, S2.10 (About
from the platform's surface), S2.11 (resizing through the platform's frame
across the breakpoint, and the compact Filters command), S2.12, S2.14 (the
map's selection highlight), S2.15 (clean exit through the platform's
own quit or close), S3.5 (Retina crispness on macOS), S3.9–S3.11, S3.12
(wheel, trackpad, keyboard and pointer input, under Wayland on KDE) and the
platform notes, including the loaded style on both platforms and, on KDE,
the colour-scheme and font observation. A scenario's production column needs the production pass too.
