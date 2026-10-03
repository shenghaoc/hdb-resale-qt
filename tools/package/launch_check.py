#!/usr/bin/env python3
"""Launch a relocated private RC with fresh user state and no build environment.

Tests shell/data/map-engine readiness and clean teardown, not tile pixels,
network success, visual quality or cross-distribution compatibility.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import time

MARKERS = ("HDB_PACKAGE_SHELL", "HDB_PACKAGE_DATA", "HDB_PACKAGE_MAP_READY", "HDB_PACKAGE_EXIT")
FAILURES = re.compile(
    r"error while loading shared libraries|is not installed|is not a type|"
    r"QQmlApplicationEngine failed|Could not find the Qt platform plugin|"
    r"This application failed to start|TypeError|ReferenceError|Binding loop|"
    r"Unable to assign|Cannot assign|Cannot read property|Unhandled exception|"
    r"HDB_PACKAGE_FAIL|HDB_PACKAGE_TIMEOUT|Error calling dotnet|Error loading library|"
    r"(?:No|Only prerelease) .*\.NET runtime|No functional TLS backend|"
    r"TLS initialization failed|ASSERT|Aborted", re.I)
KEEP = ("DISPLAY", "XAUTHORITY", "XDG_RUNTIME_DIR", "DBUS_SESSION_BUS_ADDRESS",
        "XDG_SESSION_TYPE", "USER", "LOGNAME", "LANG", "LC_ALL")


def clean_environment(original: dict[str, str], temporary: Path) -> dict[str, str]:
    result = {key: original[key] for key in KEEP if key in original}
    if "XAUTHORITY" not in result and original.get("HOME"):
        authority = Path(original["HOME"]) / ".Xauthority"
        if authority.is_file():
            result["XAUTHORITY"] = str(authority)
    result.update({"PATH": "/usr/bin:/bin", "HOME": str(temporary / "home"),
                   "XDG_CONFIG_HOME": str(temporary / "config"),
                   "XDG_CACHE_HOME": str(temporary / "cache"),
                   "XDG_DATA_HOME": str(temporary / "data"),
                   "HDB_PACKAGE_SMOKE": "1", "QT_QPA_PLATFORM": "xcb",
                   "QT_FORCE_STDERR_LOGGING": "1", "QT_MESSAGE_PATTERN": "%{type}: %{message}"})
    return result


def verify(code: int, log: str, mapped: set[str], package: Path) -> None:
    if code != 0:
        raise ValueError("Packaged process exit was " + str(code))
    if FAILURES.search(log):
        raise ValueError("Loader/QML/runtime failure signature in launch log")
    observed = re.findall(r"HDB_PACKAGE_(?:SHELL|DATA|MAP_READY|EXIT)\b", log)
    if observed != list(MARKERS):
        raise ValueError("Missing, duplicated or out-of-order readiness/exit markers: " + repr(observed))
    relevant = [p for p in mapped if re.search(r"/(?:libQt6[^/]*|libicu[^/]*|libcoreclr\.so|libhostfxr\.so|libqtgeoservices_osm\.so)$", p)]
    if any(not Path(p).is_relative_to(package) for p in relevant):
        raise ValueError("Qt/ICU/.NET/OSM library loaded from outside the package")
    for required in ("libQt6Core.so", "libcoreclr.so", "libqtgeoservices_osm.so"):
        if not any(required in p for p in relevant):
            raise ValueError("No mapped-library evidence for " + required)


def check(package: Path, log_path: Path, report_path: Path, timeout: float) -> dict:
    package = package.resolve(strict=True)
    launcher = package / "hdb-resale-explorer"
    manifest = json.loads((package / "manifest.json").read_text())
    # All shipped bytes must still match staging. Tests never rewrite the package.
    for relative, digest in manifest["files"].items():
        candidate = package / relative
        if not candidate.resolve().is_relative_to(package):
            raise ValueError("Manifest path escapes package")
        if hashlib.sha256(candidate.read_bytes()).hexdigest() != digest:
            raise ValueError("Packaged file hash changed: " + relative)
    log_path.parent.mkdir(parents=True, exist_ok=True)
    started = time.monotonic()
    mapped: set[str] = set()
    with tempfile.TemporaryDirectory(prefix="hdb-rc-launch-", dir=log_path.parent) as temp:
        temporary = Path(temp)
        for name in ("home", "config", "cache", "data", "unrelated-cwd"):
            (temporary / name).mkdir(mode=0o700)
        environment = clean_environment(dict(os.environ), temporary)
        if not environment.get("DISPLAY"):
            raise ValueError("A real X11 DISPLAY is required; no offscreen substitution")
        with log_path.open("w") as log:
            child = subprocess.Popen([str(launcher)], cwd=temporary / "unrelated-cwd",
                                     env=environment, stdout=log, stderr=subprocess.STDOUT)
            try:
                while child.poll() is None:
                    try:
                        for line in Path(f"/proc/{child.pid}/maps").read_text().splitlines():
                            fields = line.split(maxsplit=5)
                            if len(fields) == 6 and fields[5].startswith("/"):
                                mapped.add(fields[5].removesuffix(" (deleted)"))
                    except (FileNotFoundError, ProcessLookupError):
                        pass
                    if time.monotonic() - started > timeout:
                        child.kill()
                        child.wait()
                        raise ValueError(f"Packaged process exceeded {timeout:g} seconds")
                    time.sleep(0.02)
                code = child.wait()
            finally:
                if child.poll() is None:
                    child.kill()
                    child.wait()
        verify(code, log_path.read_text(errors="replace"), mapped, package)
    report = {"passed": True, "package": str(package), "elapsed_seconds": round(time.monotonic() - started, 3),
              "exit_code": code, "markers": list(MARKERS), "fresh_home_and_xdg": True,
              "clean_loader_environment": True, "unrelated_working_directory": True,
              "mapped_libraries": sorted(mapped), "tile_pixels_verified": False,
              "network_success_verified": False, "distribution_cleared": False}
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(report, indent=2) + "\n")
    return report


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", type=Path, required=True)
    parser.add_argument("--log", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--timeout", type=float, default=30)
    args = parser.parse_args()
    try:
        report = check(args.package, args.log, args.report, args.timeout)
        print("Package launch passed: shell/data/map engine ready, native/.NET exit 0; "
              + str(report["elapsed_seconds"]) + " seconds. Tile pixels were not tested.")
    except (OSError, ValueError, subprocess.SubprocessError) as error:
        print("Package launch FAILED: " + str(error), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
