#!/usr/bin/env python3
"""Package-manager level facts about an installed (or removed) HDB Resale Explorer package.

Runs inside the clean test container, after verify_installed.sh has installed the package:

    verify_installed.py <deb|rpm> --packages DIR --report FILE   installed metadata, dependency
                                                                 mapping, desktop integration
    verify_installed.py <deb|rpm> --removed                      after uninstall: nothing left

Facts come from dpkg / rpm / ldconfig / desktop-file-validate / appstreamcli, never from this
repository's own bookkeeping, so a wrong declaration cannot confirm itself.
"""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))

from package_common import APPLICATION_ID, INSTALL_PREFIX, PACKAGE_NAME  # noqa: E402

ROOT = Path(INSTALL_PREFIX)
OWNED = (INSTALL_PREFIX, f"/usr/bin/{PACKAGE_NAME}",
         f"/usr/share/applications/{APPLICATION_ID}.desktop",
         f"/usr/share/icons/hicolor/scalable/apps/{APPLICATION_ID}.svg",
         f"/usr/share/metainfo/{APPLICATION_ID}.metainfo.xml", f"/usr/share/doc/{PACKAGE_NAME}")


def capture(*command: str) -> str:
    return subprocess.run(command, text=True, capture_output=True, check=True).stdout


def installed_version(family: str) -> tuple[str, str]:
    if family == "deb":
        version, arch = capture("dpkg-query", "-W", "-f=${Version} ${Architecture}", PACKAGE_NAME).split()
    else:
        version, arch = capture("rpm", "-q", "--qf", "%{VERSION}-%{RELEASE} %{ARCH}", PACKAGE_NAME).split()
    return version, arch


def owner(family: str, path: str) -> str:
    candidates = [path, os.path.realpath(path)]
    for candidate in candidates:
        if family == "deb":
            result = subprocess.run(["dpkg", "-S", candidate], text=True, capture_output=True)
            if result.returncode == 0:
                return result.stdout.split(":")[0]
        else:
            result = subprocess.run(["rpm", "-qf", "--qf", "%{NAME}\n", candidate], text=True, capture_output=True)
            if result.returncode == 0:
                return result.stdout.splitlines()[0]
    raise ValueError("No installed package owns " + path)


def resolved_library(name: str) -> str:
    for line in capture("ldconfig", "-p").splitlines():
        fields = line.split()
        if fields and fields[0] == name and "x86-64" in line:
            return fields[-1]
    raise ValueError("ldconfig does not know " + name)


def mapping_mismatches(family: str, libraries: dict, wanted: list[str]) -> list[str]:
    problems = []
    for name in wanted:
        expected = libraries[name][family]
        try:
            found = owner(family, resolved_library(name))
        except (ValueError, subprocess.CalledProcessError) as error:
            problems.append(f"{name}: {error}")
            continue
        if found != expected:
            problems.append(f"{name}: provided by {found}, but packaging/system-dependencies.json says {expected}")
    return problems


def verify_installed(family: str, packages: Path, report: Path) -> None:
    summary = json.loads((packages / "packages.json").read_text())
    expected = f"{summary['version']}-{summary['release']}"
    version, arch = installed_version(family)
    arch_ok = arch in ("amd64", "x86_64")
    if version != expected or not arch_ok:
        raise ValueError(f"Installed {version} {arch}, expected {expected} amd64/x86_64")
    manifest = json.loads((ROOT / "manifest.json").read_text())
    if manifest["version"] != summary["version"] or manifest["distribution_cleared"] is not False:
        raise ValueError("Installed manifest disagrees with the package metadata")

    table = json.loads((Path(__file__).resolve().parents[2] / "packaging/system-dependencies.json").read_text())
    wanted = [line for line in (ROOT / "system-libraries.txt").read_text().splitlines() if line and line[0] != "#"]
    problems = mapping_mismatches(family, table["libraries"], wanted)
    if problems:
        raise ValueError("Dependency mapping is wrong for this distribution:\n  " + "\n  ".join(problems))

    launcher = Path("/usr/bin") / PACKAGE_NAME
    if Path(os.path.realpath(launcher)) != ROOT / PACKAGE_NAME or not os.access(launcher, os.X_OK):
        raise ValueError("The launcher on PATH does not resolve to the installed tree")
    if shutil.which(PACKAGE_NAME) != str(launcher):
        raise ValueError("The launcher is not found on PATH")
    desktop = f"/usr/share/applications/{APPLICATION_ID}.desktop"
    capture("desktop-file-validate", desktop)
    capture("appstreamcli", "validate", "--no-net", f"/usr/share/metainfo/{APPLICATION_ID}.metainfo.xml")
    if f"Exec={PACKAGE_NAME}" not in Path(desktop).read_text():
        raise ValueError("The desktop entry does not start the installed launcher")
    for path in OWNED[2:]:
        if not Path(path).exists():
            raise ValueError("Missing installed file: " + path)
    release = Path("/etc/os-release").read_text()
    facts = {"passed": True, "distribution": {line.split("=", 1)[0]: line.split("=", 1)[1].strip('"')
                                              for line in release.splitlines() if "=" in line and
                                              line.split("=", 1)[0] in ("ID", "VERSION_ID", "PRETTY_NAME")},
             "format": family, "package_version": version, "architecture": arch,
             "libraries_checked_against_package_manager": len(wanted), "desktop_file_valid": True,
             "appstream_valid": True, "distribution_cleared": False}
    report.write_text(json.dumps(facts, indent=2) + "\n")
    print(json.dumps(facts, indent=2))


def verify_removed(family: str) -> None:
    left = [path for path in OWNED if os.path.lexists(path)]
    if left:
        raise ValueError("Files remain after uninstall: " + ", ".join(left))
    command = ["dpkg-query", "-W", PACKAGE_NAME] if family == "deb" else ["rpm", "-q", PACKAGE_NAME]
    if subprocess.run(command, capture_output=True).returncode == 0:
        raise ValueError("The package manager still lists the package")
    print("Uninstall left no application files behind.")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("family", choices=("deb", "rpm"))
    parser.add_argument("--packages", type=Path)
    parser.add_argument("--report", type=Path)
    parser.add_argument("--removed", action="store_true")
    args = parser.parse_args()
    try:
        if args.removed:
            verify_removed(args.family)
        else:
            verify_installed(args.family, args.packages, args.report)
    except (OSError, ValueError, KeyError, subprocess.CalledProcessError) as error:
        print("Installed-package verification FAILED: " + str(error), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
