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
