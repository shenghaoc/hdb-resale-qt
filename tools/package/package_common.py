#!/usr/bin/env python3
"""Facts and checks shared by Linux staging, package construction and package verification.

Nothing here knows how a package format is built. It answers three questions the
formats must agree on: what version this is, what the staged tree must contain
(and must never contain), and whether a tree still matches its manifest.
"""
from __future__ import annotations

import hashlib
import os
from pathlib import Path
import re
import stat
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / "src/HdbResale.App/HdbResale.App.csproj"

PRODUCT = "HDB Resale Explorer"
PACKAGE_NAME = "hdb-resale-explorer"
APPLICATION_ID = "io.github.shenghaoc.hdb-resale-qt"
INSTALL_PREFIX = "/opt/" + PACKAGE_NAME
ARCH = "x86_64"
SEMVER = re.compile(r"[0-9]+\.[0-9]+\.[0-9]+")

# The verified runtime stack. Changing any of these is a deliberate native re-validation,
# not a routine update (see packaging/README.md and the Dependabot configuration).
PINS = {"qt": "6.12.0", "bridge": "0.4.0-beta", "dotnet_runtime": "10.0.12", "icu": "73"}

# The application reads the Worker API. None of the old local-corpus or developer data may ship.
FORBIDDEN_TOP_LEVEL = {"data", "tests", "src", "docs", "experiments", "tools", "obj", "bin", ".git", ".local"}
FORBIDDEN_SUFFIXES = (".csv", ".geojson", ".parquet", ".sqlite", ".sqlite3", ".db", ".mbtiles", ".pmtiles",
                      ".cs", ".csproj", ".sln", ".slnx", ".nupkg", ".pdb", ".pem", ".key", ".p12", ".pfx",
                      ".debug", ".a", ".prl", ".pyc")
FORBIDDEN_NAMES = {".env", ".gitignore", "global.json", "nuget.config", "nuget.config.user", "provenance.json",
                   "transactions.csv"}
FORBIDDEN_PARTS = {"__pycache__", ".git", ".nuget", "tiles", "tile-cache", "qmltooling", "egldeviceintegrations"}
# Text files in the tree may not name the build machine.
BUILD_PATH_MARKERS = (re.compile(rb"/home/[^/\s\"']+/"), re.compile(rb"/Users/[^/\s\"']+/"),
                      re.compile(rb"/\.nuget/"), re.compile(rb"/opt/Qt/"), re.compile(rb"/runner/work/"))


def application_version(project: Path = PROJECT) -> str:
    """The one authoritative application version: the project's <Version>.

    AssemblyVersion/FileVersion must agree, so a bump cannot leave package metadata stale.
    """
    tree = ET.parse(project)
    version = tree.findtext(".//Version")
    if not version or not SEMVER.fullmatch(version):
        raise ValueError("The project <Version> must be a plain X.Y.Z version")
    for name in ("AssemblyVersion", "FileVersion"):
        if tree.findtext(f".//{name}") != version + ".0":
            raise ValueError(f"{name} must equal <Version>.0 so one version is authoritative")
    return version


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def tree_entries(root: Path) -> tuple[dict[str, str], dict[str, str], dict[str, str]]:
    """(file sha256, file mode, symlink target) by relative path, for a staged or installed tree.

    manifest.json describes everything else, so it is not part of its own listing.
    """
    files: dict[str, str] = {}
    modes: dict[str, str] = {}
    links: dict[str, str] = {}
    for path in sorted(root.rglob("*")):
        relative = str(path.relative_to(root))
        if relative == "manifest.json":
            continue
        if path.is_symlink():
            links[relative] = os.readlink(path)
        elif path.is_file():
            files[relative] = sha256(path)
            modes[relative] = format(stat.S_IMODE(path.lstat().st_mode), "04o")
    return files, modes, links


def forbidden_reason(relative: str) -> str | None:
    """Why a staged path may never ship, or None. Path rules only; see scan_text_for_build_paths."""
    parts = Path(relative).parts
    name = parts[-1].lower()
    if parts[0] in FORBIDDEN_TOP_LEVEL:
        return "top-level directory is development or local-corpus content"
    if name in FORBIDDEN_NAMES:
        return "file name is local data, credentials or repository configuration"
    if name.endswith(FORBIDDEN_SUFFIXES):
        return "file type is data, source, credential or debug material"
    if FORBIDDEN_PARTS.intersection(parts):
        return "directory is a cache, repository or debug-only Qt tooling"
    return None


def scan_text_for_build_paths(root: Path, relatives: list[str]) -> list[str]:
    """Config/launcher text naming the build machine would make the tree depend on it."""
    findings = []
    for relative in relatives:
        path = root / relative
        if path.suffix not in (".json", ".conf", ".txt", ".md", "") or path.stat().st_size > 2_000_000:
            continue
        data = path.read_bytes()
        if b"\x00" in data[:4096]:
            continue
        if relative.startswith("licenses/"):
            continue  # Licence prose legitimately contains URLs and paths.
        if any(marker.search(data) for marker in BUILD_PATH_MARKERS):
            findings.append(relative)
    return findings


def audit_tree(root: Path) -> None:
    """Fail on any file the API-era package must not contain."""
    problems = []
    relatives = []
    for path in sorted(root.rglob("*")):
        relative = str(path.relative_to(root))
        if path.is_file() or path.is_symlink():
            relatives.append(relative)
            reason = forbidden_reason(relative)
            if reason:
                problems.append(f"{relative}: {reason}")
    problems += [f"{r}: names the build machine" for r in scan_text_for_build_paths(root, relatives)]
    if problems:
        raise ValueError("Forbidden package content:\n  " + "\n  ".join(problems))


def verify_manifest(root: Path, manifest: dict) -> None:
    """The tree must be exactly what staging recorded: no changed, missing, extra or re-moded file."""
    files, modes, links = tree_entries(root)
    expected = manifest["files"]
    for relative in [*expected, *manifest["symlinks"]]:
        if Path(relative).is_absolute() or ".." in Path(relative).parts:
            raise ValueError("Manifest path escapes the package: " + relative)
    missing = sorted(set(expected) - set(files))
    extra = sorted(set(files) - set(expected))
    changed = sorted(r for r in expected if r in files and files[r] != expected[r])
    remoded = sorted(r for r in expected if r in modes and modes[r] != manifest["modes"][r])
    if missing or extra or changed or remoded or links != manifest["symlinks"]:
        detail = []
        for label, items in (("missing", missing), ("unlisted", extra), ("hash changed", changed),
                             ("mode changed", remoded)):
            if items:
                detail.append(f"{label}: {', '.join(items[:5])}" + (" ..." if len(items) > 5 else ""))
        if links != manifest["symlinks"]:
            detail.append("symlinks differ from the manifest")
        raise ValueError("Package tree does not match its manifest (" + "; ".join(detail) + ")")
