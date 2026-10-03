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
- Sidebar begin-reset and combined filtering/end-reset are measured separately.
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
