#!/usr/bin/env python3
"""Stage a private Linux x64 RC from a built app and pinned local runtimes.

No download, installation, signing or publication occurs. The output directory
must be outside the checkout and must not already exist. This is a same-family
Linux package, not a promise of portability across distributions.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import shutil
import subprocess
import sys
import tarfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
DATA_FILES = ("transactions.csv", "address-evidence.csv", "postal-address-evidence.csv",
              "building-evidence.geojson", "provenance.json", "README.md")
PLUGIN_FILES = (
    "platforms/libqxcb.so", "xcbglintegrations/libqxcb-glx-integration.so",
    "xcbglintegrations/libqxcb-egl-integration.so", "geoservices/libqtgeoservices_osm.so",
    "tls/libqopensslbackend.so", "tls/libqcertonlybackend.so",
    "imageformats/libqjpeg.so", "imageformats/libqsvg.so", "imageformats/libqgif.so",
    "imageformats/libqico.so", "iconengines/libqsvgicon.so",
    "platforminputcontexts/libcomposeplatforminputcontextplugin.so",
)
QT_SBOMS = ("qtbase", "qtdeclarative", "qtlocation", "qtpositioning", "qtsvg",
            "qtgraphs", "qtquick3d", "qtquicktimeline", "qtshadertools")


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


def copy_app_native_libraries(build, app):
    # Managed P/Invoke dependencies are absent from the executable's ELF NEEDED list.
    copy(build / "libhdb_tile_status.so", app / "libhdb_tile_status.so")


def configure_app_local_icu(package: Path) -> None:
    """Configure only staged files; never alter host libraries or globalization."""
    runtime_dir = package / "dotnet/shared/Microsoft.NETCore.App/10.0.12"
    for name in ("libicudata.so.73", "libicuuc.so.73", "libicui18n.so.73"):
        target = package / "qt/lib" / name
        target.resolve(strict=True)
        (runtime_dir / name).symlink_to(os.path.relpath(target, runtime_dir))
    runtime_config = package / "app/HdbResale.App.runtimeconfig.json"
    config = json.loads(runtime_config.read_text())
    config["runtimeOptions"].setdefault("configProperties", {})["System.Globalization.AppLocalIcu"] = "73"
    runtime_config.write_text(json.dumps(config, indent=2) + "\n")


def stage(args: argparse.Namespace) -> Path:
    output = args.output.resolve()
    validate_output(output)
    if platform.system() != "Linux" or platform.machine() != "x86_64":
        raise ValueError("Only Linux x86_64 is supported by this local RC recipe")
    qt = args.qt.resolve(strict=True)
    dotnet = args.dotnet.resolve(strict=True)
    build = args.build.resolve(strict=True)
    bridge = args.bridge.resolve(strict=True)
    project = ET.parse(ROOT / "src/HdbResale.App/HdbResale.App.csproj")
    application_license = project.findtext(".//PackageLicenseExpression")
    if application_license != "GPL-3.0-or-later" or not (ROOT / "LICENSE").is_file():
        raise ValueError("The approved GPL-3.0-or-later application metadata/LICENSE is required")
    if not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?", args.version):
        raise ValueError("Version must be a plain semantic version")
    if run(str(qt / "bin/qmake"), "-query", "QT_VERSION").strip() != "6.12.0":
        raise ValueError("Expected Qt 6.12.0")
    runtimes = run(str(dotnet / "dotnet"), "--list-runtimes")
    if "Microsoft.NETCore.App 10.0.12 " not in runtimes:
        raise ValueError("Expected local .NET runtime 10.0.12")
    nuspec = next(bridge.glob("*.nuspec")).read_text(encoding="utf-8-sig")
    if "<version>0.4.0-beta</version>" not in nuspec or "linux-x64" not in nuspec:
        raise ValueError("Expected Linux Qt Bridge 0.4.0-beta package")
    output.mkdir(parents=True)
    package = output / ("hdb-resale-explorer-" + args.version + "-linux-x64-private-rc")
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
    for name in DATA_FILES:
        copy(ROOT / "data" / name, app / "data" / name)

    # Do not recursively scan stale obj/bin copies beneath the source folder.
    scan_root = output / "qml-inputs"
    for source in sorted((ROOT / "src/HdbResale.App").glob("*.qml")):
        copy(source, scan_root / source.name)
    scanned = subprocess.run([str(qt / "libexec/qmlimportscanner"), "-rootPath",
                              str(scan_root), "-importPath", str(qt / "qml")],
                             text=True, capture_output=True, check=True)
    if scanned.stderr.strip():
        raise ValueError("QML scanner diagnostics require review:\n" + scanned.stderr)
    scanner = json.loads(scanned.stdout)
    modules = []
    for module in scanner:
        if module.get("type") != "module":
            continue
        path = Path(module.get("path", "")).resolve()
        if not path.is_relative_to(qt / "qml"):
            raise ValueError("QML module missing or outside pinned Qt: " + module.get("name", "?"))
        if module["name"].startswith(("QtCharts", "QMapLibre")):
            raise ValueError("Unapproved module: " + module["name"])
        relative = path.relative_to(qt / "qml")
        copy_qml_module(path, package / "qt/qml" / relative)
        modules.append(module["name"])
    for relative in PLUGIN_FILES:
        copy(qt / "plugins" / relative, package / "qt/plugins" / relative)

    pending = [path for path in package.rglob("*") if path.is_file() and elf(path)]
    copied_libraries: set[str] = set()
    while pending:
        current = pending.pop()
        for name in dynamic(current)[0]:
            source = qt / "lib" / name
            if source.exists() and name not in copied_libraries:
                # Only Qt archive runtime libraries are copied. Never bundle the
                # host's glibc, graphics drivers, arbitrary /usr libraries, or SDK.
                if not (name.startswith("libQt6") or name.startswith("libicu")):
                    raise ValueError("Unexpected Qt-prefix dependency requires review: " + name)
                destination = package / "qt/lib" / name
                copy(source, destination)
                copied_libraries.add(name)
                pending.append(destination)
            elif name.startswith(("libQt6", "libicu")) and not source.exists():
                raise ValueError("Pinned Qt dependency is missing: " + name)

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
    (app / "qt.conf").write_text("[Paths]\nPrefix = ../qt\nPlugins = plugins\nQmlImports = qml\n")

    copy(dotnet / "dotnet", package / "dotnet/dotnet")
    for relative in ("host/fxr/10.0.12", "shared/Microsoft.NETCore.App/10.0.12"):
        for source in sorted((dotnet / relative).rglob("*")):
            if source.is_file():
                copy(source, package / "dotnet" / source.relative_to(dotnet))
    # .NET otherwise chooses the newest system ICU independently of Qt's ICU.
    # App-local mode fails closed instead of falling back to a host installation.
    # CoreLib's native probing directory is the bundled framework directory.
    configure_app_local_icu(package)
    licenses = package / "licenses"
    for name in ("LICENSE.txt", "ThirdPartyNotices.txt"):
        copy(dotnet / name, licenses / ("dotnet-" + name))
    copy(bridge / "LICENSE.txt", licenses / "Qt-Bridge-CSharp-LICENSE.txt")
    for name in ("LGPL-3.0-only.txt", "GPL-3.0-only.txt", "BSD-3-Clause.txt"):
        copy(args.bridge_source / "LICENSES" / name, licenses / name)
    for module in QT_SBOMS:
        for suffix in ("spdx.json", "source.spdx"):
            copy(qt / "sbom" / (module + "-6.12.0." + suffix),
                 licenses / "qt-sbom" / (module + "-6.12.0." + suffix))
    qt_license_records = {}
    for module in QT_SBOMS:
        for record in json.loads((qt / "sbom" / (module + "-6.12.0.spdx.json")).read_text())["packages"]:
            qt_license_records[record["name"]] = record.get("licenseConcluded", "NOASSERTION")
    selected_qt_licenses = {}
    for library in sorted(copied_libraries):
        if library.startswith("libQt6"):
            name = library.removeprefix("libQt6").split(".so")[0]
            spdx_name = "XcbQpaPrivate" if name == "XcbQpa" else name
            expression = qt_license_records.get(spdx_name, "NOASSERTION")
            if "LGPL-3.0-only" not in expression and "GPL-3.0-only" not in expression:
                raise ValueError("Qt library licensing needs an explicit decision: " + library + ": " + expression)
            selected_qt_licenses[library] = expression
    copy(ROOT / "src/HdbResale.App/assets/README.md", licenses / "OneMap-logo-provenance.md")
    copy(ROOT / "docs/product-rc/licensing-packaging.md", licenses / "licensing-packaging.md")
    if (ROOT / "LICENSE").exists():
        copy(ROOT / "LICENSE", licenses / "APPLICATION-LICENSE")
    for name in ("REUSE.toml", "THIRD_PARTY_NOTICES.md"):
        copy(ROOT / name, licenses / name)
    (licenses / "DISTRIBUTION-BLOCKERS.txt").write_text(
        "PRIVATE LOCAL RC. NOT A DISTRIBUTION CLEARANCE.\n"
        "Original application is GPL-3.0-or-later. Complete exact application/Qt/Bridge corresponding-source provision,\n"
        "relinking/rebuild instructions (including compiled Bridge headers), and all third-party\n"
        "copyright/license notices before distributing. SBOM files are inventory, not a substitute\n"
        "for the license texts or corresponding source. OneMap logo/data keep their own terms.\n")
    launcher = package / "hdb-resale-explorer"
    launcher.write_text('''#!/bin/sh
set -eu
here=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
export DOTNET_ROOT="$here/dotnet"
export DOTNET_MULTILEVEL_LOOKUP=0
export PATH="$here/dotnet:/usr/bin:/bin"
export QT_PLUGIN_PATH="$here/qt/plugins"
export QML_IMPORT_PATH="$here/qt/qml"
exec "$here/app/HdbResale.App" "$@"
''')
    launcher.chmod(0o755)
    elf_files = [path for path in package.rglob("*") if path.is_file() and elf(path)]
    dependencies: set[str] = set()
    optional_unresolved = []
    audit_env = {"PATH": "/usr/bin:/bin", "LC_ALL": "C", "LD_LIBRARY_PATH": str(package / "qt/lib")}
    for path in elf_files:
        for rpath in dynamic(path)[1]:
            if any(part.startswith("/") for part in rpath.split(":")):
                raise ValueError("Absolute runtime search path in package: " + str(path.relative_to(package)))
        output_ldd = run("ldd", str(path), env=audit_env)
        missing = re.findall(r"\s+(\S+) => not found", output_ldd)
        if missing:
            # Microsoft's untouched runtime carries an optional LTTng tracing
            # provider built for the old LTTng ABI. It is not needed for app
            # execution or EventPipe. Do not install an obsolete system library
            # merely to enable this unused diagnostic feature. No other missing
            # dependency is accepted, and native launch remains mandatory.
            if (path == package / "dotnet/shared/Microsoft.NETCore.App/10.0.12/libcoreclrtraceptprovider.so"
                    and missing == ["liblttng-ust.so.0"]):
                optional_unresolved.append({"file": str(path.relative_to(package)), "libraries": missing,
                                            "feature": "optional LTTng tracing, unavailable on this host"})
            else:
                raise ValueError("Unresolved runtime dependency in " + str(path) + "\n" + output_ldd)
        for name, resolved in re.findall(r"\s+(\S+) => (\S+)", output_ldd):
            if resolved != "not" and not Path(resolved).is_relative_to(package):
                if name.startswith(("libQt6", "libicu")):
                    raise ValueError("Qt/ICU escaped the package: " + resolved)
                dependencies.add(name)
    manifest = {
        "product": "HDB Resale Explorer", "version": args.version,
        "application_license": application_license,
        "scope": "private local Linux x64 RC; system ABI/graphics/X11 dependencies remain",
        "qt": "6.12.0", "bridge": "0.4.0-beta", "dotnet_runtime": "10.0.12",
        "packaging_runtime_overrides": {"System.Globalization.AppLocalIcu": "73",
            "icu_source": "Unmodified ICU 73.2 runtime from the pinned official Qt archive"},
        "source_commit": run("git", "-C", str(ROOT), "rev-parse", "HEAD").strip(),
        "source_dirty": bool(run("git", "-C", str(ROOT), "status", "--porcelain").strip()),
        "source_files_sha256": {
            str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in sorted((ROOT / "src").rglob("*"))
            if p.is_file() and not {"bin", "obj"}.intersection(p.relative_to(ROOT).parts)
        },
        "built_host_input_sha256": hashlib.sha256((build / "HdbResale.App").read_bytes()).hexdigest(),
        "qml_modules": sorted(set(modules)), "qt_libraries": sorted(copied_libraries),
        "qt_library_license_choices_from_sbom": selected_qt_licenses,
        "system_dependencies": sorted(dependencies), "optional_unresolved": optional_unresolved,
        "data_files": list(DATA_FILES),
        "distribution_cleared": False, "launch_verified": False,
        "files": {str(p.relative_to(package)): hashlib.sha256(p.read_bytes()).hexdigest()
                  for p in sorted(package.rglob("*")) if p.is_file()},
    }
    (package / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    archive = output / (package.name + ".tar.gz")
    with tarfile.open(archive, "w:gz") as tar:
        tar.add(package, arcname=package.name)
    (output / (archive.name + ".sha256")).write_text(hashlib.sha256(archive.read_bytes()).hexdigest()
                                                     + "  " + archive.name + "\n")
    print(json.dumps({"package": str(package), "archive": str(archive),
                      "files": len(manifest["files"]), "bytes": archive.stat().st_size,
                      "launch_verified": False, "distribution_cleared": False}, indent=2))
    return package


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build", type=Path, default=ROOT / "src/HdbResale.App/bin/Release/net10.0")
    parser.add_argument("--qt", type=Path, required=True)
    parser.add_argument("--dotnet", type=Path, required=True)
    parser.add_argument("--bridge", type=Path, required=True)
    parser.add_argument("--bridge-source", type=Path, required=True)
    parser.add_argument("--cmake", default="cmake")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--version", default="0.1.0")
    args = parser.parse_args()
    try:
        stage(args)
    except (OSError, ValueError, subprocess.CalledProcessError) as error:
        print("RC staging failed: " + str(error), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
