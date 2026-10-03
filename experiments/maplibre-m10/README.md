# M10 isolated MapLibre source build and HDB address-layer experiment

This is an isolated source/evidence experiment, not a production dependency. Build and runtime outputs belong in a separate external M10_ROOT. No production migration, old-ICU installation, machine-wide package change, coordinate change, geocoding, or database work is performed here.

## Official source pins

- Qt binding stable v3.0.0: d929c783737120787b43417d9ef05da88da75dfd, https://github.com/maplibre/maplibre-native-qt/releases/tag/v3.0.0
- Stable native core: 0a4e5a4474b54a36ffbe331a0f058124b5a4783a
- Qt binding development/main: c3485f3a9590081c7be8edbc542341e688f6663d, declares 4.0.0, explicitly incompatible with the 3.x API. It is not a released stable dependency.
- Development native core: 702ebc6114ad864d86f86dd23b469183014fd548
- Exact submodule revisions and official Debian package hashes are captured in evidence/build-manifest.json. Source checkouts, dependency trees, generated data and binaries are not intended for Git publication.

## ICU56 diagnosis

The official stable Linux Qt6.7.3 binary directly needs libicuuc.so.56 and references 13 distinct ICU functions suffixed `_56` (mostly bidi and Arabic shaping). The pinned Qt6.12 installation ships ICU73.2; the base system carries ICU76.1. A symlink cannot satisfy those versioned symbols. See evidence/stable-binary-needed.txt and stable-binary-icu56-symbols.txt. No such symlink or obsolete ICU install was attempted.

This is a binary ABI dependency, not proof that MapLibre source requires ICU56 or is incompatible with Qt6.12. Both source branches have a system/internal-ICU build decision. The experimental build uses official Debian trixie ICU76.1 development/runtime packages extracted with dpkg-deb into this directory's sysroot. Qt continues to use its own ICU73 symbols. No system packages were installed or replaced.

## Toolchain and local dependencies

Use source /workspace/shared/hdb-env.sh: Qt6.12.0, .NET10.0.401, GCC14.2.0, CMake4.4.3/Ninja. Existing local OpenGL/EGL/xkb headers/libraries are in hdb-toolchain/sysroot. Additional local packages: libicu-dev and libicu76 76.1-4; zlib1g-dev and zlib1g 1:1.3.dfsg+really1.3.1-1+b1. Package SHA-256 values are retained. Runtime ICU and zlib use local SONAME-correct libraries, with separate Qt ICU73 remaining present.

The required Qt stack is Core/Gui/Network/Sql/Qml/Quick/Location/Positioning and exact-version private Location/Quick/Gui headers. Plugin deployment needs libQMapLibre, libQMapLibreQuickPrivate, libQMapLibreLocation, GeoServices plugin and QML modules. Private Qt APIs tie the result to this exact Qt build. Current core uses C++20; stable uses C++17.

## Reproducibility and small patches

fetch-current-source.sh is a source-only reconstruction recipe for an empty isolated root (it is not run again here). It fetches each gitlink pin from the official upstream URLs and aborts on network failure. configure.sh and build.sh contain the commands actually used in this workspace. Build uses an isolated prefix and `MLN_WITH_OPENGL=ON`; Vulkan/Metal/GLFW/widgets are disabled. Compilation is limited to GeoServices and QML plugin targets, not examples or test executables. measure_command.py records wall/CPU time and peak child RSS for the command.

- Stable unpatched source reaches a real CMake generation failure: Qt6::LocationPrivate is not imported, and its core tests require Qt::OpenGLWidgets even when the widget wrapper is disabled. stable-qt612-cmake.patch explicitly imports these packages. With that patch stable configures successfully against Qt6.12. The qgeomap.cpp and texture_node.cpp translation units pass direct syntax compilation using the generated stable compile commands. This is not yet a complete stable binary build or runtime test.
- Stable base CMake automatically fetches submodules during configure. stable-no-implicit-fetch.patch removes those implicit network commands; pinned dependencies are prepared explicitly instead.
- Current source configures with Qt6.12 directly. A pinned pixelmatch-cpp commit failed with proxy CONNECT 403; pixelmatch is a test-only image comparator, unused by the Qt runtime. native-library-only.patch conditionally excludes that dependency and native test/benchmark/render-test targets for MLN_QT_LIBRARY_ONLY. It changes no renderer implementation. Exact cached official submodule commits were reused where available; no authentication attempts or random binary fallbacks were used.
- instrumentation.patch adds opt-in M10_MAPLIBRE_TRACE logging to native source-update and native observer paths. It does not synthesize completion or modify rendering. Logs are steady-clock nanoseconds, instance/thread ID, source ID, submitted byte count, update sequence, native frame-start/full/partial state and repaint/placement flags.

## Completion semantics

`updateSource()` return is submission/conversion time, not a visible-frame completion. MapChangeSourceDidChange signals mutation and is not completion. QtLocation `mapReady` indicates initialization, not data rendering. `isFullyLoaded()` can contain old renderer state. Native frame-full is an observer callback, not a GPU fence or desktop presentation timestamp. Serialize transitions, drain preceding activity, require a new update return followed by frame-start and frame-full with no pending repaint, reject failures, and record the next Qt frameSwapped separately. Independent screenshots/selection checks are needed; submitted feature count is never mislabeled as a visible feature count.

## Equivalent input and candidate boundary

The candidate consumes unchanged C# CsvImport→BlockSummaries results serialized by the M10 exporter. Exact local snapshots are in /workspace/shared/hdb-m10-maplibre-data: full7,618 address summaries (241,920 source transactions;188,573 located), ANG MO KIO340, BEDOK197, BEDOK≤500k145, empty0. They preserve original coordinates/coverage and stable address key/latest transaction ID, transaction count, median price and label. C# filter/aggregation/serialization times are separate manifest measurements; file handoff/Qt-native ingestion is measured separately. It is not an in-process Qt Bridge replacement benchmark.

QML uses one GeoJSON source and two native circle layers (data-driven prices and selection halo), not 7,618 MapQuickItem wrappers. OneMap raster and bundled required logo/attribution remain. Application-side nearest-center picking preserves key/transaction ID; the public Qt wrapper lacks native queryRenderedFeatures, so native hit-testing equivalence is not claimed. Native labels/accessibility and production Bridge integration remain additional work. The observed B viewport is 871×529 logical pixels, centered at1.3521,103.8198 at QtLocation zoom11 for full/town/different/budget. The native runner uses exactly those dimensions; highzoom targets full selected ANG MO KIO101 atzoom16. A richer candidate drawing all native circles versus B's grouped markers is a different presentation policy; input coverage alone does not establish visually identical work.

## Licenses

Qt wrapper/core binding code: BSD-2-Clause. Qt-derived Location/rendering code: LGPL-3.0-only OR GPL-2.0-only OR GPL-3.0-only. Examples: MIT. Native core: BSD-2-Clause plus dependency notices (Boost Software License1.0, MIT/BSD variants, zlib and others). Current CMake generates mbgl-core.license (about42KB), retained with build evidence; Debian package copyright files cover local ICU/zlib. ICU runtime's main license is Unicode/MIT; Debian packaging notices also list GPL tooling, so inventory is package/file-specific. This is a license inventory, not legal clearance.

## Outcome

Build and native runtime outcomes are recorded below. A configure or host compile success alone is not a MapLibre rendering success.

### Confirmed build result

Development source completed all 513 original Ninja steps; install succeeded. Current Qt core, QuickPrivate, Location, GeoServices and both QML plugins compiled/linked successfully against exact Qt6.12.0. This qualifies an actual source build, not only the host. The headless Qt6.12 loader exited0, reports `Plugin loaded: true` and lists `maplibre` beside `osm`/`itemsoverlay`. `ldd -r` reports no missing dependency or unresolved symbol; libicuuc.so.76 resolves from the isolated prefix and Qt retains libicu*.so.73. See logs/probe-current.log and logs/ldd-current.log.

The initial build was deliberately interrupted three times to protect production A/B timing or adjust parallelism, retaining completed Ninja objects. The final177-step segment took145.47seconds at4jobs,493.72CPU-user-seconds and62.42system-seconds; largest child RSS905,176KiB (not aggregate parallel-build RSS). Total active compilation was approximately10minutes; no uninterrupted clean-build wall time is claimed. On-disk current source1.1GiB, build output59MiB, installed headers/runtime/CMake/QML prefix16MiB. Shared Qt and localICU/zlib are additional requirements; development source/test history accounts for much of staging size. Compiled runtime libraries/modules total approximately13MiB before deployment packaging.

QML formatting/parsing passes. Installed-module qmllint exits0 with context-property and QVariantMap→QJsonObject warnings, so runtime validation is still required. Native runtime outcome follows below.

### Exploratory native run (not the matched comparison)

The first native run completed all12 observer-verified stages, preserved exported source/selection identities, and produced a screenshot with real address circles, selected halo, OneMap raster, logo and attribution. However, the desktop window manager enlarged the actual Map to1180×812 (DPR1). This run remains outside the compact publication pack and is explicitly excluded from equal-viewport claims. The Map item is now sized independently of the root window, and the host will assert the requested871×529 dimensions for the separately named matched run.

Eight OneMap `Unsupported image type` tile warnings occurred during this exploratory run. A read-only probe of the exact failing tile https://www.onemap.gov.sg/maps/tiles/Default/11/1612/1017.png returned HTTP200, image/png, with zero response bytes; the central tile11/1614/1016 returned a valid24,253-byte PNG. These warnings must be retained as incomplete basemap evidence, not silently suppressed or labeled a successful complete tile load. Source-update/identity evidence remains distinct from raster completeness. See evidence/onemap-tile-probe.json. The official source of the XYZ endpoint and attribution requirement is https://www.onemap.gov.sg/docs/maps/ . The official HD TileJSON has bounds but uses a differentHD tile endpoint; this experiment does not silently switch basemap resolution to improve its result.

### Matched native result and decision

Keep candidate B for M10. MapLibre is now a **working, source-built Qt6.12 future option**, rather than blocked solely by an old ICU56 binary. These tests do not justify a production migration: the candidate's fastest layer-submission number is not a full application result, and several existing interaction/label/accessibility/Bridge behaviors are still unqualified.

`matched` and `matched-final` both exited0 and completed all12 stages. Every stage asserted actual871×529 logical map dimensions (DPR1), exact expected serialized byte lengths/counts/identity anchors and correct selection identity. C# snapshots contained7,618/340/197/145/0 summaries as appropriate. Every source stage submitted exactly once; selection/zoom/pan submitted zero source updates. Both screenshots show actual OneMap pixels, native circles and a selected halo. The final repeat includes the exact logo plus “OneMap © contributors | Singapore Land Authority” attribution.

Measurements below are ranges across the two matched runs, in milliseconds. The first column is the synchronous native `updateSource` call; the second is host property submission until the qualifying native observer full-frame callback. Neither is an end-to-end C#/Bridge/app timing or guaranteed physical presentation time.

| Operation | Native source call | Host submission → settled observer |
|---|---:|---:|
| Initial full7,618 |78.0–91.5|1853–1880|
| Warm full7,618 restores (six observations)|42.7–85.8|60.9–554.9|
| ANG MO KIO340|2.0–2.5|19.7–26.7|
| BEDOK197|1.8–2.1|328.4–333.2|
| BEDOK≤500k145|0.9–1.0|12.7–13.4|
| Empty0|0.08–0.09|487.2–488.5|
| Selection-only|No source update|332.1–332.5|
| Zoom11→16 at selected AMK101|No source update|959.2–970.5|
| Local pan atzoom16|No source update|632.2–639.2|

Application-side nearest-center picking took2.14ms and7.93ms in the automated selection stages. A separate physical native-desktop click moved the selection ring/text from AMK101/HDB-222666 to JURONG WEST671B/HDB-231089 in the live host. The recorded picker call was2.29ms, with only the initial address-source submission in that entire manual run. Desktop validation confirmed the visible ring/text change and normal titlebar close; manual exit 0. The manual JSON’s generic close-message text is not a failed automatic benchmark: its status is`manual_closed`, which is the intended stopping condition.

Standalone process main-entry→QML-load-return was323–497ms; main-entry→first qualifying settled-full callback was2.23–2.38s. First-full RSS was445.7–445.8MiB; observed peak RSS was472.7–517.5MiB across the full runs. These include the measurement host, retained serialized fixtures, Qt and llvmpipe graphics work, and exclude .NET/Qt Bridge/the241,920-row application model. They cannot establish a full-app memory/startup advantage. The GPU identified itself as Mesa llvmpipe LLVM19.1.7, so these are software-rendered Linux measurements.

Three OneMap tile warnings remain in each matched run, for11/1612/{1015,1016,1017}. Do not equate the renderer’s “full” observer state with complete basemap availability: failed/empty tile resources can still yield that state. An exact failed tile was independently observed returningHTTP200 with Content-Typeimage/png and0bytes. Central tiles render. No warning suppression, replacement basemap, fabricated blank tile, or bulk tile download was used.

For context only, the independently qualified B presentation gate reported full-state275ms, town166ms, BEDOK128ms, budget35ms, small→full499ms, empty329ms, empty→full365ms, selection153ms and zoom16/individuals485ms. Those B states include live C# domain/filter/aggregation, projection, model notifications/incubation, viewport/identity assertions and25ms polling. B’s lowzoom23 groups account for all7,618 addresses, whereas MapLibre draws circles without equivalent grouping/price-label UI. **Do not compute a renderer speedup ratio from these unlike endpoints or presentations.** The input/camera match supports feasibility testing, not a completed production feature-parity comparison. The pan trajectories also differ, so pan figures are candidate behavior observations, not an A/B speed comparison.

Remaining qualification: live C#→Qt Bridge→native source ownership/cancellation and completion acknowledgement; complete labels and grouping/detail workflow; native rendered-feature hit-testing (not exposed by public Qt wrapper); keyboard/per-feature accessibility; image-source empty-tile behavior; repeatable deployment/licensing on target machines; unreleased4.0 API stability. B already preserves the existing app’s qualified workflows and adds no new native dependency, which is the practical reason to keep it now.

Evidence: `evidence/result-summary.json`, `evidence/scoped-stage-comparison.json`, complete `results/{matched,matched-final,manual}.{json,log,exit}` and `results/matched-final.full.png`. The earlier `comparison` run remains outside the compact publication pack as deliberately unmatched exploratory evidence. Raw GeoJSON and upstream/build/install trees are excluded from the pack.

### Concrete reproduction sequence

The pack contains the exact C# `exporter/Program.cs` and project used to create the snapshots. The checked-in exporter project references this repository’s Domain project relatively; the original workspace-specific reference has been made portable without changing exporter code. Use unchanged source data/domain semantics. Snapshot files are deliberately excluded.

1. Source `/workspace/shared/hdb-env.sh` and choose an empty isolated `M10_ROOT` outside the app repository.
2. Run `M10_ROOT=<root> ./prepare-local-dependencies.sh` using the existing signed official Debian metadata configuration; compare printed package hashes to `evidence/packages.sha256`.
3. Run `M10_ROOT=<root> ./fetch-current-source.sh`. This is the reconstruction recipe, not a claim of a second clean download/build in this session. Copy this pack's experiment/scripts into the root, then run `M10_ROOT=<root> ./configure.sh current` and `M10_ROOT=<root> JOBS=4 ./build.sh current`.
4. Run `cmake --install <root>/build-current`. Configure the small host with `cmake -S <root>/experiment -B <root>/experiment-build -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_TOOLCHAIN_FILE=$QtDir/lib/cmake/Qt6/qt.toolchain.cmake`, then build that host. Its CMake requires exactQt6.12.0.
5. Run `dotnet run --project exporter/export.csproj -c Release -- /workspace/shared/hdb-m9-expanded <isolated-data-directory>`. Compare counts/identity anchors and SHA-256 to `evidence/build-manifest.json`; the exporter’s timing values will naturally vary.
6. In the coordinated native desktop environment, run `M10_ROOT=<root> M10_DATA_DIR=<isolated-data-directory> M10_RUN_NAME=reproduced ./run-native.sh`. The runner sets OpenGL/plugin/library/QML paths, enforces a360-second process bound and the host enforces25seconds per stage. Expected map viewport is871×529; mismatches fail. A manual run uses`--manual` and should be closed normally after click verification.

No command in this recipe installs obsolete ICU, upgrades Qt/the machine, pushes a repository, or downloads HDB coordinates. Source/license/patch evidence and results are included; native binaries/dependency trees/generated GeoJSON are not.
