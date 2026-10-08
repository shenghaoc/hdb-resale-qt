# Requirements: Linux packaging, package CI and dependency automation

> Written after the implementation (implementation-first, spec-after). Each requirement states what the
> shipped design guarantees and where it is enforced. Known gaps are in `design.md` under "Discrepancies".

## R1 — Build authority and deployment ownership

- **R1.1** .NET/MSBuild/NuGet remain the application build authority. No top-level CMake application build and
  no CPack are introduced.
- **R1.2** Qt Bridge and Qt's deploy script own native Qt deployment to the extent the **pinned published**
  Bridge (Linux `0.4.0-beta`) supports it. The staging step takes those files from the Bridge deployment first and
  fails if they differ from the pinned Qt prefix. (`stage_linux.locate`)
- **R1.3** HDB tooling owns only what the pinned Bridge does not: QML module deployment, the OSM geoservice and
  Wayland platform plugin, plugin policy, relocation, the .NET runtime, licences, auditing and verification.
  `docs/packaging/qt-bridge-deployment-audit.md` records the comparison with upstream `dev`.
- **R1.4** An unreleased Bridge is never consumed. Qt Bridge is not upgraded by this work.

## R2 — One staged runtime, thin package formats

- **R2.1** `stage_linux.py` produces exactly one staged tree with a `manifest.json` (hashes, modes, symlinks).
- **R2.2** tar.gz, DEB and RPM are wrappers around that tree. They re-verify the manifest and re-audit the content
  before wrapping and never resolve Qt libraries, QML modules or .NET themselves. (`build_packages.build`)
- **R2.3** The version comes from the project's `<Version>` only; `AssemblyVersion`/`FileVersion` must agree.
- **R2.4** Artifact names follow convention: `hdb-resale-explorer_<v>-1_amd64.deb`,
  `hdb-resale-explorer-<v>-1.x86_64.rpm`, `hdb-resale-explorer-<v>-linux-x64.tar.gz`.

## R3 — Runtime contents (API era)

- **R3.1** The package bundles the verified Qt 6.12.0 runtime and the .NET 10.0.12 runtime, with Qt's ICU 73 as
  app-local ICU. It never depends on host Qt or host .NET.
- **R3.2** No resale corpus, research data, tile cache, credentials, debug tooling or source ships; the manifest
  records `bundled_resale_data: false` and the content audit rejects violations. (`package_common.audit_tree`)
- **R3.3** The application reads the Worker API (`HDB_API_BASE_URL`). Offline/coverage tooling is not packaged.
- **R3.4** Every packaged ELF resolves inside the package or to a named system library; absolute search paths and
  Qt/ICU escapes fail staging. One optional gap (LTTng tracing) is accepted and recorded.
- **R3.5** Licences, SBOMs and `DISTRIBUTION-BLOCKERS.txt` are packaged; `distribution_cleared` is always `false`.

## R4 — DEB and RPM

- **R4.1** x86_64/amd64 packages install the tree under `/opt/hdb-resale-explorer`, a launcher at
  `/usr/bin/hdb-resale-explorer`, a desktop entry, an SVG icon, AppStream metadata and a copyright notice, under
  the id `io.github.shenghaoc.hdb-resale-qt`.
- **R4.2** System dependencies are declared per family from `packaging/system-dependencies.json`, with glibc and
  libstdc++ floors computed from the binaries. An unmapped library fails construction. Bundled Qt/.NET sonames never
  become dependencies (RPM dependency generation is off).
- **R4.3** Uninstall removes every packaged file and directory.

## R5 — Installed-package verification

- **R5.1** Each package is installed in a clean container of its family (Debian 13 for DEB, Fedora 43 for RPM) with
  only the package manager supplying dependencies; test tooling is added afterwards.
- **R5.2** Every dependency mapping is checked against the package manager's ownership of the resolved library.
- **R5.3** The installed application is launched through `/usr/bin` as an unprivileged user from an unrelated
  directory with fresh HOME/XDG and all inherited overrides cleared except `HDB_API_BASE_URL`, on real X11, against
  the recorded Worker API (`tools/api_fixture_server.py`). Shell, data, map and chart markers and a clean exit are required.
- **R5.4** The check proves Qt, ICU, .NET and plugins load from the package, that no build-tree, system-Qt or host-.NET
  file is opened (strace), and that the installed tree still equals the manifest.
- **R5.5** Negative controls fail as they should: an unreachable API, a tampered staged tree, an unmapped library.
- **R5.6** The production Worker is never required.

## R6 — CI structure

- **R6.1** `domain.yml` stays Qt-free and fast. The package/native workflow `linux-package.yml` is separate and is
  triggered only by inputs that can change the package (app/native source, QML, resources, project and Bridge pins,
  packaging, recorded fixtures, files copied into the package, the workflow).
- **R6.2** It builds Release from a clean checkout, runs the existing C#/Python tests and native gates first,
  then stages, packages and verifies.
- **R6.3** Package binaries are not uploaded; only reports, manifests and checksums. No tag, release, signing or publication.

## R7 — Dependency automation

- **R7.1** Dependabot covers NuGet and GitHub Actions, weekly and grouped.
- **R7.2** Qt Bridge packages are ignored; Linux and macOS pins stay deliberately different. Qt, aqtinstall, cmake,
  ninja and the .NET SDK/runtime are documented manual pins; no ecosystem is added that would only desynchronise them.
- **R7.3** Actions stay pinned to commit SHAs.

## R8 — Honest status

- **R8.1** Package CI is not distribution clearance. Docs distinguish historical M11 private-corpus evidence, the
  current Worker API app, what is packaged and what is distributed, which distributions were tested, and the remaining
  licensing/source/signing/release blockers.
