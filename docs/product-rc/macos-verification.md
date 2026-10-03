# Current M11 macOS checkpoint: publication held

Verified M11 base: `a8fba1b9e9fcbc8fe906de67807cae2048d49640`, tree
`8e6e8c3b2199640fc7f35657e9a12e1cf2d64c99`. The complete Library recovery bundle
was verified at 3,932,509 bytes and SHA-256
`55f5a0e5f2c84c15e7277001b07345967572b1b2a1860393397f30f1f8512b67`.
The separate macOS portability fix is `10ed7ba537bce74a523943049a07d8df9dbf232e`:
resolve both sides of the package-test path comparison because macOS resolves
`/var` to `/private/var`. Production behavior is unchanged.

## Completed current checks

macOS 27.0.1 (26A434), arm64, Apple M5, Qt RHI Metal. Pinned .NET SDK
10.0.401/runtime 10.0.12, Qt 6.12.0, macOS Bridge 0.4.0.22-beta.
Official matching Graphs, Quick3D and Timeline were installed with documented
aqtinstall 3.3.0. No toolchain version was upgraded.

Both root Debug and Release builds pass with zero warnings/errors. Initial
NU1900 vulnerability-feed timeouts were investigated; the feed subsequently
returned HTTP 200, normal restore passed, and both builds passed without
suppressing audit. Each configuration passes 191 C# tests (0 failed, 0 skipped).
All 40 Python tests pass.

The available native Cocoa matrix passes 12 positive runs: canonical and
strict original 10,000-row classic/extended/reentrant/presentation/buyer
scenarios in both configurations. All 22 corresponding deliberate-failure
runs emit the expected assertion and clean managed teardown. The highest
recorded completed positive state is 391 ms. The ordinary 5-second state
budgets and expanded-only 10-second budgets were not changed.
These native assertions do not certify visible tile pixels or physical input.

Current Release reproduces the frozen 416-row report byte-for-byte, SHA-256
`b4041707f37acfe189f473e163bc2fff28890d42e5bd8389a16dabcc7203d821`, and the
frozen M5 report SHA-256
`d76614762c351eb121d3dd11ed03658e53daae1a5feeae36809a029b7e62d4be`.
Original full-corpus transaction/property/footprint/postal files match every
committed M6 derived hash. Canonical and protected evidence files are unchanged.

The all-ref reachable-history audit with exact M10 base and
`--m11-buyer-contract` passes with no issues. Excluded `843df64` remains
unreachable. The exact excluded private reference was scanned locally without
transferring or printing raw data. Its unchanged scanner flagged nine additional
blobs: five CSV blobs were individually traced to public street fields in pinned
official source rows 110/119; four image blobs contained only a two-byte token
in compressed JPEG scan/PNG IDAT payload, absent metadata. Raw flags and the
per-blob provenance/payload review remain local. No blanket exception or scanner
relaxation was added, and the original scanner's flagged result is preserved.

## Remaining independent publication blockers

The expanded normalized input directory is absent. All 27 pinned ACRA raw
partitions are missing: A–Z and Others. The documented public acquisition API
returned HTTP 403 with body `error code: 1010`; no client-signature change or
alternate-environment bypass was attempted. A legitimate user Download CSV
flow on each official page can recover snapshots; verify the exact SHA-256 and
size from [source provenance](../address-coverage/source-provenance.json)
before running the documented generation commands. Do not substitute current
monthly data or regenerate from a private cache.

[Expanded generation instructions](../address-coverage/normalization/README.md#reproduction)
use existing pinned full-corpus inputs, all 27 source CSVs, the normalization
source-projection manifest and the already approved minimized historical
benchmark. Required output includes transactions.csv, address-evidence.csv,
building-evidence.geojson, normalized postal-address-evidence.csv,
address-normalization.txt, historical-postal-evidence.json and manifest.json.
Compare every derived hash against the committed expanded manifest.

Ten expanded positive and twenty expanded negative runs remain unperformed.
The expanded native old-field digest
`88118bdb3a9a68f59ea47cddae27568623f136cb7b7d5320a376f8f752dc6cc5` and v2 digest
`46ac65f199f916b78f56b388a19fa3a510617a6c0917c5d4787ef4e6cc856393`
remain to be reproduced on this Mac.

Physical current Release acceptance remains pending. The resumed executor has
no computer-use/desktop screenshot/control capability in its current inventory.
Native gates are not a replacement for visual acceptance, and no human hardware
wheel test is claimed. Launch the current development build with:

```sh
export PATH="$HOME/.local/share/hdb-qt-toolchain/bin:$PATH"
export QtDir="$HOME/Qt/6.12.0/macos"
HDB_DATA_DIRECTORY=/absolute/verified-expanded-data \
  dotnet run --project src/HdbResale.App -c Release --no-build
```

Complete and record this actual app checklist:

- OneMap tile pixels, logo/attribution; grid groups, drilldown, individual pins.
- Town/type/minimum/maximum and latest 12/24 source-month filters; empty/reset.
- Address list/details, latest 15 rows, medians/per-m², source/derived lease.
- Actual Qt Graphs gaps and single observed point; map/list/detail synchronization.
- Offscreen selection return, filter-hidden selection, rapid FIFO transitions.
- Pan, zoom buttons, recenter, keyboard result navigation and focused highlight.
- About 0.1.0, GPL-3.0-or-later/nonaffiliation; clean exit.
- Hardware wheel only when an actual physical device/operator is available.

## Private packaging assessment

Existing StageMacDevelopmentBundle copies build output and retains an absolute
Qt development rpath; it has no bundled Qt/.NET closure. It is development
staging, not a redistributable package.

A separate private local stage copied matching Qt frameworks (including
Graphs/Graphs2DImpl, Quick3D and Location), QML modules, Cocoa/geoservices plugins,
managed Bridge assemblies and runtime-only .NET 10.0.12. macdeployqt changed
native rpaths to relative Frameworks paths but reported non-object files under
MacOS and automatic ad-hoc bundle-signature verification failures caused by
managed/data files in that executable directory. A relocated launch in a path
with spaces, fresh HOME, cleared developer environment and bundled DOTNET_ROOT
exited -9 before readiness or managed teardown; no successful isolated runtime
mapping evidence exists. No identity signing or notarization readiness is claimed.

Remaining package work includes a proper code/resources bundle layout, complete
Qt/QML/plugin configuration, correct .NET host discovery, Bridge manifest/metadata
closure, valid local load signatures, and a successful fresh relocated launch
with actual runtime mapping evidence. This assessment does not introduce a DMG,
installer or official artifact.

Original code is GPL-3.0-or-later; Qt Graphs/Quick3D retain their own GPL-only
terms. Public binary distribution still needs complete corresponding source,
notices, component licence/relinking and applicable installation-information
work from the [licensing checklist](licensing-packaging.md). Source publication
and binary distribution are separate decisions.

Local main and origin/main remained
`2a4a1e99429e1d9b83b3b827443df971c77a2409`. No push, tag, GitHub Release or official
asset upload occurred. Remote CI for the current unpushed descendant has not run.
Recommendation: hold publication until the remaining data and physical checks pass.
