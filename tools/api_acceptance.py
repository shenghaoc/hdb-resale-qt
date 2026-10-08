#!/usr/bin/env python3
"""Native buyer acceptance of the desktop app over the recorded Worker API responses.

Each step of the buyer flow has fixed expectations, written out by hand from the recorded responses: the
listed addresses in order, the summary, the selection and a few displayed figures. Independent output
checks read recorded fields directly (names, postal codes, registrations, monthly medians); nothing here
re-implements the app's filtering. `ApiAcceptanceGate.qml` performs each step through the app's own
model, list, map and buttons and compares the window with these expectations.

The app reads a loopback server of tests/fixtures/worker-api, which refuses the first detail request
for one address with HTTP 503 so the Retry action is exercised. Requires a built HdbResale.App. For
unattended functional runs set QT_QPA_PLATFORM=offscreen; native visual and input acceptance is a separate,
scheduled run on the real platform. Only map tiles reach the network; no imagery is kept, nothing is written.

    python3 tools/api_acceptance.py --executable <app> --log <file>
    python3 tools/api_acceptance.py --executable <app> --log <file> --fault reorder   # must fail at "type"
"""
from __future__ import annotations

import argparse
import copy
import http.server
import json
import os
import re
import subprocess
import sys
import tempfile
import threading
from pathlib import Path

from api_fixture_server import FIXTURES, Handler, resolve

ALL_TYPES = "All flat types"
DEFAULT_FILTERS = {"town": "All towns", "type": ALL_TYPES, "minimum": 0, "maximum": 1_000_000, "months": 0}
# Its first detail request is refused with 503; nothing selects it before the detail-error step.
RETRY_KEY = "bedok-39-bedok-sth-rd"
STEPS = ("loaded", "full", "town", "type", "minimum", "budget", "list-selected", "recent-window", "empty", "reset",
         "map-selected", "hidden-address", "detail-error", "detail-retry", "reentry", "viewport-burst")
# Deliberate faults and the step at which the gate must then fail.
FAULTS = {"reorder": "type", "trend": "list-selected", "no-503": "detail-error", "skip-reentry": "reentry"}
FORBIDDEN = re.compile(
    r"TypeError|ReferenceError|Binding loop|Unable to assign|is not defined|Cannot assign|Cannot read property|"
    r"QQmlApplicationEngine failed|Unhandled exception|ArgumentException|ASSERT|fatal|Aborted|"
    r"(?:qrc:|\.qml).*(?:Error|Warning)", re.I)


# ---- Fixed expectations -------------------------------------------------------------------------------
# Written out by hand from the recorded responses and checked against their fields, like the expected keys in
# AddressExplorerTests.cs. This tool deliberately does not re-implement the app's filtering, ordering or
# formatting; it only reads recorded fields directly (names, postal codes, registrations, monthly medians).

AMK_727, AMK_588B, AMK_588C = ("ang-mo-kio-727-ang-mo-kio-ave-6", "ang-mo-kio-588b-ang-mo-kio-st-52",
                               "ang-mo-kio-588c-ang-mo-kio-st-52")
BEDOK_10D, BEDOK_39, BEDOK_115 = "bedok-10d-bedok-sth-ave-2", "bedok-39-bedok-sth-rd", "bedok-115-bedok-nth-rd"
BEDOK_747A, BEDOK_748A, BEDOK_748B = ("bedok-747a-bedok-reservoir-cres", "bedok-748a-bedok-reservoir-cres",
                                      "bedok-748b-bedok-reservoir-cres")
KALLANG_46, KALLANG_58 = "kallang-whampoa-46-bendemeer-rd", "kallang-whampoa-58-jln-ma-mor"
LATEST_MONTH = "2026-10"
MAXIMUM_AVAILABLE = 1_400_000
TREND_WINDOW = ("2024-11", "2026-10")
DEFAULT_KEYS = [AMK_727, BEDOK_39, BEDOK_115, BEDOK_748A, BEDOK_748B, BEDOK_747A]
ALL_KEYS = DEFAULT_KEYS + [AMK_588C, BEDOK_10D, AMK_588B, KALLANG_46, KALLANG_58]
# Displayed medians where the flat-type filter changes them: each address's, then its 4-room figure.
DEFAULT_MEDIANS = {AMK_727: "S$320,000", BEDOK_39: "S$448,000", BEDOK_115: "S$448,444", BEDOK_748A: "S$832,000",
                   BEDOK_748B: "S$837,500", BEDOK_747A: "S$860,000"}
FOUR_ROOM_MEDIANS = {BEDOK_39: "S$510,000", BEDOK_115: "S$560,000", BEDOK_748A: "S$846,000", BEDOK_748B: "S$850,000",
                     BEDOK_747A: "S$860,000"}
FIGURES = {
    (BEDOK_748B, "4 ROOM"): ["BEDOK · 3 ROOM, 4 ROOM, 5 ROOM",
                             "4 ROOM sales: 6 in the 24 source months to 2026-10 · latest 2026-02", "Median S$850,000"],
    (BEDOK_747A, ALL_TYPES): ["BEDOK · 3 ROOM, 4 ROOM, 5 ROOM",
                              "Sales of all flat types: 6 in the 24 source months to 2026-10 · latest 2026-02",
                              "Median S$860,000"],
    (BEDOK_39, ALL_TYPES): ["BEDOK · 3 ROOM, 4 ROOM",
                            "Sales of all flat types: 9 in the 24 source months to 2026-10 · latest 2026-07",
                            "Median S$448,000"],
    (BEDOK_748A, ALL_TYPES): ["BEDOK · 3 ROOM, 4 ROOM, 5 ROOM",
                              "Sales of all flat types: 7 in the 24 source months to 2026-10 · latest 2026-07",
                              "Median S$832,000"],
}
REFUSED = "Could not load this address's registrations. The HDB Resale API answered 503."


def filters(**changes) -> dict:
    return {**DEFAULT_FILTERS, **changes}


# name, actions, filters, listed keys, summary, selection (key, index, detail state) or None, medians shown
SCENARIO = [
    ("loaded", [], filters(), DEFAULT_KEYS, "6 of 11 addresses", None, DEFAULT_MEDIANS),
    ("full", [{"do": "maximum", "value": MAXIMUM_AVAILABLE}], filters(maximum=MAXIMUM_AVAILABLE), ALL_KEYS,
     "11 of 11 addresses", None, {}),
    ("town", [{"do": "town", "value": "BEDOK"}], filters(town="BEDOK", maximum=MAXIMUM_AVAILABLE),
     [BEDOK_39, BEDOK_115, BEDOK_748A, BEDOK_748B, BEDOK_747A, BEDOK_10D], "6 of 11 addresses", None, {}),
    ("type", [{"do": "type", "value": "4 ROOM"}], filters(town="BEDOK", type="4 ROOM", maximum=MAXIMUM_AVAILABLE),
     [BEDOK_39, BEDOK_115, BEDOK_748A, BEDOK_748B, BEDOK_747A], "5 of 11 addresses", None, FOUR_ROOM_MEDIANS),
    ("minimum", [{"do": "minimum", "value": 550_000}],
     filters(town="BEDOK", type="4 ROOM", minimum=550_000, maximum=MAXIMUM_AVAILABLE),
     [BEDOK_115, BEDOK_748A, BEDOK_748B, BEDOK_747A], "4 of 11 addresses", None, {}),
    ("budget", [{"do": "maximum", "value": 850_000}], filters(town="BEDOK", type="4 ROOM", minimum=550_000, maximum=850_000),
     [BEDOK_115, BEDOK_748A, BEDOK_748B], "3 of 11 addresses", None, {}),
    ("list-selected", [{"do": "click-row", "key": BEDOK_748B, "index": 2}],
     filters(town="BEDOK", type="4 ROOM", minimum=550_000, maximum=850_000),
     [BEDOK_115, BEDOK_748A, BEDOK_748B], "3 of 11 addresses", (BEDOK_748B, 2, "ready"), {}),
    ("recent-window", [{"do": "months", "value": 12}],
     filters(town="BEDOK", type="4 ROOM", minimum=550_000, maximum=850_000, months=12),
     [BEDOK_748A, BEDOK_748B], "2 of 11 addresses with a registration since 2025-11", (BEDOK_748B, 1, "ready"), {}),
    ("empty", [{"do": "maximum", "value": 500_000}],
     filters(town="BEDOK", type="4 ROOM", minimum=550_000, maximum=500_000, months=12),
     [], "0 of 11 addresses with a registration since 2025-11", None, {}),
    ("reset", [{"do": "reset-button"}], filters(), DEFAULT_KEYS, "6 of 11 addresses", None, {}),
    ("map-selected", [{"do": "marker", "key": BEDOK_747A, "zoom": 16}], filters(), DEFAULT_KEYS, "6 of 11 addresses",
     (BEDOK_747A, 5, "ready"), {}),
    ("hidden-address", [{"do": "town", "value": "ANG MO KIO"}], filters(town="ANG MO KIO"), [AMK_727],
     "1 of 11 addresses", None, {}),
    ("detail-error", [{"do": "reset"}, {"do": "select-index", "key": RETRY_KEY, "index": 1}], filters(), DEFAULT_KEYS,
     "6 of 11 addresses", (BEDOK_39, 1, "error"), {}),
    ("detail-retry", [{"do": "retry-button"}], filters(), DEFAULT_KEYS, "6 of 11 addresses", (BEDOK_39, 1, "ready"), {}),
    # The injected intents run while the town change is resetting the list; queued in order they reset the
    # filters, select 748A and raise the maximum, so every address is listed with 748A selected.
    ("reentry", [{"do": "reentry", "trigger": {"do": "town", "value": "KALLANG/WHAMPOA"}, "inject": [
        {"do": "maximum", "value": 0}, {"do": "reset"}, {"do": "select", "key": BEDOK_748A},
        {"do": "maximum", "value": MAXIMUM_AVAILABLE}]}], filters(maximum=MAXIMUM_AVAILABLE), ALL_KEYS,
     "11 of 11 addresses", (BEDOK_748A, 3, "ready"), {}),
    ("viewport-burst", [{"do": "viewport", "sequence": [["zoom", 12], ["pan", 70, 80], ["zoom", 16],
                                                         ["center", 1.37, 103.85]]}],
     filters(maximum=MAXIMUM_AVAILABLE), ALL_KEYS, "11 of 11 addresses", (BEDOK_748A, 3, "ready"), {}),
]


# ---- Direct reads of the recorded responses ------------------------------------------------------------

def load(root: Path = FIXTURES) -> tuple[dict, dict, dict]:
    read = lambda path: json.loads(path.read_text(encoding="utf-8"))
    addresses = {a["addressKey"]: a for a in read(root / "block-summaries.json")}
    details = {key: read(root / "details" / f"{key}.json") for key in addresses}
    return read(root / "manifest.json"), addresses, details


def month_index(month: str) -> int:
    year, number_ = month.split("-")
    return int(year) * 12 + int(number_) - 1


def name(address: dict) -> str:
    return f"{address['block']} {address['streetName']}"


def trend_points(detail: dict) -> dict[str, float]:
    """The recorded monthly medians inside the chart's fixed window, in thousands, by month offset."""
    first, last = (month_index(m) for m in TREND_WINDOW)
    return {str(month_index(p["month"]) - first): p["medianPrice"] / 1000
            for p in detail["monthlyTrend"] if first <= month_index(p["month"]) <= last}


def expectations(root: Path = FIXTURES) -> dict:
    manifest, addresses, details = load(root)
    if manifest["dataWindow"]["maxMonth"] != LATEST_MONTH or len(addresses) != len(ALL_KEYS):
        raise AssertionError("The recorded responses changed; review the fixed expectations")
    steps = []
    for step_name, actions, step_filters, keys, summary, selection, medians in copy.deepcopy(SCENARIO):
        for action in actions:
            if action["do"] == "marker":
                coordinates = addresses[action["key"]]["coordinates"]
                action["latitude"], action["longitude"] = coordinates["lat"], coordinates["lng"]
        step = {
            "name": step_name, "actions": actions, "filters": step_filters, "summary": summary, "selected": None,
            "addresses": [{"key": key, "address": name(addresses[key]),
                           "prefix": f"{name(addresses[key])}\n{addresses[key]['town']} · ",
                           "median": medians.get(key),
                           "latitude": addresses[key]["coordinates"]["lat"],
                           "longitude": addresses[key]["coordinates"]["lng"]} for key in keys],
        }
        if selection:
            key, index, state = selection
            address, detail = addresses[key], details[key]
            first, last = address["leaseCommenceRange"]
            step["selected"] = {
                "key": key, "index": index, "detail": state, "heading": name(address),
                "figures": FIGURES[(key, step_filters["type"])],
                "lease": f"Lease commenced {first}" + ("" if first == last else f"–{last}") + ":",
                "postal": f"postal code {address['postalCode']}",
                "status": REFUSED if state == "error" else "",
                "recent": [{"id": t["id"], "prefix": f"{t['month']} · {t['flatType']} · "}
                           for t in detail["recentTransactions"]] if state == "ready" else [],
                "trend": trend_points(detail) if state == "ready" else {},
            }
        for action in actions:
            if action["do"] == "viewport":
                zoom = [c for c in action["sequence"] if c[0] == "zoom"][-1][1]
                center = [c for c in action["sequence"] if c[0] == "center"][-1]
                step["camera"] = {"zoom": zoom, "latitude": center[1], "longitude": center[2]}
            if action["do"] == "marker":
                step["camera"] = {"zoom": action["zoom"], "latitude": action["latitude"], "longitude": action["longitude"]}
        steps.append(step)
    for previous, step in zip(steps, steps[1:]):
        # A scrolled details pane must return to its top whenever the selected address changes or clears.
        moved = bool(previous["selected"] and previous["selected"]["detail"] == "ready" and (
            step["selected"] is None or step["selected"]["key"] != previous["selected"]["key"]))
        previous["scrollDetailsBeforeNext"] = moved
        step["detailsAtTop"] = moved
    if [s["name"] for s in steps] != list(STEPS):
        raise AssertionError("SCENARIO and STEPS disagree")
    return {"latestMonth": LATEST_MONTH, "maximumAvailablePrice": MAXIMUM_AVAILABLE, "trendMonths": 24,
            "retryKey": RETRY_KEY, "steps": steps}


def apply_fault(plan: dict, fault: str | None) -> dict:
    """A copy of the plan with one deliberate defect, which the native gate must detect."""
    plan = copy.deepcopy(plan)
    steps = {s["name"]: s for s in plan["steps"]}
    if fault == "reorder":
        rows = steps["type"]["addresses"]
        rows[0], rows[1] = rows[1], rows[0]
    elif fault == "trend":
        points = steps["list-selected"]["selected"]["trend"]
        first = next(iter(points))
        points[first] += 1
    elif fault == "skip-reentry":
        steps["reentry"]["actions"][0]["inject"] = []
    elif fault not in (None, "no-503"):
        raise ValueError("Unknown fault " + fault)
    return plan


# ---- Running the native gate ---------------------------------------------------------------------------

class RecordedApi(Handler):
    """The recorded API, refusing the first detail request for one address with 503."""
    lock = threading.Lock()
    refuse: set[str] = set()
    requests: list[tuple[str, int]] = []

    def do_GET(self) -> None:
        key = self.path.removeprefix("/api/details/")
        with RecordedApi.lock:
            refused = key in RecordedApi.refuse
            RecordedApi.refuse.discard(key)
        if refused:
            body = b'{"error":"Service unavailable"}'
            self.send_response(503)
            self.send_header("content-type", "application/json; charset=utf-8")
            self.send_header("content-length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)
            status = 503
        else:
            super().do_GET()
            status = 200 if resolve(self.path) else 404
        with RecordedApi.lock:
            RecordedApi.requests.append((self.path, status))

    def do_POST(self) -> None:
        with RecordedApi.lock:
            RecordedApi.requests.append(("POST " + self.path, 405))
        self.send_response(405)
        self.end_headers()

    def log_message(self, *args) -> None:
        pass


def verify(code: int, log: str, fault: str | None, requests: list[tuple[str, int]]) -> str:
    if code != 0:
        raise RuntimeError(f"Native process exit {code}")
    steps = re.findall(r"HDB_API_STEP ([a-z-]+)", log)
    failed = re.findall(r"HDB_API_GATE_FAIL acceptance step=([a-z-]+)", log)
    if log.count("HDB_API_GATE_EXIT") != 1:
        raise RuntimeError("The managed process did not return from the QML event loop exactly once")
    if FORBIDDEN.search(log):
        raise RuntimeError("QML/runtime failure signature: " + FORBIDDEN.search(log).group(0))
    if any(path.startswith("POST") for path, _ in requests):
        raise RuntimeError("The app sent a write request")
    if fault:
        expected = FAULTS[fault]
        if failed != [expected] or steps != list(STEPS[:STEPS.index(expected)]) or "HDB_API_ACCEPTANCE_PASS" in log:
            raise RuntimeError(f"Fault {fault} should fail exactly at {expected}; steps={steps} failed={failed}")
        return f"PASS fault {fault}: the gate failed at {expected} after {len(steps)} steps"
    if failed or steps != list(STEPS) or log.count("HDB_API_ACCEPTANCE_PASS") != 1:
        raise RuntimeError(f"Incomplete acceptance: steps={steps} failed={failed}")
    retry = [status for path, status in requests if path == "/api/details/" + RETRY_KEY]
    if retry != [503, 200]:
        raise RuntimeError(f"Expected one refused and one retried detail request for {RETRY_KEY}: {retry}")
    reentry = re.search(r"HDB_API_REENTRY injected=(\d+) queued=(\d+)", log)
    if not reentry or int(reentry.group(1)) != 4:
        raise RuntimeError("The reentrant intents were not injected")
    return f"PASS acceptance: {len(steps)} steps; reentrant queue depth {reentry.group(2)}"


def run(executable: Path, log_path: Path, fault: str | None = None, timeout: float = 90) -> str:
    plan = apply_fault(expectations(), fault)
    RecordedApi.refuse = set() if fault == "no-503" else {RETRY_KEY}
    RecordedApi.requests = []
    server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), RecordedApi)
    threading.Thread(target=server.serve_forever, daemon=True).start()
    try:
        with tempfile.TemporaryDirectory(prefix="hdb-api-acceptance-") as temp:
            expectation = Path(temp) / "expectation.json"
            expectation.write_text(json.dumps(plan, ensure_ascii=False), encoding="utf-8")
            env = {k: v for k, v in os.environ.items() if not k.startswith("HDB_")}
            env.update(HDB_API_BASE_URL=f"http://127.0.0.1:{server.server_port}/", HDB_API_GATE="acceptance",
                       HDB_API_EXPECTATION=str(expectation))
            log_path.parent.mkdir(parents=True, exist_ok=True)
            with log_path.open("w", encoding="utf-8") as output:
                try:
                    result = subprocess.run([str(executable.resolve())], env=env, stdout=output,
                                            stderr=subprocess.STDOUT, timeout=timeout)
                except subprocess.TimeoutExpired as error:
                    raise RuntimeError(f"Native process exceeded {timeout:g} seconds; inspect {log_path}") from error
        with RecordedApi.lock:
            requests = list(RecordedApi.requests)
        return verify(result.returncode, log_path.read_text(encoding="utf-8", errors="replace"), fault, requests)
    finally:
        server.shutdown()
        server.server_close()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--executable", type=Path, required=True)
    parser.add_argument("--log", type=Path, required=True)
    parser.add_argument("--fault", choices=sorted(FAULTS), help="run a deliberate defect; passes only if the gate catches it")
    args = parser.parse_args()
    try:
        print(run(args.executable, args.log, args.fault))
    except (OSError, RuntimeError, ValueError) as error:
        print(f"FAIL {args.fault or 'acceptance'}: {error}; inspect {args.log}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
