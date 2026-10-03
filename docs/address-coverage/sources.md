# Public source investigation and licensing

Reviewed/retrieved 2026-10-03. [Exact source provenance](source-provenance.json)
records all 27 dataset IDs, titles, public dataset/download URLs, source retrieval
and official modification times, SHA-256 hashes, sizes, counts, schema hashes and
projection hashes. These are ACRA **A–Z plus Others**, not a B-only extract.

## Acquired evidence

[ACRA Information on Corporate Entities](https://data.gov.sg/collections/2/view)
is a monthly public collection. The pinned full source has 2,117,243 records,
688,359,378 bytes and 53 columns in every partition. Each download size equals the
official catalogue size. Official last-modified timestamps span
2026-09-16T02:00:25Z–02:04:13Z. The original B bytes are reused unchanged.
Full original files stay outside Git and the app. The lossless address-only
projection retains **108,882 assertion rows / 2,579 normalized corpus addresses**:
source_dataset, physical ending source_row, block, street_name, postal_code.
No company/entity names, UENs, people, unit, level, contact, business descriptions,
cached coordinates or display names enter this projection. Duplicates and every
contradictory/malformed value remain; raw strings are not repaired or rewritten.
The exact projection SHA-256 is
`2408032fbbcdad12783c8c03a30a20728357cd2a41dfae23b7ff8f39344635e6`.

The [independent source audit](independent-source-audit.json) independently
re-read every eligible row across all 2,117,243 original source records and
verified every projected field/source row and completeness, not a sample.
Row numbers identify the pinned CSV snapshot, not authoritative address IDs.
All current source records are single physical CSV lines; tooling still uses the
ending line consistently for quoted multiline records.

Downloads use the documented public `poll-download` route with no credentials.
The [public download limit](https://guide.data.gov.sg/developer-guide/api-overview/api-rate-limits)
is two requests per ten seconds; sequential preparation spaces calls at least
5.5 seconds apart. Transient signed download URLs are neither committed nor
required in the provenance. No private Search cache or token was requested.
Current endpoints may return future monthly bytes; regeneration deliberately
rejects a hash mismatch rather than silently updating the experiment.

## Stronger alternatives considered first

- [HDB Property Information](https://data.gov.sg/datasets/d_17f5382f26140b1fdae0ba2ef6239d2f/view)
  has explicit block/street but no postal or shared footprint ID. Its official
  page directs postal enquiries to SingPost and notes possible charges.
- [HDB Existing Building](https://data.gov.sg/datasets/d_16b157c52ed637edd6ba1232e026258d/view)
  has BLK_NO/POSTAL_COD/OBJECTID/ENTITYID/ST_COD but no street-name field. No
  verified public ST_COD-to-street crosswalk was found in this bounded search.
  Block-set, proximity and street-code inference remain forbidden.
- [OneMap Search documentation](https://www.onemap.gov.sg/apidocs/search)
  requires authorization and relevance-sorts results. No fresh Search was
  acquired. A future authorized acquisition would need all candidates/pages,
  actual returned block/road and conflicts, rather than just the first result.
- [SingPost data solutions](https://www.singpost.com/business/promote-your-products/data-solutions)
  are a plausible higher-authority subscription source, not a verified free
  bulk dataset available here. No subscription/account was opened.
- Narrow public registrant lists from BCA, NEA and HDB contractors were considered
  but do not provide a broad authoritative residential address map; they were
  not integrated. This is not proof that no other public source exists.

ACRA registered addresses remain **official-record corroboration**, not an
HDB building identifier, verified returned residential identity or historical
transaction truth. Adding sources exposes contradictions as well as gains.

## Licence and attribution

Contains information from ACRA via data.gov.sg, retrieved 2026-10-03, and HDB
resale/property/existing-building datasets, under the
[Singapore Open Data Licence v1.0](https://data.gov.sg/open-data-licence).
The licence permits copying/adapted analysis with conspicuous source/date/licence
attribution, subject to exclusions for personal data and unlicensed third-party
rights; no endorsement or accuracy/completeness warranty is claimed. Only public
building-level address assertions and required provenance are distributed.
There was no new account, subscription or explicit legal-acceptance step.

Historical assertions separately contain information derived from OneMap Search,
Singapore Land Authority, under its [Open Data Licence](https://www.onemap.gov.sg/legal/opendatalicence.html),
from the unchanged approved M5 minimized projection. Its raw source was provided
2026-10-03 with cache-write times May–August 2026; actual query dates are unknown.

## Sibling web producer rechecked

Read-only inspection of [the sibling web repository](https://github.com/shenghaoc/hdb-resale-visualizer)
found the five relevant files byte-identical at the prior pinned
`387e3352699efd13f0e7dab9c3fe14b6fe7595e5` and observed default
`3ead543a325559c6e9bc58db4025f78189af7279`:

- `scripts/lib/pipeline.ts`: trim/collapse whitespace/uppercase town-block-street,
  then lowercase/nonalphanumeric-hyphen producer key.
- `scripts/sync-data.ts`: only absent keys queried, no expiry; exact request
  `<block> <street> SINGAPORE`.
- `scripts/lib/sync/geocode.ts`: page 1, `results[0]`; returned block/road and
  other candidates are not retained. Loading cache ignores row timestamps;
  persistence writes a batch time.
- `scripts/lib/schemas.ts`: cached postal was stripped to digits and padded;
  provider original postal cannot be reconstructed.
- `migrations/0001_initial.sql`: cache schema confirms these limited fields.

M9 neither downloads nor reconstructs the private cache. It reuses only the
unchanged 378-identity approved M5 projection, exact-hash checked, with explicit
first-page/first-result limitations and no cached coordinates/display names.
