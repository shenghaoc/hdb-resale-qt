# M11 buyer workflow and bounded 0.1.0 RC

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

Final builds, tests, timings, digest parity, physical Release evidence, deliberate failure sensitivity and publication audit are recorded after native verification. See [licensing and local packaging](licensing-packaging.md) for the separate RC distribution blockers.

## Physical Linux Release check

The final source's actual 1180×812 window was inspected on XFCE/X11. After clicking an address, Down+Enter selected the next address and Up+Enter returned, with a visible focused-row outline. A map marker selected a different address and scrolled the list to its highlight; the summary and recent rows agreed. Registration controls, price/type/window combinations, crossed-bounds empty state, reset, detail scrolling, Show selected address, recenter, About/Escape and clean exit were checked.

[Overview](screenshots/overview-linux.jpg) and [address detail](screenshots/address-detail-linux.jpg) are unaltered native JPEG captures. Required OneMap logo/attribution remain visible and are not application branding. No macOS/Windows physical verification or human mouse-wheel zoom test is claimed.

## Approved GPL licence and bounded trend chart

After the initial no-chart checkpoint, the owner explicitly selected GPL-3.0-or-later for the original application and asked to reconsider the selected-address trend. LICENSE, scoped REUSE/SPDX metadata, application package metadata, About and separate third-party notices record that choice. This does not relicense public data, OneMap assets or Qt/other dependencies. Public redistribution still requires the relevant complete-source/notices/installation-information work; a licence choice alone is not distribution clearance.

The small chart is technically useful and bounded: Qt Graphs 2D GraphsView/LineSeries plots 24 calendar months ending at the fixed dataset maximum. C# computes decimal monthly medians and sale counts from **all matching selected-address rows**, never the recent-15 subset. QML only loads those series values; null months render as documented NaN gaps, not zero or interpolated sales. A point delegate makes a single observed month visible. The C# axis bounds and aggregate series have pure tests; the native buyer gate compares every actual XYSeries coordinate against an independent CSV oracle, including missing months. Empty windows say so explicitly. No chart framework, new dataset or map migration is introduced.

Official Graphs 6.12 needs Qt Quick 3D libraries even for 2D. The exact official module closure is inventoried in the local package; no 3D application feature is added. API references: [GraphsView](https://doc.qt.io/qt-6/qml-qtgraphs-graphsview.html), [LineSeries and missing-value gaps](https://doc.qt.io/qt-6/qml-qtgraphs-lineseries.html), [ValueAxis](https://doc.qt.io/qt-6/qml-qtgraphs-valueaxis.html).

The first full native checkpoint exposed eight new Facts fields in the historically byte-frozen M5 report, although every old value and match outcome agreed. A report-only six-field JSON converter restores that legacy serialization. Full ImportResult remains v2; the frozen oracle itself is not changed. Final acceptance reruns the M5 byte comparison, not merely the counts.

## Narrow history-audit evolution

The original audit still rejects changed protected Domain/evidence bytes by default. M11 adds an explicit `--m11-buyer-contract` mode requiring exact M10 ancestry and the committed [data-contract evidence](data-contract.json): precisely four authorized Domain source SHA-256 pins, the exact old-field digest, and a distinct v2 full digest. Evidence, canonical/sample bytes, source/build pins and unrelated Domain files remain protected. Unit tests reject each of those unexpected changes. This mode acknowledges authorized importer/model evolution; it does not call the Domain unchanged or replace native digest reproduction.

The generic all-ref audit checks excluded cache/build/binary paths, raw-export hash/header, credential-shaped content and minimized historical fields. Exact private display-name/coordinate-pair absence cannot be newly certified without the excluded private reference, which was not available, transferred, read or reconstructed.
