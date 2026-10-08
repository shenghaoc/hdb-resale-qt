#!/usr/bin/env python3
"""Launch an installed or relocated package against the recorded Worker API in a clean environment.

The package under test is started through its launcher (for an installed package, the
launcher on PATH), from an unrelated working directory, with fresh HOME/XDG state and every
inherited Qt, .NET, loader, data and test override removed. The only deliberate input is
HDB_API_BASE_URL, which points at tools/api_fixture_server.py's recorded responses, so the
check never depends on the production Worker.

It tests shell/data/map-engine/chart readiness and clean teardown over real X11. It does not
test tile pixels, network success, visual quality, Wayland or distributions it was not run on.
"""
from __future__ import annotations

import argparse
import http.server
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import threading
import time

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
sys.path.insert(0, str(Path(__file__).resolve().parent))

from package_common import verify_manifest  # noqa: E402

MARKERS = ("HDB_PACKAGE_SHELL", "HDB_PACKAGE_DATA", "HDB_PACKAGE_MAP_READY",
           "HDB_PACKAGE_CHART_READY", "HDB_PACKAGE_EXIT")
FAILURES = re.compile(
    r"error while loading shared libraries|is not installed|is not a type|"
    r"QQmlApplicationEngine failed|Could not find the Qt platform plugin|"
    r"This application failed to start|TypeError|ReferenceError|Binding loop|"
    r"Unable to assign|Cannot assign|Cannot read property|Unhandled exception|"
    r"HDB_PACKAGE_FAIL|HDB_PACKAGE_TIMEOUT|Error calling dotnet|Error loading library|"
    r"(?:No|Only prerelease) .*\.NET runtime|No functional TLS backend|"
    r"TLS initialization failed|Error loading metadata file|Failed to load app-local ICU|ASSERT|Aborted", re.I)
KEEP = ("DISPLAY", "XAUTHORITY", "XDG_RUNTIME_DIR", "DBUS_SESSION_BUS_ADDRESS",
        "XDG_SESSION_TYPE", "USER", "LOGNAME", "LANG", "LC_ALL")
# Libraries that must come from the package, and the proof that X11 (not offscreen) was used.
RUNTIME_LIBRARY = re.compile(
    r"/(?:libQt6[^/]*|libicu[^/]*|libcoreclr[^/]*|libhostfxr|libhostpolicy|libclrjit|libSystem\.[^/]*|libqt[^/]*"
    r"|libqxcb[^/]*|libhdb_[^/]*)\.so[^/]*$|/(?:qml|plugins)/.*\.so[^/]*$")
REQUIRED_MAPPED = ("libQt6Core.so", "libcoreclr.so", "libqtgeoservices_osm.so", "libQt6Graphs.so", "libqxcb.so")
# Things a package must never read: system Qt, a host .NET, and package-manager caches.
SYSTEM_RUNTIME_ROOTS = ("/usr/lib/qt6", "/usr/lib64/qt6", "/usr/lib/x86_64-linux-gnu/qt6", "/usr/share/qt6",
                        "/usr/share/dotnet", "/usr/lib/dotnet", "/usr/lib64/dotnet", "/etc/dotnet")


def clean_environment(original: dict[str, str], temporary: Path, api_base_url: str) -> dict[str, str]:
    result = {key: original[key] for key in KEEP if key in original}
    if "XAUTHORITY" not in result and original.get("HOME"):
        authority = Path(original["HOME"]) / ".Xauthority"
        if authority.is_file():
            result["XAUTHORITY"] = str(authority)
    result.update({"PATH": "/usr/bin:/bin", "HOME": str(temporary / "home"),
                   "XDG_CONFIG_HOME": str(temporary / "config"),
                   "XDG_CACHE_HOME": str(temporary / "cache"),
                   "XDG_DATA_HOME": str(temporary / "data"),
                   "HDB_API_BASE_URL": api_base_url,
                   "HDB_PACKAGE_SMOKE": "1", "QT_QPA_PLATFORM": "xcb",
                   "QT_FORCE_STDERR_LOGGING": "1", "QT_MESSAGE_PATTERN": "%{type}: %{message}"})
    return result


def verify(code: int, log: str, mapped: set[str], package: Path) -> None:
    if code != 0:
        raise ValueError("Packaged process exit was " + str(code))
    if FAILURES.search(log):
        raise ValueError("Loader/QML/runtime failure signature in launch log")
    observed = re.findall(r"HDB_PACKAGE_(?:SHELL|DATA|MAP_READY|CHART_READY|EXIT)\b", log)
    if observed != list(MARKERS):
        raise ValueError("Missing, duplicated or out-of-order readiness/exit markers: " + repr(observed))
    relevant = [p for p in mapped if RUNTIME_LIBRARY.search(p)]
    escaped = [p for p in relevant if not Path(p).is_relative_to(package)
               and not system_icu_pulled_in_by_system_library(p, mapped, package)]
    if escaped:
        raise ValueError("Qt/ICU/.NET/plugin library loaded from outside the package: " + repr(sorted(escaped)))
    for required in REQUIRED_MAPPED:
        if not any(required in p for p in relevant):
            raise ValueError("No mapped-library evidence for " + required)


def system_icu_pulled_in_by_system_library(library: str, mapped: set[str], package: Path) -> bool:
    """A host ICU is acceptable only when another host library links it (for example libxml2).

    ICU symbols carry their major version, so it coexists with the package's ICU 73. What must
    never happen is Qt or .NET choosing it, which would show as an ICU no host library explains.
    """
    name = Path(library).name
    if not name.startswith("libicu") or ".so." not in name:
        return False
    soname = name.split(".so.")[0] + ".so." + name.split(".so.")[1].split(".")[0]
    for other in mapped:
        if other == library or Path(other).is_relative_to(package) or not other.startswith("/"):
            continue
        try:
            if soname.encode() in Path(other).read_bytes():
                return True
        except OSError:
            continue
    return False


def forbidden_opens(trace: str, forbidden: tuple[str, ...]) -> list[str]:
    """Successful opens/executions under a forbidden prefix, from `strace -f -e trace=openat,execve` output."""
    hits = []
    for line in trace.splitlines():
        match = re.search(r'(?:openat|execve)\([^"]*"([^"]+)".*\)\s+=\s+(-?\d+)', line)
        if match and not match.group(2).startswith("-") and match.group(1).startswith(forbidden):
            hits.append(match.group(1))
    return sorted(set(hits))


def process_tree(root: int) -> set[int]:
    """The process and its descendants (strace, when used, is the root's child)."""
    parents = {}
    for stat in Path("/proc").glob("[0-9]*/stat"):
        try:
            parents[int(stat.parent.name)] = int(stat.read_text().rpartition(")")[2].split()[1])
        except (OSError, ValueError, IndexError):
            continue
    tree = {root}
    grew = True
    while grew:
        grew = False
        for pid, parent in parents.items():
            if parent in tree and pid not in tree:
                tree.add(pid)
                grew = True
    return tree


def mapped_files(pid: int) -> set[str]:
    found = set()
    try:
        for line in Path(f"/proc/{pid}/maps").read_text().splitlines():
            fields = line.split(maxsplit=5)
            if len(fields) == 6 and fields[5].startswith("/"):
                found.add(fields[5].removesuffix(" (deleted)"))
    except (OSError, ProcessLookupError):
        pass
    return found


def serve_recorded_api() -> http.server.ThreadingHTTPServer:
    from api_fixture_server import Handler
    server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), Handler)
    threading.Thread(target=server.serve_forever, daemon=True).start()
    return server


def check(package: Path, launcher: Path, log_path: Path, report_path: Path, timeout: float,
          api_base_url: str | None = None, forbid: tuple[str, ...] = (), trace: bool = False) -> dict:
    package = package.resolve(strict=True)
    manifest = json.loads((package / "manifest.json").read_text())
    # All shipped bytes must still match staging. Tests never rewrite the package.
    verify_manifest(package, manifest)
    log_path.parent.mkdir(parents=True, exist_ok=True)
    server = None if api_base_url else serve_recorded_api()
    api = api_base_url or f"http://127.0.0.1:{server.server_port}/"
    started = time.monotonic()
    mapped: set[str] = set()
    forbidden = tuple(forbid) + SYSTEM_RUNTIME_ROOTS
    code = None
    try:
        with tempfile.TemporaryDirectory(prefix="hdb-launch-", dir=log_path.parent) as temp:
            temporary = Path(temp)
            for name in ("home", "config", "cache", "data", "unrelated-cwd"):
                (temporary / name).mkdir(mode=0o700)
            environment = clean_environment(dict(os.environ), temporary, api)
            if not environment.get("DISPLAY"):
                raise ValueError("A real X11 DISPLAY is required; no offscreen substitution")
            command = [str(launcher)]
            trace_file = temporary / "opens.txt"
            if trace:
                if not shutil.which("strace"):
                    raise ValueError("strace is required for the file-access check")
                command = ["strace", "-f", "-qq", "-e", "trace=openat,execve", "-o", str(trace_file), *command]
            with log_path.open("w") as log:
                child = subprocess.Popen(command, cwd=temporary / "unrelated-cwd", env=environment,
                                         stdout=log, stderr=subprocess.STDOUT)
                try:
                    while child.poll() is None:
                        for pid in process_tree(child.pid):
                            mapped |= mapped_files(pid)
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
            opened = forbidden_opens(trace_file.read_text(errors="replace"), forbidden) if trace else []
        verify(code, log_path.read_text(errors="replace"), mapped, package)
        if opened:
            raise ValueError("Package read or executed build-tree/system runtime files: " + repr(opened))
    except ValueError as error:
        report_path.parent.mkdir(parents=True, exist_ok=True)
        report_path.write_text(json.dumps({"passed": False, "package": str(package), "exit_code": code,
            "reason": str(error), "mapped_libraries": sorted(mapped)}, indent=2) + "\n")
        raise
    finally:
        if server:
            server.shutdown()
            server.server_close()
    report = {"passed": True, "package": str(package), "launcher": str(launcher),
              "api": "recorded Worker API fixtures" if server else api,
              "elapsed_seconds": round(time.monotonic() - started, 3),
              "exit_code": code, "markers": list(MARKERS), "fresh_home_and_xdg": True,
              "clean_loader_environment": True, "unrelated_working_directory": True,
              "manifest_verified": True, "file_access_traced": trace, "forbidden_prefixes": list(forbidden),
              "mapped_libraries": sorted(p for p in mapped if RUNTIME_LIBRARY.search(p)),
              "tile_pixels_verified": False, "network_success_verified": False, "distribution_cleared": False}
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(report, indent=2) + "\n")
    return report


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--package", type=Path, required=True, help="the staged or installed package root")
    parser.add_argument("--launcher", type=Path, help="how a user starts it (default: <package>/hdb-resale-explorer)")
    parser.add_argument("--log", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--timeout", type=float, default=60)
    parser.add_argument("--api-base-url", help="override the recorded API (used for negative controls only)")
    parser.add_argument("--forbid", action="append", default=[], help="path prefix the package must not read")
    parser.add_argument("--trace-file-access", action="store_true", help="use strace to prove no forbidden reads")
    args = parser.parse_args()
    launcher = args.launcher or args.package / "hdb-resale-explorer"
    try:
        report = check(args.package, launcher, args.log, args.report, args.timeout, args.api_base_url,
                       tuple(args.forbid), args.trace_file_access)
        print("Package launch passed: shell/data/map engine/chart ready, native/.NET exit 0; "
              + str(report["elapsed_seconds"]) + " seconds. Tile pixels were not tested.")
    except (OSError, ValueError, subprocess.SubprocessError, KeyError) as error:
        print("Package launch FAILED: " + str(error), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
