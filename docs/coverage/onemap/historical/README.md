# Historical OneMap first-hit experiment — 2026-10-03

Actual user-supplied historical D1 export, kept unchanged outside Git at
`/Users/shenghaochen/Documents/Codex/2026-10-03/task-2/private-onemap/hdb-resale-geocode-cache.csv`:
1,389,521bytes,10,333rows, seven exact columns (`cache_key,search_value,postal_code,
display_name,lat,lng,updated_at`), no duplicate keys or empty/null postals.
SHA-256 `8daf79156eb574be19815bbe7b6c77c98593230735e71bb192133a9585893ec4`.
Library identity was preserved through supported transfer. No live API request.

Read-only sibling inspection at HEAD387e3352699efd13f0e7dab9c3fe14b6fe7595e5:
`pipeline.ts` normalizes trim/whitespace/uppercase, builds town-block-street,
then lowercases/replaces non-alphanumeric runs with hyphens. Town is used only
inside this exact producer key, not as a native matching/disambiguation heuristic.
`sync-data.ts:408` searches `<block> <street> SINGAPORE`; only missing keys are
queried, existing keys never expire. `sync/geocode.ts` requests only page1 and
stores `results[0]`; returned block/road and other candidates are discarded.
`schemas.ts` strips non-digits from postal and pads to6; raw provider postal is
unrecoverable. Native experiment requires already-cached exact6digits and repairs
nothing. `updated_at` is batch persistence time, not proven query date; loading
web cache discards row timestamps and uses global epoch. Raw writes span
2026-05-24T10:24:51.418Z–2026-08-29T01:36:52.104Z.

The378identity `benchmark.json` is a minimal sample projection, no lat/lng.
SHA-256 `7d8af54d5cae591455e5161535218b66c9f85b9718d9a00d730602f72086ccd5`.
Exact producer key AND exact search string required:375hits/413transactions,
3missing/3transactions,0search mismatches/invalid postals. No fuzzy, alias,
proximity, neighbouring-address or town fallback. A missing key is not API no-match.
`HistoricalPostalAssertion` remains separate from `OneMapSearch`; no invented
returned block/road or candidate set. Every experimental match reason labels
historical first-hit evidence. Default app/QML/OSM/data stay unchanged.

| Measure | M4 baseline | Experimental postal-assisted |
|---|---:|---:|
| ExactAddress |22|22|
| NormalizedAddress |48|388|
| Ambiguous |1|1|
| Unmatched |345|5|
| BlockApproximation |70|410|
| Missing coordinates |346|6|

340Unmatched→NormalizedAddress transitions across310identities; all others
unchanged (22Exact,48Normalized,1Ambiguous,5Unmatched). No conflict resolved by
majority; no matched regression.416rows conserved,0diagnostics/rejections.
Coordinates come solely from one HDB block/postal footprint bbox midpoint.
**This asks what happens if the historical first-hit postal is useful evidence;
it does not establish exhaustive returned-address/candidate uniqueness.**

Independent full-source mechanical checks passed on all340changed rows:
unique original HDB property,0ACRAassertions, exact raw cache key/search/postal/time,
one block/postal footprint, identical OBJECTID and independently computed point.
An [individual changed-address review](manual-review.md) is now complete for
all310identities/340transactions, alongside the5residual failures and1conflict.
All310returned identities and candidate uniqueness remain **Unverified**. This
is record-chain inspection, not a manual ground-truth audit. Fifteen addresses
covering17transactions have institutional/tenant first-hit labels, demonstrating
why BUILDING cannot prove intended residential identity.
Audit-only cache-point distances: max58.31m atHDB-82898,0above100m; never a join,
footprint choice, conflict decision or app coordinate input. Raw lat/lng untracked.

| Row | Address | Retained decision |
|---|---|---|
|HDB-18178|83 C'WEALTH CL|Exact cache key absent;Unmatched|
|HDB-132851|81 C'WEALTH CL|Exact cache key absent;Unmatched|
|HDB-121235|1 EVERTON PK|Exact cache key absent;Unmatched|
|HDB-226640|3 QUEEN'S RD|Cached266734 absent from full HDB postal footprints;Unmatched|
|HDB-234011|11 HOLLAND DR|Cached278859 absent from full HDB postal footprints;Unmatched|
|HDB-6769|446 HOUGANG AVE8|Cache530446 agrees73ACRArows but row60428postal530836 persists;74assertions retained;Ambiguous|

Fixed SHA-ranked remaining-unmatched audit includes all5rows. `rows.csv` contains
all416outcomes/source counts/footprint IDs; `results.json` contains distributions,
transitions and checks. Full local C# report hash
`d76614762c351eb121d3dd11ed03658e53daae1a5feeae36809a029b7e62d4be`;
rows hash `9c39e483fbfd4d9e140741f6f324643f152e9cf4701b6c6d0b32bbf376abe6e6`.

Reproduce with root README toolchain exports and pinned M4 full source downloads:

```sh
python3 tools/derive_historical_onemap.py --raw /Users/shenghaochen/Documents/Codex/2026-10-03/task-2/private-onemap/hdb-resale-geocode-cache.csv --sample docs/coverage/sample/transactions.csv --output /tmp/historical-benchmark.json
cmp docs/coverage/onemap/historical/benchmark.json /tmp/historical-benchmark.json
dotnet run -c Release --project src/HdbResale.App --no-build -- --historical-onemap "$PWD/docs/coverage/sample" /tmp/historical-benchmark.json /tmp/hdb-buildings-official.geojson /tmp/historical-report.json
python3 tools/audit_historical_onemap.py --report /tmp/historical-report.json --raw /Users/shenghaochen/Documents/Codex/2026-10-03/task-2/private-onemap/hdb-resale-geocode-cache.csv --properties /tmp/hdb-property-official.csv --footprints /tmp/hdb-buildings-official.geojson --output /tmp/historical-audit
cmp docs/coverage/onemap/historical/rows.csv /tmp/historical-audit/rows.csv
cmp docs/coverage/onemap/historical/results.json /tmp/historical-audit/results.json
```

Contains information derived from [OneMap Search](https://www.onemap.gov.sg/),
Singapore Land Authority, user-provided historical export2026-10-03, cache writes
May–August2026 (actual query dates unavailable), under the
[Singapore Open Data Licence](https://www.onemap.gov.sg/legal/opendatalicence.html).
Licence/API terms reviewed2026-10-03; copying/derived analysis require conspicuous
source/date/licence attribution and respect exclusions for personal data,
third-party/IP/trademark rights and no endorsement. Projection contains only
benchmark address/postal/query/cache-write metadata; no display names, personal
fields, secrets, logos, full export or cached coordinates. Display names remain
private and are not needed to reproduce the experiment. The user expressly
authorized the minimized benchmark artifact and public M5 branch publication,
subject to terms. The single retry was rejected because automatic approval
review classified the quoted authorization as untrusted and retained the visible
do-not-push instruction. No further attempt; artifacts remain local pending
direct trusted user authorization. The licence supports copying these basic address/postal facts and
derived analysis with attribution. It does not grant excluded third-party rights.

Token absence did not block this experiment. Next: investigate exact3cache gaps
and2HDBfootprint gaps. Stronger exhaustive uniqueness would require fresh candidate
acquisition for the375first-hit identities too; omitted evidence is unrecoverable.
No API credential alternative or basemap migration is needed for this experiment.
