#!/usr/bin/env python3
"""Local synthetic 256px XYZ tiles for UI-evidence captures only.

Serves a neutral, text-free grid so review screenshots contain no OneMap tile
imagery. It is not a proxy: it never contacts OneMap and is never used by the
application unless HDB_SYNTHETIC_BASEMAP_HOST is set together with
HDB_SCREENSHOT_DIR.
"""
import http.server
import struct
import threading
import zlib

SIZE = 256
LAND = (233, 231, 224)
GRID = (214, 212, 204)
EDGE = (196, 194, 186)

def png(width, height, rows):
    def chunk(kind, data):
        body = kind + data
        return struct.pack('>I', len(data)) + body + struct.pack('>I', zlib.crc32(body))
    raw = b''.join(b'\x00' + row for row in rows)
    return (b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 2, 0, 0, 0))
            + chunk(b'IDAT', zlib.compress(raw, 6)) + chunk(b'IEND', b''))

def tile():
    rows = []
    for y in range(SIZE):
        row = bytearray()
        for x in range(SIZE):
            colour = EDGE if x in (0, SIZE - 1) or y in (0, SIZE - 1) else GRID if x % 64 == 0 or y % 64 == 0 else LAND
            row += bytes(colour)
        rows.append(bytes(row))
    return png(SIZE, SIZE, rows)

TILE = tile()

class Handler(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        self.send_response(200)
        self.send_header('Content-Type', 'image/png')
        self.send_header('Content-Length', str(len(TILE)))
        self.send_header('Cache-Control', 'no-store')
        self.end_headers()
        self.wfile.write(TILE)
    def log_message(self, *args):
        pass

def serve(port=0):
    server = http.server.ThreadingHTTPServer(('127.0.0.1', port), Handler)
    threading.Thread(target=server.serve_forever, daemon=True).start()
    return server

if __name__ == '__main__':
    import sys
    server = serve(int(sys.argv[1]) if len(sys.argv) > 1 else 0)
    print(f'http://127.0.0.1:{server.server_port}/', flush=True)
    threading.Event().wait()
