# M9 full-corpus address coverage

Baseline is the clean M8 commit `a27657099623c211bf207e1e4cc4b057ead4c953`,
measured before adding evidence or geometry support. Exact pinned full input
hashes remain in [M6 manifest](../scale/manifest.json).

The complete [failure inventory](baseline-failures.csv) contains all **7,834
unlocated raw town/block/street identities and 189,406 transactions**, with
actual runtime quality and explicit retained source references. The
[baseline summary](baseline.json) conserves all **9,755 addresses / 241,920
transactions** and records the complete footprint diagnostics.

| Strongest evidenced cause | Addresses | Transactions |
| --- | ---: | ---: |
| No ACRA B postal assertion under existing normalization | 7,828 | 189,278 |
| Conflicting ACRA postal assertions | 3 | 63 |
| Exact source footprint rejected: present ENTITYID 0 | 1 | 26 |
| Exact source footprint unsupported: MultiPolygon | 1 | 36 |
| No property record under existing normalization | 1 | 3 |
| Located | 1,921 | 52,514 |

The missing-property address is 82 MACPHERSON LANE. This is absence from the
pinned property extract under the matching rules, not proof of an invalid or
historically nonexistent address. ENTITYID 0 affects 337 ANG MO KIO AVE 1;
the positive-ID contract remains. MultiPolygon affects 226A SUMANG LANE,
corroborated by ACRA B row 39538 postal 821226 and original footprint OBJECTID
939045. The other nine MultiPolygons cannot be attributed to an address by
block alone. Conflicts remain 204 CHOA CHU KANG AVE 1, 356 HOUGANG AVE 7 and
446 HOUGANG AVE 8. A majority of assertions never overrides a conflicting row.

## Reproduction

`--address-coverage INPUT OUTPUT` writes every raw address, its complete source
assertions, transaction IDs/count, actual matching and coordinate qualities.
`tools/address_coverage.py --baseline OUTPUT --footprints ORIGINAL_GEOJSON
--output DIRECTORY` writes the complete unlocated inventory and totals. The
baseline C# JSON was captured from the exact pre-change M8 Domain assembly;
the added report command applies the same grouping/serialization. Full generated
reports/data remain local; the complete address failure inventory is checked in.

Reasons follow runtime decision precedence. Only after a runtime MissingFootprint
result does the inventory inspect every original feature matching both block and
explicit asserted postal. It distinguishes absent source geometry, unsupported
type and rejected identity, without guessing postals, street codes or coordinates.
No new normalization rule is present. Canonical six, frozen 416 and M5 projection
inputs are unchanged.

## Narrow geometry support

MultiPolygon is accepted as **one original footprint feature**, with a bounding
box spanning all component exterior rings. No component wins by position, size or
proximity. Every component exterior ring must satisfy the same finite numeric,
range, minimum-position and closure checks as Polygon; a bad later component
rejects the whole feature. Interior rings and extra ordinates keep existing
Polygon semantics. No geometry repair or footprint split occurs. Scalar bounds
remain value types; the importer retains no coordinate lists or JsonElement.
ENTITYID must remain positive. A point is only BlockApproximation and may lie
outside/between components.

On original ACRA B evidence alone, support adds exactly **1 address / 36
transactions** (226A SUMANG LANE), bringing located counts to 1,922 / 52,550.
All other address outcomes and existing coordinates are unchanged. Footprint
rejections drop from eleven to one (the same ENTITYID 0 feature). This is
separate from gains attributable to additional public postal assertions.
The frozen M5 experiment explicitly keeps its original Polygon-only policy so
its report and diagnostics remain byte-identical; `--address-coverage-baseline`
similarly reproduces the original full-data inventory.

## Evidence import contract

The ordinary postal CSV may additionally include `source_dataset`, an exact
`d_` plus 32 lowercase hex data.gov.sg ID. Every assertion retains this ID and
its physical source CSV ending line, preventing row-number collisions across
partitions. Legacy files have no added serialized field and retain their original
validation and evidence output. No assertion is deduplicated by address/postal
or chosen by frequency. Dataset identity is provenance, not proof of accuracy.

For source-qualified rows a malformed postal is retained verbatim as unresolved
evidence with a visible diagnostic. Any address carrying such an assertion becomes
Ambiguous (`InvalidPostalAssertion`), separately from conflicting valid postals.
Nothing is padded, repaired, or dropped so another valid row can win. Invalid
source dataset identities themselves are rejected, with a diagnostic. This
conservative extension does not change validation of legacy unqualified CSVs.

An optional `historical-postal-evidence.json` in an explicit input directory may
contain only the already approved, byte-exact M5 minimized projection. Its SHA-256,
origin, source hash, raw-row count and frozen-sample hash are checked. An altered
file contributes no assertions and produces a diagnostic. Matching still requires
exact producer key, exact search string and six-digit cached postal. Only its
378 known identities can contribute; missing corpus entries are not API no-match.
The private 10,333-row cache is unavailable and neither reconstructed nor fetched.
Historical first-page/first-result assertions remain weaker and separately
attributed. Dataset-qualified source rows and historical limitations are visible
in selection details. Historical lookup occurs once per memoized raw address,
not per repeated transaction, without changing match semantics.
