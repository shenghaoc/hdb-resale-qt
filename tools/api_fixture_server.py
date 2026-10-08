#!/usr/bin/env python3
"""Serve the recorded Worker API responses (tests/fixtures/worker-api) on a loopback port.

Lets the desktop app run without the network:

    python3 tools/api_fixture_server.py            # prints its URL; --port fixes the port
    HDB_API_BASE_URL=<that URL> <app>

Only the read routes the app uses are served; anything else answers 404 like the Worker.
`--fail-details 503` answers every detail request with that status instead, so the app's failed-detail state
(docs/ui/native-acceptance.md, F6) can be reached on demand; the manifest and the address list still load.
`--refuse` reserves a loopback port without listening and prints it, for the app's unreachable-API state: every
connection to it is refused for as long as this process runs, so no other local service can answer instead.
`--fail-tiles` also serves a second loopback port that answers every request 503, for the app's tile-failure
notice: launch with HDB_TILE_TEST=1 and HDB_TEST_TILE_ENDPOINT set to the printed URL.
"""
import argparse
import http.server
import pathlib
import re
import socket
import threading
import time
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
    fail_details: int | None = None
    delay: float = 0.0

    def do_GET(self) -> None:
        if self.delay:
            time.sleep(self.delay)  # hold the request so a loading state stays visible
        found = resolve(self.path)
        status = 200 if found else 404
        if self.fail_details and urllib.parse.urlsplit(self.path).path.startswith("/api/details/"):
            found, status = None, self.fail_details
        body = found.read_bytes() if found else b'{"error":"Not found"}' if status == 404 else b'{"error":"Injected failure"}'
        self.send_response(status)
        self.send_header("content-type", "application/json; charset=utf-8")
        self.send_header("content-length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)


class FailingTiles(http.server.BaseHTTPRequestHandler):
    """Answers every request 503, so Qt Location exhausts its tile retries without any map imagery."""

    def do_GET(self) -> None:
        self.send_response(503)
        self.send_header("content-length", "0")
        self.end_headers()

    def log_message(self, *args) -> None:
        pass


def reserve_refusing_port() -> socket.socket:
    """A loopback port that refuses every connection: bound, never listened on, held open by the caller."""
    refused = socket.socket()
    refused.bind(("127.0.0.1", 0))
    return refused


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--port", type=int, default=0, help="listen on this port (default: an ephemeral one, printed)")
    parser.add_argument("--fail-details", type=int, metavar="STATUS", help="answer every detail request with this HTTP status")
    parser.add_argument("--fail-tiles", action="store_true", help="also serve a tile endpoint that answers every request 503")
    parser.add_argument("--refuse", action="store_true", help="also reserve a loopback port that refuses every connection")
    parser.add_argument("--delay", type=float, default=0.0, metavar="SECONDS",
                        help="hold every API response this long before answering, so a loading state can be observed")
    args = parser.parse_args()
    port = args.port
    Handler.fail_details = args.fail_details
    Handler.delay = args.delay
    refused = reserve_refusing_port() if args.refuse else None
    if refused is not None:
        print(f"Refusing API endpoint at http://127.0.0.1:{refused.getsockname()[1]}/ "
              "(launch with HDB_API_BASE_URL=<that URL> for the unreachable state)", flush=True)
    with http.server.ThreadingHTTPServer(("127.0.0.1", port), Handler) as server:
        print(f"Serving recorded API responses at http://127.0.0.1:{server.server_port}/", flush=True)
        if args.fail_tiles:
            tiles = http.server.ThreadingHTTPServer(("127.0.0.1", 0), FailingTiles)
            threading.Thread(target=tiles.serve_forever, daemon=True).start()
            print(f"Failing tile endpoint at http://127.0.0.1:{tiles.server_port}/ "
                  "(launch with HDB_TILE_TEST=1 HDB_TEST_TILE_ENDPOINT=<that URL>)", flush=True)
        server.serve_forever()


if __name__ == "__main__":
    main()
