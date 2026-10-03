> Historical M7 spike. M10 later completed a pinned Qt 6.12 source build and real 7,618-address layer comparison. See the [M10 results and limits](../../experiments/maplibre-m10/README.md). The earlier binary failure below remains valid historical evidence.

# MapLibre Qt Location spike

Date: 2026-10-03. Isolated from the HDB application. Pinned application stack remains .NET 10.0.401, Qt 6.12.0 Linux gcc_64 and Qt Bridge 0.4.0-beta.

## Decision

Keep the current OneMap/QtLocation implementation for M7. MapLibre offers a credible future path for native, batched polygon layers and data-driven map styling, but this spike did not establish a rendering or performance advantage. A production dependency would currently add a native build/deployment qualification project.

## Verified here

- A standalone C++ loader/QML host **configured and compiled against exactly Qt 6.12.0**. This is a successful harness build, not a successful MapLibre build.
- Baseline GeoServices providers are `osm` and `itemsoverlay`; MapLibre is not bundled in this Qt installation.
- Downloaded the official v3.0.0 Linux/Qt 6.7.3 binary archive (3,336,324 bytes). Qt plugin metadata identifies provider `maplibre`, GeoServices factory IID `/6.0`, and Qt 6.7.0 build metadata.
- The compiled headless loader exited 2: `Cannot load library .../libqtgeoservices_maplibre.so: libicuuc.so.56: cannot open shared object file: No such file or directory`. `ldd -r` independently reports the missing ICU 56 library/symbols. Qt 6.12 here carries ICU 73. No library symlinks, old ICU installation or stack downgrades were attempted.
- OneMap's Singapore test tile returned HTTP 200 and a 256×256 PNG. That establishes endpoint reachability, not MapLibre rendering.
- Generated and structurally checked 4,096 **synthetic** polygons with 20,480 coordinate pairs. These are artificial geometry/prices, not HDB footprint evidence.
- QML syntax formatting/parsing succeeded. Qt 6.12 `qmllint` returned 0 but warned about the release's unresolved `Style` metadata and QVariantMap-to-QJsonObject bindings, plus context properties. This is not a clean semantic validation or a runtime pass.

**Not run:** MapLibre map rendering, actual HDB polygon load, pan/zoom, hit-testing, memory/frame timing, or comparison with M7. No desktop/GUI session was used. The bounded spike stops at the observed dependency blocker rather than building the large native-core dependency tree.

## Correct project, version and Qt support

The maintained project is [maplibre/maplibre-native-qt](https://github.com/maplibre/maplibre-native-qt). The plugin provider is `maplibre`; the binary is `libqtgeoservices_maplibre.so`; the CMake package is `QMapLibre` with `QMapLibre::Location` and helper `qmaplibre_location_setup_plugins(target)`. “maplibreGeoServices” describes the role, not the provider name/package.

- [Latest published stable release](https://github.com/maplibre/maplibre-native-qt/releases/tag/v3.0.0): **v3.0.0**, 2024-10-11, source commit `d929c783737120787b43417d9ef05da88da75dfd`. Desktop binaries cover Qt 5.15/6.5/6.6/6.7; none targets Qt 6.12. QML import: `MapLibre 3.0`.
- Inspected current main at **`c3485f3a9590081c7be8edbc542341e688f6663d`**, 2026-09-20. It declares 4.0.0 but its [README](https://github.com/maplibre/maplibre-native-qt/blob/c3485f3a9590081c7be8edbc542341e688f6663d/README.md) explicitly says it is still in development and incompatible with 3.x. Its Location import is `MapLibre.Location 4.0`.
- Main advertises Qt 6.5 and newer, but inspected [Linux CI](https://github.com/maplibre/maplibre-native-qt/blob/c3485f3a9590081c7be8edbc542341e688f6663d/.github/workflows/Linux.yml) uses Qt **6.11.2**, not 6.12. This is broad documented intent, not evidence of a tested 6.12 combination. The ICU failure is an observed binary-dependency issue; it does **not** prove Qt 6.12 source incompatibility.

## Capabilities established from source, not rendering

Both inspected versions explicitly accept `geojson`, `vector`, `raster`, `raster-dem` and `image` sources. `Style`, `SourceParameter`, `LayerParameter` and `FilterParameter` connect to the native style engine. Layer paint/layout values reach the native conversion API, supporting the normal data/zoom-expression architecture. [Source adapter](https://github.com/maplibre/maplibre-native-qt/blob/v3.0.0/src/core/style/source_style_change.cpp), [layer adapter](https://github.com/maplibre/maplibre-native-qt/blob/v3.0.0/src/core/style/layer_style_change.cpp), [expression specification](https://maplibre.org/maplibre-style-spec/expressions/).

For thousands of footprints, the appropriate experiment is **one FeatureCollection source plus a small number of fill/outline layers**. This removes the need for one QML `MapQuickItem` per feature. Just switching providers while retaining individual Qt map items does not establish that benefit: the adapter's feature path creates a source/layer pair per feature. [Feature translation](https://github.com/maplibre/maplibre-native-qt/blob/v3.0.0/src/core/style/style_change.cpp).

Important limitations:

- A GeoJSON `data` update calls `setGeoJSON` for the entire source. The Qt binding does not expose a feature-delta update API. Native filters/paint changes can avoid resending unchanged geometry, but arbitrary per-feature data changes still need qualification. [Implementation](https://github.com/maplibre/maplibre-native-qt/blob/v3.0.0/src/core/map.cpp).
- No public `queryRenderedFeatures` or `setFeatureState` binding was found in either inspected Qt source tree. Preserving selection, hover, accessibility and the existing detail workflow needs its own design, potentially a spatial index or additional native binding.
- The QML source helper forwards a limited property set. For example, GeoJSON clustering options are not forwarded by that adapter. A complete style JSON or lower-level C++ source API may be required for richer source configuration.
- Vector tiles add a data-production/hosting step; MapLibre does not supply HDB geometry. OneMap remains a raster base in this experiment. Rich layer types and expressions are capabilities to qualify, not measured improvements.

## Setup and licensing

A compatible source build needs the MapLibre Native submodules, matching Qt/private Location headers, compiler/CMake, renderer dependencies and an ICU decision. The installed Qt has the private Location headers, but this spike did not compile the MapLibre core. Deployment includes core/Location libraries, `plugins/geoservices`, QML plugins, and renderer/native dependencies. Because the plugin uses Qt private APIs, qualify against the exact pinned Qt build rather than assuming an older binary works.

Stable v3 documentation specifies OpenGL for Qt Quick. Main's [build instructions](https://github.com/maplibre/maplibre-native-qt/blob/c3485f3a9590081c7be8edbc542341e688f6663d/docs/Building.md) add Linux/Windows/Android OpenGL or Vulkan, and Metal on Apple platforms; they also exclude Qt Location on WASM. Main's older Usage page still says OpenGL-only, so prefer version-pinned source/build instructions over mixed documentation.

The core wrappers carry BSD-2-Clause; Qt-derived Location/rendering files carry LGPL-3.0-only OR GPL-2.0-only OR GPL-3.0-only; examples are MIT. Treat the integration as mixed-license, with third-party native dependency notices as well. This is a source-license inventory, not a legal clearance. [License texts](https://github.com/maplibre/maplibre-native-qt/tree/v3.0.0/LICENSES), [Qt-derived file](https://github.com/maplibre/maplibre-native-qt/blob/v3.0.0/src/location/qgeomap.cpp).

## Minimal experiment and next evidence needed

[`experiments/maplibre/`](../../experiments/maplibre/) contains a Qt 6.12 loader/QML host, v3/v4 QML variants, a deterministic fixture generator, OneMap style JSON and isolated run scripts. It exercises the intended design: OneMap raster, one polygon source, data-driven fill colors, zoom-driven opacity/line width and a price filter. The same host accepts the official MapLibre demo vector style URL. The QML rendering remains unverified because the provider cannot load here.

If richer polygons become a requirement, first obtain/build one isolated, exact-Qt-6.12 plugin with reproducible licensing/deployment. Then run the same real HDB geometry and selection/filter workload on both paths, recording cold load, filter/source updates, pan/zoom frames, memory and visual correctness. Until that evidence exists, the stable M7 incremental approach remains the justified choice.


Published evidence: [provider baseline](maplibre-evidence/probe-baseline.log), [loader failure](maplibre-evidence/probe-stable.log), [artifact hash](maplibre-evidence/stable-artifact.sha256), [metadata](maplibre-evidence/stable-plugin-metadata.txt), [lint warnings](maplibre-evidence/qmllint-v3.log). Environment-specific paths in evidence are replaced with `<spike-root>` or `<toolchain-root>`. Only the small host/prototype/generator are included; downloaded binaries, upstream trees, generated polygons and build output are excluded. The prototype now uses the same locally bundled official attribution logo as the application.
