# Independent M9 evidence-chain review — 2026-10-03

## Conclusion and scope

The unchanged-normalization candidate is internally consistent with the inspected evidence. This review covers only the existing AVE→AVENUE and CTRL→CENTRAL rules immediately before a final numeric suffix. No ST, RD, DR, CRES or other new alias was approved or applied by this review.

The final candidate conserves 241,920 transactions and 9,755 raw town/block/street identities. It locates 2,801 addresses / 75,737 transactions: 2,513 / 66,256 with public-record corroboration, plus 288 / 9,481 dependent on the bounded historical first-hit projection. These are evidence-chain outcomes, not verified residential returned-address identities or historical ground truth.

## Mechanical checks, independent of the application matcher

`inspect_sources.py` parsed all 2,117,243 original ACRA rows across all 27 official partitions. Each source hash matched the acquisition manifest. All 108,882 projected assertions exactly equal their dataset-qualified raw source rows in block, street and literal postal. Every eligible raw source row was retained, with no repaired postal, duplicate-removing collapse or voting. Source row means the physical ending line including the CSV header, not an official entity/address ID. The projection hash is `2408032fbbcdad12783c8c03a30a20728357cd2a41dfae23b7ff8f39344635e6`.

`check_report.py` checked all 9,755 candidate address rows against independently built full-source property and postal maps. Transaction IDs and membership are unchanged. All 2,801 resulting footprint identities are unique for their exact block and asserted postal; exterior-ring union bounds independently reproduce every final point. The checker uses the existing .NET scale/round/unscale semantics at ten decimal places. A Python `round(x,10)` tie at 522 HOUGANG AVE 6 differed by 1e-10; reproducing .NET's specified rounding resolves that verifier difference, without changing production coordinates.

The 378-entry historical sidecar is the exact existing hash-pinned projection; no additional private raw cache addresses were admitted. This is evidence preservation, not a fresh external validation of that cache.

## Manual review actually performed

- All 65 mandatory address sets: every distinct raw postal variant, source-qualified raw-row examples, full HDB property matches, exact block/postal footprint inventory and final disposition. Repeated agreeing corporate assertions were mechanically checked in full, not individually described as manually inspected.
- All 60 formerly matched address outcome changes, covering 1,122 transactions. These are members of the mandatory set; each now has contradictory or malformed evidence. None should be repaired by choosing the most frequent postal or the only postal with a footprint.
- All 238 distinct deterministic sample addresses: 71 newly located with public evidence, 90 historical-only additions and 77 agreeing-provenance additions. The sample uses the minimum SHA-256 rank per town × period × change class, deduplicated by address; the source generator specifies its seed and ordering. All 26 towns and four periods appear. The sampled addresses have 6,579 transactions collectively, not 6,579 independent manual address reviews.
- The sample includes 76 numeric AVE cases, six numeric CTRL cases, 68 letter-suffix blocks and one punctuated street. Examples checked include literal ST. GEORGE'S LANE, leading-zero postals at TELOK BLANGAH WAY and UPP CROSS ST, and BT BATOK CTRL / SERANGOON CTRL without a numeric suffix, which remain unexpanded. No new abbreviation equivalence is inferred from the historical sample.
- The sole geometry-only addition, 226A SUMANG LANE, was separately inspected even though it is not selected in the deterministic sample.

The reviewed ledgers retain all input evidence columns, observed transaction periods, the representative raw variants inspected and an explicit disposition. This is not a claim of exhaustive manual review of all 940 newly located addresses, all 1,861 preserved matches, or all 108,882 corporate assertions. Mechanical validation is exhaustive; ordinary manual review is deterministic sampling.

## Important mandatory findings

All 65 mandatory sets remain unlocated. There are 63 ordinary postal-conflict ambiguities, one malformed-postal ambiguity and one missing-property address that also has conflicting postal evidence. This distinction explains why the 65 source conflict sets yield 64 runtime ambiguous addresses.

- 34 WHAMPOA WEST: ACRA-S dataset `d_df7d2d661c0c11a7c367c9ee4bf896c1`, source row 126378, literally contains postal `1233`. Its 270 other assertions contain `330034`; raw footprint OBJECTID 942375 / ENTITYID 7319 corroborates the latter block/postal. The malformed assertion remains an explicit blocker. No padding, repair or valid-row winner is allowed.
- 82 MACPHERSON LANE: no HDB property record under the current normalization. The 88 public assertions contain `360082` and `368227`; the latter is ACRA-J row 32279. A raw footprint for block 82/postal 360082 does not supply the missing property link. Runtime MissingProperty is correct, and the conflict remains visible in the required-review ledger.
- 446 HOUGANG AVE 8: all 2,082 assertions are retained across nine distinct postals (`530021`, `530446`, `530466`, `530627`, `530635`, `530673`, `530836`, `538446`, `538784`). A unique source footprint for 530446 and 2,067 agreeing rows cannot override the other assertions. The original ACRA-B row 60428/postal 530836 remains present.

The 1,861 other previously matched addresses preserve match quality, footprint identity, coordinate quality and exact point. Zero previously matched address silently moves to a different footprint or coordinate. Added agreeing provenance is a change to evidence volume, not a source winner.

## Geometry and remaining evidence limits

226A SUMANG LANE, PUNGGOL, 36 transactions: raw property row 3551 matches the literal block/street; original ACRA-B row 39538 asserts postal 821226. Full-source feature 4595 is the sole block/postal footprint, OBJECTID 939045 / ENTITYID 11053760. Its MultiPolygon has two closed exterior rings with 7 and 75 vertices. Union bounds produce latitude 1.4027716141, longitude 103.8931010683, exactly the candidate point. This is a block approximation, not guaranteed interior or flat-level location.

The original positive ENTITYID requirement remains. Source feature 8384, OBJECTID 943424, block 337/postal 560337, has ENTITYID 0; the associated 337 ANG MO KIO AVE 1 remains unlocated. The historical-only 3 QUEEN'S RD/postal 266734 and 11 HOLLAND DR/postal 278859 also have no matching source footprint and remain unlocated.

All sampled historical-only additions satisfy the limited cache-key/query/postal + independent property + unique footprint chain. They do not establish the returned first-hit residential identity, exhaustive candidate uniqueness, current API validity or applicability at each 2017–2026 transaction date. No live Search or cached coordinates were used. Public ACRA registered-address agreement likewise remains corroboration, not an authoritative HDB ID relationship.

## Frozen review inputs and results

- Full candidate report SHA-256: `407c9e2683c2ab798334da6edb2aed381f486577896211958dc9ad58216a59e2`
- Public-only report SHA-256: `1faf6bab16f9f09f86371ffeab466ef69c05867a56c75c8544e8b64d8133c031`
- Required input CSV SHA-256: `e1fe47b3937b3d61d5d4323939d9b261f5209bc112ee34a2fd2bc1847485af00`
- Sample input CSV SHA-256: `4a10d86e9cb1cb4873ef9748b400551eb71f0b0d4923faec76eb84daff5db630`
- Reviewed required CSV SHA-256: `74e8b7b737213fefb30a036e82660e4937cdf4878e8640876c44a24d62ccb5fc`
- Reviewed sample CSV SHA-256: `479709a632958556607c6633a8399b232f452a0d12ef955491a4d9c698b69520`

`source-verification.json`, `report-verification.json` and `review-summary.json` record the exact completed counts and hashes. `conflict-source-packet.json` records all source-qualified assertions and raw source footprint candidates for the mandatory set. The audit scripts and staging packets are local supporting artifacts; full corporate CSVs and unrelated corporate/person fields are not publication material.
