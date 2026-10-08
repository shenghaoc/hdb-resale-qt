# Linux packaging inputs

Declarative inputs for the Linux packages. The behaviour is in `tools/package/`; the architecture and
verification record is in [docs/packaging/linux.md](../docs/packaging/linux.md).

| File | Purpose |
| --- | --- |
| `metadata.json` | Package name, summary, description, maintainer, licence expression, release number |
| `system-dependencies.json` | Every system shared library the package may expect, and the Debian 13 / Fedora 43 package that provides it; version floors; fonts, keyboard data and certificates |
| `linux/*.desktop`, `*.svg`, `*.metainfo.xml.in`, `copyright` | Menu entry, icon, AppStream metadata (version and date are filled in at build time), Debian copyright notice |

The application version is **not** here: it is the project's `<Version>`
(`src/HdbResale.App/HdbResale.App.csproj`), read by `tools/package/package_common.py`.

Pins that must not drift automatically (Dependabot ignores them): Qt Bridge, Qt 6.12.0, the bundled .NET
runtime 10.0.12 and ICU 73 (`PINS` in `package_common.py`). Changing one is a native re-validation.

## Build locally

```sh
export QtDir=/path/to/Qt/6.12.0/gcc_64          # the verified Qt, with Graphs/Quick3D/Location modules
dotnet build src/HdbResale.App -c Release
python3 tools/package/stage_linux.py --qt "$QtDir" --dotnet "$DOTNET_ROOT" \
  --bridge ~/.nuget/packages/qtgroup.qt.bridge.csharp.linux-x64/0.4.0-beta --output /tmp/hdb-stage
python3 tools/package/build_packages.py --staged /tmp/hdb-stage/hdb-resale-explorer-*-linux-x64 --output /tmp/hdb-packages
```

`build_packages.py` needs `dpkg-deb` for the DEB and `rpmbuild` for the RPM. Both formats wrap the one staged tree.
