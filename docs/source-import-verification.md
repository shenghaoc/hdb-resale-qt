# Local ordered-source verification — 2026-10-06

Mac arm64; .NET SDK 10.0.401/runtime 10.0.12, Qt 6.12.0,
Bridge 0.4.0.22-beta. This is local verification, with no production database
write, deployment, source-corpus download, snapshot publication or Neon cutover.
The web repository and separate UI worktree were not edited.

## Correctness and native evidence

| Check | Result |
|---|---|
| Full root Debug / Release builds | Pass; zero MSBuild warnings/errors, normal restore/audit. Existing Qt CMake notices remain. |
| Full C# Debug / Release | 212 passed, 0 failed/skipped, each. |
| Python 3.12 | 47 passed. |
| Historical M11 native-host full/legacy digest checks | 4 passed across Debug/Release. |
| Native positive matrix | 24 passed: canonical, strict 10,000-row and expanded M11 classic/extended/reentrant/presentation/buyer checks, plus the mixed-format source buyer flow, both configurations. |
| Final buyer rechecks after wording corrections | 8 passed: canonical, strict, M11 and mixed sources, both configurations. |
| Deliberate native failures | 54 detected with `HDB_GATE_FAIL` and clean `HDB_GATE_EXIT`, including wrong displayed source hashes and wrong lease expectations. |
| Maximum captured completed positive state | 884 ms. Existing 5-second state limits and validated M11 10-second limits were unchanged. |

Native gates use the development bundle's actual Cocoa executable under
`obj/<configuration>/net10.0/HdbResale.app/Contents/MacOS/HdbResale.App`.
They assert C#/QML state, actual map/list projections, selection, chart series,
reentry and displayed source evidence. They do not certify tile pixels,
hardware input, paint/frame latency, accessibility, packaging or remote API use.
The mixed-format native fixture is derived from the six canonical public rows
with explicitly synthetic absent/integer/year-month variants; it is a test
fixture, not additional official evidence.

M11's exact retained 2017+ inputs contain year/month lease text. Full digest
`46ac65f199f916b78f56b388a19fa3a510617a6c0917c5d4787ef4e6cc856393`
and legacy digest
`88118bdb3a9a68f59ea47cddae27568623f136cb7b7d5320a376f8f752dc6cc5`
still match. No old expected hash was rewritten. This is historical regression
evidence, not authority over the new integer-year interpretation or qualified
source keys. The corrected unit/as-of evidence and contract are documented in
[ordered-source-import.md](ordered-source-import.md).

## Representative mixed-source import

Five retained captures were read locally: 1990–1999, 2000–February 2012,
March 2012–2014, 2015–2016 and the retained M11 2017+ capture. Each raw hash
matched its retained receipt before preparation. Captures have different dates;
this is not the active August D1 publication or a same-date complete snapshot.
Approval-date and registration-date sources retain their original month meaning.
Location evidence is the unchanged M11 prepared evidence set, not a claim of
historical building-location ground truth.

Preparation took 1.283 seconds. Original CSVs totalled 83,166,023 bytes;
prepared transaction shards totalled 89,638,079 bytes, adding source-local rows.
Both fresh-process managed runs independently checked declared source counts,
unique qualified keys, source order/hash/row provenance and integer-year math.

| Observation | Result |
|---|---|
| Accepted transaction rows | 988,123; all declared rows retained. |
| Duplicate-looking fact occurrences retained beyond the first | 1,929; not collapsed into unique sales. |
| Absent remaining-lease fields | 709,050; remain null. |
| Integer-year observations | 37,153; now interpreted, raw text unchanged. Previously their numeric interpretation was null. |
| Year/month observations | 241,920. |
| Addresses / mapped / missing | 10,016 / 7,618 / 2,398. |
| Transactions with / without coordinates | 733,810 / 254,313. |
| Rejections / diagnostics | 1 / 198, from the unchanged location evidence; no transaction source record was rejected. |
| Fresh-process import, warm filesystem cache | 5,490.8 ms and 3,249.1 ms. |
| Town, 3-room, S$300k–700k, latest 12 source months | 14.0 ms and 11.6 ms; 378 rows, 158 addresses, 149 mapped. |
| Full reset aggregation | 716.3 ms and 396.7 ms. |
| Managed retained roots after diagnostic forced GC | 637.6 MiB and 664.1 MiB. |
| Whole-process maximum RSS, supported-access run | 1,933,246,464 bytes (about 1.80 GiB). |

These are two observations, not a statistical performance claim. The .NET-only
probe includes independent validation, duplicate grouping and diagnostic GC;
whole-process peak RSS therefore does not isolate production import memory or
native Qt memory. Retained roots include the import and full ExplorerState, plus
runtime/parser pools. No forced GC was added to the application. Per-import
allocation was about 2.09 GiB. The memory cost of materializing all source facts
is disclosed; no streaming/cache/storage framework was introduced to improve a
score. The first resource wrapper was blocked by the sandbox's `kern.clockrate`
query; supported-access repetition exited successfully and supplied RSS.

## Reproduce the checks

With the existing toolchain's Python/ninja and Homebrew CMake on `PATH`, and
`QtDir` pointing to Qt 6.12.0:

```sh
dotnet build -c Debug -m:1
dotnet test -c Debug --no-build
dotnet build -c Release -m:1
dotnet test -c Release --no-build
python3 -m unittest discover -s tools -p 'test_*.py' -v
```

Run `tools/buyer_gate.py` with `--executable`, `--data` and `--log` against an
ordered source pack. Use `--expanded-coverage` only for the exact pinned expanded
M11 directory; its validator intentionally does not accept an arbitrary corpus.
Use the existing scale/presentation/native gate commands for their workloads.

Raw local evidence is preserved outside the source checkout in the task's
`snapshot-evidence` directory: TRX files, build/unit logs, `native/*.json` and
individual native logs, preparation receipts, two import reports and resource
statistics. The probe and native orchestration scripts remain there as
measurement artifacts; they are not extra production projects or dependencies.

## Occurrence reconciliation checkpoint, 2026-10-06

The retained local audit proves **988,123 captured source occurrences + five
explicitly approved unresolved retentions = 988,128 recorded Neon candidate
rows**. All declared transaction rows in this Qt reconstruction were accepted.
The audited difference contains five candidate-only occurrences and zero
Qt-only occurrences. No data was dropped in this audited import/comparison and
no counts were forced to agree. These are different source and retained-storage
cohorts; this is not a fresh query or serving acceptance of the live candidate.

| Recorded retained ID | Candidate multiplicity | Captured source multiplicity |
| --- | ---: | ---: |
| 550818 | 1 | 0 |
| 601171 | 1 | 0 |
| 934839 | 1 | 0 |
| 955489 | 1 | 0 |
| 959788 | 1 | 0 |

The reviewed candidate was reconstructed from the 985,533-row retained August
baseline plus 2,595 staged insertions, with zero transaction updates/deletions.
The exact source fact-multiset SHA-256 is
`90b8d365c031da247c5a41966afd1b0f6be5dcf5c95fb8598b37bf095165d9d3`;
the reconstructed 988,128-row candidate digest is
`21b2fb88e3f6c025a54fd09996450a0327ed1281f250659082896f4eb8457787`.
The five-retention multiset digest is
`44c13d42f9277d1b4e9a86f2b36e69b79b50bf287b2ad0953e9f2f5592878276`.
None of these hashes is an official physical-sale identity.

The two 2017+ captures each contain 241,920 rows and 23,928,760 bytes:

| Capture | Raw SHA-256 |
| --- | --- |
| Qt/M11 recovery pin | `3c3d8bf9b12adb88919fa74869922fe05cdb144f5f69d93a0de4345d047cc7d4` |
| Web capture saved 2026-10-04T06:03:06.149Z | `9835dfe6cd92a46a1302fabf3a692bf893ee5b86ec95638d10dfce61dbfbdb9a` |

Their exact raw CSV-line and parsed-field multisets are equal, including every
duplicate occurrence and remaining-lease string. **39 data positions differ
only by ordering**; header bytes and LF endings agree. Their distinct byte
hashes and source-local row locators must stay separate. The Qt recovery pin
does not prove an acquisition date; original Web HTTP response headers were
not retained. The four historical October 6 captures total 746,203 rows and
match the retained August baseline's normalized historical fact multiset;
original August raw byte identity is unproven. The exact prepared source versions
are pinned below, with descriptor SHA-256
`ea36ce53fd727cb7bf76e4b90a9af572f59a86aae58323cb4752c133b6f96f5a`.
This remains a mixed-date reconstruction with approval and registration month
meanings preserved, not a uniformly timed publication.

| Source identity | Rows | Original SHA-256 |
| --- | ---: | --- |
| d_ebc5ab87086db484f88045b47411ebc5 | 287,196 | `2e064923f41cc96536db04c978901695aa0191022829895cf4ce177e42afad57` |
| d_43f493c6c50d54243cc1eab0df142d6a | 369,651 | `5f6a72bd7b9120281863beb1fc4c89aa5f4923f00b0f6c52494c4347363f2c36` |
| d_2d5ff9ea31397b66239f245f57751537 | 52,203 | `6a16e2dc78f70048ec1a522b59a9727581c36e45e02022641c53e755a7657c72` |
| d_ea9ed51da2787afaf8e51f827c304208 | 37,153 | `4ff7ce4a4f642fb384d2b75a42e75b0477e50005f4aaa9989591a7a92507d185` |
| d_8b84c4ee58e3cfc0ece0d773c8ca6abc | 241,920 | `3c3d8bf9b12adb88919fa74869922fe05cdb144f5f69d93a0de4345d047cc7d4` |

Duplicate grain explains **Qt 1,929 versus D1 2,014 extra duplicate-looking
occurrences**. Qt `TransactionFacts` retains exact optional source text,
including remaining lease. The common D1 tuple contains month, town, block,
street, address key, flat type, storey range, area, lease commencement year,
price and model; it excludes raw remaining lease, source identity and location.
There are exactly **85 groups with differing remaining-lease strings** and
otherwise equal common tuples. Those variants account for the entire difference.
The common tuple is sorted and byte-length framed for multiset hashing; every
occurrence contributes separately. Neither policy deduplicated any input row.

Evidence is the read-only sibling repository's
`docs/proposals/transaction-occurrence-reconciliation-2026-10-06.md`
(SHA-256 `6ebf524dd8450baf2b6debd354b801f3268b09eb5a5a6109b4f5e910a9d47bd3`)
and `docs/evidence/transaction-occurrence-reconciliation-2026-10-06.json`
(SHA-256 `993b62b8acf9e8d856ee030f361e11e7a3afcdb20b163d77ff2fb63f36d74383`).
That audit used read-only SQLite, local captures, staged insertions and recorded
publication receipts; it made zero provider calls or database writes. Its input
preservation checks passed. The desktop checkpoint independently rechecked the
report hashes, descriptor and five original/prepared source pairs before
recording these conclusions. The web repository remains under its own owner;
no web code or database was changed here.
