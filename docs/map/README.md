# M7 map presentation

M7 keeps Qt Location's `osm` CustomMap and switches the raster source to
[OneMap Default](https://www.onemap.gov.sg/docs/maps/), matching the sibling
visualizer's actual Default/Night endpoints (its older GreyLite README is stale).
The URL prefix is `https://www.onemap.gov.sg/maps/tiles/Default/`; Qt appends
`%z/%x/%y.png`. This is the standard 256-pixel source, with documented zoom 11–19.
The always-visible overlay supplies the official logo and linked
“OneMap © contributors | Singapore Land Authority” credit. Qt's generated
copyright panel is replaced because it prepends its own Map/Data copyright text.
An application-specific `onemap-default-v1` cache prevents confusion with earlier
OSM custom tiles; existing caches are not deleted. Provider repository lookup is
disabled and prefetching remains disabled. No Search request/token, evidence
refresh, offline tile bundle or proxy is added.

Sources checked 2026-10-03:
- [Qt 6.12 OSM plugin parameters](https://doc.qt.io/qt-6/location-plugin-osm.html)
- [OneMap attribution and tiles](https://www.onemap.gov.sg/docs/maps/)
- [Default TileJSON](https://www.onemap.gov.sg/maps/json/raster/tilejson/2.2.0/Default.json)
- [Token exemption for map services](https://www.onemap.gov.sg/apidocs/authentication)
- [OneMap Terms of Use](https://www.onemap.gov.sg/legal/termsofuse.html)
- [API terms](https://www.onemap.gov.sg/legal/apitermsofservice.html)
- [Sibling constants, exact revision](https://github.com/shenghaoc/hdb-resale-visualizer/blob/3ead543a325559c6e9bc58db4025f78189af7279/src/shared/lib/constants.ts)

## Measurement contract

`HDB_SCALE_GATE=1` enables stage logs and exact QML address-key, representative
transaction, count, label and coordinate comparisons. Delegate callbacks only
increment counters and record the most recent creation/destruction timestamp.
Identity scans occur after complete model/list/map cardinalities agree, not in
per-delegate callbacks. `HDB_MAP_LIFECYCLE=0` disables lifecycle callbacks for an
instrumentation comparison. Normal UI runs do not collect lifecycle events.

- C# aggregation is timed separately from synchronous map-model notifications.
- Sidebar filtering, begin-reset and end-reset notifications are measured separately.
- Property notifications, revision observation, last delegate event and QML
  readiness are separate observations. QML includes its 25ms timer cadence and
  assertion overhead; it is not a GPU frame-time measurement.
- Neither synchronous notifications nor QML timing isolate Bridge marshalling,
  layout, painting, network or GPU cost. Tile readiness is not asserted by the
  native state gate; pixels are checked separately on the physical desktop.

The ordinary scale gate retains its 5-second per-state and 25-second process
acceptance limits. Explicit `--measurement` is a diagnostic-only run with
10-second states /45-second process, allowing a slow baseline to finish. Passing
that mode does not pass the ordinary acceptance limits. Baseline and optimized
measurements must use the same provider, inputs and assertion workload, with
cache/instrumentation differences disclosed. Final results are recorded in
`verification.md` after native verification.

## Stable-key incremental updates

`MapRowDiff` plans ordinal-key removals, insertions and value updates before the
first Qt notification. Contiguous removals are applied backwards; insertions
are applied forwards. Retained address keys never move relative to each other.
Matching `BeginRemoveRows`/`EndRemoveRows` and `BeginInsertRows`/`EndInsertRows`
pairs bracket the backing-list changes. `DataChanged` announces only roles whose
values changed; coordinates are explicitly compared rather than assumed stable.
No filtered transaction, summary grouping, geometry or identity rule changes.
The sidebar remains the existing virtualized model, independently reset.

These helpers are part of Qt Bridge's Model contract and native event dispatcher.
The inspected upstream source revision was
`7019264f1a771a1f44ec55c33aa748f693faba75`; installed Linux package support is
verified by native Debug/Release execution, rather than inferred to be identical
to that newer source. No unofficial native host patch or new package is used.

`HDB_MAP_UPDATE=reset` restores the baseline strategy only when the scale gate is
opted in. This permits like-for-like measurements of the final provider, pin
appearance, assertions and instrumentation. Normal UI uses incremental updates.
`--extended` exercises sixteen full/subset/different/empty/reset/selection-hidden/
repeat/zoom/pan transitions with the same 5-second state bound and a separately
stated 45-second process budget. Every completed snapshot checks unique keys and
all displayed fields; surviving keys must refer to the same QML objects.

All markers remain present at low zoom. Below zoom 13 their dots are 14px and
ordinary count labels are hidden; at zoom 13+ they are 24px with counts. The
selected address stays orange, 28px and raised above other pins at all zoom
levels, retaining its multi-transaction count label. This is density styling, not clustering or viewport culling.

The town-label array is cached from the immutable import. In the extended gate,
the alternate-town value is evaluated once before scanning rows; an early test
mistakenly evaluated that derived label in the per-row predicate, causing an
O(n²) oracle startup timeout. That introduced test bug was fixed without changing
any deadline or production filtering rule.

## Reentrant input and offline attribution

Bridge's synchronous model notification helpers may process QML events while
waiting for native acknowledgement. `UiMutationQueue` therefore serializes the
complete public UI mutation intents, including selection and property
notifications. Queued Town/Price inputs are evaluated when executed; FIFO order
preserves intermediate empty-state selection clearing. No event or exception is
silently swallowed. A focused regression reproduced the previously stale
removal range, and the native `--reentrant` gate injects four queued inputs from
`rowsAboutToBeRemoved` itself. It also checks selection clearing when an address
survives with a different visible transaction. Its negative fault is `skip-burst`.

The mandatory logo is bundled unchanged as a Qt resource, with
[official source/hash provenance](../../src/HdbResale.App/assets/README.md).
The logo therefore does not depend on a separate network image request when
cached tiles are displayed. Both native gates require `Image.Ready`, and a Python
test checks the exact PNG and local resource wiring. The linked text stays
visible; no raster tiles are bundled.

## Local tile-failure test

Normal launches retain the OneMap Default URL, attribution, and
`onemap-default-v1` cache. Only `HDB_TILE_TEST=1` enables the test-only endpoint
override: set `HDB_TEST_TILE_ENDPOINT` to a loopback HTTP(S) URL prefix ending in
`/`. Remote endpoints and URL credentials/query/fragment are rejected, and the
variable is ignored without the explicit opt-in. A test launch always uses a
fresh, empty cache of its own in the temporary directory, removed when the
process exits, so no earlier tile can hide a failure. As defence in depth that
directory must not contain, or lie inside, the production cache, wherever links,
junctions or Windows short names lead.

A loopback test server can return HTTP 503 for every XYZ request to reproduce
missing tiles in the normal native app without modifying system networking or
reading warm production tiles. The endpoint hook affects tile requests only; the
repeated-failure notice below is runtime behavior, and tile failures do not set
the existing map error overlay. `tools/api_native_smoke.py --mode tile-failure`
runs exactly this: the recorded Worker API (`tools/api_fixture_server.py`) and a
loopback tile server answering 503 to every request. The harness clears inherited
test variables and fails unless at least two tiles were refused through all of
Qt's attempts.

### Repeated tile failures and selected-address contrast

Address delegates use their palette's `highlight` for selection and `base` / `text`
for other rows, with hover and pressed feedback as tints of the row surface and a
palette-derived keyboard focus border. Row text is the palette's colour where it
reaches 4.5:1 on the fill actually shown (translucent colours composited over the
window), as on macOS (6.99:1 active, 14.13:1 inactive for selections); otherwise
opaque black or white, whichever reads better. Qt's generic palette, used
offscreen, pairs white with `#308cc6` at only 3.69:1; there selected text becomes
black (5.69:1).

After two tile requests exhaust Qt's retries, a small map notice reads: “Some map
tiles failed to load. Address results and details are not affected.” It remains
for that launch and does not change `Map.error`, the API data, filters, selection,
details, or marker behavior. A new launch starts without the notice. This is a
historical failure notice; it does not claim that every currently visible tile is
unavailable or that later requests cannot recover.

The `tile-failure` gate passes only when the notice is visible, `Map.error` is
unset, every recorded address is listed, and the selected address's details have
loaded. It also checks the actual colours of the selected delegate (highlighted,
distinct from the base surface, at least 4.5:1) and of the notice (at least 4.5:1).

Qt Location does not expose exhausted per-tile requests through `Map.error`.
The small `Native/TileStatus.cmake` library uses the public Qt Core
[`qInstallMessageHandler`](https://doc.qt.io/qt-6/qtlogging.html) API to count only
the exhaustion warning emitted by the pinned Qt 6.12 tile request manager. It is
installed once at startup, with one atomic swap, and never removed. It forwards
every diagnostic to the handler it replaced: Qt's own default handler unless the
application installed another, so the platform's sink (the debugger for a Windows
GUI app, the system log, stderr) is unchanged. It never formats or writes
diagnostics itself. A message arriving before the replaced handler is published
waits for it rather than being dropped. If Qt returned no handler, Qt's default
is restored and monitoring stays off. The counter is atomic and polled on the
QML/UI thread every 250 ms. No managed callback
or native allocation crosses P/Invoke. If external logging rules suppress this
warning, or a future Qt version changes its wording, this diagnostic-based notice
cannot observe it; rerun the `tile-failure` gate after a Qt upgrade.

Builds install the native helper beside the managed app. Linux packaging copies
this explicit P/Invoke dependency before collecting its ELF dependencies. Native
`ctest --test-dir src/HdbResale.App/obj/Release/net10.0/qt/native/build
--output-on-failure` verifies classification, concurrent requests, forwarding
every diagnostic exactly once to an earlier handler, idempotent installation, and
that diagnostics reach Qt's default sink when no custom handler exists.
