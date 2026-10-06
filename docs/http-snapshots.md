# Local HTTP snapshot MVP

The desktop can now discover a static snapshot, download it over HTTP, verify
compressed and unpacked SHA-256/byte counts plus declared transaction counts,
activate it locally, and open it in the existing buyer views. C# retains source
facts, quality, filters, selection and reentry ownership. This adds no provider
interface, project, database dependency or runtime AI. OneMap direct tiles,
attribution, assets and cache behavior remain unchanged.

This is a local-first continuation. The current public D1 API returns aggregate
and bounded detail responses, not the complete source facts and location evidence
required by the snapshot importer. It does not currently publish this static
contract. Its responses are not substituted for full imported cohorts. Neon
serving acceptance is not a prerequisite. No production publication, API change,
database write, deployment, refresh schedule or routing change is included.

## Use a prepared directory and local server

First use [the existing preparation tool](ordered-source-import.md) to create an
ordered `transaction-sources.json` directory. Local legacy `transactions.csv`
imports and bundled fixtures continue to work through `HDB_DATA_DIRECTORY` or
the default bundled data. HTTP packs explicitly use the ordered-source layout.

```sh
python3 tools/prepare_http_snapshot.py --prepared /path/to/prepared --output /path/to/new-http-pack
python3 -m http.server 8765 --bind 127.0.0.1 --directory /path/to/new-http-pack
```

In a second terminal, after the usual Release build, use the native development
bundle on macOS:

```sh
src/HdbResale.App/obj/Release/net10.0/HdbResale.app/Contents/MacOS/HdbResale.App --sync-snapshot http://127.0.0.1:8765/ /path/to/cache
src/HdbResale.App/obj/Release/net10.0/HdbResale.app/Contents/MacOS/HdbResale.App --snapshot-cache /path/to/cache
```

Synchronization prints the activated directory and exits. Failure prints a
reason and exits 1. Opening a cache verifies its pointer, manifest and file
inventory/hashes before loading the existing UI. The server may be stopped
between these two commands. Keeping synchronization as a separate command
prevents its validation import and the native UI import from being retained
together, and avoids any change to UI thread/reentry behavior. The explicit
`--snapshot-cache` selection takes precedence over `HDB_DATA_DIRECTORY` for that
launch. Normal launches retain their existing local import/fixture behavior.

The preparation tool writes a new local directory only, refusing an existing
output. It compresses the actual prepared files deterministically and hashes the
same held bytes. It does not upload, publish a current pointer remotely or query
any database. No gzip/CSV/evidence data or generated cache belongs in Git.

## Concrete version 1 contract

All requests are ordinary unauthenticated GETs under one explicitly supplied
HTTPS directory, with loopback HTTP allowed for local checks. Redirects, URI
userinfo, queries and fragments are rejected. There are no credentials, private
POSTs or per-client database reads.

`current.json` contains exactly:

```json
{"schemaVersion":"hdb-snapshot-current-v1","manifestSha256":"<64 lowercase hex characters>"}
```

Its manifest is `manifests/<manifestSha256>.json`; the entire manifest byte hash
must match the pointer. The manifest contains:

```json
{"schemaVersion":"hdb-desktop-snapshot-v1","importerVersion":2,"files":[
  {"path":"transaction-sources.json","sha256":"<unpacked SHA-256>","bytes":123,
   "gzipSha256":"<compressed SHA-256>","gzipBytes":100}
]}
```

The example file declaration illustrates the shape; a real pack requires all
evidence and source entries. Every compressed object is
`objects/<gzipSha256>.gz`. Only `transaction-sources.json`,
`address-evidence.csv`, `postal-address-evidence.csv`,
`building-evidence.geojson`, the optional `address-normalization.txt`, and
`transactions/<sourceIdentity>.csv` are accepted. Historical postal sidecars are
excluded. New packs declare importer version 2; sidecar-free version-1 packs
remain readable. A version-1 pack containing historical evidence fails explicitly
and stays on disk, rather than being reinterpreted as the canonical-only cohort.
Inventory must match the ordered source descriptor exactly. The
[canonical-only candidate review](canonical-snapshot/README.md) records its exact
quality changes and new digest expectations. Hashes detect corruption; source
raw-hash metadata is not independent authentication of an official publisher.
The selected origin and its publication process must be trusted.

Bounds: current metadata 4 KiB, manifest 1 MiB, 5–21 files, each compressed or
unpacked file at most 256 MiB, total compressed and total unpacked at most
768 MiB. Existing source descriptor bounds also apply: 1–16 sources, at most
512 MiB prepared transactions, and at most 2 million records per source.
Metadata rejects unknown members/unsupported versions. A streamed object cannot
exceed its declared length; gzip output is independently bounded and hashed.
Each request has a two-minute timeout, with a ten-minute overall network/download
deadline. Semantic import validation is synchronous and completes before
activation; it is not a cancellable UI operation.

## Activation and failure behavior

An exclusive cache lock rejects competing synchronization. Downloads use a new
owned staging directory under `snapshots/`, never the active directory. The
existing importer checks source headers, integrity, row identities and declared
counts; all declared transaction occurrences must be accepted. Existing evidence
quality diagnostics remain visible on subsequent UI import rather than being
repaired or converted into inferred coordinates.

The client rereads `current.json` after verification. If its manifest hash
changed, activation fails and the caller can retry explicitly. A complete stage
is renamed to `snapshots/<manifestSha256>`, then a flushed temporary active
pointer atomically replaces `active.json`. A previous complete snapshot survives
download, corruption, unsupported version, count, cancellation or pointer-change
failure. Cached complete generations can be reverified and reused without object
downloads. Old generations remain for explicit inspection; there is no automatic
eviction policy in this MVP.

Caught failures remove their owned incomplete stage. A killed process can leave
an orphan `.download-*` directory; it is never selected as active and does not
prevent a later sync. The file lock is released by process exit. There is no
automatic deletion of another process's leftover files. Atomic activation is
verified for ordinary process/transfer failure, not arbitrary storage corruption
or power-loss durability. Offline integrity checks reject a damaged active cache.

## Lease precision

Integer-year and year-only source observations retain their exact raw text and
an explicit whole-year precision flag. Arithmetic may use months internally,
but any aggregate containing such an observation displays only rounded whole
years and states that exact expiry and the source rounding convention are
unknown. For example, the mixed fixture now shows approximately `68 years`,
instead of a falsely precise `68y 0m–68y 5m`. Filtering to observations that
actually report months restores approximate year/month display. Missing lease
stays unavailable; commencement year is not substituted. Display rounding does
not assert a source rounding rule or a guaranteed expiry interval.

## Next publication decision

The missing production piece is an owner-approved public static directory
serving this exact versioned current/manifest/object inventory, produced from a
reviewed source/evidence cohort. It could be hosted independently of the serving
database. Choosing that cohort, handling retained unresolved rows, publishing
files, and changing the remote current pointer need their own authorization.
The imported 988,123-row reconstruction is not relabelled as the 988,128-row
recorded retained candidate. [The occurrence reconciliation](source-import-verification.md#occurrence-reconciliation-checkpoint-2026-10-06)
preserves that distinction. No server implementation or production change was
made to fill this gap.

[Local tests, native evidence and memory limits](http-snapshot-verification.md)
