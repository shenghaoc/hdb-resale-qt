#!/usr/bin/env python3
"""Launch a relocated private RC with fresh user state and no build environment.

The package bundles no data; the app reads the HDB Resale Explorer Worker API. By default
the launch reads the recorded API responses (tests/fixtures/worker-api) from a loopback
server. `--api production` reads the production API (GET requests only), and
`--api unreachable` checks that a refused API is reported without claiming readiness.
Tests shell/data/map-engine readiness and clean teardown, not tile pixels, visual quality
or cross-distribution compatibility.
"""
from __future__ import annotations

import argparse
import hashlib
import http.server
import json
import os
from pathlib import Path
import re
import socket
import subprocess
import sys
import tempfile
import threading
import time

MARKERS = ("HDB_PACKAGE_SHELL", "HDB_PACKAGE_DATA", "HDB_PACKAGE_MAP_READY",
           "HDB_PACKAGE_CHART_READY", "HDB_PACKAGE_EXIT")
UNREACHABLE_MARKERS = ("HDB_API_UNREACHABLE_PASS", "HDB_API_GATE_EXIT")
API_MODES = ("recorded", "production", "unreachable")
FAILURES = re.compile(
    r"error while loading shared libraries|is not installed|is not a type|"
    r"QQmlApplicationEngine failed|Could not find the Qt platform plugin|"
    r"This application failed to start|TypeError|ReferenceError|Binding loop|"
    r"Unable to assign|Cannot assign|Cannot read property|Unhandled exception|"
    r"HDB_PACKAGE_FAIL|HDB_PACKAGE_TIMEOUT|HDB_API_GATE_FAIL|Error calling dotnet|Error loading library|"
    r"(?:No|Only prerelease) .*\.NET runtime|No functional TLS backend|"
    r"TLS initialization failed|Error loading metadata file|Failed to load app-local ICU|ASSERT|Aborted", re.I)
KEEP = ("DISPLAY", "XAUTHORITY", "XDG_RUNTIME_DIR", "DBUS_SESSION_BUS_ADDRESS",
        "XDG_SESSION_TYPE", "USER", "LOGNAME", "LANG", "LC_ALL")


def clean_environment(original: dict[str, str], temporary: Path, api: str = "recorded",
                      base_url: str | None = None) -> dict[str, str]:
    """Only the X11 session survives; the API address comes from the launch mode, never the caller."""
    result = {key: original[key] for key in KEEP if key in original}
    if "XAUTHORITY" not in result and original.get("HOME"):
        authority = Path(original["HOME"]) / ".Xauthority"
        if authority.is_file():
            result["XAUTHORITY"] = str(authority)
    result.update({"PATH": "/usr/bin:/bin", "HOME": str(temporary / "home"),
                   "XDG_CONFIG_HOME": str(temporary / "config"),
                   "XDG_CACHE_HOME": str(temporary / "cache"),
                   "XDG_DATA_HOME": str(temporary / "data"),
                   "QT_QPA_PLATFORM": "xcb",
                   "QT_FORCE_STDERR_LOGGING": "1", "QT_MESSAGE_PATTERN": "%{type}: %{message}"})
    if api == "unreachable":
        result["HDB_API_GATE"] = "unreachable"
    else:
        result["HDB_PACKAGE_SMOKE"] = "1"
    if base_url is not None:
        result["HDB_API_BASE_URL"] = base_url
    return result


def verify(code: int, log: str, mapped: set[str], package: Path, api: str = "recorded") -> None:
    if code != 0:
        raise ValueError("Packaged process exit was " + str(code))
    if FAILURES.search(log):
        raise ValueError("Loader/QML/runtime failure signature in launch log")
    observed = re.findall(r"HDB_PACKAGE_(?:SHELL|DATA|MAP_READY|CHART_READY|EXIT)\b|HDB_API_(?:UNREACHABLE_PASS|GATE_EXIT)\b", log)
    expected = UNREACHABLE_MARKERS if api == "unreachable" else MARKERS
    if observed != list(expected):
        raise ValueError("Missing, duplicated, out-of-order or unexpected markers: " + repr(observed))
    relevant = [p for p in mapped if re.search(r"/(?:libQt6[^/]*|libicu[^/]*|libcoreclr\.so|libhostfxr\.so|libqtgeoservices_osm\.so)$", p)]
    escaped = [p for p in relevant if not Path(p).is_relative_to(package)]
    if escaped:
        raise ValueError("Qt/ICU/.NET/OSM library loaded from outside the package: " + repr(escaped))
    # Without data the chart, and so Qt Graphs, never loads.
    required_libraries = ("libQt6Core.so", "libcoreclr.so", "libqtgeoservices_osm.so") + (
        () if api == "unreachable" else ("libQt6Graphs.so",))
    for required in required_libraries:
        if not any(required in p for p in relevant):
            raise ValueError("No mapped-library evidence for " + required)


class ApiEndpoint:
    """The API address for a launch mode: recorded responses served on loopback, production, or a refused port."""

    def __init__(self, api: str):
        self.api, self.server, self.refused, self.base_url = api, None, None, None
        if api == "recorded":
            tools = str(Path(__file__).resolve().parents[1])
            if tools not in sys.path:
                sys.path.insert(0, tools)
            from api_fixture_server import Handler
            self.server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), Handler)
            threading.Thread(target=self.server.serve_forever, daemon=True).start()
            self.base_url = f"http://127.0.0.1:{self.server.server_port}/"
        elif api == "unreachable":
            # Reserve the port without listening, so no unrelated server can answer it.
            self.refused = socket.socket()
            self.refused.bind(("127.0.0.1", 0))
            self.base_url = f"http://127.0.0.1:{self.refused.getsockname()[1]}/"
        elif api != "production":
            raise ValueError("Unknown API mode " + api)

    def close(self) -> None:
        if self.server:
            self.server.shutdown()
            self.server.server_close()
        if self.refused:
            self.refused.close()


def check(package: Path, log_path: Path, report_path: Path, timeout: float, api: str = "recorded") -> dict:
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
    endpoint = ApiEndpoint(api)
    try:
        with tempfile.TemporaryDirectory(prefix="hdb-rc-launch-", dir=log_path.parent) as temp:
            temporary = Path(temp)
            for name in ("home", "config", "cache", "data", "unrelated-cwd"):
                (temporary / name).mkdir(mode=0o700)
            environment = clean_environment(dict(os.environ), temporary, api, endpoint.base_url)
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
            try:
                verify(code, log_path.read_text(errors="replace"), mapped, package, api)
            except ValueError as error:
                report_path.parent.mkdir(parents=True, exist_ok=True)
                report_path.write_text(json.dumps({"passed": False, "package": str(package), "api": api,
                    "exit_code": code, "reason": str(error), "mapped_libraries": sorted(mapped)}, indent=2) + "\n")
                raise
    finally:
        endpoint.close()
    report = {"passed": True, "package": str(package), "api": api,
              "api_base_url": endpoint.base_url or "production default",
              "elapsed_seconds": round(time.monotonic() - started, 3),
              "exit_code": code, "markers": list(UNREACHABLE_MARKERS if api == "unreachable" else MARKERS),
              "fresh_home_and_xdg": True, "clean_loader_environment": True, "unrelated_working_directory": True,
              "mapped_libraries": sorted(mapped), "tile_pixels_verified": False,
              "network_success_verified": api == "production", "distribution_cleared": False}
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(report, indent=2) + "\n")
    return report


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", type=Path, required=True)
    parser.add_argument("--log", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--timeout", type=float, default=30)
    parser.add_argument("--api", choices=API_MODES, default="recorded",
                        help="recorded loopback responses (default), the production API, or a refused port")
    args = parser.parse_args()
    try:
        report = check(args.package, args.log, args.report, args.timeout, args.api)
        if args.api == "unreachable":
            print("Package launch passed: a refused API was reported without data readiness, native/.NET exit 0; "
                  + str(report["elapsed_seconds"]) + " seconds.")
        else:
            print(f"Package launch passed with the {args.api} API: shell/data/map engine/chart ready, native/.NET exit 0; "
                  + str(report["elapsed_seconds"]) + " seconds. Tile pixels were not tested.")
    except (OSError, ValueError, subprocess.SubprocessError) as error:
        print("Package launch FAILED: " + str(error), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
