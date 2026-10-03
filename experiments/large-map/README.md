# M10 controlled presentation comparisons

Run comparisons on the pinned Linux Qt 6.12 / Bridge 0.4.0-beta toolchain and
unchanged expanded M9 corpus. Native hosts/builds require the documented desktop
session. Never commit local input exports, downloads, generated geometry, native
build trees or credentials. No production renderer choice is implied by a spike.

## A: retain every delegate

`retention.patch` applies to exact M9
`bdc35e67cdff7ec52d2a9cc6cb144d9065c138df` in a disposable separate worktree.
It keeps all 7,618 address rows/delegates alive, notifies only active membership
and changed fact roles, and hides inactive items. The experimental gate validates
the active subset's exact roles/counts/identities. It is **not production
acceptance** and does not run the removal-triggered reentrant test, because the
experiment no longer emits removals. Classic and extended scenarios, startup,
role reads, lifecycle object totals and heap/working-set observations were run.
See the measurements for the rejection: retention makes several states slower
and keeps the large native object population/memory.

## Equivalent source snapshots for renderer comparisons

The small console exporter uses the production C# `CsvImport` and
`BlockSummaries` without Qt. It writes local-only GeoJSON point snapshots and
counts/timings for full, initial-budget, town, different-town, different-town
budget and empty states. Coordinates remain the original imported public-derived
approximate points; no private cache, lookup, new geometry or matching rules are
used. Every address key, representative ID, transaction count, label and median
is preserved in the feature properties. A cluster centroid is never exported as
an address coordinate.

```sh
dotnet run -c Release --project experiments/large-map/export/Export.csproj -- \
  /absolute/hdb-m9-expanded /absolute/local-comparison-data
```

Compare one complete GeoJSON source plus style layers, not one source/layer or
QML item per address. Selection-only changes, data upload, source completion,
filtering, startup, camera/viewport, memory and coordinate/count fidelity must
be measured or explicitly marked unverified. Serialization and C# aggregation
are distinct from native source update and frame observations.
