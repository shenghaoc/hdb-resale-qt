# M6 verification, 2026-10-03

Executed on macOS arm64 with README's pinned toolchain:

- Root `dotnet build`, `dotnet test`, `dotnet build -c Release`,
  `dotnet test -c Release`: successful; **46 C# tests in each configuration**,
  no skipped/failed tests. MSBuild summaries show zero warnings/errors. Existing
  native configure notices about Qt private headers and optional TaskTree remain;
  no warnings were suppressed.
- Python unittest discovery: **13 tests passed**, including deterministic
  minimized scale generation and rejection of changed source bytes.
- Canonical Debug/Release native gates: all ten ordered transitions plus
  clean native/C# exit; no QML/runtime error signatures.
- Full-corpus Debug/Release native scale gates: all nine ordered transitions,
  including nonempty combined budget, exact model/delegate cardinalities,
  selection/empty clearing, reset, zoom/pan/recenter, and clean exit.
- Deliberate `skip-empty` canonical Release fault: harness exit 1, timeout phase
  5, no acceptance. Full-corpus scale fault: harness exit 1, timeout phase 4,
  no acceptance. These prove rejection rather than treating process launch as success.
- Independent full-corpus input regeneration byte-compared all four derived
  data files. The manifest pins exact source counts (241,920 resales, 13,357
  property records, 94,785 ACRA B assertions, 13,436 building features) and sizes.
- Indexed/reference differential tests preserve every serialized import field
  in six and frozen-416 inputs. Frozen M4 report byte comparison passed. Frozen
  M5 historical report regenerated with SHA
  `d76614762c351eb121d3dd11ed03658e53daae1a5feeae36809a029b7e62d4be`;
  established 22/388/1/5 match and 410/6 coordinate counts unchanged.
- Full-size parser regression generates 241,920 unique source rows, verifies
  no rejection or row loss, one conserved located-address summary, and selection
  of the last large-model row followed by hidden invalidation. Other tests cover
  aggregation order independence, even median, counts/filter conservation,
  retained selectable unlocated/ambiguous rows and inconsistent-point rejection.

Raw timing/memory data are in the adjacent JSON reports. The final staged
Release data run measured first/later CSV parse 1,474.8 / 1,162.5 / 1,077.2 ms;
fact validation/creation 130.1 / 139.7 / 75.2 ms; matching/location resolution
91.3 / 63.0 / 47.0 ms; transaction object creation 25.8 / 38.4 / 35.1 ms.
Later state construction was 2.9–3.3 ms, filters 1.1–7.1 ms, and aggregation
27.2–33.6 ms. These are three observations, not statistically stable guarantees.

Final pre-heap native runs: Debug construction 2,364 ms, loaded 753 ms, reset
22 ms C# / 301 ms QML; Release construction 2,284 ms, loaded 745 ms, reset
18 ms C# / 306 ms QML. Both expose 236,791 sidebar rows / 1,920 address markers
at the default budget, 9,441 / 279 for ANG MO KIO, and 6,469 / 264 with the
S$500k combined budget. QML timings include test cadence and transitions;
C# reset includes filter, summaries and synchronous Qt model notifications.
Tile latency and bridge-only cost are not isolated by these numbers.

## Physical Release desktop evidence

The adopted full-corpus Release configuration was launched through dotnet run,
with actual CUA desktop input and screenshots. Observed:

- Singapore OSM tile pixels and visible OSM contributor attribution, numbered
  address markers, complete import counts, and a usable virtualized transaction list.
- Town selected by physical ComboBox keyboard input: 9,441 ANG MO KIO rows,
  7,119 located transactions / 279 address markers, 2,322 unlocated.
- Exact sidebar transaction selection HDB-2 (406 ANG MO KIO AVE 10, S$232k)
  displayed its month, quality and property/ACRA/footprint provenance.
- Physical budget entry S$200k reduced that town to 22 transactions, 21 located
  in eight markers / one unlocated, and cleared HDB-2 selection. Zero budget
  showed empty list/map. Physical reset restored the default full-data counts.
- Physical map tap selected latest-month representative HDB-241715 at
  289 YISHUN AVE 6. Raising the selected address marker made its orange count-45
  pin visible above neighboring overlapping markers; sidebar transactions remained intact.
- Drag moved the center; physical wheel input reached zoom 12.7 at approximately
  1.2999, 103.6849. Singapore button restored zoom 11 / 1.3521, 103.8198.
- App quit through native keyboard input; launching terminal process exited 0.

The final fixed-width Flickable wrapping recheck is currently blocked because
CUA reported the Mac locked. Earlier physical inputs used the same corpus,
filter/domain state, aggregation, selected-marker raising and renderer, with
an intermediate ScrollView sizing implementation. Final Flickable implementation
passes both automated gates with no binding-loop warnings, but its pixels are
not yet claimed physically rechecked. The final diagnostic heap opt-in does not
change normal UI behavior and only runs inside an explicitly opted-in scale gate.

Initial UI issues were ordinary introduced app issues: C# arrays did not support
QML indexOf; labels/ScrollView implicit sizing caused a binding cycle; overlapping
markers hid selection. They were resolved without suppressing gate error checks.
There is no credible upstream Qt issue/repro/report here. Linux/Windows GUI,
physical pinch, signed distribution and cold tile/network benchmarking are unrun.

## Publication

Audit every candidate commit before every push with `tools/audit_publication.py`,
using the private raw reference **outside Git**. It traverses all reachable
commit trees/blobs, checks raw export identity/header, actual cached names and
full-precision coordinate pairs, credential-shaped content, excluded local paths,
unexpected large blobs, every historical projection's field whitelist, and
unreachability of the previously excluded local object. Reviewed immutable
HDB source-street substring collisions are narrowly pinned to their exact blobs.
No raw cache data or matching private values are printed by the audit.

The implementation checkpoint `e9ddfe1c0ed3df222f4fe1af248d2fd68c12c15e`
was pushed to `origin/milestone-6-scale` and fetched back with matching SHA.
The complete pre-push audit covered 13 commits / 151 unique blobs / 91 paths,
with zero issues and the excluded object unreachable. Exact-head
[Actions run 37105028317](https://github.com/shenghaoc/hdb-resale-qt/actions/runs/37105028317)
completed successfully: scoped Release domain/test build, all 46 C# tests, and
all 13 Python tests. This proves Linux Qt-free checks, not Linux GUI support.

The final documentation checkpoint will undergo the same audit and exact-head
CI verification. Main remains at M5 `2a4a1e99429e1d9b83b3b827443df971c77a2409`
while the required final desktop wrapping/budget recheck is blocked by the
locked Mac. After manual unlock, physically verify the final Release app and
budget above S$1m, close it cleanly, update this record, audit/push the branch,
verify exact-head CI, then fetch and fast-forward main only if origin/main is
still an ancestor. Audit again before main push and verify local main,
origin/main and GitHub default HEAD match the final commit. No force push,
tags, release, PR or unrelated history rewrite is part of this milestone.
