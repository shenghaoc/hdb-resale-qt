# Linux packaging (current architecture)

Status: implemented 2026-10-08. This describes the **current Worker API application**. The earlier 0.1.0 private
release candidate, built from a bundled local corpus, is historical evidence in
[docs/product-rc/](../product-rc/README.md) and [licensing-packaging.md](../product-rc/licensing-packaging.md); it is not
rewritten here and its verification does not apply to these packages.

## What is packaged, and what is not

Technically packaged: `tar.gz`, `.deb` and `.rpm` for Linux x86_64/amd64, built from one staged tree
(`tools/package/stage_linux.py`) that holds the application, the verified Qt 6.12.0 runtime (libraries, plugins, QML
modules, ICU 73), the .NET 10.0.12 runtime, licences, a launcher and a hashed manifest. Installed layout:

```
/opt/hdb-resale-explorer/            the staged tree
/usr/bin/hdb-resale-explorer         symlink to the launcher
/usr/share/applications/io.github.shenghaoc.hdb-resale-qt.desktop
/usr/share/icons/hicolor/scalable/apps/io.github.shenghaoc.hdb-resale-qt.svg
/usr/share/metainfo/io.github.shenghaoc.hdb-resale-qt.metainfo.xml
/usr/share/doc/hdb-resale-explorer/copyright   (DEB only; the RPM keeps its notices in /opt/.../licenses)
```

Not packaged: any resale data (the app reads the Worker API through `HDB_API_BASE_URL`, production by default),
research corpora, OneMap tile caches, credentials, source or debug tooling.

**Publicly distributed: nothing.** CI builds and tests the packages and keeps only reports. No tag, release, repository,
signature or upload exists, and `distribution_cleared` is `false` in every manifest.

## Ownership

- .NET/MSBuild/NuGet build the application; there is no top-level CMake build or CPack.
- Qt Bridge 0.4.0-beta and Qt deploy the native host's own Qt closure and plugins; staging uses those files first.
- HDB staging adds what that release does not and applies policy: see
  [qt-bridge-deployment-audit.md](qt-bridge-deployment-audit.md) for exactly which parts are custom and when each can go.
- DEB and RPM wrap the verified tree and declare system libraries; they do not discover Qt or .NET.

## System dependencies

Declared per family in `packaging/system-dependencies.json` (Debian 13 and Fedora 43 names). The package depends on
glibc and libstdc++ floors computed from the binaries, X11/XCB and xkbcommon, OpenGL/EGL vendor-neutral libraries,
Wayland client libraries, fontconfig/freetype, D-Bus, GLib, OpenSSL 3, Kerberos (for .NET), zlib, zstd, brotli,
certificates, a font, keyboard data and the hicolor icon theme. It recommends the distribution's Mesa DRI driver. It
deliberately does not depend on Qt or .NET packages: it ships its own.

## Build and verify locally

See [packaging/README.md](../../packaging/README.md). Verification in a clean container:

```sh
docker run --rm -v "$PWD/dist:/pkg:ro" -v "$PWD:/work:ro" -v "$PWD/report:/out" debian:13 \
  sh /work/tools/package/verify_installed.sh deb /pkg/hdb-resale-explorer_0.1.0-1_amd64.deb /work /out
```

The script installs only the package, audits every ELF, then adds Python/Xvfb/strace, checks each dependency mapping
with dpkg/rpm, launches `/usr/bin/hdb-resale-explorer` as an unprivileged user on X11 against the recorded API, runs
an unreachable-API negative control, uninstalls and confirms nothing is left.

## CI

`.github/workflows/linux-package.yml` (separate from the Qt-free `domain.yml`) runs for changes to app/native source,
QML and resources, project and Bridge pins, packaging, the recorded fixtures, files copied into the package and the
workflow itself; not for documentation-only changes. Steps: C# and Python tests, native gates against the build
tree (ctest plus four API smoke modes on X11), Release build, staging, tar/DEB/RPM, tamper negative control, relocated
tar smoke, DEB on Debian 13, RPM on Fedora 43. It uploads `linux-package-verification` (manifests, checksums, package
metadata, logs, os-release and image digests) for 14 days and **never the package binaries**.

## What is actually tested

| Format | Environment | Evidence |
| --- | --- | --- |
| DEB | `debian:13` container on GitHub's ubuntu-24.04 runner | installed with `apt`, launched on Xvfb (X11) through `/usr/bin/hdb-resale-explorer` |
| RPM | `fedora:43` container on the same runner | installed with `dnf`, launched the same way |
| tar.gz | the ubuntu-24.04 build host (relocated, spaces in path) | launched on Xvfb; has the build host's libraries installed, so it proves relocation, not dependencies |

The exact image digests are in each run's report. Nothing else is claimed: not other Debian/Ubuntu/Fedora releases, not
RHEL-family rebuilds, not Wayland (staged but never launched under a compositor), not hardware GL, not arm64, and not
tile pixels or tile network success.

## Remaining blockers before any distribution

Corresponding-source delivery for the exact application, Qt and Bridge revisions; relinking/rebuild instructions
including the compiled Bridge headers; third-party notice review; package signing and a signing-key policy; a
publication channel and its terms; and a decision on the OneMap logo terms. See
[licensing-packaging.md](../product-rc/licensing-packaging.md). Passing package CI changes none of these.
