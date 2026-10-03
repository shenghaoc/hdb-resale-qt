# M9 final: source-backed terminal road-type normalization

The final expanded profile locates **7,618 addresses / 188,573 transactions**
out of the unchanged 9,755 / 241,920 corpus. It retains 209 ambiguous addresses /
4,305 transactions and 1,928 unmatched addresses / 49,042 transactions. Total
missing coordinates are 2,137 addresses / 53,347 transactions. No transaction
is dropped to improve coverage.

This stage is separate from the [unchanged-rule source expansion](../README.md).
The [complete comparison](comparison.json), [all changed-address ledger](changed-addresses.csv),
[normalization-only changes](normalization-changes.csv), [remaining failures](remaining-failures.csv)
and [per-rule impact](normalization-impact.json) are deterministic artifacts.

## Exactly which rules apply

An explicit input directory opts in through `address-normalization.txt` containing
exactly `terminal-road-types-v1` plus LF. Unknown/unreadable profile text produces
a diagnostic and does not enable aliases. Without that file, legacy matching is
unchanged. Canonical six, frozen 416 and the M5 experiment do not opt in.

- ST → STREET, RD → ROAD, DR → DRIVE: final token, or the token immediately
  before a final ASCII all-digit token.
- CRES → CRESCENT: final token only; there was no observed pre-numeric CRES
  source pair, so that unsubstantiated context is not enabled.
- New expansion requires an actual preceding street-name token (index > 0).
  Leading ST/Saint and standalone `ST`/`ST 11` remain literal.
- Existing AVE/CTRL immediately before a final numeric token is unchanged.
- Block letters, punctuation, numbers/leading zeroes, other aliases and other
  tokens are preserved. No substrings, fuzzy/town/spatial/majority rule is used.

Raw facts and assertions are never rewritten. Normalization affects only the
address-equality keys used by both indexes and the matcher. ExactAddress still
requires raw equality; normalized equality does not claim an exact source match.

## Observed spelling evidence and review

All **297 distinct literal HDB→ACRA street-name pairs** were manually reviewed,
with [individual dispositions and source examples](manual-street-semantics.json)
and [summary](manual-street-semantics-summary.json). All 891 cited representative
same-block property/ACRA source contexts were separately verified against pinned
CSV bytes. The entire 311,110-row projection was independently re-derived and
checked, including every contrary and malformed assertion.

| Rule | Terminal pairs | Before-number pairs | Literal example |
| --- | ---: | ---: | --- |
| ST | 13 | 122 | ANCHORVALE ST / STREET; ANG MO KIO ST / STREET 52 |
| RD | 98 | 1 | BEDOK RESERVOIR RD / ROAD; EUNOS RD / ROAD 5 |
| DR | 22 | 20 | BRIGHT HILL DR / DRIVE; PASIR RIS DR / DRIVE 10 |
| CRES | 21 | 0 | TELOK BLANGAH CRES / CRESCENT |

Examples including QUEEN'S RD and ST. GEORGE'S RD preserve punctuation and the
leading Saint abbreviation. Spelling review also includes streets whose evidence
remains conflicting or malformed; semantic equivalence never erases a bad row.
No normalized-key collision between distinct corpus raw block/street identities
was found. The full HDB property subset was independently checked: expanded and
legacy projections have the same 9,754 original source records in the same order,
so copying the pinned property subset does not hide another candidate.

[Independent boundary results](boundary-results.txt) cover 47 normalization
vectors, 5 block-identity controls and the legacy opt-out. Examples include
leading/standalone ST, numbered roads with leading zeroes, signed/decimal/
alphanumeric/Unicode suffixes, two-number endings, punctuation, unrelated
substrings and unsupported CRES-before-number. Production unit tests also cover
indexed/unindexed/reference CSV agreement and invalid profile handling.

## Source acquisition and reproducibility

No additional upstream snapshot beyond the [27 public ACRA partitions](../sources.md)
was needed. The expanded lossless projection has **311,110 assertions across
7,743 corpus addresses**, including **197 malformed assertions at 18 addresses**.
Only the five approved dataset/row/block/street/postal fields are retained.
SHA-256 `d5b09b63e3123a4cdf3cbe3c934473d03bd8c6355b48e09639619833790d4584`.
[Source projection manifest](source-projection.json) records exact grammar,
partition/row order, source hashes, property equality and derived hashes. Both
independent generation and a fresh-directory reproduction were byte-identical.
Full raw/projection files remain local. Original byte hashes and licence/source
attribution remain in the linked provenance; no private export was acquired.

## Separate gains and adverse evidence

| Stage | Located addresses | Located transactions |
| --- | ---: | ---: |
| Clean M8 | 1,921 | 52,514 |
| All public sources, old rules, MultiPolygon | 2,513 | 66,256 |
| All public sources, expanded rules | 7,533 | 185,706 |
| Expanded public + bounded historical evidence | 7,618 | 188,573 |

Normalization therefore adds **5,020 / 119,450** on public evidence alone.
Relative to the old-rule public+historical stage, it adds 4,829 newly located
addresses / 113,146 transactions while withholding **12 previously historical
matches / 310 transactions** now contradicted by public evidence. Net
normalization-stage change is +4,817 / +112,836. The remaining 2,789 interim
matches retain their identical point/footprint.

Historical evidence now supplies **86 otherwise-unlocated addresses / 2,951
transactions**, still explicitly first-page/first-result and not verified returned
identities/candidate uniqueness. It also withholds **11 HOLLAND DR / 84 transactions**:
97 public assertions agree on postal 271011 with one original footprint, while
the unchanged historical first hit asserts 278859. Neither overrides the other.
Thus the net historical stage is +85 addresses / +2,867 transactions. The
[historical marginal ledger](historical-marginal.csv) includes both positive and
adverse outcomes; its 87 addresses / 3,035 transactions are the total affected
set, not a positive-gain count.

Compared with M8, 5,757 addresses / 137,181 transactions gain coordinates, and
60 / 1,122 lose coordinates to stronger contrary evidence, for net +5,697 /
+136,059. The 1,861 preserved M8 matches retain their exact point/footprint.
All 60 M8 downgrades are disclosed separately from improvements. There is no
first-winner, majority vote, postal repair or nearest-geometry fallback.

| Final quality | Addresses | Transactions |
| --- | ---: | ---: |
| ExactAddress | 897 | 28,396 |
| NormalizedAddress | 6,721 | 160,177 |
| Ambiguous | 209 | 4,305 |
| Unmatched | 1,928 | 49,042 |
| BlockApproximation | 7,618 | 188,573 |
| Missing | 2,137 | 53,347 |

Per-rule counts below combine terminal/numeric contexts. “Applicable” counts raw
addresses where the rule changes the key, including missing evidence. “Newly
public-located” compares expanded public against the identical public sources
under old rules. All contradiction/malformed counts remain explicit in the
[context-level impact](normalization-impact.json).

| Rule | Applicable addresses / transactions | Newly public-located addresses / transactions | New final ambiguities addresses / transactions |
| --- | ---: | ---: | ---: |
| ST | 2,880 / 60,781 | 2,558 / 53,417 | 68 / 1,021 |
| RD | 1,582 / 41,518 | 1,293 / 33,955 | 61 / 1,582 |
| DR | 872 / 22,204 | 809 / 20,595 | 13 / 429 |
| CRES | 389 / 12,631 | 360 / 11,483 | 3 / 77 |

The final located set contains **two MultiPolygon addresses / 43 transactions**:
226A SUMANG LANE (36) and 928 TAMPINES ST 91 (7). The latter becomes corroborated
only after the ST rule; its original OBJECTID 939790 / ENTITYID 2458, two closed
component rings and union-bounds point were additionally manually inspected.
The geometry-only marginal at the original evidence stage remains one / 36.

## Audit scope and limits

The user approved systematic rule audit rather than pretending that all thousands
of ordinary gains could be manually ground-truthed: 100% mechanical address/source
validation, manual every rule/equivalence context and every conflict/former-match
change, plus a deterministic outcome sample.

Every one of 9,755 address outcomes / 241,920 transaction memberships is independently
checked for exact rule application, complete property/postal/historical candidates,
postal agreement and unique footprint. All 7,618 resulting points are independently
recomputed from original HDB geometry. Full assertion rows are local reproducible
artifacts; compact changed-address ledgers retain candidate counts and hashes of
complete source-reference sets, not a claim that candidates were discarded.

The mandatory set contains **210** addresses: all 209 public conflicting/malformed
sets (including missing-property 82 MACPHERSON LANE) plus the historical/public
contradiction at 11 HOLLAND DR. This includes every final ambiguity, every original
matched change and every one of the 12 interim historical matches now contradicted.
A separate **64-address** deterministic public-evidence-gain sample (63 previously
unmatched plus one historical-only address gaining public corroboration) covers all seven
observed rule contexts, all four periods, all 24 towns with qualifying gains,
rare low-count addresses, high-count addresses, letter-suffix blocks and punctuated
streets. The other two towns have no qualifying newly public-located normalization
outcomes; none was invented for coverage. Sampling uses a published SHA rank,
greedy uncovered-tag coverage, then hash-fill, with no manual success selection.

This remains source-chain/spelling review, not an address-accuracy estimate or
historical residential ground truth. ACRA can contain erroneous registered
addresses. The historical projection remains weak. Future sources must retain
all contradictory assertions and pass the same mechanical/audit process.

## Remaining failures

- No postal assertion under the exact rules: **1,926 / 49,013**.
- Conflicting valid public postals: **190 / 3,759**.
- Malformed public postal blockers: **18 / 462**. All 197 malformed rows remain
  verbatim, with diagnostics; no four-digit value is zero-padded.
- Public/historical postal contradiction: **1 / 84**.
- Source footprint rejected for present ENTITYID 0: **1 / 26**.
- Missing HDB property record: **1 / 3**, also carrying conflicting postal evidence.

Other road/prefix abbreviations (including BT versus BUKIT), other source omissions,
possible historical/current changes and the positive-ENTITYID source condition
remain separate evidence tasks. M9 stops here; none is guessed away.

## Reproduction

```sh
python3 tools/prepare_address_coverage.py --base /absolute/hdb-scale-full --source-directory /absolute/acra-snapshots --source-manifest docs/address-coverage/source-provenance.json --normalization-manifest docs/address-coverage/normalization/source-projection.json --output /absolute/hdb-m9-expanded-public
python3 tools/prepare_address_coverage.py --base /absolute/hdb-scale-full --source-directory /absolute/acra-snapshots --source-manifest docs/address-coverage/source-provenance.json --normalization-manifest docs/address-coverage/normalization/source-projection.json --historical docs/coverage/onemap/historical/benchmark.json --output /absolute/hdb-m9-expanded
HdbResale.App --address-coverage /absolute/hdb-m9-expanded-public /tmp/expanded-public.json
HdbResale.App --address-coverage /absolute/hdb-m9-expanded /tmp/expanded.json
python3 tools/audit_normalization.py --original /tmp/baseline.json --current-public /tmp/public.json --current-final /tmp/final.json --public /tmp/expanded-public.json --final /tmp/expanded.json --directory /absolute/hdb-m9-expanded --footprints /absolute/buildings.geojson --output /tmp/normalization-audit
HDB_DATA_DIRECTORY=/absolute/hdb-m9-expanded HdbResale.App
```


Completed review evidence: [review report](review.md), [required ledger](manual-required-reviewed.csv),
[sample ledger](manual-sample-reviewed.csv), [review summary](review-summary.json),
[independent full source audit](independent-source-audit.json),
[independent outcome/geometry audit](independent-outcome-audit.json), and
[independent exact sample reproduction](sample-verification.json). The earlier
238-address ordinary sample remains a separate record; it is not counted as64
additional normalization cases or as exhaustive manual address validation.
