# Verification — 2026-10-03

Executed on macOS 27.0.1 arm64 with the exact toolchain in README.
Target checkout was initially an empty directory with no Git repository or
repository-specific instructions. The existing web repository was read only as
a product reference and was not edited.

## Build and domain tests

```sh
export PATH="$HOME/.local/share/hdb-qt-toolchain/bin:$PATH"
export QtDir="$HOME/Qt/6.12.0/macos"
dotnet build
dotnet test tests/HdbResale.Tests/HdbResale.Tests.csproj --no-build \
  --logger 'console;verbosity=normal'
dotnet run --project src/HdbResale.App/HdbResale.App.csproj --no-build
```

- Build passed: **0 warnings, 0 errors** in the final application build.
- **2 tests passed**, 0 failed: combined inclusive town/budget filtering,
  hidden-selection clearing, rejecting hidden selections, preserving visible
  selections, reset, empty results, and invalid-budget state integrity.
- `Qt6Config.cmake` exists under the selected Qt prefix.
- `qmake -query QT_VERSION` reports **6.12.0**.
- `file plugins/geoservices/libqtgeoservices_osm.dylib` confirms x86_64 and arm64.
- Native bridge C++ output compiles; genuine C# roles/notifications reach QML.

## Observed native UI checks

Used the desktop UI tools on the actual running `HdbResale.app`, including
screenshots and accessibility-tree reads after interactions. Process launch
alone was not used as rendering evidence.

| Check | Observed result |
|---|---|
| Initial map | Singapore OSM basemap rendered; six blue markers and six C# rows; visible attribution |
| Marker tap | Selection displayed TP-2, 201 Tampines Street 21, 3 ROOM, S$420,000; marker turned orange and matching row highlighted |
| List selection | Selecting TP-1 row displayed its address, 4 ROOM, S$580,000 |
| Town | Tampines reduced model and map to two points |
| Combined filter | Tampines + S$450,000 reduced map/list to TP-2 only |
| Hidden selection | Selected TP-1 cleared when applying S$450,000 |
| Accessible marker | Native accessibility activation of remaining marker selected TP-2 |
| Empty result | S$0 yielded zero markers/rows, cleared selection, and showed no-matches text |
| Reset | Restored All towns, S$1,000,000, and six rows/markers |
| Pan | Drag changed center from 1.3521, 103.8198 to 1.4554, 103.6821; tiles and markers moved together |
| Recenter | Singapore button restored center 1.3521, 103.8198 and zoom 11 |
| Zoom buttons | Zoom in/out changed 11 → 12 → 11 with basemap/markers scaling |
| Wheel | Final scroll changed zoom 11.0 → 12.7, with anchored center 1.4005, 103.7882 |

Screenshots of the basemap, one-point combined filter/selection, pan, and zoom
were captured in the task's desktop-tool output. No separate native Library
attachment was created; this did not gate implementation.

## Ordinary issues resolved and remaining limitations

- Fixed QML map/sidebar sizing revealed by the first screenshot.
- Configured the documented custom OSM HTTPS endpoint because the default Qt
  provider returned key-watermarked Thunderforest tiles. This is provider
  configuration, not a Qt defect.
- Drag trace showed active/inactive transitions but no translation update for
  the automation's single-move gesture. Handling initial threshold-crossing
  movement made it pan correctly. No upstream defect was inferred.
- Fixed ordinary app use of `WheelEvent.position`: QML WheelEvent supplies
  `x`/`y`. Verified wheel zoom after correction, with gentler sensitivity and
  OSM tile zoom capped at 19. No runtime errors remained in the final check log.
- Qt Bridge's template emits an unbundled macOS host. A minimal MSBuild staging
  target gives `dotnet run` a discoverable development `.app` identity. Qt's
  native deployment step reports it skips self-contained deployment for the
  original non-bundle target; the staging target intentionally uses installed
  Qt frameworks. Signing/notarization/distribution remain outside this slice.
- Fresh native configuration may print Qt's private-Core-header compatibility
  warning. It was not suppressed. Keep the same Qt build for compile and run.
- Bridge's README linked snapshot documentation returned HTTP 404. Official
  template output, C# source API declarations, and examples were inspected.
- Qt Location 6.12 is deliberately Technology Preview; Bridge is 0.4 beta.
- Public OSM tiles require Internet for cache misses and offer no SLA. Default
  caching remains enabled; prefetch is disabled and no bulk download exists.
- Trackpad pinch is implemented using documented Qt gesture APIs but was not
  separately synthesized; wheel, buttons, and pointer pan were exercised.
- Linux/Windows builds and UI, production datasets, offline maps, and packaged
  distribution were not tested. No credible upstream defect was found, and no
  external bug report was filed.
