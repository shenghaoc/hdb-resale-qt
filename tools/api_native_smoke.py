#!/usr/bin/env python3
"""Run the native Qt API smoke checks; retain text only, never map imagery.

Requires a real graphical session and a built HdbResale.App. Production mode only
uses the application's GET routes. No server deployment or database write occurs.
Run from a macOS development bundle, it also checks the identity macOS shows for it.
"""
import argparse
import http.server
import os
import pathlib
import plistlib
import socket
import subprocess
import sys
import threading

from api_fixture_server import Handler

GATES = {"high-zoom": "HDB_API_HIGH_ZOOM_PASS", "unreachable": "HDB_API_UNREACHABLE_PASS",
         "tile-failure": "HDB_API_TILE_NOTICE_PASS", "keyboard": "HDB_API_KEYBOARD_PASS",
         # The keyboard gate taking the other desktops' branches (no menu bar, list-item rows, About in the window) on
         # any platform; Qt's own platform defaults stay the host's.
         "keyboard-other-desktop": "HDB_API_KEYBOARD_PASS"}
# Qt Location fetches a failing tile once and retries it five times before giving up.
ATTEMPTS_PER_EXHAUSTED_TILE = 6
# Finder, the Dock and Cmd-Tab name an app after its bundle's file name, the menu bar after CFBundleName, and the
# icon comes from CFBundleIconFile in Contents/Resources. (LaunchServices reports CFBundleName as the running app's
# display name, so it cannot tell a misnamed bundle apart.)
BUNDLE_NAME = "HDB Resale Explorer"
BUNDLE_IDENTIFIER = "io.github.shenghaoc.hdb-resale-qt"
# The elements iconutil writes for a complete iconset, 16 to 1024 pixels.
ICON_ELEMENTS = {"ic04", "ic05", "ic07", "ic08", "ic09", "ic10", "ic11", "ic12", "ic13", "ic14"}


class FailingTiles(http.server.BaseHTTPRequestHandler):
    """Answers every tile request 503, so Qt exhausts its retries without any map imagery."""
    lock = threading.Lock()
    requests = 0

    def do_GET(self) -> None:
        with FailingTiles.lock:
            FailingTiles.requests += 1
        self.send_response(503)
        self.send_header("content-length", "0")
        self.end_headers()

    def log_message(self, *args) -> None:
        pass


def mac_bundle(executable: pathlib.Path) -> pathlib.Path | None:
    contents = executable.resolve().parents[1]
    if sys.platform == "darwin" and contents.name == "Contents" and contents.parent.suffix == ".app":
        return contents.parent
    return None


def icon_elements(icns: pathlib.Path) -> set[str]:
    """The element types in an .icns file; none if it is missing or malformed."""
    data = icns.read_bytes() if icns.is_file() else b""
    if data[:4] != b"icns" or int.from_bytes(data[4:8], "big") != len(data):
        return set()
    found, offset = set(), 8
    while offset + 8 <= len(data):
        length = int.from_bytes(data[offset + 4:offset + 8], "big")
        if length < 8:
            break
        found.add(data[offset:offset + 4].decode("latin-1"))
        offset += length
    return found


def bundle_problems(bundle: pathlib.Path) -> list[str]:
    info = plistlib.loads((bundle / "Contents/Info.plist").read_bytes())
    problems = [] if bundle.stem == BUNDLE_NAME else [f"the bundle is named {bundle.stem!r}"]
    problems += [f"{key} is {info.get(key)!r}" for key in ("CFBundleName", "CFBundleDisplayName")
                 if info.get(key) != BUNDLE_NAME]
    if info.get("CFBundleIdentifier") != BUNDLE_IDENTIFIER:
        problems.append(f"CFBundleIdentifier is {info.get('CFBundleIdentifier')!r}")
    icon = info.get("CFBundleIconFile")
    missing = ICON_ELEMENTS - (icon_elements(bundle / "Contents/Resources" / (pathlib.Path(icon).stem + ".icns"))
                               if icon else set())
    if missing:
        problems.append(f"icon {icon!r} lacks {', '.join(sorted(missing))}")
    return problems


def serve(handler) -> http.server.ThreadingHTTPServer:
    server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), handler)
    threading.Thread(target=server.serve_forever, daemon=True).start()
    return server


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--executable", type=pathlib.Path, required=True)
    parser.add_argument("--mode", choices=("recorded", "production", *GATES), required=True)
    parser.add_argument("--log", type=pathlib.Path, required=True)
    args = parser.parse_args()
    env = os.environ.copy()
    for name in ("HDB_API_BASE_URL", "HDB_PACKAGE_SMOKE", "HDB_API_GATE", "HDB_TILE_TEST", "HDB_TEST_TILE_ENDPOINT"):
        env.pop(name, None)
    servers = []
    refused = None
    try:
        if args.mode in ("recorded", "high-zoom", "tile-failure", "keyboard", "keyboard-other-desktop"):
            servers.append(serve(Handler))
            env["HDB_API_BASE_URL"] = f"http://127.0.0.1:{servers[-1].server_port}/"
        elif args.mode == "unreachable":
            # Reserve the port without listening, so no unrelated server can answer it.
            refused = socket.socket()
            refused.bind(("127.0.0.1", 0))
            env["HDB_API_BASE_URL"] = f"http://127.0.0.1:{refused.getsockname()[1]}/"
        if args.mode == "tile-failure":
            # The app gives a tile-test launch a fresh cache of its own, so no earlier tile hides a failure.
            servers.append(serve(FailingTiles))
            env["HDB_TILE_TEST"] = "1"
            env["HDB_TEST_TILE_ENDPOINT"] = f"http://127.0.0.1:{servers[-1].server_port}/"
        if args.mode in GATES:
            env["HDB_API_GATE"] = args.mode
            markers = [GATES[args.mode]]
        else:
            env["HDB_PACKAGE_SMOKE"] = "1"
            markers = ["HDB_PACKAGE_SHELL", "HDB_PACKAGE_DATA", "HDB_PACKAGE_MAP_READY", "HDB_PACKAGE_CHART_READY"]
        args.log.parent.mkdir(parents=True, exist_ok=True)
        with args.log.open("w") as output:
            result = subprocess.run([str(args.executable.resolve())], env=env, stdout=output,
                                    stderr=subprocess.STDOUT, timeout=45)
        log = args.log.read_text()
        if result.returncode != 0 or any(marker not in log for marker in markers) or any(error in log for error in
                ("_FAIL", "TypeError:", "ReferenceError:", "Unhandled exception", "ArgumentException")):
            raise SystemExit(f"FAIL {args.mode}: exit={result.returncode}; inspect {args.log}")
        bundle = mac_bundle(args.executable)
        problems = bundle_problems(bundle) if bundle else []
        if problems:
            raise SystemExit(f"FAIL {args.mode}: {bundle.name}: " + "; ".join(problems))
        if args.mode == "unreachable" and "HDB_PACKAGE_DATA" in log:
            raise SystemExit("FAIL unreachable API claimed data readiness")
        if args.mode == "tile-failure" and FailingTiles.requests < 2 * ATTEMPTS_PER_EXHAUSTED_TILE:
            raise SystemExit(f"FAIL tile-failure: only {FailingTiles.requests} tile requests failed")
        detail = f" ({FailingTiles.requests} tile requests answered 503)" if args.mode == "tile-failure" else ""
        if bundle:
            detail += f"; bundle {bundle.name!r}"
        print(f"PASS {args.mode}: exit=0; " + ", ".join(markers) + detail)
    finally:
        for server in servers:
            server.shutdown()
            server.server_close()
        if refused:
            refused.close()


if __name__ == "__main__":
    main()
