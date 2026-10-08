#!/usr/bin/env python3
"""Stage the verified Linux x86_64 application tree that every package format wraps.

Input is a finished Release build (the Qt Bridge build has already run Qt's own deploy
script into its output directory), the pinned Qt prefix and the pinned .NET runtime.
Output is one self-contained directory: host, managed assemblies, Qt libraries, plugins
and QML modules, the .NET runtime, licences, a launcher, `system-libraries.txt` and
`manifest.json`. tar, DEB and RPM are wrappers around that directory; none of them
resolves Qt or .NET dependencies itself.

Qt/Bridge own what the pinned Bridge deploys (the host's own Qt link closure and its
platform plugins). This script adds what the pinned Bridge does not deploy (QML imports,
the OSM geoservice, Wayland, the .NET runtime) and records which source supplied each
Qt file, so the custom share shrinks visibly as Bridge deployment improves.

No download, installation, signing or publication occurs. The output directory must be
outside the checkout and must not already exist.
"""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import platform
import re
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET

from package_common import (APPLICATION_ID, ARCH, INSTALL_PREFIX, PACKAGE_NAME, PINS, PRODUCT, ROOT, audit_tree,
                            application_version, sha256, tree_entries)

# HDB policy: X11 and Wayland, the OSM map plugin, TLS and image support. Nothing for
# embedded display stacks (EGLFS/KMS), input stacks it does not use, or QML debugging.
PLUGIN_FILES = (
    "platforms/libqxcb.so", "xcbglintegrations/libqxcb-glx-integration.so",
    "xcbglintegrations/libqxcb-egl-integration.so",
    "platforms/libqwayland.so", "wayland-shell-integration/libxdg-shell.so",
    "wayland-decoration-client/libbradient.so",
    "wayland-graphics-integration-client/libqt-plugin-wayland-egl.so",
    "wayland-graphics-integration-client/libshm-emulation-server.so",
    "geoservices/libqtgeoservices_osm.so",
    "tls/libqopensslbackend.so", "tls/libqcertonlybackend.so",
    "imageformats/libqjpeg.so", "imageformats/libqsvg.so", "imageformats/libqgif.so",
    "imageformats/libqico.so", "iconengines/libqsvgicon.so",
    "platforminputcontexts/libcomposeplatforminputcontextplugin.so",
)
QT_SBOMS = ("qtbase", "qtdeclarative", "qtlocation", "qtpositioning", "qtsvg",
            "qtgraphs", "qtquick3d", "qtquicktimeline", "qtshadertools")
# Libraries Qt and .NET load at run time rather than link against. They are system packages.
DLOPENED_LIBRARIES = ("libssl.so.3", "libcrypto.so.3")
# Microsoft's untouched runtime carries an optional LTTng tracing provider built for an old
# LTTng ABI. It is not needed to run or for EventPipe, so it is the one accepted gap.
OPTIONAL_UNRESOLVED = {"dotnet/shared/Microsoft.NETCore.App/{runtime}/libcoreclrtraceptprovider.so":
                       ["liblttng-ust.so.0"]}
SCAN_OWNED = ("libQt6", "libicu")


def run(*args: str, env: dict | None = None) -> str:
    return subprocess.check_output(args, text=True, stderr=subprocess.STDOUT, env=env)


def copy(source: Path, destination: Path) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source.resolve(strict=True), destination)
    destination.chmod(0o755 if os.access(source, os.X_OK) else 0o644)


def elf(path: Path) -> bool:
    with path.open("rb") as stream:
        return stream.read(4) == b"\x7fELF"


def dynamic(path: Path) -> tuple[list[str], list[str]]:
    output = run("readelf", "-d", str(path))
    needed = re.findall(r"\(NEEDED\).*?\[(.*?)\]", output)
    rpaths = re.findall(r"\((?:RUNPATH|RPATH)\).*?\[(.*?)\]", output)
    return needed, rpaths


def version_floors(paths: list[Path]) -> dict[str, str]:
    """Highest glibc / libstdc++ symbol versions the packaged binaries require."""
    floors: dict[str, tuple[int, ...]] = {}
    for path in paths:
        needs = run("readelf", "-V", str(path)).partition("Version needs section")[2]
        for family, version in re.findall(r"Name: (GLIBC|GLIBCXX)_([0-9.]+)", needs):
            parsed = tuple(int(part) for part in version.split("."))
            floors[family] = max(floors.get(family, parsed), parsed)
    return {"glibc": ".".join(map(str, floors["GLIBC"])), "glibcxx": ".".join(map(str, floors["GLIBCXX"]))}


def copy_qml_module(source: Path, destination: Path) -> None:
    """Copy one scanner-selected module, never unselected child modules."""
    destination.mkdir(parents=True, exist_ok=True)
    for item in sorted(source.iterdir()):
        if item.is_dir():
            if not (item / "qmldir").exists():
                copy_qml_module(item, destination / item.name)
        elif not item.name.endswith((".debug", ".a", ".prl")):
            copy(item, destination / item.name)


def validate_output(output: Path, root: Path = ROOT) -> None:
    if output == root or root in output.parents:
        raise ValueError("Package outputs must stay outside the repository")
    if output.exists():
        raise ValueError("Output already exists; choose a new directory (nothing is deleted)")


def copy_app_native_libraries(build: Path, app: Path) -> None:
    # Managed P/Invoke dependencies are absent from the executable's ELF NEEDED list.
    copy(build / "libhdb_tile_status.so", app / "libhdb_tile_status.so")


def locate(relative: str, bridge_root: Path, qt_root: Path) -> tuple[Path, str]:
    """The Qt file to ship and who supplied it: the Bridge's deploy output first, else the Qt prefix.

    When both exist they must be identical, so the choice cannot change what ships.
    """
    deployed, installed = bridge_root / relative, qt_root / relative
    if deployed.exists():
        if installed.exists() and sha256(deployed.resolve()) != sha256(installed.resolve()):
            raise ValueError("Bridge-deployed Qt file differs from the pinned Qt prefix: " + relative)
        return deployed, "bridge"
    if installed.exists():
        return installed, "qt"
    raise FileNotFoundError(relative)


def select_qt_libraries(roots: list[Path], package: Path, bridge_root: Path, qt_root: Path) -> dict[str, str]:
    """Copy exactly the Qt/ICU libraries the staged ELF files need, transitively. Returns name -> supplier."""
    supplied: dict[str, str] = {}
    pending = list(roots)
    while pending:
        current = pending.pop()
        for name in dynamic(current)[0]:
            if name in supplied:
                continue
            owned = name.startswith(SCAN_OWNED)
            try:
                source, supplier = locate("lib/" + name, bridge_root, qt_root)
            except FileNotFoundError:
                if owned:
                    raise ValueError("Pinned Qt dependency is missing: " + name) from None
                continue
            # Only Qt archive runtime libraries are copied. Never bundle the host's glibc,
            # graphics drivers, arbitrary /usr libraries, or an SDK.
            if not owned:
                raise ValueError("Unexpected Qt-prefix dependency requires review: " + name)
            destination = package / "qt/lib" / name
            copy(source, destination)
            supplied[name] = supplier
            pending.append(destination)
    return supplied


def select_plugins(package: Path, bridge_root: Path, qt_root: Path) -> dict[str, str]:
    suppliers = {}
    for relative in PLUGIN_FILES:
        source, supplier = locate("plugins/" + relative, bridge_root, qt_root)
        copy(source, package / "qt/plugins" / relative)
        suppliers[relative] = supplier
    return suppliers


def bridge_deployed_files(bridge_root: Path) -> set[str]:
    return {str(path.relative_to(bridge_root)) for sub in ("lib", "plugins") for path in (bridge_root / sub).rglob("*")
            if path.is_file() or path.is_symlink()}


def configure_app_local_icu(package: Path, runtime: str = PINS["dotnet_runtime"]) -> None:
    """Configure only staged files; never alter host libraries or globalization."""
    runtime_dir = package / "dotnet/shared/Microsoft.NETCore.App" / runtime
    for name in ("libicudata.so.73", "libicuuc.so.73", "libicui18n.so.73"):
        target = package / "qt/lib" / name
        target.resolve(strict=True)
        (runtime_dir / name).symlink_to(os.path.relpath(target, runtime_dir))
    runtime_config = package / "app/HdbResale.App.runtimeconfig.json"
    config = json.loads(runtime_config.read_text())
    config["runtimeOptions"].setdefault("configProperties", {})["System.Globalization.AppLocalIcu"] = PINS["icu"]
    runtime_config.write_text(json.dumps(config, indent=2) + "\n")


def launcher_script() -> str:
    # readlink -f lets /usr/bin/<name> (a symlink into /opt) find the real tree.
    return '''#!/bin/sh
set -eu
self=$(readlink -f -- "$0")
here=${self%/*}
export DOTNET_ROOT="$here/dotnet"
export DOTNET_MULTILEVEL_LOOKUP=0
export PATH="$here/dotnet:/usr/bin:/bin"
export QT_PLUGIN_PATH="$here/qt/plugins"
export QML_IMPORT_PATH="$here/qt/qml"
exec "$here/app/HdbResale.App" "$@"
'''


def check_inputs(args: argparse.Namespace) -> tuple[str, str]:
    project = ET.parse(ROOT / "src/HdbResale.App/HdbResale.App.csproj")
    application_license = project.findtext(".//PackageLicenseExpression")
    if application_license != "GPL-3.0-or-later" or not (ROOT / "LICENSE").is_file():
        raise ValueError("The approved GPL-3.0-or-later application metadata/LICENSE is required")
    version = application_version()
    if run(str(args.qt / "bin/qmake"), "-query", "QT_VERSION").strip() != PINS["qt"]:
        raise ValueError("Expected Qt " + PINS["qt"])
    if f"Microsoft.NETCore.App {PINS['dotnet_runtime']} " not in run(str(args.dotnet / "dotnet"), "--list-runtimes"):
        raise ValueError("Expected local .NET runtime " + PINS["dotnet_runtime"])
    nuspec = next(args.bridge.glob("*.nuspec")).read_text(encoding="utf-8-sig")
    if f"<version>{PINS['bridge']}</version>" not in nuspec or "linux-x64" not in nuspec:
        raise ValueError("Expected Linux Qt Bridge " + PINS["bridge"] + " package")
    if not (args.build / "lib/libQt6Core.so.6").exists():
        raise ValueError("The build directory holds no Bridge Qt deployment; run a Release build with QtDir set")
    # A stale build would package QML that no longer matches the source.
    for source in sorted((ROOT / "src/HdbResale.App").glob("*.qml")):
        built = args.build / "Application" / source.name
        if not built.is_file() or built.read_bytes() != source.read_bytes():
            raise ValueError("Build output is stale or incomplete for " + source.name + "; rebuild first")
    return application_license, version


def stage(args: argparse.Namespace) -> Path:
    output = args.output.resolve()
    validate_output(output)
    if platform.system() != "Linux" or platform.machine() != ARCH:
        raise ValueError("Only Linux x86_64 is supported by this staging recipe")
    for name in ("qt", "dotnet", "build", "bridge"):
        setattr(args, name, getattr(args, name).resolve(strict=True))
    application_license, version = check_inputs(args)
    qt, dotnet, build, bridge = args.qt, args.dotnet, args.build, args.bridge
    runtime = PINS["dotnet_runtime"]
    output.mkdir(parents=True)
    package = output / f"{PACKAGE_NAME}-{version}-linux-x64"
    app = package / "app"

    for name in ("HdbResale.App", "HdbResale.App.deps.json", "HdbResale.App.runtimeconfig.json",
                 "qt_bridge_metadata.json"):
        copy(build / name, app / name)
    copy_app_native_libraries(build, app)
    for source in sorted(build.glob("*.dll")):
        copy(source, app / source.name)
    # Application QML is normally compiled into the host; retaining its deployed
    # source module is useful for inspection and matches the Bridge build output.
    for source in sorted((build / "Application").iterdir()):
        if source.is_file() and (source.suffix == ".qml" or source.name == "qmldir"):
            copy(source, app / "Application" / source.name)

    # Qt's own scanner chooses the QML modules; the pinned Bridge deploys none of them.
    # Do not recursively scan stale obj/bin copies beneath the source folder.
    scan_root = output / "qml-inputs"
    for source in sorted((ROOT / "src/HdbResale.App").glob("*.qml")):
        copy(source, scan_root / source.name)
    scanned = subprocess.run([str(qt / "libexec/qmlimportscanner"), "-rootPath", str(scan_root),
                              "-importPath", str(qt / "qml")], text=True, capture_output=True, check=True)
    if scanned.stderr.strip():
        raise ValueError("QML scanner diagnostics require review:\n" + scanned.stderr)
    modules = []
    for module in json.loads(scanned.stdout):
        if module.get("type") != "module":
            continue
        path = Path(module.get("path", "")).resolve()
        if not path.is_relative_to(qt / "qml"):
            raise ValueError("QML module missing or outside pinned Qt: " + module.get("name", "?"))
        if module["name"].startswith(("QtCharts", "QMapLibre")):
            raise ValueError("Unapproved module: " + module["name"])
        copy_qml_module(path, package / "qt/qml" / path.relative_to(qt / "qml"))
        modules.append(module["name"])
    shutil.rmtree(scan_root)

    plugin_suppliers = select_plugins(package, build, qt)
    roots = [path for path in package.rglob("*") if path.is_file() and elf(path)]
    library_suppliers = select_qt_libraries(roots, package, build, qt)

    # CMake's supported edit handles the ELF dynamic table. This changes only
    # the staged copy, never the source build or global Qt installation.
    host = app / "HdbResale.App"
    rpaths = dynamic(host)[1]
    if len(rpaths) != 1:
        raise ValueError("Expected one host RPATH/RUNPATH; refusing a blind edit")
    cmake_file = output / "relocate-host.cmake"
    cmake_file.write_text('file(RPATH_CHANGE FILE [[' + str(host) + ']] OLD_RPATH [[' + rpaths[0]
                          + ']] NEW_RPATH [[$ORIGIN/../qt/lib]])\n')
    run(args.cmake, "-P", str(cmake_file))
    cmake_file.unlink()
    (app / "qt.conf").write_text("[Paths]\nPrefix = ../qt\nPlugins = plugins\nQmlImports = qml\n")

    copy(dotnet / "dotnet", package / "dotnet/dotnet")
    for relative in ("host/fxr/" + runtime, "shared/Microsoft.NETCore.App/" + runtime):
        for source in sorted((dotnet / relative).rglob("*")):
            if source.is_file():
                copy(source, package / "dotnet" / source.relative_to(dotnet))
    # .NET otherwise chooses the newest system ICU independently of Qt's ICU.
    # App-local mode fails closed instead of falling back to a host installation.
    configure_app_local_icu(package)

    selected_qt_licenses = write_licenses(package, qt, bridge, dotnet, sorted(library_suppliers))
    launcher = package / PACKAGE_NAME
    launcher.write_text(launcher_script())
    launcher.chmod(0o755)

    elf_files = [path for path in package.rglob("*") if path.is_file() and elf(path)]
    dependencies, optional_unresolved = audit_runtime_dependencies(package, elf_files, runtime)
    floors = version_floors(elf_files)
    system_libraries = sorted(set(dependencies) | set(DLOPENED_LIBRARIES))
    (package / "system-libraries.txt").write_text(
        "# Shared libraries the package expects from the host (ELF NEEDED, then dlopen).\n"
        + "\n".join(system_libraries) + "\n")

    deployed = bridge_deployed_files(build)
    shipped = {"lib/" + name for name in library_suppliers} | {"plugins/" + name for name in plugin_suppliers}
    files, modes, links = tree_entries(package)
    manifest = {
        "schema": 2, "product": PRODUCT, "package": PACKAGE_NAME, "version": version,
        "application_id": APPLICATION_ID, "application_license": application_license,
        "install_prefix": INSTALL_PREFIX, "architecture": ARCH,
        "scope": "Linux x86_64; system ABI/graphics/X11 dependencies remain; one tested distribution per family",
        "data_source": "HDB Resale Explorer Worker API (HDB_API_BASE_URL); no resale data is bundled",
        "bundled_resale_data": False,
        "qt": PINS["qt"], "bridge": PINS["bridge"], "dotnet_runtime": runtime,
        "packaging_runtime_overrides": {"System.Globalization.AppLocalIcu": PINS["icu"],
            "icu_source": "Unmodified ICU 73.2 runtime from the pinned official Qt archive"},
        "source_commit": run("git", "-C", str(ROOT), "rev-parse", "HEAD").strip(),
        "source_dirty": bool(run("git", "-C", str(ROOT), "status", "--porcelain").strip()),
        "built_host_input_sha256": sha256(build / "HdbResale.App"),
        "qml_modules": sorted(set(modules)),
        "qt_library_suppliers": dict(sorted(library_suppliers.items())),
        "qt_plugin_suppliers": dict(sorted(plugin_suppliers.items())),
        "bridge_deployed_not_shipped": sorted(deployed - shipped),
        "qt_library_license_choices_from_sbom": selected_qt_licenses,
        "system_dependencies": sorted(dependencies), "dlopened_dependencies": list(DLOPENED_LIBRARIES),
        "minimum_versions": floors, "optional_unresolved": optional_unresolved,
        "distribution_cleared": False,
        "files": files, "modes": modes, "symlinks": links,
    }
    audit_tree(package)
    (package / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    print(json.dumps({"package": str(package), "files": len(files), "qt_libraries": len(library_suppliers),
                      "from_bridge_deploy": sum(s == "bridge" for s in library_suppliers.values()),
                      "distribution_cleared": False}, indent=2))
    return package


def write_licenses(package: Path, qt: Path, bridge: Path, dotnet: Path, copied_libraries: list[str]) -> dict:
    licenses = package / "licenses"
    for name in ("LICENSE.txt", "ThirdPartyNotices.txt"):
        copy(dotnet / name, licenses / ("dotnet-" + name))
    copy(bridge / "LICENSE.txt", licenses / "Qt-Bridge-CSharp-LICENSE.txt")
    for name in ("LGPL-3.0-only.txt", "GPL-3.0-only.txt", "BSD-3-Clause.txt"):
        copy(ROOT / "LICENSES" / name, licenses / name)
    for module in QT_SBOMS:
        for suffix in ("spdx.json", "source.spdx"):
            copy(qt / "sbom" / f"{module}-{PINS['qt']}.{suffix}", licenses / "qt-sbom" / f"{module}-{PINS['qt']}.{suffix}")
    records = {}
    for module in QT_SBOMS:
        for record in json.loads((qt / "sbom" / f"{module}-{PINS['qt']}.spdx.json").read_text())["packages"]:
            records[record["name"]] = record.get("licenseConcluded", "NOASSERTION")
    selected = {}
    for library in copied_libraries:
        if library.startswith("libQt6"):
            name = library.removeprefix("libQt6").split(".so")[0]
            expression = records.get("XcbQpaPrivate" if name == "XcbQpa" else name, "NOASSERTION")
            if "LGPL-3.0-only" not in expression and "GPL-3.0-only" not in expression:
                raise ValueError("Qt library licensing needs an explicit decision: " + library + ": " + expression)
            selected[library] = expression
    copy(ROOT / "src/HdbResale.App/assets/README.md", licenses / "OneMap-logo-provenance.md")
    copy(ROOT / "docs/product-rc/licensing-packaging.md", licenses / "licensing-packaging.md")
    copy(ROOT / "LICENSE", licenses / "APPLICATION-LICENSE")
    for name in ("REUSE.toml", "THIRD_PARTY_NOTICES.md"):
        copy(ROOT / name, licenses / name)
    (licenses / "DISTRIBUTION-BLOCKERS.txt").write_text(
        "TECHNICALLY PACKAGED, NOT A DISTRIBUTION CLEARANCE.\n"
        "Original application is GPL-3.0-or-later. Complete exact application/Qt/Bridge corresponding-source provision,\n"
        "relinking/rebuild instructions (including compiled Bridge headers), and all third-party\n"
        "copyright/license notices before distributing. SBOM files are inventory, not a substitute\n"
        "for the license texts or corresponding source. OneMap logo/data keep their own terms.\n")
    return selected


def audit_runtime_dependencies(package: Path, elf_files: list[Path], runtime: str) -> tuple[set[str], list[dict]]:
    """Every packaged ELF must resolve inside the package or to a named system library.

    Returns the system libraries the package's own files link directly (what a package must
    declare; the distribution resolves their dependencies) and the one accepted gap.
    """
    direct: set[str] = set()
    optional_unresolved = []
    audit_env = {"PATH": "/usr/bin:/bin", "LC_ALL": "C", "LD_LIBRARY_PATH": str(package / "qt/lib")}
    allowed = {key.format(runtime=runtime): value for key, value in OPTIONAL_UNRESOLVED.items()}
    for path in elf_files:
        needed, rpaths = dynamic(path)
        for rpath in rpaths:
            if any(part.startswith("/") for part in rpath.split(":")):
                raise ValueError("Absolute runtime search path in package: " + str(path.relative_to(package)))
        output_ldd = run("ldd", str(path), env=audit_env)
        missing = re.findall(r"\s+(\S+) => not found", output_ldd)
        relative = str(path.relative_to(package))
        if missing:
            if allowed.get(relative) == missing:
                optional_unresolved.append({"file": relative, "libraries": missing,
                                            "feature": "optional LTTng tracing, unavailable on this host"})
            else:
                raise ValueError("Unresolved runtime dependency in " + str(path) + "\n" + output_ldd)
        resolved_by_name = dict(re.findall(r"\s+(\S+) => (\S+)", output_ldd))
        for name, resolved in resolved_by_name.items():
            if resolved != "not" and not Path(resolved).is_relative_to(package) and name.startswith(SCAN_OWNED):
                raise ValueError("Qt/ICU escaped the package: " + resolved)
        direct |= {name for name in needed if name in resolved_by_name and resolved_by_name[name] != "not"
                   and not Path(resolved_by_name[name]).is_relative_to(package)}
    return direct, optional_unresolved


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--build", type=Path, default=ROOT / "src/HdbResale.App/bin/Release/net10.0")
    parser.add_argument("--qt", type=Path, required=True)
    parser.add_argument("--dotnet", type=Path, required=True)
    parser.add_argument("--bridge", type=Path, required=True)
    parser.add_argument("--cmake", default="cmake")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    try:
        stage(args)
    except (OSError, ValueError, subprocess.CalledProcessError) as error:
        print("Staging failed: " + str(error), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
