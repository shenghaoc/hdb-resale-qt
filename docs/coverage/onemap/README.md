# Milestone 5 — fresh Search tooling and historical experiment

A later user-supplied [historical cache experiment](historical/README.md) is verified locally. Token absence did not block that experiment. This page describes the separate fresh Search route.

This branch implements offline comparison and a user-run Search acquisition tool.
**No real OneMap requests or responses have been acquired.** The task executor has
no `ONEMAP_ACCESS_TOKEN`. No fresh-API before/after coverage or audit is claimed; historical results are separate. Main remains the completed milestone 4.

## Frozen scope

The unchanged 416-transaction M4 sample has 378 normalized block/road addresses.
The C# query generator uses the existing normalizer and the first source-order
address spelling, sorted by normalized address. Queries contain only block and
street. No town, price, coordinates, alternative spellings or fuzzy searches.

SHA-256 pins:

- transactions.csv: `875aceb10230d4a52ea6159397cd40a69cbe81346094f6bb1a81423c6e1b5b76`
- M4 manifest: `c83187afa49e39f2d60e60575b6ce6e82ad877137bf6931007a1b165c510e4a1`
- query manifest: `b553d7807994afae9424b7f2efa16b0d917dd32336945e6b42185fc9e1a3f0a9`
- M4 report: `b4041707f37acfe189f473e163bc2fff28890d42e5bd8389a16dabcc7203d821`
- full original HDB footprint snapshot: `7c987511548de3a82da403cabca02702031e02ade8703a9e401417883dfeb702`

M4 baseline: 22 ExactAddress, 48 NormalizedAddress, 1 Ambiguous, 345 Unmatched;
70 BlockApproximation, 346 Missing; zero diagnostics/rejected transactions.
The original source snapshots and all existing canonical demo data are unchanged.

## User-run acquisition

In **your own terminal where the token is already exported**, run:

```sh
cd "$HOME/repos/hdb-resale-qt"
python3 tools/acquire_onemap.py --queries docs/coverage/onemap/queries.json --cache .local/onemap
```

Do not send, print or commit the token. An export in a separate terminal may not
propagate into the task executor; running this command in that terminal uses its
existing environment reliably. The sole credential input is `ONEMAP_ACCESS_TOKEN`.
The tool does not sign in, refresh tokens or store credentials. A missing token
fails before a request or cache directory is created. `.local/` and `.env*` are
ignored. After successful acquisition, tell the agent that the sanitized local
cache is ready; it can complete comparison/audit without needing the credential.

Search uses the official HTTPS endpoint, raw Authorization token, `returnGeom=N`,
`getAddrDetails=Y` and every reported page. It saves only query identity, acquisition
UTC date/source URL, block/road/postal/building candidates with page/index, response
SHA-256 hashes and fixed error classifications. Geometry, raw responses, headers,
credentials and raw error messages are excluded. Redirects are refused. Default
explicit pacing is 30 requests/minute, configurable 1–60; it is a conservative
request budget, not an asserted account quota. HTTP429 stops acquisition. There
are no automatic retries. After resolving an error, `--retry-errors` explicitly
reacquires failed queries; successful caches are reused after validation.

HTTP200 `error` envelopes are classified before empty results/pagination on every
page. Auth, HTTP, malformed and partial-pagination failures remain ApiError;
none supplies no-match evidence. The parser has offline regressions for expired
HTTP200 tokens on initial and subsequent pages.

## Offline comparison after actual acquisition

With the README toolchain exports, generate queries or compare:

```sh
dotnet run -c Release --project src/HdbResale.App --no-build -- --onemap-queries "$PWD/docs/coverage/sample" /tmp/onemap-queries.json
dotnet run -c Release --project src/HdbResale.App --no-build -- --onemap-compare "$PWD/docs/coverage/sample" "$PWD/.local/onemap" /tmp/hdb-buildings-official.geojson /tmp/onemap-comparison.json
```

The final command requires the exact pinned full original footprint file; obtain
it following the [M4 source instructions](../README.md) if it is unavailable.
Missing caches fail visibly. Error-bearing acquisitions produce an incomplete
report and exit 1. Reports contain all 416 before/after classifications, source
assessments, named transition counts, every changed/conflicting/error row, and a
fixed SHA-ranked remaining-unmatched audit subset (two per year). Real reports
and manual audit conclusions must be reviewed before M5 is called complete.

OneMap identity is separate from ACRA evidence. Matching requires a unique HDB
property identity plus normalized block/road agreement and a six-digit postal.
Identical normalized block/road/postal/building candidates may aggregate; distinct
postals or building identities remain ambiguous. ACRA contradictions cannot be
overridden by OneMap or a majority. In particular HDB-6769, 446 HOUGANG AVE 8,
retains all 74 ACRA assertions, including source row 60428 / postal530836 versus
73 postal530446 assertions. Coordinates still require one HDB block/postal
footprint and come solely from its bounding-box midpoint. OneMap geometry never
enters the domain model. Search candidate uniqueness is corroboration, not ground
truth. Existing app runtime, fixture, OSM basemap and normalizer are unchanged.

## Official contract and licensing review — 2026-10-03

Reviewed official [Search documentation](https://www.onemap.gov.sg/apidocs/search)
and [token-error documentation](https://www.onemap.gov.sg/apidocs/docs/verifyingsearchapitoken).
The Search-specific example uses the raw token in Authorization. This tool does
not guess another credential protocol or create an alternative credential flow.

The [API Terms of Service](https://www.onemap.gov.sg/legal/apitermsofservice.html)
apply when using the API; credentials must remain confidential, use must not
misrepresent or disrupt the service, and availability/accuracy are not guaranteed.
No request has been made by this task while authentication is absent.
The [Singapore Open Data Licence](https://www.onemap.gov.sg/legal/opendatalicence.html)
permits copying, distribution and derived analysis subject to its conditions and
conspicuous source/date/licence attribution. Its exclusions include personal data,
third-party rights and intellectual property not licensed under it; it conveys no
endorsement. Before sharing actual derived evidence, review those exclusions and
retain the source, acquisition date and licence links. This document is a bounded
implementation record, not a claim that every returned field can be redistributed.

Required attribution for actual reports: “Contains information from OneMap Search,
Singapore Land Authority, accessed [actual acquisition date], made available under
the Singapore Open Data Licence”, with links above. Synthetic unit tests are
explicitly labeled and cannot be loaded as acquired OneMap evidence.

## Blocker and next milestone

Only an authorized existing token and actual acquisition are missing. No real
query outcome counts, coverage gain, source conflicts or OneMap manual audit have
been invented. Run the acquisition command, then finish the all-416 comparison,
manual transition/conflict/remaining-unmatched audit and evidence review. A sensible
following milestone is improving explainability of verified match evidence in the
native selection panel, after this acquisition milestone is truly complete.
