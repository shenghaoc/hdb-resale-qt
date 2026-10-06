# HDB Resale Explorer

An independent C# / Qt desktop app for exploring Singapore HDB resale registrations by address. **0.1.0 release candidate**, with the Linux x64 workflow verified. It is not affiliated with HDB, SLA or the Singapore Government.

![Linux Release with address groups and the selected-address monthly median trend](docs/product-rc/screenshots/monthly-trend-linux.jpg)

Actual Linux Release capture with the locally prepared expanded corpus and default S$1,000,000 maximum. The default clone uses six registrations. [Verification screenshots and checkpoints](docs/product-rc/README.md#physical-linux-release-check).

## Find an address worth investigating

- Combine town, flat type and inclusive minimum/maximum price filters.
- Optionally compare the latest 12 or 24 source months. Windows end at the dataset's latest month, not the computer clock; that final month may be partial.
- Browse address summaries: matching-sale count, latest registration month, median price and median price per m², price/area ranges, and lease commencement years.
- Select the same address from the list or map. Review its latest 15 matching registrations with source storey, area, model, lease commencement and remaining-lease text.
- Inspect a compact monthly-median price trend for the latest 24 source months. Dots mean observed months; gaps mean no matching sale.
- Inspect address match quality separately from approximate block coordinates. Unmapped addresses remain in the list and details.
- Navigate the list by keyboard, use native controls, and inspect provenance in the details and About dialog.

Statistics describe **exactly the transactions matching the filters**, not listings or asking prices. The default maximum is S$1,000,000. Reset restores all towns/types, minimum zero, that maximum and all source months. If minimum exceeds maximum, the result is empty until corrected.

The original six real registrations remain the checked-in default so a clone runs offline without acquiring a full dataset. A locally prepared pinned expanded corpus has 241,920 registrations across 9,755 addresses: 7,618 mapped and 2,137 without coordinates. Full-corpus inputs, private caches and generated packages are not committed.

## Map and selection

Qt Location displays OneMap raster tiles with visible attribution. C# keeps complete filtered address summaries and projects only the viewport's addresses: stable grid groups at low zoom and individual addresses at zoom 15+. Group numbers count addresses; individual pin numbers count matching transactions. A group click changes the camera; it never creates a pretend address.

The selected address survives panning offscreen, with a notice and “Show selected address”. It also survives a filter removing some of its transactions, with its summary and recent rows recomputed. It clears when no matching transaction remains. List, map and details share one C# selection.

No database, cloud account, runtime geocoding or AI dependency is required. Uncached map tiles need network access; facts and filters are local. The small Qt Graphs 2D chart plots C#-aggregated matching sales without a custom chart framework. Its 24-month display window is labelled separately from all-history summary statistics.

## Run from source

Pinned toolchain: .NET SDK **10.0.401**, runtime **10.0.12**, Qt **6.12.0**, Qt Bridge **0.4.0-beta** on Linux x64 and **0.4.0.22-beta** on macOS. Do not mix Qt installations: the Bridge uses Qt private headers.

Follow [Linux setup](docs/linux.md) or the [historical macOS provisioning record](docs/milestone-10-history.md). Set `QtDir` to the matching Qt installation and put .NET, CMake and Ninja on `PATH`. Alongside Location/Positioning/ShaderTools, M11 requires the matching official Qt Graphs and Qt Quick 3D modules (the latter is a runtime dependency of the Graphs build, even for this 2D view).

```sh
dotnet build -c Debug -m:1
dotnet test -c Debug --no-build
dotnet build -c Release -m:1
dotnet test -c Release --no-build
dotnet run --project src/HdbResale.App -c Release --no-build
```

Set `HDB_DATA_DIRECTORY` to an offline prepared input directory to explore another supported corpus. [Source preparation and provenance](data/README.md) and the [full-corpus coverage study](docs/address-coverage/README.md) document the pinned inputs; no automatic acquisition runs at startup.

[Ordered offline source imports](docs/ordered-source-import.md) preserve different source headers, source-qualified row identity and raw provenance through the existing buyer views. HDB's integer remaining-lease years are interpreted without inventing missing observations or extra precision.

[Local HTTP snapshots](docs/http-snapshots.md) add explicit discovery, bounded verified downloads, atomic local activation and offline buyer views. The public API does not yet publish this snapshot contract; no production serving change is included.

## Data interpretation

- Source months are registration months. Floor area is approximate and may include purchased recess areas or improvements.
- Median price per m² is the median of individual valid price/area ratios. Display rounding never drives filtering or aggregation.
- Recent rows are capped only after cohort filtering and deterministic sorting; medians and counts use every matching transaction.
- Source remaining lease retains its documented resale-application reference. A separately labelled estimate subtracts elapsed calendar months using the fixed latest dataset month. Cohorts containing whole-year observations display rounded whole years, with unknown exact expiry/source rounding; month observations retain approximate year/month display. Commencement year is not used as a replacement for missing source lease.
- Coordinates are approximate HDB footprint points, never exact flats. Neither a match nor a lease estimate is an eligibility, valuation or affordability assessment.

See [buyer semantics and verification](docs/product-rc/README.md), [official HDB dataset](https://data.gov.sg/datasets/d_8b84c4ee58e3cfc0ece0d773c8ca6abc/view), and [Singapore Open Data Licence](https://data.gov.sg/open-data-licence).

## Architecture

`HdbResale.Domain` owns immutable source facts, evidence, filtering, address summaries, deterministic recent rows and selection. `HdbResale.App` exposes Qt models, an incremental viewport/grid map projection and native QML controls. Reentrant UI mutations drain through one FIFO queue. The parentless Bridge map wrapper retains its explicit QML lifetime anchor.

The [sibling web visualizer](https://github.com/shenghaoc/hdb-resale-visualizer) is a product reference. Its block-median filters and out-of-filter selection behavior are deliberately not copied: this app filters transactions, then summarizes the matching cohort.

## Verification

Both Debug and Release build and pass **191 C# tests each**; all **40 Python tests** pass. The final Linux matrix passes **22 native positive cases** and detects all **42 deliberate failures**. Every completed interaction state is below the unchanged five-second goal; the slowest observed case is a 2.239-second reentrant burst. The expanded buyer flow peaks at 695 ms in Release.

Fresh-process full-corpus startup is 7.339–7.637 seconds with warm OS/cache state, compared with the measured M10 baseline of 5.950 seconds. That startup cost is disclosed separately from interaction responsiveness. [Detailed results, scope and caveats](docs/product-rc/README.md#final-linux-native-acceptance).

## Platforms, licence and RC limits

Linux x64 has the complete M11 verification record. Current macOS arm64 builds, unit tests and the available strict native gates pass; expanded-data and physical acceptance remain pending. See the [current Mac checkpoint and blockers](docs/product-rc/macos-verification.md). Windows has not been built or UI-tested. No installer publication, signing, notarization, tag, push or official release is included.

Original application code is licensed **GPL-3.0-or-later**, as chosen by its owner. See [LICENSE](LICENSE), scoped [SPDX metadata](REUSE.toml) and [third-party notices](THIRD_PARTY_NOTICES.md). Qt Graphs and its required Qt Quick 3D libraries retain their GPL-3.0-only terms; Qt/Bridge, other bundled dependencies, public data and OneMap keep their own terms. See the [local Linux RC package and licensing checklist](docs/product-rc/licensing-packaging.md). The documented local Linux directory/tar recipe includes a fresh-state, unrelated-working-directory real-X11 launch check. It is a bounded test artifact, not a claim of general distribution readiness. The final private tar passes a fresh-state, relocated launch check including the real chart and bundled runtime mappings. [Package hashes and verified scope](docs/product-rc/package-verification.json). No AppImage, DMG or Windows installer is produced, and no remote package CI is claimed.

Detailed milestone evidence stays under `docs/`: [M10 architecture and measurements](docs/large-map/README.md), [M9 coverage](docs/address-coverage/README.md), and [earlier development history](docs/milestone-10-history.md).
