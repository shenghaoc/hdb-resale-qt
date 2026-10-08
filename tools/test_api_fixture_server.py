import unittest

import api_fixture_server as server


class ResolveTests(unittest.TestCase):
    def test_serves_the_recorded_read_routes(self):
        self.assertEqual(server.resolve("/api/manifest").name, "manifest.json")
        self.assertEqual(server.resolve("/api/block-summaries?x=1").name, "block-summaries.json")
        self.assertEqual(server.resolve("/api/details/bedok-39-bedok-sth-rd").name, "bedok-39-bedok-sth-rd.json")

    def test_answers_404_for_unknown_or_unsafe_paths(self):
        for path in ("/api/details/no-such-block", "/api/details/..%2Fmanifest", "/api/details/../manifest",
                     "/api/search", "/", "/api/shortlist"):
            self.assertIsNone(server.resolve(path), path)

    def test_every_recorded_address_has_its_detail(self):
        import json
        keys = [row["addressKey"] for row in json.loads((server.FIXTURES / "block-summaries.json").read_text())]
        self.assertEqual(len(keys), 11)
        for key in keys:
            self.assertIsNotNone(server.resolve(f"/api/details/{key}"), key)


class FailDetailsTests(unittest.TestCase):
    def test_injected_detail_failure_leaves_the_list_routes_alone(self):
        import http.client
        import http.server
        import threading

        class Failing(server.Handler):
            fail_details = 503

            def log_message(self, *args):
                pass

        with http.server.ThreadingHTTPServer(("127.0.0.1", 0), Failing) as httpd:
            threading.Thread(target=httpd.serve_forever, daemon=True).start()
            try:
                connection = http.client.HTTPConnection("127.0.0.1", httpd.server_port, timeout=5)
                for path, status in (("/api/manifest", 200), ("/api/block-summaries", 200),
                                     ("/api/details/bedok-39-bedok-sth-rd", 503), ("/api/details/no-such-block", 503),
                                     ("/api/search", 404)):
                    connection.request("GET", path)
                    response = connection.getresponse()
                    response.read()
                    self.assertEqual(response.status, status, path)
            finally:
                httpd.shutdown()

    def test_failing_tiles_answer_503_to_every_path(self):
        import http.client
        import http.server
        import threading

        with http.server.ThreadingHTTPServer(("127.0.0.1", 0), server.FailingTiles) as httpd:
            threading.Thread(target=httpd.serve_forever, daemon=True).start()
            try:
                connection = http.client.HTTPConnection("127.0.0.1", httpd.server_port, timeout=5)
                for path in ("/11/1612/1014.png", "/"):
                    connection.request("GET", path)
                    response = connection.getresponse()
                    response.read()
                    self.assertEqual(response.status, 503, path)
            finally:
                httpd.shutdown()

    def test_the_reserved_port_refuses_connections(self):
        import socket
        held = server.reserve_refusing_port()
        try:
            port = held.getsockname()[1]
            with self.assertRaises(ConnectionRefusedError):
                socket.create_connection(("127.0.0.1", port), timeout=5).close()
        finally:
            held.close()


if __name__ == "__main__":
    unittest.main()
