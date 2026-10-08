#!/usr/bin/env python3
"""Run the native Qt API smoke checks; retain text only, never map imagery.

Requires a real graphical session and a built HdbResale.App. Production mode only
uses the application's GET routes. No server deployment or database write occurs.
"""
import argparse
import http.server
import os
import pathlib
import socket
import subprocess
import threading

from api_fixture_server import Handler


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--executable", type=pathlib.Path, required=True)
    parser.add_argument("--mode", choices=("recorded", "production", "high-zoom", "unreachable"), required=True)
    parser.add_argument("--log", type=pathlib.Path, required=True)
    args = parser.parse_args()
    env = os.environ.copy()
    for name in ("HDB_API_BASE_URL", "HDB_PACKAGE_SMOKE", "HDB_API_GATE"):
        env.pop(name, None)
    server = None
    refused = None
    try:
        if args.mode in ("recorded", "high-zoom"):
            server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), Handler)
            threading.Thread(target=server.serve_forever, daemon=True).start()
            env["HDB_API_BASE_URL"] = f"http://127.0.0.1:{server.server_port}/"
        elif args.mode == "unreachable":
            # Reserve the port without listening, so no unrelated server can answer it.
            refused = socket.socket()
            refused.bind(("127.0.0.1", 0))
            env["HDB_API_BASE_URL"] = f"http://127.0.0.1:{refused.getsockname()[1]}/"
        if args.mode in ("high-zoom", "unreachable"):
            env["HDB_API_GATE"] = args.mode
            markers = ["HDB_API_HIGH_ZOOM_PASS" if args.mode == "high-zoom" else "HDB_API_UNREACHABLE_PASS"]
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
        if args.mode == "unreachable" and "HDB_PACKAGE_DATA" in log:
            raise SystemExit("FAIL unreachable API claimed data readiness")
        print(f"PASS {args.mode}: exit=0; " + ", ".join(markers))
    finally:
        if server:
            server.shutdown()
            server.server_close()
        if refused:
            refused.close()


if __name__ == "__main__":
    main()
