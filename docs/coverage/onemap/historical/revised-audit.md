# Revised milestone5 audit — 2026-10-03

This is the final acceptance audit under the user's revised requirement:
mechanical validation of all340changed rows, manual inspection of every ambiguous
result and uncovered cache address, and a deterministic stratified32newly-resolved
rows. The earlier310address review was already recorded locally; no further
exhaustive review was planned or required. It is supporting local work, not the
revised acceptance threshold. No matching rule or frozen cohort was changed.

## Deterministic sampling

`tools/sample_historical_audit.py` selects only Unmatched→NormalizedAddress rows.
SHA-256 rank is UTF-8 `hdb-m5-revised-audit-v1`, NUL, transaction ID. First choose
the minimum rank per town, in ordinal town order. Add the lowest-rank unused
identity if a period, road-normalization class, block-suffix case, street-punctuation
case or institutional first-hit flag is absent. Fill to32using the least represented
(town,period), then town, then rank. Always exclude already-selected cache identities.
Final output order is town/period/ID. Selection uses benchmark facts and generic
review flags, never cached geometry or display-name strings.

The reproducible result has **32rows,32distinct addresses,all26towns**, with:
8rows2017–2019,7rows2020–2022,8rows2023–2025,9rows2026;2numericAVE and1numericCTRL
cases,29other-road cases;9letter-suffix blocks and1punctuated street. Notably
120BTBATOKCTRL stays unexpanded because no final numeric suffix exists; only the
existing exact AVE/CTRL-before-number aliases apply. No new normalizer/lookup alias.

`sample-review.csv` SHA-256:
`1d4fa8f6a981ac4df96ae5fa913d01fc54d51447c81a1ad1205ec483f57b239f`.
Each row supplies source property row, footprint OBJECTID/ENTITYID, cached postal,
normalization stratum, review flag and honest identity/uniqueness status.

```sh
python3 tools/sample_historical_audit.py --report /tmp/hdb-m5-historical-report.json \
  --ledger docs/coverage/onemap/historical/manual-review.csv \
  --output /tmp/revised-audit-sample.csv
cmp docs/coverage/onemap/historical/sample-review.csv /tmp/revised-audit-sample.csv
```

## Executed review and findings

Individually inspected all32selected rows with the preserved exact cache query/
postal/first-hit label, HDB property source row/block/street and HDB block/postal
footprint identity. Each preserved chain is consistent, without ACRA assertions.
Repeated transaction copies were not used to inflate address diversity. Numeric
AVE/CTRL, literal/no-alias roads, suffix blocks, punctuated streets and leading-zero
postals remain preserved. Coordinates are solely independently checked HDB bbox
midpoints; raw cache geometry is never a choice/acceptance input.

SampleHDB-145185(170BEDOKSTHRD) has a preschool first-hit label;HDB-38284
(817TAMPINESST81) has an organisation first-hit label. These were inspected from
the private raw source, not published as names. Both agree with the preserved
postal/HDB footprint chain but do not verify the returned residential identity.
They reinforce the stated **historical page1/results[0] postal evidence** limitation;
neither contradicts that conditional evidence model. No sample silently promotes
a first-hit label into an exhaustive/current Search identity claim.

All32returned identity and candidate-uniqueness fields remain **Unverified**.
This is an expected evidence limit, not a newly discovered contradiction. The
sample supports internal chain consistency, not a statistical accuracy estimate,
exhaustive candidate validation or historical validity for2017–2026transactions.

The full mechanical audit still covers all340changed rows across310identities:
exact source cache key/query/postal/time, one original HDB property,zeroACRA,
one full-source block/postal footprint,sameOBJECTID and independently identical
midpoint.416rows conserved; no source winner chosen by name/proximity/majority.

Every ambiguous result reviewed: HDB-6769,446HOUGANGAVE8,cache530446,
73ACRAassertions530446 and source row60428postal530836. All74retained;Ambiguous.
Every uncovered benchmark cache address reviewed: HDB-18178/83C'WEALTHCL,
HDB-132851/81C'WEALTHCL,HDB-121235/1EVERTONPK. Exact keys absent; no invented
no-match response/alternate spelling. Also reviewed both remaining footprint gaps:
HDB-226640/3QUEEN'SRD/postal266734,HDB-234011/11HOLLANDDR/postal278859; neither
postal exists in the frozen full HDB footprints. They remain Unmatched.

## Acceptance boundary and publication

No sampled case undermines the explicitly conditional historical-first-result
postal model. Mechanical coverage, revised sample,allconflicts/allcachegaps and
reproduction checks meet the revised audit condition. This is not proof that the
340experimental additions are verified returned-address matches. Stronger returned
identity/current/exhaustive candidate evidence remains a separate future scope.
The original frozen M4 baseline and six-row default native demo are unchanged.

No new live API acquisition was needed or attempted. Root Debug/Release builds,
C#/Python tests,both native gates and real negative gate are rerun for final
verification, with results recorded in `docs/verification.md`.

Public publication remains blocked by automatic approval review, which treats
relayed user confirmation as untrusted and retains the original visible do-not-push
instruction. One permitted retry was made to the **same92a072e→origin/M5branch**
action after history inspection; rejected again. No alternative push or main
advancement. This is an approval-evidence transport blocker, not a request to
reapprove the project's substantive scope.

History audit: only92a072e andd25db39 are unpublished ancestors before this revised
work; each benchmark projection has only key/search/postal/status/time metadata.
Former843df64 has display names in an **unreachable local commit object**, no
cached coordinates; it is not an ancestor of92a072e/currentHEAD and would not be
transmitted by that push. It has not been purged/reflog-expired or rewritten again.
Targeted authorization would be required if complete local removal of those old
unreachable Git objects is desired. The full raw export never entered Git.
