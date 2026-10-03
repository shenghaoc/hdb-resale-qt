# Official-record fixture provenance

HDB and ACRA, via data.gov.sg. Adapted under the
[Singapore Open Data Licence](https://data.gov.sg/open-data-licence).
Retrieved 2026-10-03. Full downloaded-source SHA-256 hashes and URLs are pinned
in [provenance.json](provenance.json). The app credits HDB / ACRA / data.gov.sg
and links the licence. No endorsement or exact-location claim.

- [HDB resale registration prices since Jan-2017](https://data.gov.sg/datasets/d_8b84c4ee58e3cfc0ece0d773c8ca6abc/view):
  `transactions.csv` preserves all 11 official columns for the same six M2 rows.
  Added `source_row` is the 1-based CSV line including header: 34, 1188, 371,
  382, 7555, 24303. Local `HDB-<source_row>` IDs are snapshot references, not
  official transaction IDs. The source supplies no coordinates or unit numbers.
- [HDB Property Information](https://data.gov.sg/datasets/d_17f5382f26140b1fdae0ba2ef6239d2f/view):
  `address-evidence.csv` retains all 76 `blk_no` / `street` records on the three
  selected streets and adds their original CSV `source_row`. No postal or
  footprint ID occurs in this source. All six resale/property addresses match
  directly, before any normalization.
- [ACRA Information on Corporate Entities ('B')](https://data.gov.sg/datasets/d_3a3807c023c61ddfba947dc069eb53f2/view):
  `postal-address-evidence.csv` retains **all 30** assertions found for the six
  normalized addresses in this pinned source. Only `block`, `street_name`,
  `postal_code` are projected; added `source_row` means the original CSV line
  including header, not an entity/address ID. No company/person/unit/UEN fields
  are retained. Metadata reported last update 2026-09-16T10:00:35+08:00.
  These are registrant-supplied registered addresses in an official register,
  useful corroboration rather than authoritative HDB identity.
- [HDB Existing Building](https://data.gov.sg/datasets/d_16b157c52ed637edd6ba1232e026258d/view):
  `building-evidence.geojson` retains six complete original polygon features,
  including OBJECTID, ENTITYID, BLK_NO, POSTAL_COD and geometry. ST_COD remains
  an untouched raw field but **is not used**. Coverage is current, not a
  historical flat-level address source.

## Conservative C# linkage

For each valid resale, require exactly one explicit HDB property block/street
record. Find all ACRA assertions on that normalized block/street. Require them
to agree on one distinct six-digit postal code, then require exactly one HDB
footprint matching **both block and postal**. No entity is chosen from agreeing
corporate assertions; preserve them all. Conflicting postals or multiple
property/footprint records, including duplicate candidates, yield `Ambiguous`;
missing evidence yields `Unmatched`. Neither gets an arbitrary point. Accepted
transactions survive all identity outcomes and remain filterable/selectable.

Block normalization trims outer whitespace and uppercases only. Street
normalization uppercases and collapses whitespace, and expands `AVE` to
`AVENUE` / `CTRL` to `CENTRAL` only immediately before a final ASCII numeric
road suffix. These aliases are observed in the actual retained HDB/ACRA rows.
Leading zeroes, punctuation, block suffixes, road numbers and other
abbreviations are preserved. No fuzzy, spatial or street-code fallback exists.
`ExactAddress` requires the raw input fields to agree; all six real chains are
`NormalizedAddress` because ACRA spells out AVENUE/CENTRAL.

| Resale/property address | ACRA street | Postal | Assertions | OBJECTID / ENTITYID |
| --- | --- | --- | --- | --- |
| 509 ANG MO KIO AVE 8 | ANG MO KIO AVENUE 8 | 560509 | 5 | 937499 / 7861 |
| 510 ANG MO KIO AVE 8 | ANG MO KIO AVENUE 8 | 560510 | 1 | 946126 / 4194 |
| 449 CLEMENTI AVE 3 | CLEMENTI AVENUE 3 | 120449 | 9 | 942992 / 2806 |
| 461 CLEMENTI AVE 3 | CLEMENTI AVENUE 3 | 120461 | 6 | 945486 / 2850 |
| 503 TAMPINES CTRL 1 | TAMPINES CENTRAL 1 | 520503 | 3 | 936585 / 9869 |
| 505 TAMPINES CTRL 1 | TAMPINES CENTRAL 1 | 520505 | 6 | 937147 / 9870 |

Identity match quality and coordinate quality are independent. Explicit null
geometry keeps the unique identity matched but coordinates `Missing`.
Malformed geometry produces a rejected feature and visible diagnostic; valid
resales remain accepted. For each valid matched exterior WGS84 polygon ring,
C# calculates and rounds to 10 decimal places:

```text
longitude = (minimum vertex longitude + maximum vertex longitude) / 2
latitude  = (minimum vertex latitude  + maximum vertex latitude)  / 2
```

This is `BlockApproximation`, a bounding-box midpoint, not an area centroid,
interior guarantee, unit location or authoritative address point. Provenance
includes the original OBJECTID, ENTITYID and postal. The original five M2
midpoints remain unchanged; the legitimate sixth polygon now supplies block
510. Missing-coordinate behavior is tested without artificially omitting this
canonical official evidence.

## Bounded reproduction and limits

Full sources were downloaded during development through the official
`api-open.data.gov.sg/v1/public/api/datasets/<id>/poll-download` endpoint.
Full source files and expiring signed URLs are not checked in. Against those
pinned source bytes, verify SHA-256; select the six transaction source lines;
select all property records on the three streets; normalize and scan the entire
ACRA B CSV for the six block/street keys, retaining every assertion; then select
unique block/postal building features and recompute midpoints. Later source
updates may reorder rows or change coverage. Do not use snapshot row numbers
as durable global keys. No broader ACRA coverage or conflict-free production
join is claimed.

M2's block-set inferred street-code crosswalk and precomputed coordinate table
are preserved only in [docs/milestone-2](../docs/milestone-2/README.md) and Git
history. They are not packaged, loaded or used as fallback. The app reads only
these tiny local CSV/GeoJSON inputs, not the full sources. ACRA records and
current footprints do not prove an exact 2017/2018 location. No online geocoding
or invented fixture coordinates were used.
