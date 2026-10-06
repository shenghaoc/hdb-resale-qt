# Canonical-only snapshot candidate, 2026-10-06

This local candidate removes historical first-hit OneMap assertions from ordinary
imports and static HTTP packs. Published `5dc803b` and its historical evidence are
preserved. No source capture, transaction fact, legitimate duplicate occurrence,
normalization rule, lease precision, tile URL/cache/attribution, QML presentation,
threading or re-entry behavior was changed. No production data was published.

`CsvImport.LoadDirectory` no longer automatically reads a historical sidecar.
Explicit `historicalAssertions` calls and the historical experiment command remain
available for reproducing offline research. The source preparation tool omits that
sidecar; HTTP packaging rejects an already-prepared directory containing it.
New packs declare importer version 2. The client also accepts sidecar-free version-1
packs. Historical HTTP packs fail explicitly before object download or activation,
and their existing files remain on disk. They require the published checkpoint
for their original runtime interpretation; they are not silently relabelled.

## Cohort and exact tradeoff

The five retained public HDB captures still contain 988,123 accepted occurrences,
across 10,016 raw town/block/street addresses. Source descriptor SHA-256 remains
`ea36ce53fd727cb7bf76e4b90a9af572f59a86aae58323cb4752c133b6f96f5a`.
All ten remaining prepared files were regenerated and independently compared byte
for byte with the retained inputs. HDB property/footprint pins and the address-only
projection of all 27 pinned ACRA partitions are unchanged. The terminal-road-types
normalization profile remains explicit. Captures have mixed dates; current property
and footprint evidence is not historical location ground truth. Before March 2012,
source months use approval dates; later sources use registration dates.

| Classification transition | Addresses | Transactions |
|---|---:|---:|
| ExactAddress → ExactAddress | 897 | 62,661 |
| NormalizedAddress → NormalizedAddress | 6,635 | 660,806 |
| Ambiguous → Ambiguous | 208 | 22,158 |
| Unmatched → Unmatched | 2,189 | 231,752 |
| NormalizedAddress → Unmatched | 86 | 10,343 |
| Ambiguous → NormalizedAddress | 1 | 403 |

Coordinates change from block approximation to missing for those 86 addresses and
10,343 occurrences. One address and 403 occurrences gain a block approximation.
The remaining coordinate transitions are unchanged: 7,532 addresses / 723,467
occurrences stay located; 2,397 / 253,910 stay without coordinates. New totals are
**7,533 located / 2,483 missing addresses**, and **723,870 located / 264,253 missing
occurrences**. Canonical classifications are Exact 897 / 62,661; Normalized 6,636 /
661,209; Ambiguous 208 / 22,158; Unmatched 2,275 / 242,095.

All 86 losses have one canonical HDB property record and no canonical ACRA postal
assertion under the retained normalization. Their old locations depended on weak
first-hit assertions. They remain available in list/filter/detail cohorts; removal
does not delete their transactions or invent replacement coordinates.

The gain is QUEENSTOWN / **11 HOLLAND DR**. HDB property row 484 and 97 retained,
dataset-qualified ACRA assertions corroborate postal 271011. Unique accepted HDB
footprint OBJECTID 946508 / ENTITYID 3348 supplies the approximate point
(1.3089032648, 103.7940631233). The removed historical assertion supplied 278859,
previously making the match a cross-source conflict. This is canonical-record
corroboration, not a verified flat identity or historical truth.

Another **291 addresses / 31,696 occurrences** lose historical provenance without
changing classification or coordinates. Complete property/postal candidates,
malformed assertions, ordered source-qualified IDs and transaction membership were
independently checked for every address. Rejections / diagnostics remain 1 / 198,
from location evidence; no declared transaction occurrence was rejected.
[The complete 87-address inventory](changed-addresses.csv) and
[pins, transitions and native digest evidence](verification.json) record the review.
The 10,746 changed source-qualified transaction IDs and the 291 provenance-only
address identities are retained locally outside Git, with their hashes recorded.

## Deliberate new expectations

The native buyer gate's pinned expanded profile now expects 7,533 mapped addresses.
The 2017-only corpus keeps 241,920 occurrences / 9,755 addresses. Its canonical
classifications are Exact 897 / 28,396; Normalized 6,636 / 157,310; Ambiguous 208 /
4,221; Unmatched 2,014 / 51,993. Here, 86 addresses / 2,951 occurrences lose mapping
and 11 HOLLAND DR / 84 occurrences gain mapping. These are deliberate changes;
the old 7,618 expectation and historical digests remain recorded in prior evidence.

| Canonical cohort | Full import SHA-256 | Legacy projection SHA-256 |
|---|---|---|
| Five-source | `33bd80d17ffd69555c6513c27d7b203e94430faed2738717dde043c79d3cdbca` | `7c928addf4ed12f2e6a9783d0a506711b802d23b6b18108999daea5b97ff54d2` |
| 2017-only | `e1b0e7a08e4be347b266f35190d60a8cefbdbc0bd37f107515faca4bb00c8fcb` | `4e53fa128ec62e0be7b5d2e7cd433d19f1a3e680ffd410cb6d4ec93ff80e6f84` |

Each digest agrees between the actual Debug and Release native CLI hosts. The
Release candidate's complete five-source address report is byte-identical to the
canonical-only report measured using unchanged checkpoint code before edits.
This verifies the decisions independently of the new default-loading path.

## Current OneMap and publication scope

Zero current OneMap Search requests were made. The 86 missing-postal cases are a
bounded possible future acquisition set, not a target that must recover the old
coverage. A process token was absent; no credential files were inspected, no token
was persisted, and Qt gains no authentication/geocoding dependency. The canonical
candidate is valid on its own. Future complete authenticated Search responses
would require separate evidence, conflict/uniqueness review, a new cohort hash and
tests; first-hit or empty/error responses cannot substitute for that contract.

The local immutable pack has ten files, manifest
`4060448ae47bd658aa3e0aaca1ed8e7c460beb842d04d921fbe29c1178c4421e`,
33,398,293 compressed bytes and 168,665,674 unpacked bytes. A first sync makes
13 GETs and transfers 33,401,557 body bytes, excluding HTTP/TLS overhead: 9,275
bytes less than the historical pack. The largest object remains 16,421,057 bytes.
This removes a licensing/provenance dependency and a small file; it does not make
the nearly one-million-row retained buyer state lightweight.

Static immutable hosting remains the preferred future design. The revised local
publication proposal retains pointer-only rollback, complete retained object
unions, current + two previous + pins, at least 30 days after last deactivation,
and a 24-hour reviewed deletion hold. No host, upload, deploy, schedule, D1/Neon
write or serving cutover was performed.

## Physical acceptance still required

Automated native gates and startup readiness are not visual/hardware acceptance.
The checkpoint's separate visual pass cannot certify this changed data cohort.
Use the canonical Release cache and verify fresh/cached/offline startup, physical
filter/reset/empty flows, list/map/detail selection, pan/zoom and clean shutdown.
In particular, select **BEDOK / 10D BEDOK STH AVE 2** and other entries in the
86-address loss inventory: they must remain in the list/details with missing
coordinates and no historical assertion. Then inspect **11 HOLLAND DR** in both
list and map: canonical provenance and approximate coordinates must agree.

## Completed local validation and memory scope

Both root Debug/Release builds finish with zero warnings/errors and each passes
235 C# tests; all 53 Python offline-tool tests pass. The 24 native positive cases
pass and all 54 deliberate faults are detected, with unchanged gate deadlines.
The matrix exercises the canonical six-row data, strict 10,000-row fixture,
canonical 2017 full corpus and the mixed-source precision/provenance fixture.
These are automated native applications, not physical input or visual acceptance.

Both native hosts perform a fresh full-cohort HTTP sync (13 loopback GETs each),
reject corrupt and truncated test updates while preserving the old active pointer,
and launch the canonical cache at 988,123 rows / 7,533 markers in the opt-in full
view. Release SIGKILL during download preserves the old active pointer and leaves
one inactive owned orphan stage; explicit retry activates a verified small test
fixture and completes 15 buyer transitions. Resynchronization restores the full
canonical cache with three metadata GETs. Its outbound-network-denied Release
launch also reaches the full-view assertion and exits cleanly. No API provider
Search call, production corpus retrieval, package publication or pixel acceptance
is implied.

The fresh managed-only import/buyer probe retains 598,369,232 bytes (570.65 MiB)
after diagnostic GC with the import/state alive. Its process maximum RSS is
1,774,305,280 bytes. This diagnostic collection is not a production GC policy.
Cached native Release startup has process maximum RSS 1,671,053,312 bytes;
network-denied native startup has 1,800,847,360 bytes. Maximum instantaneous
working-set samples are respectively 1,520,369,664 and 1,771,831,296 bytes, with
GC.GetTotalMemory(false) readiness samples of 1,439,014,616 and 1,439,213,136 bytes.
Process RSS peaks come from /usr/bin/time -l; working-set/managed readings are
stage samples, not a native allocation attribution or post-GC retained heap.
Recorded Release stage durations total 4,580.966 ms cached and 4,515.926 ms
network-denied, excluding uninstrumented startup/process overhead and pixels.

These are single scope-labelled observations. Earlier approximately 571 MiB
retained buyer state and 1.68 GiB native peak remain material observations, not
upper bounds; earlier uncollected startup working-set samples reached 1.804 GiB.
Removing a 66,356-byte sidecar does not materially reduce the full fact corpus's
memory cost. No statistical speed or peak-memory improvement is claimed, and no
optimization or architecture change was made. An initial ordinary-sandbox probe
could not complete resource accounting (kern.clockrate denied); its raw output
is retained separately and is not the final resource-accounted result.
