# HDB resale native fixture explorer

A small native C# / Qt Quick vertical slice. Six checked-in **synthetic** resale
transactions have approximate Singapore coordinates. These are not real resale
records or buying advice.

C# owns the transactions, combined town/budget filtering, selection, summaries,
and the Qt item model. QML owns the native window, controls, marker presentation,
and map navigation. Changing a filter resets the bridge model, so both
`MapItemView` markers and the transaction list update. Selection is retained
while visible and cleared when its transaction is filtered out.

## Verified toolchain (macOS arm64)

- .NET SDK **10.0.401**, `net10.0`, runtime **10.0.12**; Microsoft macOS arm64 SDK.
- Qt **6.12.0**, macOS `clang_64` universal x86_64/arm64 binaries,
  installed under `$HOME/Qt/6.12.0/macos`.
- Qt Bridge `QtGroup.Qt.Bridge.CSharp.osx-arm64` **0.4.0.22-beta**.
- Official Bridge template `QtGroup.Qt.Bridge.CSharp.Templates` **0.4.0-beta**.
- aqtinstall **3.3.0**, Python **3.12.14**, Ninja Python package **1.13.2**
  (`ninja --version`: `1.13.2.git.kitware.jobserver-pipe-1`).
- CMake **4.4.3** and Xcode **27.0** / AppleClang **21.0.0.21000334**.
- Verified host: macOS **27.0.1**, Apple Silicon.
- Test dependencies: Microsoft.NET.Test.Sdk **17.14.1**, xUnit **2.9.3**,
  xunit.runner.visualstudio **3.1.4**.

Qt Bridge on macOS requires an external Qt installation. Install Xcode and its
command-line tools, CMake, and the Microsoft .NET 10 arm64 SDK first. The SDK
version is pinned in `global.json`; no .NET 11 prerelease is used.

### Qt provisioning with aqtinstall

This setup used `uv` and an isolated user-local virtual environment. Existing
Qt installations are preserved. Package installs require Internet access.

```sh
uv venv --python 3.12.14 "$HOME/.local/share/hdb-qt-toolchain"
uv pip install --python "$HOME/.local/share/hdb-qt-toolchain/bin/python" \
  aqtinstall==3.3.0 ninja==1.13.2
export PATH="$HOME/.local/share/hdb-qt-toolchain/bin:$PATH"

aqt list-qt mac desktop --spec '6.12'
aqt list-qt mac desktop --arch 6.12.0
aqt list-qt mac desktop --modules 6.12.0 clang_64
# Actual results: 6.12.0; clang_64; qtlocation, qtpositioning, qtshadertools available.
aqt install-qt mac desktop 6.12.0 clang_64 -O "$HOME/Qt" \
  -m qtlocation qtpositioning qtshadertools

export QtDir="$HOME/Qt/6.12.0/macos"
test -f "$QtDir/lib/cmake/Qt6/Qt6Config.cmake"
test -f "$QtDir/plugins/geoservices/libqtgeoservices_osm.dylib"
file "$QtDir/plugins/geoservices/libqtgeoservices_osm.dylib"
"$QtDir/bin/qmake" -query QT_VERSION
```

The base Qt archive includes Qt Quick, QML, Quick Controls, SVG, and tools.
Location/Positioning are additional queried modules; the OSM GeoServices plugin
was verified as a universal binary containing arm64. Qt Location 6.12 is a
**Technology Preview**, deliberately used here.

The project was based on inspected official template output:

```sh
dotnet new install QtGroup.Qt.Bridge.CSharp.Templates@0.4.0-beta
dotnet new qt -n BridgeProbe --SampleCode --Framework net10.0
```

These commands are provenance; do not generate another template over this
checkout. Its wildcard runtime version was replaced with an exact package pin.
Qt and Bridge have open-source/commercial license terms; the Bridge package
license identifies Qt Commercial or LGPL-3.0-only. No Qt Online Installer,
account sign-in, or interactive agreement acceptance was used.

## Build, test, run

From the repository root in each new terminal:

```sh
export PATH="$HOME/.local/share/hdb-qt-toolchain/bin:$PATH"
export QtDir="$HOME/Qt/6.12.0/macos"
dotnet build
dotnet test tests/HdbResale.Tests/HdbResale.Tests.csproj --no-build
dotnet run --project src/HdbResale.App/HdbResale.App.csproj --no-build
```

Alternatively pass `-p:QtDir="$HOME/Qt/6.12.0/macos"` to build/run. Ninja and
CMake must be on PATH. Close the window to exit. Run without `--no-build` when
changing source. The small macOS-only MSBuild target stages build output into
`src/HdbResale.App/obj/Debug/net10.0/HdbResale.app`; `dotnet run` launches its
native bridge host. This development bundle provides macOS app identity and
accessibility discovery. It uses the installed Qt frameworks and is **not** a
self-contained, signed, notarized distribution. Generated bridge code and bundle
output stay under ignored `obj`/`bin` directories.

## Structure

```text
src/HdbResale.Domain/  immutable transaction record, tiny fixture, ExplorerState
src/HdbResale.App/     official-template csproj, Program, ResaleMapModel, Main.qml, Info.plist
tests/HdbResale.Tests/ domain tests, independent of Qt and desktop rendering
```

The bridge singleton `Resales` derives from the documented `Qt.Bridge.Models.Model`,
exposes custom model roles and `INotifyPropertyChanged` properties, and handles
filter/selection methods called by QML. Begin/end reset notifications surround
C# model changes. No business filtering or selection logic lives in QML JS;
its JavaScript only forwards UI commands and manages map gestures/presentation.

## Map provider

The GeoServices plugin is still **osm**. It uses Qt's documented custom-map mode
with `https://tile.openstreetmap.org/`, visible OSM contributor attribution, an
identifying `HdbResaleQt/0.1` user agent, default Qt tile caching, and
`NoPrefetching`. Only normal interactive viewing is supported; there is no bulk
or offline downloader. Internet access is needed for uncached tiles. The public
OSM tile service is best-effort and subject to its usage policy; choose an
appropriate provider before a wider production rollout.

The default Qt remote provider returned Thunderforest tiles with an API-key
watermark during verification. Explicit provider configuration fixes that;
this is a provider choice, **not an upstream Qt bug**. Map attribution stays
visible at the bottom. Drag includes movement crossing the activation threshold,
so a single-move automation gesture can pan as well as normal pointer movement.

## Verification

See [docs/verification.md](docs/verification.md) for executed checks and limits.
Supported RID package selection from the template remains for Windows x64/arm64,
Linux x64, and macOS x64/arm64. Other desktop targets require their matching Qt
installation and toolchain; Linux/Windows have not been built or UI-tested here.
Keep build/run on the same Qt installation: the bridge uses Qt private headers
and fresh native configuration warns about coupling to that Qt build.

Next useful milestone: load a small validated local CSV of real transactions,
with explicit provenance and location quality, then validate Linux/Windows.
Keep the same small C# model and native map before adding broader features.

## Official references

- [Qt Bridge C# README / examples](https://code.qt.io/cgit/qt/qtbridge-csharp.git/tree/)
- [Published Bridge runtime](https://www.nuget.org/packages/QtGroup.Qt.Bridge.CSharp.osx-arm64/0.4.0.22-beta)
- [Qt 6.12 Map and gesture documentation](https://doc.qt.io/qt-6.12/qml-qtlocation-map.html)
- [Qt 6.12 OSM plugin parameters](https://doc.qt.io/qt-6.12/location-plugin-osm.html)
- [OSM tile usage policy](https://operations.osmfoundation.org/policies/tiles/)
- [aqtinstall](https://aqtinstall.readthedocs.io/)

The Bridge README's linked snapshot documentation returned HTTP 404 during
setup; API declarations in the official C# source and official examples were
consulted instead. No APIs were inferred from the Rust/Python bridges.
