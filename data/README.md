# Official-data fixture provenance

Housing & Development Board (HDB), via data.gov.sg. Adapted under the
[Singapore Open Data Licence](https://data.gov.sg/open-data-licence).
Retrieved 2026-10-03. Source URLs and full downloaded-source SHA-256 hashes
are pinned in [provenance.json](provenance.json). The app visibly credits HDB /
data.gov.sg and links this licence. No endorsement or exact-location claim.

- [Resale flat prices based on registration date from Jan-2017 onwards](https://data.gov.sg/datasets/d_8b84c4ee58e3cfc0ece0d773c8ca6abc/view):
  `transactions.csv` preserves all 11 official columns for six records. Added
  `source_row` is the 1-based CSV line including the header: 34, 1188, 371,
  382, 7555, 24303. Local app IDs `HDB-<source_row>` identify this snapshot only;
  upstream has no transaction-ID or coordinate column. Month is registration
  month. No unit number is provided. The checked-in facts are real, not synthetic.
- [HDB Existing Building](https://data.gov.sg/datasets/d_16b157c52ed637edd6ba1232e026258d/view):
  `building-evidence.geojson` preserves five selected original polygon features
  with block, street code, postal code, OBJECTID and geometry. This is current
  building coverage, not historical flat-level geometry.
- [HDB Property Information](https://data.gov.sg/datasets/d_17f5382f26140b1fdae0ba2ef6239d2f/view):
  `address-evidence.csv` retains the official `blk_no` and `street` columns for
  all 76 blocks on the three selected streets. This dataset has no coordinates
  or postal codes; it is address-join evidence only.

## Explicit inferred join

Existing Building's `ST_COD` is not the resale street name. Block alone is
ambiguous and was never used alone. During offline fixture preparation, group
**all** downloaded building features by `ST_COD` and collect their `BLK_NO`
sets; group property records by `street` and collect `blk_no` sets. For each of
these three streets, exactly one code group has a completely identical set
across the full building dataset:

| Resale/property street | Inferred building code | Exact block-set equality |
| --- | --- | --- |
| ANG MO KIO AVE 8 | ANA09H | 21 / 21 |
| CLEMENTI AVE 3 | CLA06H | 43 / 43 |
| TAMPINES CTRL 1 | TAC10N | 12 / 12 |

[street-code-evidence.json](street-code-evidence.json) pins the matching sets
and explicitly labels this inference. The complete set agreement strongly
supports the crosswalk; it is **not an authoritative code dictionary** and is
not generalized to other streets. Each fixture address also occurs exactly
once in the official property table. Join street through that crosswalk, then
require exactly one polygon matching both code and block. Ambiguous or
unmatched addresses must stay unlocated.

## Deterministic derived coordinates

For the exterior WGS84 ring of each matched polygon:

```text
longitude = (minimum vertex longitude + maximum vertex longitude) / 2
latitude  = (minimum vertex latitude  + maximum vertex latitude)  / 2
```

Round each coordinate to 10 decimal places in `locations.csv`. This is a
bounding-box midpoint, not an area centroid, interior-point guarantee, unit
location or authoritative address point. It may lie outside an irregular
footprint. Its quality is **BlockApproximation**; source text pins OBJECTID,
street code, postal code and inferred join method. Tests recompute these points
from the checked-in original geometries and check the address/code/quality.

| Local transaction ID | Address | Polygon OBJECTID | Quality |
| --- | --- | --- | --- |
| HDB-34 | 509 ANG MO KIO AVE 8 | 937499 | BlockApproximation |
| HDB-371 | 449 CLEMENTI AVE 3 | 942992 | BlockApproximation |
| HDB-382 | 461 CLEMENTI AVE 3 | 945486 | BlockApproximation |
| HDB-7555 | 503 TAMPINES CTRL 1 | 936585 | BlockApproximation |
| HDB-24303 | 505 TAMPINES CTRL 1 | 937147 | BlockApproximation |
| HDB-1188 | 510 ANG MO KIO AVE 8 | omitted intentionally | Missing |

HDB-1188 intentionally has no coordinate-table entry to exercise partial local
coverage. This does not assert that the official building dataset lacks that
block. It remains a valid, filterable/selectable transaction in the sidebar;
only located rows enter the map model. No placeholder coordinate is generated.
No `Authoritative` or `StreetApproximation` entries are claimed by this fixture.

## Bounded offline reproduction

Full sources were downloaded only during development using the official
`api-open.data.gov.sg/v1/public/api/datasets/<id>/poll-download` endpoint's
returned download URL. Full source files and expiring signed URLs are not
checked in. To reproduce against those pinned snapshots, verify their SHA-256,
select the documented transaction rows, perform the set-equality/unique
address+polygon joins above, and recompute the midpoint formula. Later source
updates can reorder resale rows or change coverage; do not treat row numbers
or inferred codes as durable global keys.

The application reads only the two tiny CSVs locally. Evidence files are small
bounded extracts needed for review and tests, not generated build output or a
production ingestion pipeline. No live geocoding was used.
