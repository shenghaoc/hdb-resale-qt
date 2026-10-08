import http.server
import threading
import unittest
import urllib.error
import urllib.request

import api_acceptance as acceptance


class ExpectationTests(unittest.TestCase):
    """The fixed expectations must stay consistent with the recorded responses they were written from."""

    @classmethod
    def setUpClass(cls):
        cls.manifest, cls.addresses, cls.details = acceptance.load()
        cls.plan = acceptance.expectations()
        cls.steps = {s["name"]: s for s in cls.plan["steps"]}

    def test_steps_follow_the_buyer_flow(self):
        self.assertEqual([s["name"] for s in self.plan["steps"]], list(acceptance.STEPS))
        self.assertEqual(self.manifest["dataWindow"]["maxMonth"], acceptance.LATEST_MONTH)
        self.assertEqual(sorted(acceptance.ALL_KEYS), sorted(self.addresses))

    def test_listed_addresses_are_unique_and_respect_the_town_and_type(self):
        for step in self.plan["steps"]:
            keys = [row["key"] for row in step["addresses"]]
            self.assertEqual(len(keys), len(set(keys)), step["name"])
            for key in keys:
                if step["filters"]["town"] != "All towns":
                    self.assertEqual(self.addresses[key]["town"], step["filters"]["town"], step["name"])
                if step["filters"]["type"] != acceptance.ALL_TYPES:
                    self.assertIn(step["filters"]["type"], self.addresses[key]["flatTypes"], step["name"])

    def test_selections_name_a_listed_address_at_its_index(self):
        for step in self.plan["steps"]:
            if step["selected"]:
                self.assertEqual(step["addresses"][step["selected"]["index"]]["key"], step["selected"]["key"], step["name"])

    def test_displayed_medians_are_the_recorded_figures(self):
        for key, shown in acceptance.DEFAULT_MEDIANS.items():
            self.assertEqual(shown, f"S${self.addresses[key]['medianPrice']:,}")
        for key, shown in acceptance.FOUR_ROOM_MEDIANS.items():
            self.assertEqual(shown, f"S${self.addresses[key]['medianPriceByFlatType']['4 ROOM']:,}")

    def test_registrations_and_trend_are_read_from_the_details(self):
        selected = self.steps["list-selected"]["selected"]
        detail = self.details[acceptance.BEDOK_748B]
        self.assertEqual([r["id"] for r in selected["recent"]], [t["id"] for t in detail["recentTransactions"]])
        self.assertTrue(selected["trend"] and all(0 <= int(x) < 24 for x in selected["trend"]))
        refused = self.steps["detail-error"]["selected"]
        self.assertEqual((refused["recent"], refused["trend"], refused["status"]), ([], {}, acceptance.REFUSED))

    def test_details_return_to_the_top_when_the_selection_changes_or_clears(self):
        self.assertEqual([s["name"] for s in self.plan["steps"] if s.get("detailsAtTop")], ["empty", "hidden-address", "reentry"])

    def test_each_fault_changes_only_its_own_copy(self):
        for fault, step in acceptance.FAULTS.items():
            with self.subTest(fault=fault):
                faulty = acceptance.apply_fault(self.plan, fault)
                changed = [a["name"] for a, b in zip(self.plan["steps"], faulty["steps"]) if a != b]
                self.assertEqual(changed, [] if fault == "no-503" else [step])
        self.assertEqual(self.plan, acceptance.expectations())


class VerifyTests(unittest.TestCase):
    PASS = "\n".join([*(f"qml: HDB_API_STEP {s} ms=1" for s in acceptance.STEPS),
                      "qml: HDB_API_REENTRY injected=4 queued=4", "qml: HDB_API_ACCEPTANCE_PASS steps=16", "HDB_API_GATE_EXIT"])
    RETRY = [("/api/details/" + acceptance.RETRY_KEY, 503), ("/api/details/" + acceptance.RETRY_KEY, 200)]

    def test_complete_flow_passes(self):
        self.assertIn("16 steps", acceptance.verify(0, self.PASS, None, self.RETRY))

    def test_incomplete_or_failing_flows_cannot_pass(self):
        for log in (self.PASS.replace("HDB_API_STEP empty", "HDB_API_STEP reset"),
                    self.PASS.replace("HDB_API_GATE_EXIT", ""), self.PASS + "\nqml: TypeError: x is undefined",
                    self.PASS.replace("injected=4", "injected=0")):
            with self.assertRaises(RuntimeError):
                acceptance.verify(0, log, None, self.RETRY)
        with self.assertRaises(RuntimeError):
            acceptance.verify(1, self.PASS, None, self.RETRY)
        with self.assertRaises(RuntimeError):
            acceptance.verify(0, self.PASS, None, self.RETRY[1:])
        with self.assertRaises(RuntimeError):
            acceptance.verify(0, self.PASS, None, self.RETRY + [("POST /api/shortlists", 405)])

    def test_a_fault_must_fail_exactly_at_its_step(self):
        steps = "\n".join(f"qml: HDB_API_STEP {s} ms=1" for s in acceptance.STEPS[:3])
        caught = steps + "\nqml: HDB_API_GATE_FAIL acceptance step=type timed out: row 0\nHDB_API_GATE_EXIT"
        self.assertIn("failed at type", acceptance.verify(0, caught, "reorder", []))
        for log in (caught.replace("step=type", "step=minimum"), self.PASS):
            with self.assertRaises(RuntimeError):
                acceptance.verify(0, log, "reorder", [])


class RecordedApiTests(unittest.TestCase):
    def test_refuses_the_first_detail_request_once(self):
        acceptance.RecordedApi.refuse = {acceptance.RETRY_KEY}
        acceptance.RecordedApi.requests = []
        server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), acceptance.RecordedApi)
        threading.Thread(target=server.serve_forever, daemon=True).start()
        url = f"http://127.0.0.1:{server.server_port}/api/details/{acceptance.RETRY_KEY}"
        try:
            with self.assertRaises(urllib.error.HTTPError) as refused:
                urllib.request.urlopen(url, timeout=5)
            self.assertEqual(refused.exception.code, 503)
            with urllib.request.urlopen(url, timeout=5) as response:
                self.assertEqual(response.status, 200)
        finally:
            server.shutdown()
            server.server_close()
        self.assertEqual([status for _, status in acceptance.RecordedApi.requests], [503, 200])


if __name__ == "__main__":
    unittest.main()
