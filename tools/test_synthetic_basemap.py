import struct
import unittest
import zlib

import synthetic_basemap


class SyntheticBasemapTest(unittest.TestCase):
    def test_tile_is_a_valid_256_pixel_rgb_png_without_text_or_network(self):
        data = synthetic_basemap.TILE
        self.assertEqual(data[:8], b'\x89PNG\r\n\x1a\n')
        width, height, depth, colour = struct.unpack('>IIBB', data[16:26])
        self.assertEqual((width, height, depth, colour), (256, 256, 8, 2))
        start = data.index(b'IDAT') + 4
        raw = zlib.decompress(data[start:data.index(b'IEND') - 8])
        self.assertEqual(len(raw), 256 * (1 + 256 * 3))

    def test_server_answers_any_tile_path_locally(self):
        import urllib.request
        server = synthetic_basemap.serve()
        try:
            with urllib.request.urlopen(f'http://127.0.0.1:{server.server_port}/11/1/2.png', timeout=5) as response:
                self.assertEqual(response.headers['Content-Type'], 'image/png')
                self.assertEqual(response.read(), synthetic_basemap.TILE)
        finally:
            server.shutdown()


if __name__ == '__main__':
    unittest.main()
