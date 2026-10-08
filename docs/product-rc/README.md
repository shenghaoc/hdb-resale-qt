# M11 buyer workflow and bounded 0.1.0 RC

> Historical record of the offline 0.1.0 release candidate, which imported bundled CSV data. The current app
> reads the Worker API ([worker-api.md](../worker-api.md)). Its native checks run over the recorded API, and its
> Linux package bundles no data ([licensing-packaging.md](licensing-packaging.md)).

Base: clean exact M10 `ff5cc7d1f8a61664408f35455e19a7fb8fff5227`, branch `milestone-11-product-rc`. No stale remote branch, push, tag, official release or automatic next milestone.

## Product decisions

The list now displays complete address summaries instead of 241,920 individual rows. Town, type and inclusive min/max price filter transactions in C#; source-month recency is 0/12/24 months, anchored to the unfiltered dataset maximum. A reversed price range is safely empty and explained in the UI. There is no silent clamping or accidental exception while editing.

All summary metrics and the latest 15 transactions use that exact cohort. Median per m² is a median of row ratios, in decimal arithmetic, independent of display rounding. Recent ordering is month descending, then ordinal type, storey, numeric area, price, price per m², ordinal model, source lease, and ID. No recent-row cap changes aggregate statistics.

Selection is intentionally address-level in M11. Hiding only its old representative transaction preserves the address and recomputes representative, metrics and recent rows; hiding the entire address clears list/map/detail selection. The reentrant legacy gate now asserts this stronger address contract, including retained selected map delegate and non-stale representative, rather than the obsolete transaction-clear expectation. Empty/filter/FIFO/lifetime/truth assertions remain.

Mapped summaries are the same instances as the complete address table. Every contributing row must agree on one nonnull point. A contrived address mixing missing/conflicting points stays in the table without an invented map coordinate. Real imported address keys already share match results, so production coverage is unchanged. The legacy `BlockSummaries.Located` helper retains its located-transaction projection for frozen study callers.

## Source facts and lease

No CSV/evidence/source snapshot was changed. Existing official columns are now represented: storey range, raw/parsed floor area, flat model, raw/parsed commencement year and raw/parsed remaining lease. They stay optional for older seven-column fixtures. Invalid populated optional metadata stays raw with a nullable interpreted value; it does not silently reject an otherwise accepted transaction or create a zero/default fact.

Derived remaining lease uses the source remaining-lease observation at its registration month, minus elapsed months to the explicitly labelled dataset reference month. It uses all matching observations, returning a range where they disagree. It never substitutes `99 + commencement year − current year`; a researched source record differs by 15 years from that shortcut. No eligibility claims are made.

The serialized full ImportResult is intentionally **contract v2** because facts and derived per-m² fields were added. `--import-digest` therefore changes. `--legacy-import-digest` serializes the exact previous field projection and must equal the frozen M10 full digest `88118bdb3a9a68f59ea47cddae27568623f136cb7b7d5320a376f8f752dc6cc5`. Canonical six-row and benchmark416 source rows remain pinned. Final verification records both digests separately; never describe the changed full serialization as identical.

## Native gates

`tools/buyer_gate.py` independently reads the pinned CSV, computes cohorts, decimal medians and deterministic recent rows, and supplies expectations to `BuyerGate.qml`. It covers full/town/type/min/max/cohort selection, recency, empty/reset, list/map selection, address hidden/reentry and viewport bursts. It uses the original 5-second state limit, or explicitly validated pinned expanded corpus's 10-second limit; the practical target remains comfortably below five seconds.

Existing canonical/classic/extended/reentrant/presentation gates keep transaction truth, mapped-address truth, all-role/delegate identity, native Qt projection coverage, map lifetime and FIFO checks. List count now means addresses and is checked against the independent buyer oracle.

Final builds, tests, timings, digest parity, physical Release evidence and deliberate failure sensitivity are recorded in [verification.json](verification.json) and summarized below. See [licensing and local packaging](licensing-packaging.md) for the separate RC distribution blockers.

## Physical Linux Release check

The final source's actual 1180×812 window was inspected on XFCE/X11. The entire chart, title, date endpoints and gap caption fit together. A single S$500,000 observation was visibly centered within a S$450,000–550,000 axis, and zero results cleared the chart with the selection. After clicking an address, Down+Enter selected the next address and Up+Enter returned, with a visible focused-row outline. A map marker selected a different address and scrolled the list to its highlight; the summary and recent rows agreed. Registration controls, price/type/window combinations, crossed-bounds empty state, reset, detail scrolling, Show selected address, recenter, About/Escape and clean exit were checked.

[Final monthly-trend view](screenshots/monthly-trend-linux.jpg) is the final compact-chart Release capture. [Overview](screenshots/overview-linux.jpg) and [address detail](screenshots/address-detail-linux.jpg) preserve the earlier pre-chart checkpoint. All are unaltered native JPEG captures. Required OneMap logo/attribution remain visible and are not application branding. No macOS/Windows physical verification or human mouse-wheel zoom test is claimed.

## Approved GPL licence and bounded trend chart

After the initial no-chart checkpoint, the owner explicitly selected GPL-3.0-or-later for the original application and asked to reconsider the selected-address trend. LICENSE, scoped REUSE/SPDX metadata, application package metadata, About and separate third-party notices record that choice. This does not relicense public data, OneMap assets or Qt/other dependencies. Public redistribution still requires the relevant complete-source/notices/installation-information work; a licence choice alone is not distribution clearance.

The small chart is technically useful and bounded: Qt Graphs 2D GraphsView/LineSeries plots 24 calendar months ending at the fixed dataset maximum. C# computes decimal monthly medians and sale counts from **all matching selected-address rows**, never the recent-15 subset. QML only loads those series values; null months render as documented NaN gaps, not zero or interpolated sales. A point delegate makes a single observed month visible. The C# axis bounds and aggregate series have pure tests; the native buyer gate compares every actual XYSeries coordinate against an independent CSV oracle, including missing months. Empty windows say so explicitly. No chart framework, new dataset or map migration is introduced.

Official Graphs 6.12 needs Qt Quick 3D libraries even for 2D. The exact official module closure is inventoried in the local package; no 3D application feature is added. API references: [GraphsView](https://doc.qt.io/qt-6/qml-qtgraphs-graphsview.html), [LineSeries and missing-value gaps](https://doc.qt.io/qt-6/qml-qtgraphs-lineseries.html), [ValueAxis](https://doc.qt.io/qt-6/qml-qtgraphs-valueaxis.html).

The first full native checkpoint exposed eight new Facts fields in the historically byte-frozen M5 report, although every old value and match outcome agreed. A report-only six-field JSON converter restores that legacy serialization. Full ImportResult remains v2; the frozen oracle itself is not changed. Final acceptance reruns the M5 byte comparison, not merely the counts.

## Narrow history-audit evolution

The original audit still rejects changed protected Domain/evidence bytes by default. M11 adds an explicit `--m11-buyer-contract` mode requiring exact M10 ancestry and the committed [data-contract evidence](data-contract.json): precisely four authorized Domain source SHA-256 pins, the exact old-field digest, and a distinct v2 full digest. Evidence, canonical/sample bytes, source/build pins and unrelated Domain files remain protected. Unit tests reject each of those unexpected changes. This mode acknowledges authorized importer/model evolution; it does not call the Domain unchanged or replace native digest reproduction.

The generic all-ref audit checks excluded cache/build/binary paths, raw-export hash/header, credential-shaped content and minimized historical fields. Exact private display-name/coordinate-pair absence cannot be newly certified without the excluded private reference, which was not available, transferred, read or reconstructed.

## Final Linux native acceptance

Production source is `82e6e99` (full hash in [verification.json](verification.json)); subsequent commits add audit/documentation evidence only. Source, Debug and Release Main.qml all have SHA-256 `1d0cb863e7a8daa4cc70e71206ffd6722122892a7dd6b9fe6cc362350c1770ca`. Both root configurations build with zero warnings/errors and each passes 191 C# tests. The final Python suite passes 40 tests with no skip after the package-only ICU/metadata repair (39 at the native matrix checkpoint).

All **22 native positive cases** pass: canonical plus classic/extended/reentrant, presentation and buyer scenarios across the original strict workload and expanded M9 workload in both Debug and Release. The normal five-second state budgets and explicitly validated expanded-corpus ten-second acceptance budgets are unchanged. No completed positive state exceeded five seconds. The slowest complete state was the original-workload Release reentrant burst at **2,239 ms**; expanded Debug presentation peaked at 2,148 ms.

All **42 deliberately faulty runs** fail with expected `HDB_GATE_FAIL` assertions and clean C# teardown, rather than crashes. They cover both builds/workloads and the inherited skipped-empty, skipped-burst and dropped-projection faults plus buyer skipped minimum/list action, dropped chart point and NaN substituted for an observed value. Skipped minimum fails at buyer phase 4; list/chart faults fail at phase 6. The full matrix preserves the independent Qt viewport oracle and exact CSV-derived buyer/plot oracles.

### Same-host Release observations

| Expanded scenario | M10 process / max state | Final M11 process / max state |
| --- | ---: | ---: |
| Classic | 8.37 s / 440 ms | 9.26 s / 728 ms |
| Extended | 11.45 s / 835 ms | 14.62 s / 786 ms |
| Reentrant | 10.28 s / 839 ms | 10.59 s / 1,046 ms |
| Presentation | 16.83 s / 1,145 ms | 18.14 s / 1,482 ms |
| New buyer workflow | Not present | 11.34 s / 695 ms |

These are sequential observations, not statistical estimates. Product layouts/viewport dimensions differ. Completed states include timer cadence and all assertions; they do not isolate GPU, native paint, layout, Bridge marshalling or network time. The new buyer scenario is not a like-for-like M10 process comparison.

Full native startup was **7.339 / 7.637 seconds**, versus the fresh M10 baseline's 5.950 seconds. These are fresh processes using warm OS/file/tile caches, not controlled cold starts. Optional source parsing and complete address metrics add work; this record makes no startup-speedup claim. Managed profiles include the inherited study's additional aggregation/probes, so those diagnostic operations must not be confused with the production model's single shared summary set.

A runner label initially wrote final startup samples under the earlier raw-log names; exact final files were copied to the verified prefix. The prior pre-chart 7.437 / 8.007-second summary remains historical only. No overwritten pre-chart raw sample is presented as preserved evidence.

### Data and frozen reports

The final native host reproduces the old-field digest `88118bdb3a9a68f59ea47cddae27568623f136cb7b7d5320a376f8f752dc6cc5` and distinct v2 digest `46ac65f199f916b78f56b388a19fa3a510617a6c0917c5d4787ef4e6cc856393`. The 416-row report byte comparison passes. The M5 report again has exact frozen SHA-256 `d76614762c351eb121d3dd11ed03658e53daae1a5feeae36809a029b7e62d4be`; its first expanded-facts serialization failure was corrected, not accepted.

Address qualities remain **897 Exact / 6,721 Normalized / 209 Ambiguous / 1,928 Unmatched**. Transaction qualities remain **28,396 / 160,177 / 4,305 / 49,042** respectively. There are **7,618 mapped and 2,137 missing-coordinate addresses**, and **188,573 mapped and 53,347 missing-coordinate transactions**. The six-row default, 416-row benchmark, source hashes, evidence grammar and coordinates are unchanged.

The separate clean, relocated private Linux package check passes as recorded below. Its result does not broaden verification to another distribution, macOS, Windows, human wheel zoom, signing, notarization or official publication. Source/notices obligations for redistribution remain open even when technical launch passes.

## Final bounded Linux package

The final tar is **88,616,918 bytes**, SHA-256 `0e03054d1a3df1268a3d7ef194b19b8831071d8b2d054dcc94d104dec2bb2cc8`, produced from clean commit `a5c5ca610e921917dd5a5c474e9f298c13647b91`. It includes 1,948 files, 44 Qt/ICU libraries and 30 QML modules, with runtime-only .NET 10.0.12 and the canonical six-row data. All 33 application source hashes and native input bytes equal the accepted chart checkpoint. [Machine-readable package evidence](package-verification.json) records hashes, monitored library paths and limitations. The tar stays outside Git history.

A fresh extraction to a path containing spaces, unrelated working directory, fresh HOME/XDG state and cleared developer loader environment passes in **2.005 seconds** with native/.NET exit 0. The shell, six-row data, map engine, actual GraphsView/LineSeries and final teardown produce all five ordered markers with no warning. Actual process mappings prove monitored Qt, Graphs, .NET, ICU and OSM libraries are inside the package. This is canonical launch readiness, not a full-corpus startup, fresh tile-pixel or network-success claim.

The first relocation test correctly failed: .NET chose host ICU 76 although Qt used bundled ICU 73, and Bridge metadata had been omitted. The package-only repair retains the metadata and uses Microsoft's documented app-local ICU mode with relative internal links to the existing, unmodified Qt ICU 73.2 files. No system library was installed and no mapping assertion was relaxed. The fixed packaged runtime also reproduces the exact expanded-profile legacy digest.

Host OpenSSL 3/TLS, glibc, X11/graphics and other inventoried system dependencies remain. Optional LTTng tracing is unavailable on this host; ordinary launch does not need it. The original application is GPL-3.0-or-later, but complete corresponding source/notices and other component-specific redistribution obligations remain a public-shipping blocker. No signing, notarization, installer publication, push or tag occurred. macOS/Windows remain unverified for M11.
