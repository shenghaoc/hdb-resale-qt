# Individual changed-address review — 2026-10-03

Completed an individual inspection of every changed address: **310 distinct
producer cache identities, covering all340changed transactions**. Repeated
transactions share one review; `manual-review.csv` explicitly lists every covered
transaction ID, HDB property CSV source row, HDB footprint OBJECTID/ENTITYID,
historical postal, decision and reason. Review batches were IDs1–60,61–120,
121–180,181–240,241–310, in ascending cache-key order. This was a manual review of
the preserved record chain after the independent mechanical audit, not another
name-regex classification or automatic acceptance step.

Ledger SHA-256:
`c6ec0cdf246139cd6ef8abc897a8e2f87b06387872cfab3ea434fb9f3c6f0a83`.
Source linkage: unchanged raw SHA-256
`8daf79156eb574be19815bbe7b6c77c98593230735e71bb192133a9585893ec4`,
minimized benchmark SHA-256
`7d8af54d5cae591455e5161535218b66c9f85b9718d9a00d730602f72086ccd5`,
full original HDB property/footprint source pins from the frozen M4 manifest.
Actual first-hit names were inspected only from the private raw export; ledger
contains descriptive flag classes, never those names or cached coordinates.

## What was reviewed

For each address, inspected the exact preserved producer key/search, cached
postal/name, source HDB property block/street/row, selected footprint block/postal
and IDs, and covered transactions. The previously checked full-source uniqueness
and independent midpoint calculation provide supporting evidence. No geographic
proximity, inferred postcode pattern, town fallback, fuzzy alias, first-hit name
or majority selected a match. Neither source rules nor experimental outcomes
were changed by this review. Batch-write timestamps do not establish query dates
or validity at any historical transaction date.

All310chains are internally consistent: matching preserved query/key/postal;
one HDB property; no ACRA assertions; one HDB block/postal footprint. This is
sufficient to describe the **conditional historical postal-assisted experiment**,
not to verify that OneMap returned the intended residential address.

Every ledger row marks both **ReturnedIdentityReview=Unverified** and
**CandidateUniquenessReview=Unverified**. The export lacks returned BLK_NO and
ROAD_NAME, response pages/candidate set, found count and raw provider postal.
A source query is not a returned-address assertion. Those missing facts cannot
be recreated from a key, plausible label, point proximity or HDB footprint.
Thus all340experimental additions retain unresolved identity uncertainty; they
are not promoted into verified/address-accurate production matches.

## Per-address decisions and flags

-128addresses have NIL first-hit names: no returned-name corroboration.
-167have a descriptive name only: no returned block/road or uniqueness proof.
-15addresses/17transactions have institutional/tenant first-hit labels. These
  were selected individually during review, not by an inferred name-matching rule.

| Review IDs | Source-label concern | Covered transactions |
|---|---|---|
|9,104,166,237,271,299,302|Preschool first hit|7|
|57|Kindergarten first hit|1|
|87|Schoolhouse first hit|3|
|78|Neighbourhood police post|1|
|183|Fire post|1|
|218|Community centre|1|
|269|Organisation label; residential identity not established|1|
|294|Childcare centre|1|
|298|Student-care first hit|1|

These15are specifically flagged, not declared false: the preserved HDB footprint
still agrees on block/postal. The label may describe a tenant, service unit or
sub-address. In particular review78,1TOH YI DR/postal591501/HDB property row35 /
OBJECTID935733/ENTITYID11057157 has a police-post first hit. This does not prove
that the residential block's intended returned identity was checked. No substitute
postal or coordinate was selected. Unnamed/estate-like hits are equally unverified;
absence of a suspicious label is not affirmative correctness evidence.

The manual review did not find a new contradiction in the **preserved** changed
chains. This cannot exclude contradictory omitted API candidates. The unchanged
Hougang conflict and all five residual failures were individually reviewed and
recorded in [the experiment README](README.md); row60428postal530836 remains
alongside the73postal530446ACRAassertions, with no majority resolution.

## Verification boundary and remaining prerequisite

The required per-changed-address **inspection** is now complete; there is no
remaining unread changed-address ledger entry. Reproduction, count conservation,
source-chain audit and scoped manual review are verified for this explicitly
experimental study.

Calling the340additions **verified returned-address matches**, or claiming the
410points establish matching accuracy, would require additional independent
address→postal evidence or preserved returned-address/candidate data for all
310changed identities. Exhaustive uniqueness specifically requires the relevant
candidate set/pages; this export cannot supply it. No further network acquisition
was undertaken. The three cache gaps and two absent HDB postals remain as recorded.

Publication remains paused after the single approval retry rejection. Main stays
unchanged. Completion as a bounded historical experiment must retain these
unverified outcomes; a stronger identity/uniqueness acceptance condition remains
unsatisfied without the missing evidence. No misleading manual ground-truth or
historical-transaction-validity claim should accompany publication.
