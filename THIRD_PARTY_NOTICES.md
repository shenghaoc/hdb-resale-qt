# Licensing and third-party notices

## Original application

Copyright (C) 2026 HDB Resale Explorer contributors.

The original application code is free software: you may redistribute it and/or
modify it under the GNU General Public License as published by the Free
Software Foundation, either version 3 of the License, or (at your option) any
later version. It is distributed without any warranty, including implied
warranties of merchantability or fitness for a particular purpose. See
[LICENSE](LICENSE) for the complete GNU GPL version 3 text.

SPDX licence expression: **GPL-3.0-or-later**. [REUSE.toml](REUSE.toml) records
scoped annotations for the original application, tests and build/tool code.
This does not relicense third-party components, datasets, provenance records,
logos, tiles or imported experimental source. Existing upstream notices stay
in force. Application documentation distinguishes verified behaviour from
distribution readiness; no official release is made by adding this licence.

## Qt and Qt Bridge

Qt is copyright The Qt Company Ltd. and other contributors. The actual used
Qt 6.12 libraries and QML/plugin closure are recorded in each private package's
manifest and supplied Qt SPDX inventories.

- Qt Core/Gui/Qml/Quick/Controls/Layouts/Location/Positioning and related
  runtime components offer LGPLv3 alternatives (and other stated choices).
- Qt Graphs and Qt Quick 3D have **GPL-3.0-only** open-source licence choices;
  they are not converted to GPL-3.0-or-later by the application's licence.
  The selected 2D chart uses the official Graphs build, which also links
  Quick 3D libraries. Follow the exact licence for every component.
- Linux Qt Bridge for C# 0.4.0-beta and macOS arm64 0.4.0.22-beta both state
  LGPL-3.0-only or commercial licensing. The package-declared source revisions
  differ and are recorded in the detailed report. Parts of Bridge are compiled
  into the native host, not all replaceable managed libraries.
- Qt's bundled third-party code, including ICU, retains its own notices and
  terms. An inventory is not a substitute for full notices or source provision.

See [Qt licensing](https://doc.qt.io/qt-6/licensing.html),
[Qt Graphs](https://doc.qt.io/qt-6/qtgraphs-index.html),
[Qt Quick 3D](https://doc.qt.io/qt-6/qtquick3d-index.html), and
[the detailed packaging/licensing report](docs/product-rc/licensing-packaging.md).
Commercial licensing is a separate option; this project does not assert that
a commercial Qt licence was acquired.

## .NET

Microsoft .NET runtime: copyright .NET Foundation and contributors, MIT
licence, with separate third-party notices. The runtime's original LICENSE.txt
and ThirdPartyNotices.txt are included when that runtime is staged. The SDK
and NuGet caches are not bundled in the private RC.

## Data and map attribution

The checked-in fixture and derived evidence come from HDB/ACRA datasets via
data.gov.sg. Dataset identities, source URLs, dates and hashes are recorded in
[data/provenance.json](data/provenance.json) and [data/README.md](data/README.md).
They retain the [Singapore Open Data Licence](https://data.gov.sg/open-data-licence)
and applicable exclusions, not the application's GPL. Attribution must remain
conspicuous. Neither completeness nor current availability is guaranteed.

At runtime the app reads the HDB Resale Explorer API, which serves the same
HDB resale data, with block locations and MRT distances it derived from OneMap.
The recorded responses in `tests/fixtures/worker-api` are that output and keep
the same terms ([fixture notes](tests/fixtures/README.md)).
`tests/fixtures/web-parity/product-core-golden.json` is copied from
[hdb-resale-visualizer](https://github.com/shenghaoc/hdb-resale-visualizer)
under its MIT License, Copyright (c) 2026 Shenghao Chen.

OneMap basemap: **OneMap © contributors | Singapore Land Authority**.
The unchanged official OneMap attribution logo retains SLA/other applicable
rights. Its origin and hash are in
[asset provenance](src/HdbResale.App/assets/README.md). It is included only for
the required map attribution, not as the application icon or an endorsement.
See [OneMap map documentation](https://www.onemap.gov.sg/docs/maps/) and
[Terms of Use](https://www.onemap.gov.sg/legal/termsofuse.html). No tile cache,
OneMap credentials, acquisition cache or private full corpus is packaged.

## Experimental code and packaging reference

The `experiments/` subtree is outside the original-code REUSE annotation.
Imported MapLibre/upstream source, patches and assets retain their own notices
and provenance; none becomes GPL merely because of this application licence.
MapLibre is not a production renderer in this RC.

rowplay-qt's GPL-3.0-or-later packaging scripts were read as a design reference;
no script was copied. The small HDB staging/check scripts were independently
written and are covered by this application's GPL-3.0-or-later choice.

## Distribution remains a separate gate

Before distributing binaries, prepare the exact corresponding source and
required build/installation/relinking materials, full component notices and a
licence-compatible delivery mechanism. A licence file, an upstream URL or an
SBOM alone does not complete that work. Private RC artifacts explicitly state
`distribution_cleared: false`. This notice is an engineering inventory, not
legal advice or a compliance certification.
