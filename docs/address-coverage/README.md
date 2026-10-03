# M9 full-corpus address coverage

Final expanded-profile results: **7,618 located addresses / 188,573 transactions**.
See [normalization rules, final comparison and systematic audit](normalization/README.md).
The unchanged-rule source-expansion stage below is preserved separately to isolate
public-source gains from normalization gains.


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

## Preserved unchanged-rule source-expansion stage

All 241,920 transactions / 9,755 addresses remain accepted. These counts use the
same corpus and unchanged AVE/CTRL normalization. [Complete comparison](comparison.json),
[all changed addresses](changed-addresses.csv), [remaining failures](remaining-failures.csv),
and [source investigation/provenance](sources.md) are retained.

| Evidence stage | Located addresses | Located transactions |
| --- | ---: | ---: |
| Original M8: ACRA B, Polygon-only | 1,921 | 52,514 |
| Geometry support only | 1,922 | 52,550 |
| All 27 public ACRA partitions + geometry | 2,513 | 66,256 |
| Public evidence + bounded historical M5 assertions | 2,801 | 75,737 |

Net gain is **880 addresses / 23,223 transactions**, comprising 940 newly located
addresses / 24,345 transactions and **60 previously located addresses / 1,122
transactions now withheld** because additional source rows conflict or contain
malformed postal evidence. This is a conservative evidence correction, not a
conflict to erase. The 1,861 preserved matches retain exactly their points and
footprints. No Exact-to-Normalized or point-movement change occurred.

The historical marginal gain is separately **288 addresses / 9,481 transactions**,
all Unmatched-to-Normalized, using no public postal assertion at those addresses.
These are explicitly weaker first-hit matches, not new exhaustive Search evidence.
The bounded projection has entries for 378 corpus addresses / 12,665 transactions:
375 first-hit records and 3 absent keys. It also exposes 2 postal-without-footprint
addresses / 128 transactions, which remain unlocated. Repeated transactions at
an address do not make an assertion more certain.

| Quality | Baseline addresses / transactions | Final addresses / transactions |
| --- | ---: | ---: |
| ExactAddress | 682 / 22,234 | 897 / 28,396 |
| NormalizedAddress | 1,239 / 30,280 | 1,904 / 47,341 |
| Ambiguous | 3 / 63 | 64 / 1,196 |
| Unmatched | 7,831 / 189,343 | 6,890 / 164,987 |
| BlockApproximation | 1,921 / 52,514 | 2,801 / 75,737 |
| Missing | 7,834 / 189,406 | 6,954 / 166,183 |

Remaining causes: 6,886 addresses / 164,830 transactions without postal
corroboration under these rules; 63 / 1,105 with conflicting valid postals;
1 / 91 with malformed postal evidence (34 WHAMPOA WEST, literal `1233` from ACRA S
row 126378, not padded); 1 / 26 with rejected ENTITYID 0; 2 / 128 with no original
HDB block/postal footprint; and 1 / 3 missing the HDB property record. The last
address, 82 MACPHERSON LANE, also has contradictory postal assertions; the primary
runtime reason is missing property, but the independent conflict audit includes it.

## Mechanical and manual audit

The [independent outcome audit](independent-outcome-audit.json) and generated
[comparison](comparison.json) verify every one of 9,755 address evidence sets,
all 241,920 unchanged transaction IDs, every property/public/historical assertion,
all candidate sets and every resulting coordinate independently from original
HDB geometry. Exact midpoint rounding follows .NET's scaled ties-to-even policy;
Python's built-in decimal-place rounding differed at one binary halfway value,
which the checker corrected rather than tolerating a changed app point.

There are 2,872 changed evidence chains: 1,001 changed outcomes and 1,871
provenance-only additions. Merely adding agreeing source rows is not labelled a
weakened identity. The complete changed-address ledger preserves both classes.

[Manual review summary](review-summary.json), [mandatory review ledger](manual-required-reviewed.csv)
and [ordinary sample ledger](manual-sample-reviewed.csv) record actual review:

- All **65** conflicting/malformed address sets, including the missing-property
  address, every new ambiguity and all 60 formerly matched changes. Distinct
  postal variants were checked against original source rows, property evidence
  and full original footprint candidates. Contrary minority rows remain.
- All **238** deterministic ordinary addresses: 71 newly public-located,
  90 historical-only and 77 agreeing provenance additions, across 26 towns and
  all four source periods. Selection is SHA-ranked by town/period/change-class,
  then deduplicated; it is independent of desired success.
- The separately introduced real MultiPolygon address, with both original
  component exterior rings and the resulting union-bounds point.

No evidence-chain inconsistency was found. This is **manual record-chain review**,
not ground truth, independent postal validation or 100% manual review of all
ordinary gains. Every changed address was mechanically checked; only the stated
mandatory set and sample were manually inspected. Historical returned identity,
candidate uniqueness and historical validity remain unverified.

## Regenerating full data

Full source exports/projections/reports stay local rather than entering Git.
The checked-in provenance and tools reproduce them from exact snapshot bytes:

```sh
# Optional, explicit public acquisition. Future monthly bytes may fail hashes.
python3 tools/acquire_address_sources.py --manifest docs/address-coverage/source-provenance.json --output /absolute/acra-snapshots
# Reuse the original pinned B.csv rather than downloading a replacement.
python3 tools/prepare_address_coverage.py --base /absolute/hdb-scale-full --source-directory /absolute/acra-snapshots --source-manifest docs/address-coverage/source-provenance.json --output /absolute/hdb-m9-public
python3 tools/prepare_address_coverage.py --base /absolute/hdb-scale-full --source-directory /absolute/acra-snapshots --source-manifest docs/address-coverage/source-provenance.json --historical docs/coverage/onemap/historical/benchmark.json --output /absolute/hdb-m9-full
# Run via the pinned native host in an actual desktop session on Linux.
HdbResale.App --address-coverage-baseline /absolute/hdb-scale-full /tmp/baseline.json
HdbResale.App --address-coverage /absolute/hdb-m9-public /tmp/public.json
HdbResale.App --address-coverage /absolute/hdb-m9-full /tmp/final.json
python3 tools/audit_address_coverage.py --baseline /tmp/baseline.json --public /tmp/public.json --final /tmp/final.json --directory /absolute/hdb-m9-full --footprints /absolute/buildings.geojson --output /tmp/m9-audit
HDB_DATA_DIRECTORY=/absolute/hdb-m9-full HdbResale.App
```

`source_row` is an ending physical CSV line, including the header. Projection
order is manifest partition order and original row order. Every full-source and
base-file hash is checked before generation; fresh hashes do not silently replace
pins. No new runtime network acquisition, renderer, database or dependency is
introduced. Canonical six-row startup remains the default without the explicit
input-directory override.
