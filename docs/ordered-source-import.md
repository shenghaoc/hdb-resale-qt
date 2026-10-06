# Ordered offline resale sources

This slice prepares local public HDB resale CSVs, verifies them during import,
and displays their source locators in the existing buyer details. It has no
network acquisition, database, server export, publisher, background updater,
new project or dependency. The existing C# state, QML layout, location matching
and OneMap provider remain in place.

Keep raw fields, source headers, duplicate occurrences and conservative location
evidence. Change source identity and interpret HDB's documented integer lease
years. Defer the unimplemented general snapshot/cache framework: a reproducible
offline directory is sufficient for this local capability. The existing public
D1 API still lacks complete transaction and location evidence, so this does not
claim remote API integration.

## Prepare and open

Use Python 3.12 from the existing Qt toolchain and retained CSV captures. Each
`--source` is `IDENTITY=PATH`, in the required import order. For official files,
use the data.gov.sg dataset ID as identity. The tool preserves all columns and
parsed strings without merging headers or deduplicating records.

```sh
python3 tools/prepare_transaction_sources.py \
  --source d_ea9ed51da2787afaf8e51f827c304208=/absolute/2015-2016.csv \
  --source d_8b84c4ee58e3cfc0ece0d773c8ca6abc=/absolute/2017-onward.csv \
  --evidence /absolute/prepared-location-evidence \
  --output /absolute/new-offline-directory
HDB_DATA_DIRECTORY=/absolute/new-offline-directory /absolute/HdbResale.App
```

The evidence directory supplies `address-evidence.csv`,
`postal-address-evidence.csv` and `building-evidence.geojson`. The normalization
marker is copied unchanged when present. Historical postal sidecars are excluded;
ordinary imports do not automatically load them. Coverage and provenance come
from the retained canonical evidence set. Loading older transactions does not
establish historical building locations. The [canonical-only candidate review](canonical-snapshot/README.md)
records the deliberate coverage and digest changes, while preserving historical
checkpoint evidence and explicit offline historical experiments.

Output must be new. The tool stages beside it, writes the descriptor last and
renames the completed directory. Errors clean only the staging directory owned
by that invocation. An exclusive sibling `.prepare.lock` prevents concurrent
invocations for the same output. A killed process can leave a lock/staging
directory for operator inspection; it never advances an existing input. This
is local process-failure protection, not a power-loss durability guarantee.
Choose the directory explicitly at application startup with `HDB_DATA_DIRECTORY`.

## Input contract

The presence of `transaction-sources.json` opts into `hdb-transaction-sources-v1`.
Its `sources` array is ordered. Every entry has exactly these fields:

| Field | Meaning |
|---|---|
| `sourceIdentity` | Unique ASCII letters/digits/underscore/hyphen, 1–64 characters. |
| `path` | Exactly `transactions/<sourceIdentity>.csv`; case-insensitive path collisions and symlinks are rejected. |
| `sha256`, `bytes` | Lowercase SHA-256 and exact UTF-8 prepared CSV length. |
| `records` | CSV data-record count before semantic validation. |
| `rawSha256`, `rawBytes` | Fingerprint and length of the original source bytes read by preparation. |
| `columns` | Exact prepared header order, beginning with added `source_row`. |

The descriptor is limited to 1 MiB and 1–16 nonempty sources. Each original and
prepared CSV is limited to 256 MiB, each source to 2,000,000 records, and all
prepared transaction files to 512 MiB. These cover the measured local corpus;
they are deliberate local input bounds, not a paging or transport protocol.

C# hashes and parses the same held prepared-file stream, checks counts and
header order, and admits no transaction cohort if any declared file fails.
Diagnostics identify the descriptor or source path. Valid CSV records with bad
required facts are still rejected individually with their original parsed
fields, using the existing parser and validation. This is integrity validation
against the descriptor; source origin is evidenced by the retained raw capture
receipt and preparation fingerprint, not authenticated by a hash alone.

Without the descriptor, `transactions.csv` remains the single-file input and
keeps its `HDB-<source_row>` keys. Having both modes present is an error.

## Identity, order and duplicates

Preparation adds the original record's ending physical CSV line number,
including the header, as `source_row`. For ordinary HDB rows this begins at 2.
Quoted multiline records retain their parsed strings and their original ending
line. The prepared CSV changes quoting/newlines to deterministic UTF-8/LF;
`rawSha256` identifies the original byte representation separately.

A new import key is `HDB-<sourceIdentity>-<source_row>`. The same local row number
in different sources is valid. A duplicate parsed local row within one source
is rejected with its raw fields and diagnostic. Distinct rows with equal facts
remain distinct. No global row space is allocated. The provenance tuple adds
the raw source hash so publications/reordered sources can be distinguished;
the UI key alone is not a durable or official sale ID.

Accepted input order is source-array order, then CSV order. Buyer recent rows
retain the existing deterministic newest-first ordering and use the qualified
key as the final tie-breaker. Location quality remains independent of identity:
missing/ambiguous records remain in the list and all matching-cohort statistics.

## Corrected lease interpretation and date meaning

HDB's [2015–2016 source](https://data.gov.sg/datasets/d_ea9ed51da2787afaf8e51f827c304208/view)
documents remaining lease as years at the resale application. Its captured
37,153 nonblank values are integers. The old parser retained `70` but gave it
no numeric interpretation; the new expectation is reported `70 × 12 = 840`
months. Raw `70` or `070` is unchanged. Details mark it as reported in whole
years; conversion does not add precision or establish an exact expiry month.
The existing estimate is explicitly approximate and subtracts elapsed source
month intervals. Commencement year is never substituted for a missing source
observation. Absent header remains null, present blank remains empty, and
malformed values remain raw text with a null interpretation.

The three-digit unsigned integer grammar matches the bounded year component
of the existing years/months parser. Decimal, negative, padded-whitespace or
four-digit values are not guessed. Existing year/month text remains supported.

Source `month` also needs its original meaning:
[1990–1999](https://data.gov.sg/datasets/d_ebc5ab87086db484f88045b47411ebc5/view)
and [2000–February 2012](https://data.gov.sg/datasets/d_43f493c6c50d54243cc1eab0df142d6a/view)
use approval dates; [March 2012–2014](https://data.gov.sg/datasets/d_2d5ff9ea31397b66239f245f57751537/view)
and later files use registration dates. Filters operate on each supplied source
month; no conversion between these events is inferred. The viewer's general
caption says resale records. Its remaining-lease caption uses the documented
resale-application reference. A mixed-source history is not a uniformly timed
registration cohort, a set of unique physical sales or a same-date publication.

## Reproduction and evidence

Run the normal root Debug/Release builds and tests with the existing .NET 10,
Qt 6.12 and Bridge pins, then `python3 -m unittest discover -s tools -p 'test_*.py' -v`.
`TransactionSourcesTests` covers source order, raw values, absent/blank, duplicate
occurrences/locators, unchanged location evidence, filter/lease/presentation
flow, corrupt later files, lengths/counts/headers/schema, paths and symlinks.
Preparation tests cover determinism, malformed later input, retained old output,
interrupted copy and a competing lock. The native buyer oracle independently
reads source CSVs and checks displayed provenance and whole-year precision; its
provenance expectations are bounded to the 15 rendered recent rows per state.

Existing M11 digests are historical regression controls. Their 2017+ inputs have
year/month lease text, so the full and legacy comparisons still match without
forcing a different interpretation. New source-qualified IDs/provenance and the
intentional integer-year correction are tested separately. Do not relabel the
mixed capture as M11 or change an old expected hash to hide a difference.

[Local verification and measured tradeoffs](source-import-verification.md)
records full unit/native checks and the 988,123-row mixed-source measurement.
