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


if __name__ == "__main__":
    unittest.main()
