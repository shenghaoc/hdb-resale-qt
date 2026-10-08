#!/usr/bin/env python3
"""Serve the recorded Worker API responses (tests/fixtures/worker-api) on a loopback port.

Lets the desktop app run without the network:

    python3 tools/api_fixture_server.py --port 8787
    HDB_API_BASE_URL=http://127.0.0.1:8787/ <app>

Only the read routes the app uses are served; anything else answers 404 like the Worker.
"""
import argparse
import http.server
import pathlib
import re
import urllib.parse

FIXTURES = pathlib.Path(__file__).resolve().parent.parent / "tests" / "fixtures" / "worker-api"
DETAIL_KEY = re.compile(r"[a-z0-9-]{1,128}")


def resolve(path: str, root: pathlib.Path = FIXTURES) -> pathlib.Path | None:
    """The recorded file answering a request path, or None for a 404."""
    route = urllib.parse.urlsplit(path).path
    if route == "/api/manifest":
        return root / "manifest.json"
    if route == "/api/block-summaries":
        return root / "block-summaries.json"
    if route.startswith("/api/details/"):
        key = urllib.parse.unquote(route[len("/api/details/"):])
        candidate = root / "details" / f"{key}.json"
        if DETAIL_KEY.fullmatch(key) and candidate.is_file():
            return candidate
    return None


class Handler(http.server.BaseHTTPRequestHandler):
    def do_GET(self) -> None:
        found = resolve(self.path)
        body = found.read_bytes() if found else b'{"error":"Not found"}'
        self.send_response(200 if found else 404)
        self.send_header("content-type", "application/json; charset=utf-8")
        self.send_header("content-length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--port", type=int, default=8787)
    port = parser.parse_args().port
    with http.server.ThreadingHTTPServer(("127.0.0.1", port), Handler) as server:
        print(f"Serving recorded API responses at http://127.0.0.1:{port}/", flush=True)
        server.serve_forever()


if __name__ == "__main__":
    main()
