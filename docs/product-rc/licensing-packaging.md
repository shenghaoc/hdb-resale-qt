# HDB Resale Explorer 0.1.0: licensing and local RC packaging

Research date: 2026-10-03. This is an engineering inventory and release checklist,
not a legal opinion or a declaration that redistribution is cleared.

## Release decisions

- At the start of M11 the repository had **no application LICENSE**. The owner
  must choose the terms for their original application code before a public
  release. A third-party LGPL notice does not license the application. An
  eventual application licence must exclude third-party data, logos and code
  that retain their own terms.
- **Qt Graphs is deferred.** The official Qt 6.12 documentation offers commercial
  or GPLv3 licensing, not LGPL. The official archive and aqt list both contain
  `qtgraphs` for Linux `6.12.0 linux_gcc_64`; availability is not the obstacle.
  Introducing it would require a deliberate compatible application/distribution
  licensing decision or a suitable commercial licence. No Graphs, Charts or
  Quick 3D module is added to this RC. Qt Charts is not an assumed permissive
  alternative. [Qt Graphs](https://doc.qt.io/qt-6/qtgraphs-index.html),
  [Qt licensing](https://doc.qt.io/qt-6/licensing.html),
  [official 6.12 archive](https://download.qt.io/online/qtsdkrepository/linux_x64/desktop/qt6_6120/qt6_6120/).
- A private local RC package may be used for technical validation. It is not a
  signed installer, published release or distribution clearance. Completing the
  notices/source/relinking checklist below remains necessary before shipping.

## Actual dependencies and evidence

The production QML imports QtQuick, QtQuick.Controls, QtQuick.Layouts,
QtLocation and QtPositioning. Their dependencies include Qt Core, Gui, Network,
Qml, QmlModels, WorkerScript, Templates, Controls style plugins, OpenGL, DBus,
XcbQpa and Svg. The package script records the exact transitive library/QML
inventory, rather than treating this paragraph as a complete bill of materials.

The installed **6.12.0** SPDX JSON documents for qtbase, qtdeclarative,
qtlocation, qtpositioning and qtsvg explicitly include LGPL-3.0-only alternatives
for the used runtime libraries. This also avoids relying on a cached online
Positioning page that still reports 6.11.2. Official documentation agrees for
[Qt Quick](https://doc.qt.io/qt-6/qtquick-index.html),
[Controls](https://doc.qt.io/qt-6/qtquickcontrols-index.html) and
[Location](https://doc.qt.io/qt-6/qtlocation-index.html).

Exact NuGet package evidence (LICENSE.txt and .nuspec read from package bytes):

| Platform | Package pin | Package-declared source commit | Licence statement |
| --- | --- | --- | --- |
| Linux x64 | QtGroup.Qt.Bridge.CSharp.linux-x64 0.4.0-beta | 119b01f61536f95944c2e62676d69aa1e05e0006 | LGPL-3.0-only or Qt commercial |
| macOS arm64 | QtGroup.Qt.Bridge.CSharp.osx-arm64 0.4.0.22-beta | 3b22930bbdf88b4bc8fa2aa5e747e3487f0b683a | LGPL-3.0-only or Qt commercial |

These are different source commits, not interchangeable version labels.
Sources: [Linux package](https://www.nuget.org/packages/QtGroup.Qt.Bridge.CSharp.linux-x64/0.4.0-beta),
[macOS package](https://www.nuget.org/packages/QtGroup.Qt.Bridge.CSharp.osx-arm64/0.4.0.22-beta),
[Qt Bridge source repository](https://code.qt.io/cgit/qt/qtbridge-csharp.git/).
The Linux package's qdotnethost.h also has an LGPL-3.0-only/commercial SPDX
header. Some Bridge code is compiled into the generated host, so treating
every Bridge component as a replaceable DLL would be incomplete.

The build uses .NET SDK 10.0.401; the bundled runtime-only subset is
Microsoft.NETCore.App 10.0.12. Its MIT licence and ThirdPartyNotices.txt are
copied from the installed Microsoft runtime distribution. The SDK, NuGet
cache, build tools and private data are not shipped.

### Notices, source and replacement checklist

Before distributing under the open-source alternatives:

1. Include prominent Qt/Bridge notices, full LGPLv3 and GPLv3 texts, relevant
   copyright/third-party texts and the application's chosen licence.
2. Arrange complete corresponding source for the **exact shipped** Qt and
   Bridge revisions, including changes, with the appropriate delivery or valid
   offer mechanism. A generic upstream URL or SBOM alone is not that mechanism.
3. Preserve recipients' ability to replace/relink the LGPL portions and run
   the result. Provide build/relink instructions and materials for the compiled
   Bridge headers/generated native host as applicable; inspect the final
   licence obligations rather than assuming dynamic Qt alone resolves them.
4. Check terms accompanying any installer/store and avoid restrictions on
   these rights. Signing, source-offer commitments and commercial licence
   purchase are owner decisions, not automated by this recipe.

This checklist follows [Qt's LGPL obligations guidance](https://www.qt.io/development/open-source-lgpl-obligations)
and the [LGPLv3 text](https://www.gnu.org/licenses/lgpl-3.0.html). Obtain qualified
advice for the intended distribution if applicability is uncertain. The RC
includes selected licence texts and original Qt SBOM/source inventories, but
explicitly leaves complete source delivery and third-party notice review open.

## OneMap, data and assets

The existing 32×32 official OneMap logo is unchanged, used solely in map
attribution, and embedded as a Qt resource. Its origin/hash are recorded in
[asset provenance](../../src/HdbResale.App/assets/README.md). No tile cache,
raster tile archive, OneMap credential or bulk acquisition cache is packaged.

[OneMap's map documentation](https://www.onemap.gov.sg/docs/maps/) requires its
logo and attribution when using the basemap. Retain linked “OneMap ©
contributors | Singapore Land Authority” attribution and the existing data
source labels. Its [Terms of Use](https://www.onemap.gov.sg/legal/termsofuse.html)
reserve the relevant intellectual-property rights and restrict unauthorised
modification/use. This required attribution is not a general trademark licence
or permission to use the logo as the application's brand. Do not imply SLA
endorsement or assume that data licensing makes the logo MIT.

The small canonical fixture uses HDB/ACRA sources listed in
[data/provenance.json](../../data/provenance.json). Preserve dataset names,
source links, access dates and the
[Singapore Open Data Licence link](https://data.gov.sg/open-data-licence).
The licence requires source acknowledgement and does not grant rights in
personal data, third-party rights or trademarks. The package uses only the
checked-in fixture and provenance, not the 241,920-row local research corpus.
Basemap availability and network access remain external runtime conditions.

## What the Linux recipe does

The initial Bridge build output was not relocatable: the native executable
contained absolute build-prefix RUNPATHs, `qt.conf` was under a different
`bin/` directory, and QML/Location/geoservices plus .NET runtime discovery
depended on the developer's installation. `tools/package/linux_rc.py` stages
an explicit independent directory and tar.gz from a previously built Release:

- Native host and managed application/Bridge assemblies; canonical fixture
- Scanner-selected QML modules, including Controls styles and Layouts
- X11 xcb platform, GLX/EGL integration, PNG support in QtGui, image plugins,
  SVG icon/image support, OSM geoservice and OpenSSL/certificate TLS plugins
- Only required Qt-origin shared libraries, including Qt's own ICU 73.2
- Runtime-only .NET 10.0.12 with dotnet launcher, hostfxr and shared runtime
- Relative host RUNPATH and qt.conf, isolated launcher paths, file hashes,
  system dependency inventory, notices and an explicit distribution-blocker file

ICU 73.2 is already part of this official pinned Qt archive. It is copied only
inside the package to satisfy that Qt build. No obsolete ICU is installed
globally, no system package is upgraded, and no old MapLibre binary is used.
The application uses Qt Location's OSM plugin with OneMap raster tiles.

The runtime still needs the host's compatible glibc/libstdc++, X11/XCB,
xkbcommon, font/fontconfig/freetype, DBus/glib, OpenGL/EGL/graphics drivers,
OpenSSL 3 with CA certificates, compression and other transitive system
libraries. .NET needs its Linux native prerequisites. Exact ELF dependencies
are recorded per staged artifact; some dependencies (notably OpenSSL/TLS and
graphics drivers) load dynamically. No claim of universal Linux portability,
Wayland support or SSL/network success follows from an ELF inventory.
[Microsoft Debian prerequisites](https://learn.microsoft.com/en-us/dotnet/core/install/linux-debian).

The untouched .NET runtime also contains `libcoreclrtraceptprovider.so`, whose
optional LTTng tracing needs `liblttng-ust.so.0`, unavailable on this host. The
manifest records this exact optional diagnostic gap; every other unresolved
ELF dependency is fatal to staging. No old LTTng package is installed and no
runtime binary is modified to fake compatibility. Ordinary application launch
must still pass. Microsoft's [PerfCollect guide](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/trace-perfcollect-lttng)
describes the tracing prerequisites and ABI mismatch; EventPipe is a separate
diagnostic mechanism.

Example (build first using the documented pinned environment):

```sh
dotnet build -c Release -m:1
python3 tools/package/linux_rc.py \
  --qt "$QtDir" --dotnet "$DOTNET_ROOT" \
  --bridge "$NUGET_PACKAGES/qtgroup.qt.bridge.csharp.linux-x64/0.4.0-beta" \
  --bridge-source /absolute/path/to/official/qtbridge-csharp \
  --output /absolute/path/outside/repository/new-rc-directory
```

The output directory must not already exist. No cleanup/deletion is automatic.
The recipe is reproducible from pinned inputs; tar timestamps and the native
build are not claimed byte-for-byte reproducible. Build outputs must match the
current source before staging; the manifest states the source commit/dirty
status and hashes the exact shipped bytes.

Extract/copy the resulting package to a different location, then from a real
X11 desktop session run:

```sh
python3 tools/package/launch_check.py \
  --package /relocated/hdb-resale-explorer-0.1.0-linux-x64-private-rc \
  --log /outside/repository/package-launch.log \
  --report /outside/repository/package-launch.json
```

The 30-second bounded check verifies file hashes, starts from an unrelated
working directory with fresh HOME/XDG directories, removes inherited
Qt/.NET/loader/data overrides, preserves the legitimate X11 session, and
requires ordered shell/data/map-engine readiness and managed/native clean-exit
markers. `/proc` library observations must show Qt, ICU, .NET and the OSM
plugin inside the relocated package. Loader/QML errors, timeout, nonzero exit
or missing markers fail the check. **Map readiness is not tile-pixel evidence.**
Visual inspection of the packaged UI remains a separate check.

## rowplay-qt reference and platform limits

Read-only reference reviewed at commit
`046c2e30062f7ca38c725307ecc73c6ea62777d2`:
[rowplay-qt packaging](https://github.com/shenghaoc/rowplay-qt/tree/046c2e30062f7ca38c725307ecc73c6ea62777d2/tools/package).
Its scripts are GPL-3.0-or-later. This project's small packaging scripts were
written independently; they do not copy rowplay's implementation or adopt its
licence. Useful concepts are explicit staging, QML/plugin deployment, removal
of developer paths, bounded clean-environment launch checks and checksums.

- rowplay's Linux AppImage flow uses pinned/hash-verified linuxdeploy and its
  Qt plugin, explicit runtime plugins and an AppImage launch check.
- Its macOS flow assembles .app metadata/resources, runs macdeployqt, audits
  Mach-O paths, launch-checks, then creates a DMG. Existing HDB
  `StageMacDevelopmentBundle` only copies build output into .app; it does not
  demonstrate redistributable Qt/.NET frameworks or a tested DMG.
- Its Windows flow uses windeployqt, a staged launch check, Inno Setup and a
  portable ZIP. HDB has no equivalent tested Windows packaging flow here.

No AppImage, DMG, Windows installer, signing, notarisation, tag, release or push
is performed in this milestone. This Debian 13.6 x64 run does not certify Mac
or Windows packages.

## CI and validation boundary

The existing `.github/workflows/domain.yml` uses pinned checkout/setup-dotnet
actions, .NET SDK 10.0.401, C# domain tests and Python unit tests. It has no Qt
installation, native UI launch or packaging job. Keep this Qt-free lane
lightweight; new package-script unit tests run there without Qt. Local Release
build/native gates/package launch and graphical checks must be reported
separately. Inspecting this YAML or passing local equivalents does not mean
remote CI ran. A future native package CI matrix needs platform-specific
runtime setup, source/notices handling and evidence from each target OS.
