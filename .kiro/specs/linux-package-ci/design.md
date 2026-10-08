# Design: Linux packaging, package CI and dependency automation

> Status: Implemented. Written after the implementation was proven (implementation-first); it records the
> architecture as built, including where the build departs from the intended architecture.

## Architecture

```
dotnet/MSBuild build (Release)             .NET + MSBuild + NuGet: application build authority
        |
        v
Qt Bridge 0.4.0-beta build                 Bridge runs Qt's deploy script into bin/Release/net10.0
        |
        v
stage_linux.py                             HDB staging: one verified tree + manifest.json
  + QML modules (qmlimportscanner)           what the pinned Bridge does not deploy
  + OSM geoservice, Wayland platform plugin
  + plugin policy, relocation, app-local ICU
  + .NET 10.0.12 runtime, licences, launcher
  + system-libraries.txt, ldd/readelf audit, forbidden-content audit
        |
        v
verified staged tree (manifest: sha256, mode, symlink, supplier of every Qt file)
        |
        +-- tar.gz   build_packages.py (re-verifies manifest + audit first)
        +-- .deb     dpkg-deb, DEBIAN/control + md5sums
        +-- .rpm     rpmbuild, generated spec, AutoReqProv off, payload never rewritten
```

Ownership boundaries:

| Concern | Owner |
| --- | --- |
| Compiling and versioning the app | .csproj / MSBuild (`<Version>` is the only version) |
| Native host, Qt link closure and plugins it implies | Qt Bridge + Qt `qt_generate_deploy_app_script` (taken first by staging) |
| QML modules, extra plugins, policy, relocation, .NET, audit | `tools/package/stage_linux.py` |
| System-dependency declaration | `packaging/system-dependencies.json` (explicit per family) |
| Package container formats | `build_packages.py` (no dependency discovery) |
| Proof that the installed result works | `verify_installed.sh/.py`, `launch_check.py` |

## Key decisions

1. **Consume the Bridge deployment, do not replace it.** Verified by a real build: the pinned 0.4.0-beta already emits
   `install(TARGETS)` and `qt_generate_deploy_app_script` (the premise that only `dev` does is wrong). 17 of 45 Qt/ICU
   libraries and 15 of 17 allowed plugins come from it; the manifest names the supplier of each file.
2. **No unreleased Bridge, no upgrade.** Upstream `dev` differs in staging and app-host handling only and still
   deploys no QML imports, so nothing is gained for this task.
3. **`rpmbuild`/`dpkg-deb`, not CPack or a download.** The two native tools are installed from the distribution on the
   runner, need no pinned third-party binary, and let the packages be built and inspected locally. nfpm was evaluated
   and rejected: a Go binary fetched from GitHub releases that this environment could not reach, for a job two
   small generators do.
4. **System dependencies are data.** Soname -> package maps for Debian 13 and Fedora 43. CI asks the package manager
   who owns each resolved library, so a wrong row fails. Floors for glibc and libstdc++ come from `readelf -V`.
5. **Dependency audit is not masked.** In the container, the package is installed alone and every ELF is `ldd`-checked
   before Python, Xvfb or strace are installed.
6. **Package smoke uses the recorded Worker API.** `launch_check.py` serves `tests/fixtures/worker-api` itself.
   The launch is on real X11 (libqxcb must be mapped; `offscreen` is not accepted: the CI sandbox environment sets
   `QT_QPA_PLATFORM=offscreen`, which would otherwise have passed silently).
7. **Package binaries are ephemeral.** One job builds and verifies; DEB/RPM are mounted into containers from the
   runner workspace, so no binary artifact is uploaded while the distribution blocker is open.
8. **Dependabot preserves pins.** Bridge packages ignored; Qt/aqt/cmake/ninja/.NET documented as manual.

## Verified behaviour worth recording

- Under `strace`, the installed application opens no `/opt/Qt`, system Qt or host .NET path.
- A host `libicuuc` (74) is mapped when a host library such as libxml2 links it. That is tolerated only when a mapped
  host library names it; Qt and .NET use the package's ICU 73. The earlier M11 check rejected any host ICU; this one is
  narrower because Debian/Ubuntu's libxml2 links ICU.
- The glibc floor is 2.34 and the libstdc++ floor is GLIBCXX_3.4.29 (GCC 11.1), computed from the shipped binaries.

## Discrepancies from the intended architecture

1. **QML deployment is custom (published Bridge limitation).** The intended split gives Qt's deployment machinery
   all Qt discovery. Neither 0.4.0-beta nor current `dev` calls `qt_deploy_qml_imports`, so staging runs
   `qmlimportscanner` itself (28 of 45 libraries, 30 QML modules). Removable when a *published* Bridge deploys QML imports.
2. **Host RUNPATH is absolute in the Bridge output (published Bridge limitation).** Staging rewrites it with
   `cmake -P file(RPATH_CHANGE)`. Removable when the generated CMake installs with a relative RPATH.
3. **The Bridge deploys surplus plugins (published Bridge limitation).** EGLFS/KMS, evdev, QML debugging, GTK themes.
   Staging applies an allowlist; the excluded files are listed as `bridge_deployed_not_shipped`.
4. **Wayland is staged, not tested (CI constraint).** The Wayland platform and shell plugins are packaged and
   audited, as the native-acceptance checklist requires, but no CI job runs a Wayland compositor. Only X11 is launched.
   Qt falls back to xcb. KDE Plasma Wayland acceptance stays manual.
5. **The window is not associated with the desktop entry on Wayland (our implementation).** The app does not call
   `setDesktopFileName`; the entry sets `StartupWMClass=HdbResale.App` for X11 only. Fixing it is a code change in the
   UI layer, out of scope while UI work is paused.
6. **RPM verified with the distribution's own rpm only in CI.** `rpmbuild` runs on an Ubuntu runner (rpm 4.18) and the
   result is installed on Fedora 43. Package-system difference, mitigated by the Fedora install test.
7. **Tile requests reach OneMap during the smoke (package-system/CI).** The map requests tiles; the recorded-API
   rule covers the Worker API only. Results are not asserted (`network_success_verified: false`).
8. **The `.NET` runtime is copied from the SDK-provided shared runtime** and its version is pinned (10.0.12) separately
   from `global.json`; they must move together by hand.

No discrepancy was left unrecorded to keep this PR coherent; none required a follow-up code change before review.
Candidate follow-ups are listed in `tasks.md`.
