# Final M9 systematic normalization audit — 2026-10-03

## Verdict

The opt-in `terminal-road-types-v1` profile passes the agreed evidence-chain audit: exhaustive mechanical validation, manual rule/source-context and conflict review, and deterministic outcome sampling. No source winner, majority vote, spatial inference or fuzzy address inference was used.

The final report conserves all **241,920 transactions / 9,755 addresses** and locates **7,618 addresses / 188,573 transactions**. The public-only stage locates **7,533 / 185,706**. The frozen historical projection supplies 86 additional located addresses / 2,951 transactions, but also blocks 11 HOLLAND DR / 84 transactions through a public-versus-historical contradiction. Therefore the combined result's net historical increment is 85 addresses / 2,867 transactions, not 86 / 2,951.

## Rules and manual semantics

The original AVE→AVENUE and CTRL→CENTRAL behavior remains limited to the token directly before a final ASCII-numeric suffix. Added ST→STREET, RD→ROAD and DR→DRIVE apply only at the last token or immediately before a final ASCII-numeric suffix. CRES→CRESCENT is **terminal-only**, because no numbered CRES context was observed. Every new replacement requires a preceding street-name token, so a first/standalone token is not reinterpreted.

The separate source reviewer directly inspected all **297 literal HDB-to-ACRA street spelling pairs**, then checked all 891 cited source contexts against the pinned raw ACRA rows and exact same-block HDB property street. All passed spelling semantics; none of these passes suppresses an address-level postal contradiction. The observed contexts are:

| Rule | Distinct pairs | Terminal | Before final ASCII number |
| --- | ---: | ---: | ---: |
| ST | 135 | 13 | 122 |
| RD | 99 | 98 | 1 |
| DR | 42 | 22 | 20 |
| CRES | 21 | 21 | unsupported |

The sole pre-numeric RD example is EUNOS RD 5. ST. GEORGE'S RD keeps its initial ST. and apostrophe; only its terminal RD expands. QUEEN'S RD preserves punctuation. Unsupported tokens, substring matches, punctuation repair, leading Saint and numerical spelling changes are outside the rule.

An independent C# probe compiled the production domain source and passed **47 authored normalization vectors**, five block-identity vectors and a legacy opt-out control. Negative cases include `ST`, `ST 11`, leading/internal ST, `ST. GEORGE'S LANE`, `FOO-ST`, `FOO BROAD`, `FOO RD.`, alphanumeric/signed/decimal/Unicode road numbers, two trailing number tokens, and numbered CRES. Leading zeros in numbered streets and block suffixes remain intact. The probe's files are `negative-vectors.json`, `boundary-results.txt`, and `boundary-probe/` in the independent audit staging directory.

## Complete mechanical validation

The independent source verifier reparsed **2,117,243 raw ACRA rows across all 27 partitions**, checked the pinned source hashes, and established both directions of projection completeness: every one of **311,110** projected assertions equals its source dataset/physical CSV row in raw block, street and postal, and every eligible raw row was retained. This includes **197 non-six-digit assertions at 18 addresses**. Their literals remain blockers under the six-digit contract; this review does not infer that the raw historical values were necessarily typographical errors.

The final partition/source-order projection SHA-256 is `d5b09b63e3123a4cdf3cbe3c934473d03bd8c6355b48e09639619833790d4584`.

A second independent checker reconstructed outcomes for **all 9,755 addresses**, not only successful results. It checked complete property and postal candidate sets, exact historical assertion membership, quality/reason, complete eligible footprint identity sets, transaction IDs, and every resulting coordinate. All **7,618** final footprint midpoints reproduce from the original footprint geometries using the existing .NET ten-decimal rounding semantics. The original positive OBJECTID/ENTITYID contract is retained; feature 8384 / OBJECTID 943424 / ENTITYID 0 for 337 ANG MO KIO AVE 1 remains rejected.

No raw address collisions, new duplicate-property winners or moved matched points were admitted. All 378 entries in the historical sidecar remain the exact previously approved hash-pinned projection, with no broader private-cache admission.

## Per-rule impacts

These counts compare the expanded profile with the unchanged-normalization all-partition stage. “New public locations” includes former historical-only matches gaining public corroboration. “New final locations” excludes already located historical matches and requires the combined final result to stay located.

| Rule | Token-affected addresses | Addresses gaining public assertions | New public locations | New final locations | New final ambiguities |
| --- | ---: | ---: | ---: | ---: | ---: |
| ST | 2,880 | 2,626 | 2,558 | 2,491 | 68 |
| RD | 1,582 | 1,354 | 1,293 | 1,226 | 61 |
| DR | 872 | 821 | 809 | 776 | 13 |
| CRES | 389 | 363 | 360 | 336 | 3 |

The seven context-specific address/transaction breakdowns remain in `normalization-impact.json`; all 5,164 evidence-changed address identities remain in the complete normalization ledger. RD's 61 ambiguities include 16 non-six-digit-address blockers; CRES's three include one; DR's 13 include the separate historical/public conflict at 11 HOLLAND DR.

## Manual conflicts and changed prior outcomes

All **210 required final addresses** were manually reviewed: 190 valid-postal conflicts, 18 non-six-digit-address blockers, one cross-source conflict and one missing-property address that also contains conflicting postals. Their runtime outcomes are 209 Ambiguous and one Unmatched/MissingProperty, all with no coordinate.

For every set, review covered every distinct raw postal variant, representative dataset-qualified raw rows, exact HDB property evidence, original block/postal footprint candidates and the final disposition. All repeated assertions were mechanically checked; there is no claim that every repeated agreeing corporate row was individually read manually. The 65 earlier reviewed conflict packets are value-for-value unchanged; all 144 additional public conflict contexts and the cross-source-only case were inspected in the expanded stage.

Specific decisions retained:

- **11 HOLLAND DR:** 97 public rows assert 271011 (for example ACRA-A row 32839); property row 484 and source feature 12614 / OBJECTID 946508 / ENTITYID 3348 support that block/postal. The pinned historical first hit asserts 278859. Public-only locates it; the combined profile must remain ambiguous. Neither source overrides the other.
- **3 QUEEN'S RD:** added public evidence contains non-six-digit 1024/1026, several six-digit postal values, and historical 266734. Its final blocker is no longer merely “missing footprint”; it remains explicitly ambiguous.
- **34 WHAMPOA WEST:** source ACRA-S row 126378 literally contains 1233. It is preserved against 270 rows asserting 330034, rather than padded or outvoted.
- **82 MACPHERSON LANE:** no matching HDB property; public assertions 360082 and 368227 remain visible. Its source footprint cannot supply the missing property link.
- **15 BEACH RD:** source rows heavily favor one postal without the HDB block/postal footprint. The minority block/postal with a footprint is not selected either. This directly exercises the prohibition on majority voting and unique-footprint overrides.

All **60 original-M8 matched downgrades / 1,122 transactions** are justified contradictory or non-six-digit cases. The other 1,861 original matches preserve quality, footprint and exact point. Relative to the intermediate unchanged-rule M9 report, **12 historical-only matches / 310 transactions** become ambiguous as the added public evidence exposes contradictions; 2,789 intermediate matches keep their outcome and point. All 12 are included in the manual conflict set. No previously matched address silently switches to a different footprint or coordinate.

## Deterministic sample and limits

The additional alias sample uses seed `hdb-m9-normalization-outcomes-v1`, NUL, raw town/block/street key. The fixed algorithm greedily covers town, period, rule-context and unusual-form tags with SHA-256 tie-breaking, then fills by hash to 64. It samples public-evidence gains that remain located in the final profile; conflicts are reviewed separately and exhaustively. An independent reconstruction from the frozen transaction/report inputs reproduced the exact same 64 selections from 5,019 eligible addresses and all 39 target tags (`sample-verification.json`).

All **64** selected chains were inspected: 31 pre-numeric ST, two terminal ST, 15 terminal RD, one pre-numeric RD, seven terminal DR, two pre-numeric DR and six terminal CRES. They cover all **24 towns with qualifying alias gains**, all four periods, 17 letter-suffix blocks, QUEEN'S RD punctuation, a two-transaction rare address and a 106-transaction address. Their 1,576 transactions are not 1,576 independent manual reviews. There are **63 newly located addresses plus one previously historical-only match** (227 PASIR RIS ST 21) gaining public corroboration.

The earlier, separately fixed **238-address sample** remains supporting coverage of the unchanged normalization stage, including numeric AVE/CTRL, literal forms, historical-only evidence and all 26 towns. It is not relabeled as part of the 64. No claim is made that alias gains exist in all 26 towns. Both selection methods, source report hashes and stage-specific reviewed ledgers are retained. Sampling is a systematic consistency check, not a statistical accuracy estimate or exhaustive manual validation of thousands of ordinary addresses.

## Geometry and source-strength caveats

The geometry-only gain on the original M8 inputs remains **226A SUMANG LANE / 36 transactions**, independently reviewed earlier. The final expanded profile additionally locates **928 TAMPINES ST 91 / seven transactions** through the new ST rule: property row 12973, nine ACRA assertions (for example ACRA-D row 62556), postal 520928, and unique source feature 5349 / OBJECTID 939790 / ENTITYID 2458. This additional MultiPolygon was manually inspected: two closed exterior rings with 25 and 67 vertices yield midpoint latitude 1.346055183, longitude 103.9401719407. Thus the final MultiPolygon population is two addresses / 43 transactions; it is distinct from the one-address geometry-only marginal effect.

All coordinates remain HDB footprint bounding-box block approximations, not guaranteed interior or flat-level locations. ACRA registered-address evidence is corroboration, not an authoritative HDB-ID mapping. The remaining 86 historical-only locations still rely on a cached selected first hit; returned residential identity, exhaustive candidate uniqueness, current API validity and transaction-date validity remain unverified. Neither alias semantics nor sample consistency strengthens that underlying source limitation.

## Frozen artifact hashes

- Full expanded report: `b9d35b65f6d58463b6559a144bbadb2c0888facda5fb572681c68ff9b613ae73`
- Public-only expanded report: `32d8e0be2de4c050398b58423935b712129d330445bb0695c7ebcd3fb0522913`
- Required input ledger: `320817dacac9a94e6a97e9d03447afd03c98f4f7ab43f661bd39ed29fa967c2f`
- Sample input ledger: `3cb2b1b188fbd04a068d186871384bebdf8b40d0f4a1e7624c46c7a9d397d74f`
- Reviewed required ledger: `aa6c46357292ad17778b2c4ee6f584749d8e3a001492b4e7afd6116213445655`
- Reviewed sample ledger: `8c40070766b69e012f36d715936c2a0fba92143e25fae99c66653ca0abe2a193`
- All-pair semantics ledger: `44fd186470474f69b16f5660d8c58b25d111e9df298129e3bf1b70a1b3c89f5b`

`source-verification.json`, `report-verification.json`, `review-summary.json` and the two reviewed CSVs record completed scope. This audit does not claim integration/build/UI gates were run by the evidence reviewer; the primary implementation verification records those separately. Full raw corporate snapshots and unrelated business/person fields are not publication material.
