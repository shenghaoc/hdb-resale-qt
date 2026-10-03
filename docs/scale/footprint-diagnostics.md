# Full-corpus footprint diagnostic audit

The Domain importer audited here is unchanged from `35deb33`. This audit made no importer or source-data edits.

## Result

All 11 actual importer diagnostics are accounted for: 10 **valid but intentionally unsupported MultiPolygon geometries**, and 1 **source identity-contract failure**: a valid Polygon with `ENTITYID` present as numeric `0`, which the importer rejects because it requires positive IDs. None has malformed geometry; none of the 11 has a duplicate OBJECTID, ENTITYID, or block/postal match key. No required identity member is absent in any of the 11.

The isolated C# probe invoked `CsvImport.LoadDirectory` against the full pinned input using the existing Release Domain assembly. It confirmed **241,920 accepted transactions, 11 rejected footprint features, and exactly the 11 diagnostics below**. No transaction was rejected by these footprint diagnostics.

## Pinned source

- [HDB Existing Building](https://data.gov.sg/datasets/d_16b157c52ed637edd6ba1232e026258d/view), same source bytes as `docs/scale/manifest.json`.
- SHA-256: `7c987511548de3a82da403cabca02702031e02ade8703a9e401417883dfeb702`.
- 13,436 source features: 13,426 Polygon and 10 MultiPolygon.
- The importer diagnostic `Row` is a **1-based feature ordinal**, not a physical GeoJSON text line or OBJECTID. JSON array index is `Row - 1`.

## Per-feature classification

All rows have Feature type `Feature`; all geometry checks passed. Rings lists give ring counts per component polygon.

| Feature ordinal (array index) | OBJECTID | ENTITYID | Block / postal | Geometry | Polygons / rings | Position count | Class / exact cause |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 2659 (2658) | 938222 | 11053727 | 206 / 360206 | MultiPolygon | 3 / 1,1,1 | 99 | U: valid unsupported geometry |
| 2684 (2683) | 938224 | 11054513 | 210 / 120210 | MultiPolygon | 2 / 1,1 | 116 | U: valid unsupported geometry |
| 4595 (4594) | 939045 | 11053760 | 226A / 821226 | MultiPolygon | 2 / 1,1 | 82 | U: valid unsupported geometry |
| 4951 (4950) | 940369 | 11054584 | 209A / 121209 | MultiPolygon | 2 / 1,1 | 70 | U: valid unsupported geometry |
| 5349 (5348) | 939790 | 2458 | 928 / 520928 | MultiPolygon | 2 / 1,1 | 92 | U: valid unsupported geometry |
| 5943 (5942) | 940828 | 11051329 | 222 / 360222 | MultiPolygon | 2 / 1,1 | 14 | U: valid unsupported geometry |
| 8384 (8383) | 943424 | 0 | 337 / 560337 | Polygon | 1 / 1 | 35 | I: present numeric ENTITYID 0 fails > 0 |
| 8427 (8426) | 943436 | 11056394 | 166 / 610166 | MultiPolygon | 2 / 1,1 | 42 | U: valid unsupported geometry |
| 10207 (10206) | 945811 | 11052707 | 180 / 640180 | MultiPolygon | 2 / 1,1 | 99 | U: valid unsupported geometry |
| 10246 (10245) | 945822 | 11654 | 3 / 768020 | MultiPolygon | 2 / 1,1 | 81 | U: valid unsupported geometry |
| 11283 (11282) | 946168 | 11054633 | 63 / 610063 | MultiPolygon | 2 / 1,1 | 14 | U: valid unsupported geometry |

### Exact current diagnostics

- **U**, each of the 10 MultiPolygons: `Expected Polygon with an exterior ring of at least four positions.` The rejection occurs at `CsvImport.cs:170–173` because the geometry type is not `Polygon`. It is not evidence of malformed or short rings.
- **I**, feature 8,384: `Footprint OBJECTID, ENTITYID, block and six-digit postal code are required.` The exact failing predicate is `entityId <= 0` at `CsvImport.cs:126`; OBJECTID 943424, block `337`, and postal `560337` pass their checks. `ENTITYID` is present, numeric, and within Int32 range; its value is 0. Its Polygon has one 35-position exterior ring and would pass the supported geometry checks if considered independently.

## Validation evidence

- Python 3 independently checked the geometry nesting, nonempty polygon/ring lists, at least four positions per ring, identical first/last positions, finite numeric position values, longitude/latitude bounds, nonzero signed ring area, and CCW exterior winding. Every listed feature passed; there are no holes in these 11 features.
- The already-installed **GEOS 3.13.1-CAPI-1.19.2**, accessed through its C API using Python ctypes, parsed each exact source geometry via `GEOSGeoJSONReader_readGeometry_r`. `GEOSisValid_r` returned true and `GEOSisValidReason_r` returned `Valid Geometry` for every listed feature. No geometry repair/conversion was applied. [GEOS C API](https://libgeos.org/doxygen/geos__c_8h.html).
- This is structural/topological geometry validation, not verification of real-world building accuracy. MultiPolygon is a defined GeoJSON geometry type; Polygon-only support is an application scope choice. [RFC 7946, sections 3.1.6–3.1.7](https://www.rfc-editor.org/rfc/rfc7946#section-3.1.6).
- All 13,436 OBJECTID values are unique; all 13,436 ENTITYID values are unique, including the sole zero. Each of the 11 diagnosed features has exactly one source feature with its normalized block + postal key. There is no duplicate-source explanation for any listed diagnostic.
- The source attribute legend only calls ENTITYID numeric and supplies no semantic description. Consequently, this audit does **not** label 0 a proven missing-value sentinel, claim a source-required-positive constraint, or claim that an absent member caused rejection. The positive-ID condition is the current importer contract.

## Recommendation

No importer bug or mishandling of an otherwise accepted geometry type was demonstrated by these 11 diagnostics. Do not relax identity checks, introduce MultiPolygon support, suppress diagnostics, modify source data, or alter frozen outputs as part of this audit. No behavior-changing bugfix/regression is warranted. A documentation-only refinement can replace the vague “one invalid/missing required identity field” with “one Polygon whose present numeric ENTITYID is 0, rejected by the importer’s positive-ID rule.” Retain the 10 + 1 counts and state that every source feature has its required members present.

Any future MultiPolygon support is a separately scoped feature, not a fix for malformed source geometry. Any future acceptance of ENTITYID 0 requires an explicit policy decision or authoritative source semantics rather than guessing from this one record.
