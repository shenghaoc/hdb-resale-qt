#!/usr/bin/env python3
"""Wrap one verified staged tree as tar.gz, .deb and .rpm.

The package formats are deliberately thin. They carry the tree exactly as stage_linux.py
produced it (the manifest is re-verified first), add desktop integration files, and declare
the system libraries the manifest names. They never discover Qt modules, resolve Qt libraries
or locate .NET: that was done once, upstream of this script, and is recorded in the manifest.

Uses only dpkg-deb and rpmbuild; no download, signing, tagging or publication occurs.
"""
from __future__ import annotations

import argparse
import gzip
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tarfile
import tempfile
import time

sys.path.insert(0, str(Path(__file__).resolve().parent))

from package_common import (APPLICATION_ID, INSTALL_PREFIX, PACKAGE_NAME, ROOT, audit_tree, verify_manifest)  # noqa: E402

PACKAGING = ROOT / "packaging"
FORMATS = ("tar", "deb", "rpm")
BIN_LINK = f"/usr/bin/{PACKAGE_NAME}"
DESKTOP = f"usr/share/applications/{APPLICATION_ID}.desktop"
ICON = f"usr/share/icons/hicolor/scalable/apps/{APPLICATION_ID}.svg"
METAINFO = f"usr/share/metainfo/{APPLICATION_ID}.metainfo.xml"
DOC = f"usr/share/doc/{PACKAGE_NAME}/copyright"
DOC_DIR = f"/usr/share/doc/{PACKAGE_NAME}"


def load_metadata() -> dict:
    metadata = json.loads((PACKAGING / "metadata.json").read_text())
    if metadata["name"] != PACKAGE_NAME or metadata["application_id"] != APPLICATION_ID:
        raise ValueError("packaging/metadata.json disagrees with the application identity")
    return metadata


def load_dependency_table() -> dict:
    return json.loads((PACKAGING / "system-dependencies.json").read_text())


def source_date_epoch() -> int:
    """Reproducible timestamps: SOURCE_DATE_EPOCH, else the commit time."""
    if os.environ.get("SOURCE_DATE_EPOCH", "").isdigit():
        return int(os.environ["SOURCE_DATE_EPOCH"])
    return int(subprocess.check_output(["git", "-C", str(ROOT), "log", "-1", "--format=%ct"], text=True))


def floor_constraint(manifest: dict, table: dict) -> dict[str, str]:
    """Minimum versions the packaged binaries require, keyed by 'glibc' / 'libstdc++ (gcc)'."""
    floors = manifest["minimum_versions"]
    gcc = table["glibcxx_to_gcc"].get(floors["glibcxx"])
    if gcc is None:
        raise ValueError("Unknown libstdc++ symbol version %s; extend glibcxx_to_gcc" % floors["glibcxx"])
    return {"glibc": floors["glibc"], "glibcxx": gcc}


def dependencies_for(manifest: dict, table: dict, family: str) -> list[str]:
    """Declared dependencies for 'deb' or 'rpm': mapped libraries, floors, and unversioned needs."""
    libraries = sorted(set(manifest["system_dependencies"]) | set(manifest["dlopened_dependencies"]))
    unmapped = [name for name in libraries if name not in table["libraries"]]
    if unmapped:
        raise ValueError("System libraries without a package mapping (add them to "
                         "packaging/system-dependencies.json): " + ", ".join(unmapped))
    packages = {table["libraries"][name][family] for name in libraries}
    packages |= set(table["unversioned"][family])
    versions = floor_constraint(manifest, table)
    constraints = {table["version_floors"]["glibc"][family]: versions["glibc"],
                   table["version_floors"]["glibcxx"][family]: versions["glibcxx"]}
    packages |= set(constraints)  # the C and C++ runtimes are always needed, whatever the linked list says
    declared = []
    for package in sorted(packages):
        if package in constraints:
            declared.append(f"{package} (>= {constraints[package]})" if family == "deb"
                            else f"{package} >= {constraints[package]}")
        else:
            declared.append(package)
    return declared


def artifact_names(version: str, release: str) -> dict[str, str]:
    return {"tar": f"{PACKAGE_NAME}-{version}-linux-x64.tar.gz",
            "deb": f"{PACKAGE_NAME}_{version}-{release}_amd64.deb",
            "rpm": f"{PACKAGE_NAME}-{version}-{release}.x86_64.rpm"}


def assemble_root(staged: Path, root: Path, version: str, epoch: int) -> None:
    """The filesystem both DEB and RPM install: /opt tree, /usr/bin link, desktop integration."""
    shutil.copytree(staged, root / INSTALL_PREFIX.lstrip("/"), symlinks=True)
    (root / "usr/bin").mkdir(parents=True)
    (root / BIN_LINK.lstrip("/")).symlink_to(os.path.relpath(f"{INSTALL_PREFIX}/{PACKAGE_NAME}", "/usr/bin"))
    linux = PACKAGING / "linux"
    for destination, source in ((DESKTOP, f"{APPLICATION_ID}.desktop"), (ICON, f"{APPLICATION_ID}.svg"),
                                (DOC, "copyright")):
        target = root / destination
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(linux / source, target)
    metainfo = root / METAINFO
    metainfo.parent.mkdir(parents=True, exist_ok=True)
    date = time.strftime("%Y-%m-%d", time.gmtime(epoch))
    metainfo.write_text((linux / f"{APPLICATION_ID}.metainfo.xml.in").read_text()
                        .replace("@VERSION@", version).replace("@DATE@", date))
    opt = root / INSTALL_PREFIX.lstrip("/")
    for path in sorted(root.rglob("*"), reverse=True):
        if path.is_symlink():
            os.utime(path, (epoch, epoch), follow_symlinks=False)
            continue
        if path.is_dir():
            path.chmod(0o755)
        elif not path.is_relative_to(opt):
            path.chmod(0o644)
        os.utime(path, (epoch, epoch))
    os.utime(root, (epoch, epoch))


def installed_kib(root: Path) -> int:
    total = sum(path.lstat().st_size for path in root.rglob("*") if path.is_file() and not path.is_symlink())
    return -(-total // 1024)


def deb_description(metadata: dict) -> str:
    lines = [metadata["summary"]]
    for index, paragraph in enumerate(metadata["description"]):
        if index:
            lines.append(" .")
        lines.append(" " + paragraph)
    return "\n".join(lines)


def write_deb_control(directory: Path, metadata: dict, manifest: dict, table: dict, version: str, kib: int) -> None:
    control = [
        f"Package: {PACKAGE_NAME}", f"Version: {version}-{metadata['release']}", "Architecture: amd64",
        f"Maintainer: {metadata['maintainer']}", f"Installed-Size: {kib}",
        "Depends: " + ", ".join(dependencies_for(manifest, table, "deb")),
        "Recommends: " + ", ".join(table["recommends"]["deb"]),
        f"Section: {metadata['section']}", "Priority: optional", f"Homepage: {metadata['homepage']}",
        "Description: " + deb_description(metadata),
    ]
    directory.mkdir(parents=True, exist_ok=True)
    (directory / "control").write_text("\n".join(control) + "\n")


def build_deb(root: Path, work: Path, output: Path, metadata: dict, manifest: dict, table: dict, version: str,
              epoch: int) -> Path:
    deb_root = work / "deb-root"
    shutil.copytree(root, deb_root, symlinks=True)
    control_dir = deb_root / "DEBIAN"
    write_deb_control(control_dir, metadata, manifest, table, version, installed_kib(root))
    lines = []
    for path in sorted(p for p in root.rglob("*") if p.is_file() and not p.is_symlink()):
        lines.append(f"{hashlib.md5(path.read_bytes()).hexdigest()}  {path.relative_to(root)}")
    (control_dir / "md5sums").write_text("\n".join(lines) + "\n")
    control_dir.chmod(0o755)
    for name in ("control", "md5sums"):
        (control_dir / name).chmod(0o644)
        os.utime(control_dir / name, (epoch, epoch))
    destination = output / artifact_names(version, metadata["release"])["deb"]
    subprocess.run(["dpkg-deb", "--root-owner-group", "-Zxz", "-z6", "--build", str(deb_root), str(destination)],
                   check=True, env={**os.environ, "SOURCE_DATE_EPOCH": str(epoch)})
    return destination


def rpm_spec(metadata: dict, manifest: dict, table: dict, version: str) -> str:
    requires = "\n".join("Requires:       " + dependency for dependency in dependencies_for(manifest, table, "rpm"))
    recommends = "\n".join("Recommends:     " + name for name in table["recommends"]["rpm"])
    description = "\n\n".join(metadata["description"])
    return f"""# Generated by tools/package/build_packages.py from packaging/metadata.json. Do not edit.
# The payload is the already-verified staged tree: no stripping, byte rewriting or dependency
# generation, so installed bytes equal manifest.json and no bundled Qt soname becomes a Requires.
%global debug_package %{{nil}}
%global _build_id_links none
%global __os_install_post %{{nil}}
%global __arch_install_post %{{nil}}
%global _binary_payload w6.xzdio

Name:           {PACKAGE_NAME}
Version:        {version}
Release:        {metadata['release']}
Summary:        {metadata['summary']}
License:        {metadata['license']}
URL:            {metadata['homepage']}
ExclusiveArch:  x86_64
AutoReqProv:    no
{requires}
{recommends}

%description
{description}

%install
mkdir -p %{{buildroot}}
cp -a %{{hdb_root}}/. %{{buildroot}}/

%files
%defattr(-,root,root,-)
{INSTALL_PREFIX}
{BIN_LINK}
/{DESKTOP}
/{ICON}
/{METAINFO}
%dir {DOC_DIR}
/{DOC}
"""


def build_rpm(root: Path, work: Path, output: Path, metadata: dict, manifest: dict, table: dict, version: str,
              epoch: int) -> Path:
    spec = work / f"{PACKAGE_NAME}.spec"
    spec.write_text(rpm_spec(metadata, manifest, table, version))
    top = work / "rpmbuild"
    subprocess.run(["rpmbuild", "-bb", "--noclean", str(spec), "--define", f"_topdir {top}",
                    "--define", f"hdb_root {root}", "--define", "_buildhost reproducible",
                    "--define", "use_source_date_epoch_as_buildtime 1", "--define", "clamp_mtime_to_source_date_epoch 1",
                    "--define", "source_date_epoch_from_changelog 0"],
                   check=True, env={**os.environ, "SOURCE_DATE_EPOCH": str(epoch)})
    built = next((top / "RPMS").rglob("*.rpm"))
    destination = output / artifact_names(version, metadata["release"])["rpm"]
    shutil.copyfile(built, destination)
    return destination


def build_tar(staged: Path, output: Path, version: str, release: str, epoch: int) -> Path:
    destination = output / artifact_names(version, release)["tar"]

    def normalise(info: tarfile.TarInfo) -> tarfile.TarInfo:
        info.uid = info.gid = 0
        info.uname = info.gname = "root"
        info.mtime = epoch
        return info

    with destination.open("wb") as raw, gzip.GzipFile(fileobj=raw, mode="wb", mtime=epoch, filename="") as zipped:
        with tarfile.open(fileobj=zipped, mode="w", format=tarfile.PAX_FORMAT) as tar:
            tar.add(staged, arcname=staged.name, filter=normalise)
    return destination


def build(staged: Path, output: Path, formats: tuple[str, ...]) -> dict:
    staged = staged.resolve(strict=True)
    manifest = json.loads((staged / "manifest.json").read_text())
    verify_manifest(staged, manifest)
    audit_tree(staged)
    if manifest.get("distribution_cleared") is not False:
        raise ValueError("The staged manifest must record distribution_cleared: false")
    if manifest.get("bundled_resale_data") is not False:
        raise ValueError("The package must not bundle resale data; the application reads the Worker API")
    metadata, table = load_metadata(), load_dependency_table()
    version = manifest["version"]
    if staged.name != f"{PACKAGE_NAME}-{version}-linux-x64":
        raise ValueError("Staged directory name does not match the manifest version: " + staged.name)
    epoch = source_date_epoch()
    output.mkdir(parents=True, exist_ok=True)
    produced = {}
    with tempfile.TemporaryDirectory(prefix="hdb-package-") as temp:
        work = Path(temp)
        root = work / "root"
        if {"deb", "rpm"} & set(formats):
            assemble_root(staged, root, version, epoch)
        if "tar" in formats:
            produced["tar"] = build_tar(staged, output, version, metadata["release"], epoch)
        if "deb" in formats:
            produced["deb"] = build_deb(root, work, output, metadata, manifest, table, version, epoch)
        if "rpm" in formats:
            produced["rpm"] = build_rpm(root, work, output, metadata, manifest, table, version, epoch)
    summary = {"version": version, "release": metadata["release"], "source_date_epoch": epoch,
               "distribution_cleared": False,
               "artifacts": {kind: {"file": path.name, "bytes": path.stat().st_size,
                                    "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}
                             for kind, path in produced.items()}}
    (output / "SHA256SUMS").write_text("".join(f"{a['sha256']}  {a['file']}\n" for a in summary["artifacts"].values()))
    (output / "packages.json").write_text(json.dumps(summary, indent=2) + "\n")
    return summary


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--staged", type=Path, required=True, help="directory produced by stage_linux.py")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--format", action="append", choices=FORMATS, help="repeatable; default all")
    args = parser.parse_args()
    try:
        print(json.dumps(build(args.staged, args.output, tuple(args.format or FORMATS)), indent=2))
    except (OSError, ValueError, KeyError, StopIteration, subprocess.CalledProcessError) as error:
        print("Package construction failed: " + repr(error), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
