# Qt Bridge deployment audit

Date: 2026-10-08. Scope: how much of Linux deployment the Qt Bridge for C# already does, so the
HDB-specific staging in `tools/package/stage_linux.py` stays no larger than it must be.

## Sources compared

| | Version | Evidence |
| --- | --- | --- |
| Pinned (this repository, Linux x64) | `QtGroup.Qt.Bridge.CSharp.linux-x64` **0.4.0-beta**, package-declared commit `119b01f6…` | `build/net8.0/Qt.Bridge.targets` and the compiled generator in the NuGet package, read from package bytes; and the output of a real Release build of this application |
| Upstream `dev` | `qtbridge-csharp` `dev` at `6011fb48a38c` (2026-10-07), **unreleased** | `build/Qt.Bridge.targets`, `GenerateBuildSpec.cs`, `examples/`, `CHANGELOG.md` |

This pull request does **not** upgrade or consume the upstream `dev` Bridge. Everything below about
`dev` is a reading of its source, kept so the later simplification is already specified.

## Verified upstream observations

- Examples are ordinary .NET projects: `dotnet run --project Primes/Primes.csproj -c Release`. CMake and Ninja
  are implementation details of the Bridge build, not something the application authors.
- The generated native CMake contains `install(TARGETS …)` followed by `qt_generate_deploy_app_script(… NO_UNSUPPORTED_PLATFORM_ERROR)`
  and `install(SCRIPT …)`. **This is also true of the pinned 0.4.0-beta**: both strings are in its compiled generator.
  The premise that only `dev` uses Qt's deploy script is wrong.
- Linux/macOS deployment runs `cmake --install` after the native build. Windows runs `windeployqt`.
- `dev` differs from 0.4.0-beta in two Linux-relevant ways: it installs into a scratch directory, deletes the
  duplicate native host from it and copies the rest into the output (0.4.0-beta installs straight into the output,
  leaving `hdb_resale_app` beside `HdbResale.App`); and it adds `QtBridgePrepareAppHost`/`QtBridgePublishResourcePack`
  for the metadata-export mode and `dotnet publish`. We use the source-generation mode and do not publish with the SDK.
- **Neither version deploys QML imports.** Neither passes `QML_DIR`/`qt_deploy_qml_imports`, and `QtBridgeDeployQml`
  in both copies only the application's own `.qml` files. `dev` did not change this.

## 1. What the pinned Bridge already deploys correctly

Observed in `src/HdbResale.App/bin/Release/net10.0/` after a clean build with Qt 6.12.0:

- The Qt and ICU libraries the native host links (17 files here: Core, DBus, Gui, Network, OpenGL, Qml, QmlMeta,
  QmlModels, QmlWorkerScript, Quick, Quick3DUtils, Svg, WaylandClient, XcbQpa, ICU 73) with correct transitive closure.
- Qt plugins Qt's deploy script associates with those libraries: the `xcb` platform plugin, GLX/EGL integrations, TLS,
  image formats, SVG icon engine, input contexts, and (surplus to us) EGLFS/KMS, evdev, network-information,
  platform themes, QML debugging.
- The application's own `.qml` files and `qml` resources, `qt_bridge_metadata.json`, and the managed assemblies.
- `bin/qt.conf` with `Prefix = ..`. The host's `RUNPATH` stays the absolute `QtDir/lib`, so the tree is **not relocatable as is**.

The staging script now takes these from the Bridge output first (and fails if a byte differs from the pinned Qt
prefix). `manifest.json` records the supplier of each file: `qt_library_suppliers`, `qt_plugin_suppliers`.

## 2. What only `dev` provides

- A scratch-directory install that removes the duplicate host before copying (cosmetic for us; staging picks the
  host by name).
- Metadata-export mode support for app host handling and `dotnet publish` integration.

Nothing in `dev` replaces a step this script performs. Re-audit when a Bridge beyond 0.4.0-beta is **published**.

## 3. What HDB-specific staging still has to do

- Deploy **QML modules** chosen by Qt's own `qmlimportscanner` (30 modules: Controls and its styles, Layouts, Graphs,
  Location, Positioning, Quick3D, …) and the Qt libraries those modules need (28 more files here: Graphs, Location,
  Positioning, QuickControls2*, Quick3D, ShaderTools, …). Neither Bridge version does this.
- Add plugins the Bridge deploy omits but the application needs: `geoservices/libqtgeoservices_osm.so` (the map),
  `platforms/libqwayland.so` (the Wayland platform plugin; the Bridge ships `libQt6WaylandClient` and the Wayland
  shell plugins but not the platform plugin that loads them).
- Apply **policy**: ship an allowlist of plugins and drop EGLFS/KMS, evdev, platform themes and QML-debugging plugins and
  their libraries, so the system dependency list stays what a desktop app needs.
- Make the tree **relocatable**: rewrite the host `RUNPATH` to `$ORIGIN/../qt/lib`, write `app/qt.conf`.
- Bundle the .NET runtime, and configure app-local ICU 73 so .NET and Qt use one ICU.
- Add the licences, SBOMs and distribution-blocker file; write `system-libraries.txt` and the hashed `manifest.json`.
- Audit: exact `ldd`/`readelf` of every packaged ELF, no absolute search paths, no Qt/ICU escaping the tree,
  glibc/libstdc++ floors, forbidden-content scan (no local corpus, tiles, credentials, debug tooling).

## 4. Redundant Python that was removed or reduced

- The bundled **data corpus** (`DATA_FILES`) is gone: the application reads the Worker API.
- The per-file copy of Qt libraries from the Qt prefix is reduced to a resolver that takes the Bridge's file when
  present: 17 of 45 libraries and 15 of 17 allowed plugins now come from the Bridge deployment.
- The `--bridge-source` argument (an upstream checkout used only for licence texts) is gone; the three SPDX texts are in `LICENSES/`.
- `tar` creation moved out of staging; tar, DEB and RPM are now wrappers (`build_packages.py`).

## 5. What cannot be removed safely yet

- The QML scanner and module copy: no released Bridge deploys QML imports. *Removable when* a published Bridge's
  generated CMake calls `qt_deploy_qml_imports` (or equivalent) so `qml_modules`/`qt_library_suppliers` entries with
  supplier `qt` stop appearing.
- The extra plugins and the plugin policy: the allowlist is an HDB decision whichever tool copies the files.
- `RUNPATH` rewrite: the generated host links with an absolute Qt prefix. *Removable when* the generated CMake sets a
  relative install RPATH.
- The .NET runtime bundle, app-local ICU and the dependency audit: these are about this product's compatibility choices
  (verified Qt build, no host .NET), not Bridge gaps.
- The pinned 0.4.0-beta itself: consuming `dev` for convenience is out of scope and would need a published package.
