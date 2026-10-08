import http.server
import threading
import unittest
import urllib.error
import urllib.request
from decimal import Decimal

import api_acceptance as acceptance

BEDOK_748B = "bedok-748b-bedok-reservoir-cres"


class OracleTests(unittest.TestCase):
    """The oracle must agree with the hand-written expectations in AddressExplorerTests.cs."""

    @classmethod
    def setUpClass(cls):
        cls.manifest, cls.addresses, cls.details = acceptance.load()
        cls.latest = cls.manifest["dataWindow"]["maxMonth"]

    def keys(self, **changes):
        filters = {**acceptance.DEFAULT_FILTERS, **changes}
        return [a["addressKey"] for a in acceptance.listed(self.addresses, filters, self.latest)]

    def test_default_order_is_lowest_median_first(self):
        self.assertEqual(self.keys(), ["ang-mo-kio-727-ang-mo-kio-ave-6", "bedok-39-bedok-sth-rd", "bedok-115-bedok-nth-rd",
                                       "bedok-748a-bedok-reservoir-cres", BEDOK_748B, "bedok-747a-bedok-reservoir-cres"])

    def test_a_flat_type_bounds_and_orders_by_its_own_median(self):
        self.assertEqual(self.keys(type="4 room", maximum=1_100_000)[-2:],
                         ["ang-mo-kio-588b-ang-mo-kio-st-52", "ang-mo-kio-588c-ang-mo-kio-st-52"])
        self.assertEqual(self.keys(type="4 ROOM", minimum=850_000, maximum=1_002_888),
                         [BEDOK_748B, "bedok-747a-bedok-reservoir-cres", "ang-mo-kio-588b-ang-mo-kio-st-52"])

    def test_windows_use_the_selected_types_latest_registration(self):
        self.assertEqual(acceptance.window_start(self.latest, 12), "2025-11")
        self.assertEqual(len(self.keys(months=12, maximum=2_000_000)), 8)
        self.assertEqual(self.keys(type="4 ROOM", months=12, maximum=2_000_000),
                         ["bedok-748a-bedok-reservoir-cres", BEDOK_748B, "ang-mo-kio-588b-ang-mo-kio-st-52"])

    def test_formatting_matches_invariant_dotnet_output(self):
        self.assertEqual(acceptance.money(448444), "S$448,444")
        self.assertEqual(acceptance.money(Decimal("9770.11")), "S$9,770.11")
        self.assertEqual(acceptance.money(None), "unavailable")
        # .NET rounds decimals half away from zero; Python's default would print 67.0.
        self.assertEqual(acceptance.number(Decimal("67.05"), 1), "67.1")
        self.assertEqual(acceptance.floor_area(Decimal("67"), Decimal("92")), "67.0–92.0 m²")

    def test_trend_covers_24_months_with_unsold_months_left_empty(self):
        trend = acceptance.trend(self.details[BEDOK_748B], self.latest)
        self.assertEqual((len(trend["Points"]), trend["Start"], trend["End"]), (24, "2024-11", "2026-10"))
        empty = [p for p in trend["Points"] if p["Count"] == 0]
        self.assertTrue(empty and all(p["MedianPrice"] is None and p["PriceThousands"] is None for p in empty))
        self.assertEqual(trend["ObservedMonths"], 24 - len(empty))
        self.assertLess(trend["MinimumY"], trend["MaximumY"])

    def test_plan_follows_the_buyer_flow(self):
        plan = acceptance.expectations(year=2026)
        steps = {s["name"]: s for s in plan["steps"]}
        self.assertEqual([s["name"] for s in plan["steps"]], list(acceptance.STEPS))
        self.assertEqual(plan["maximumAvailablePrice"], 1_400_000)
        self.assertEqual([len(steps[n]["addresses"]) for n in ("loaded", "full", "town", "type", "minimum", "budget")],
                         [6, 11, 6, 5, 4, 3])
        self.assertEqual(steps["list-selected"]["selected"]["key"], BEDOK_748B)
        self.assertEqual(steps["recent-window"]["selected"]["index"], 1)
        self.assertIsNone(steps["empty"]["selected"])
        self.assertTrue(steps["empty"]["detailsAtTop"])
        self.assertEqual(steps["detail-error"]["selected"]["detail"], "error")
        self.assertNotIn("Middle half", steps["detail-error"]["selected"]["metrics"])
        self.assertEqual(steps["detail-retry"]["selected"]["detail"], "ready")
        self.assertIn("Middle half", steps["detail-retry"]["selected"]["metrics"])
        # Queued in order, the injected intents leave every address listed and 748A selected.
        self.assertEqual((len(steps["reentry"]["addresses"]), steps["reentry"]["selected"]["key"]),
                         (11, "bedok-748a-bedok-reservoir-cres"))
        self.assertEqual(steps["viewport-burst"]["camera"], {"zoom": 16, "latitude": 1.37, "longitude": 103.85})

    def test_each_fault_changes_only_its_own_copy(self):
        plan = acceptance.expectations(year=2026)
        for fault, step in acceptance.FAULTS.items():
            with self.subTest(fault=fault):
                faulty = acceptance.apply_fault(plan, fault)
                changed = [a["name"] for a, b in zip(plan["steps"], faulty["steps"]) if a != b]
                self.assertEqual(changed, [] if fault == "no-503" else [step])
        self.assertEqual(plan, acceptance.expectations(year=2026))


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
