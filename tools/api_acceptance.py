#!/usr/bin/env python3
"""Native buyer acceptance of the desktop app over the recorded Worker API responses.

This oracle re-derives, independently of the app's C#, what each step of the buyer flow must show:
the web app's filter semantics and order, every list row and individual map marker, the summary, and
the selected address's figures, registrations and 24-month trend. `ApiAcceptanceGate.qml` performs
each step through the app's own model, list, map and buttons and compares the window with it.

The app reads a loopback server of tests/fixtures/worker-api, which refuses the first detail request
for one address with HTTP 503 so the Retry action is exercised. Requires a real graphical session and
a built HdbResale.App. Only map tiles reach the network; no imagery is retained and nothing is written.

    python3 tools/api_acceptance.py --executable <app> --log <file>
    python3 tools/api_acceptance.py --executable <app> --log <file> --fault reorder   # must fail at "type"
"""
from __future__ import annotations

import argparse
import copy
import datetime
import http.server
import json
import math
import os
import re
import subprocess
import sys
import tempfile
import threading
from decimal import ROUND_HALF_EVEN, ROUND_HALF_UP, Decimal
from pathlib import Path

from api_fixture_server import FIXTURES, Handler, resolve

ALL_TOWNS = "All towns"
ALL_TYPES = "All flat types"
DEFAULT_FILTERS = {"town": ALL_TOWNS, "type": ALL_TYPES, "minimum": 0, "maximum": 1_000_000, "months": 0}
LEASE_YEARS = 99
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


# ---- Formatting, as the app presents figures (invariant culture, decimal rounding away from zero) -------

def number(value, decimals: int = 0) -> str:
    rounded = Decimal(value).quantize(Decimal(1).scaleb(-decimals), rounding=ROUND_HALF_UP)
    return f"{rounded:,.{decimals}f}"


def money(value) -> str:
    if value is None:
        return "unavailable"
    return "S$" + number(value, 0 if Decimal(value) == Decimal(value).to_integral_value() else 2)


def text_or_unavailable(value) -> str:
    return "unavailable" if value is None or not str(value).strip() else str(value)


def floor_area(minimum, maximum) -> str:
    return number(minimum, 1) + " m²" if minimum == maximum else number(minimum, 1) + "–" + number(maximum, 1) + " m²"


def month_index(month: str) -> int:
    year, number_ = month.split("-")
    return int(year) * 12 + int(number_) - 1


def month_label(index: int) -> str:
    return f"{index // 12:04d}-{index % 12 + 1:02d}"


# ---- The web app's semantics for the filters this app offers ---------------------------------------

def canonical(flat_type: str) -> str:
    value = flat_type.strip().upper()
    return "MULTI-GENERATION" if value == "MULTI GENERATION" else value


def selected_type(flat_type: str | None) -> str | None:
    return None if not flat_type or flat_type == ALL_TYPES else canonical(flat_type)


def effective_median(address: dict, flat_type: str | None):
    chosen = selected_type(flat_type)
    medians = address.get("medianPriceByFlatType") or {}
    return medians[chosen] if chosen in medians else address["medianPrice"]


def effective_per_sqm(address: dict, flat_type: str | None):
    chosen = selected_type(flat_type)
    medians = address.get("medianPricePerSqmByFlatType") or {}
    return medians[chosen] if chosen in medians else address["pricePerSqmMedian"]


def cohort(address: dict, flat_type: str | None) -> dict:
    chosen = selected_type(flat_type)
    cohorts = address.get("flatTypeCohorts") or {}
    if chosen in cohorts:
        return {**cohorts[chosen], "specific": True}
    return {"transactionCount": address["transactionCount"], "latestMonth": address["latestMonth"],
            "floorAreaRange": address["floorAreaRange"], "specific": False}


def window_start(latest: str, months: int) -> str | None:
    return month_label(month_index(latest) - (months - 1)) if months > 0 else None


def matches(address: dict, filters: dict, start: str | None) -> bool:
    if filters["town"] != ALL_TOWNS and address["town"] != filters["town"]:
        return False
    chosen = selected_type(filters["type"])
    if chosen is not None and not any(canonical(t) == chosen for t in address["flatTypes"]):
        return False
    type_cohort = (address.get("flatTypeCohorts") or {}).get(chosen) if chosen else None
    # A window with a selected type needs that type's own figures; the web app never guesses them.
    if chosen is not None and start is not None and type_cohort is None:
        return False
    price = effective_median(address, chosen)
    if price < filters["minimum"] or price > filters["maximum"]:
        return False
    latest = type_cohort["latestMonth"] if type_cohort else address["latestMonth"]
    return start is None or latest >= start


def listed(addresses: list[dict], filters: dict, latest: str) -> list[dict]:
    start = window_start(latest, filters["months"])
    # Lowest applicable median first; sorted() is stable, so equal medians keep the API's order.
    return sorted((a for a in addresses if matches(a, filters, start)), key=lambda a: effective_median(a, filters["type"]))


def maximum_available(addresses: list[dict]) -> int:
    highest = max(max([a["medianPrice"], *(a.get("medianPriceByFlatType") or {}).values()]) for a in addresses)
    return max(1_000_000, math.ceil(Decimal(highest) / 50_000) * 50_000)


# ---- What the window shows ----------------------------------------------------------------------------

def row(address: dict, flat_type: str) -> dict:
    figures = cohort(address, flat_type)
    count = figures["transactionCount"]
    noun = "sale" if count == 1 else "sales"
    median = effective_median(address, flat_type)
    if Decimal(median) != Decimal(median).to_integral_value():
        raise ValueError("Map labels round medians; extend the oracle before recording fractional medians")
    name = f"{address['block']} {address['streetName']}"
    return {
        "key": address["addressKey"], "address": name,
        "latitude": float(address["coordinates"]["lat"]), "longitude": float(address["coordinates"]["lng"]),
        "text": f"{name}\n{address['town']} · {number(count)} {noun} · median {money(median)}\n"
                f"{', '.join(address['flatTypes'])} · latest {figures['latestMonth']}",
        "mapLabel": f"{number(count)} {noun} · median S${number(median)} · latest {figures['latestMonth']}",
    }


def metrics(address: dict, flat_type: str, detail: dict | None, latest: str) -> str:
    figures = cohort(address, flat_type)
    label = canonical(flat_type) + " sales" if figures["specific"] else "Sales of all flat types"
    scope = (f"in the 24 source months to {latest}" if figures["latestMonth"] >= window_start(latest, 24)
             else "in all recorded months (none in the latest 24)")
    lines = [
        f"{address['town']} · {', '.join(address['flatTypes'])}",
        f"{label}: {number(figures['transactionCount'])} {scope} · latest {figures['latestMonth']}",
        f"Median {money(effective_median(address, flat_type))} · {money(effective_per_sqm(address, flat_type))}/m²",
        "Floor area " + floor_area(*figures["floorAreaRange"]),
    ]
    iqr = detail["summary"].get("priceIqr") if detail else None
    if iqr is not None and len(iqr) == 2:
        lines.append(f"Middle half of all sales {money(iqr[0])}–{money(iqr[1])}")
    mrt = address.get("nearestMrt")
    if mrt:
        minutes = (Decimal(mrt["walkingTimeSeconds"]) / 60).quantize(Decimal(1), rounding=ROUND_HALF_EVEN)
        lines.append(f"Nearest MRT: {mrt['stationName']} · {number(mrt['distanceMeters'])} m, about {minutes} min walk")
    return "\n".join(lines)


def lease(address: dict, year: int) -> str:
    first, last = address["leaseCommenceRange"]
    minimum, maximum = LEASE_YEARS - (year - first), LEASE_YEARS - (year - last)
    commenced = f"{first}" if first == last else f"{first}–{last}"
    remaining = f"{maximum} years" if minimum == maximum else f"{minimum}–{maximum} years"
    return (f"Lease commenced {commenced}: about {remaining} of a 99-year lease remain in {year}. "
            "Each registration below shows the remaining lease recorded at its resale application. "
            "Not an eligibility assessment.")


def location(address: dict) -> str:
    lat, lng = float(address["coordinates"]["lat"]), float(address["coordinates"]["lng"])
    postal = f" · postal code {address['postalCode']}" if (address.get("postalCode") or "").strip() else ""
    return f"Approximate block location {lat:.5f}, {lng:.5f}{postal}. Locations are block points, never individual flats."


def recent(detail: dict) -> list[dict]:
    return [{
        "id": t["id"],
        "heading": f"{t['month']} · {t['flatType']} · {money(t.get('resalePrice'))}",
        "details": f"{number(t['floorAreaSqm'], 1)} m² · {money(t.get('pricePerSqm'))}/m² · storey "
                   f"{text_or_unavailable(t.get('storeyRange'))}\n{text_or_unavailable(t.get('flatModel'))} · lease start "
                   f"{t['leaseCommenceDate']}\nSource remaining lease at resale application: "
                   f"{text_or_unavailable(t.get('remainingLease'))}",
    } for t in detail["recentTransactions"]]


def trend(detail: dict, latest: str) -> dict:
    """The 24 source months ending at the dataset's latest month; months without a registration stay null."""
    end = month_index(latest)
    count = min(24, end + 1 - 12)
    observed = {p["month"]: p for p in detail["monthlyTrend"]}
    points = []
    for x, index in enumerate(range(end - count + 1, end + 1)):
        point = observed.get(month_label(index))
        median = point["medianPrice"] if point else None
        points.append({"Month": month_label(index), "X": x, "Count": point["transactionCount"] if point else 0,
                       "MedianPrice": None if median is None else float(median),
                       "PriceThousands": None if median is None else float(Decimal(median) / 1000)})
    seen = [Decimal(observed[p["Month"]]["medianPrice"]) for p in points if p["Count"] > 0]
    low = 0 if not seen else math.floor(min(seen) / 50_000) * 50
    high = 1 if not seen else math.ceil(max(seen) / 50_000) * 50
    if seen:
        low, high = max(0, low - 50), high + 50
    return {"Points": points, "Start": points[0]["Month"], "End": points[-1]["Month"], "ObservedMonths": len(seen),
            "Sales": sum(p["Count"] for p in points if p["Count"] > 0), "MinimumY": low, "MaximumY": high}


# ---- The flow ----------------------------------------------------------------------------------------

def load(root: Path = FIXTURES) -> tuple[dict, list[dict], dict]:
    read = lambda path: json.loads(path.read_text(encoding="utf-8"), parse_float=Decimal)
    addresses = read(root / "block-summaries.json")
    details = {a["addressKey"]: read(root / "details" / f"{a['addressKey']}.json") for a in addresses}
    return read(root / "manifest.json"), addresses, details


def scenario(highest: int) -> list[tuple[str, list[dict]]]:
    """Each step's actions, as the gate performs them. Values were chosen so every step changes the result."""
    by = lambda key: {"key": key}
    return [
        ("loaded", []),
        ("full", [{"do": "maximum", "value": highest}]),
        ("town", [{"do": "town", "value": "BEDOK"}]),
        ("type", [{"do": "type", "value": "4 ROOM"}]),
        ("minimum", [{"do": "minimum", "value": 550_000}]),
        ("budget", [{"do": "maximum", "value": 850_000}]),
        ("list-selected", [{"do": "click-row", **by("bedok-748b-bedok-reservoir-cres")}]),
        ("recent-window", [{"do": "months", "value": 12}]),
        ("empty", [{"do": "maximum", "value": 500_000}]),
        ("reset", [{"do": "reset-button"}]),
        ("map-selected", [{"do": "marker", "zoom": 16, **by("bedok-747a-bedok-reservoir-cres")}]),
        ("hidden-address", [{"do": "town", "value": "ANG MO KIO"}]),
        ("detail-error", [{"do": "reset"}, {"do": "select-index", **by(RETRY_KEY)}]),
        ("detail-retry", [{"do": "retry-button"}]),
        # The injected intents run while the town change is resetting the list; they must apply in order.
        ("reentry", [{"do": "reentry", "trigger": {"do": "town", "value": "KALLANG/WHAMPOA"}, "inject": [
            {"do": "maximum", "value": 0}, {"do": "reset"}, {"do": "select", **by("bedok-748a-bedok-reservoir-cres")},
            {"do": "maximum", "value": highest}]}]),
        ("viewport-burst", [{"do": "viewport", "sequence": [["zoom", 12], ["pan", 70, 80], ["zoom", 16],
                                                             ["center", 1.37, 103.85]]}]),
    ]


class Simulation:
    """The buyer-visible state the app must reach, following the web app's semantics."""

    def __init__(self, manifest: dict, addresses: list[dict]):
        self.addresses, self.latest = addresses, manifest["dataWindow"]["maxMonth"]
        self.filters, self.selected, self.detail = dict(DEFAULT_FILTERS), None, None
        self.fetches: dict[str, int] = {}

    def listed(self) -> list[dict]:
        return listed(self.addresses, self.filters, self.latest)

    def keys(self) -> list[str]:
        return [a["addressKey"] for a in self.listed()]

    def select(self, key: str | None) -> None:
        key = key if key in self.keys() else None
        if key == self.selected:
            return
        self.selected, self.detail = key, None
        if key:
            self.fetch()

    def fetch(self) -> None:
        self.fetches[self.selected] = self.fetches.get(self.selected, 0) + 1
        refused = self.selected == RETRY_KEY and self.fetches[self.selected] == 1
        self.detail = "error" if refused else "ready"

    def perform(self, action: dict) -> None:
        kind = action["do"]
        if kind in ("town", "type", "minimum", "maximum", "months"):
            self.filters[kind] = action["value"]
        elif kind in ("reset", "reset-button"):
            self.filters = dict(DEFAULT_FILTERS)
        elif kind in ("select", "select-index", "click-row", "marker"):
            if action["key"] not in self.keys():
                raise ValueError(f"{kind} names an address that is not listed: {action['key']}")
            if kind in ("select-index", "click-row"):
                action["index"] = self.keys().index(action["key"])
            if kind == "marker":
                found = next(a for a in self.addresses if a["addressKey"] == action["key"])
                action["latitude"], action["longitude"] = (float(found["coordinates"]["lat"]),
                                                           float(found["coordinates"]["lng"]))
            self.select(action["key"])
            return
        elif kind == "retry-button":
            if self.detail != "error":
                raise ValueError("Retry is offered only after a refused detail request")
            self.fetch()
            return
        elif kind == "reentry":
            for queued in (action["trigger"], *action["inject"]):
                self.perform(queued)
            return
        elif kind == "viewport":
            return
        else:
            raise ValueError("Unknown action " + kind)
        # The selection survives a filter change while its address is still listed.
        if self.selected not in self.keys():
            self.select(None)


def expectations(root: Path = FIXTURES, year: int | None = None) -> dict:
    manifest, addresses, details = load(root)
    latest = manifest["dataWindow"]["maxMonth"]
    year = year or datetime.date.today().year
    highest = maximum_available(addresses)
    simulation = Simulation(manifest, addresses)
    steps = []
    for name, actions in scenario(highest):
        for action in actions:
            simulation.perform(action)
        shown = simulation.listed()
        filters = dict(simulation.filters)
        start = window_start(latest, filters["months"])
        step = {
            "name": name, "actions": actions, "filters": filters,
            "summary": f"{number(len(shown))} of {number(len(addresses))} addresses"
                       + (f" with a registration since {start}" if start else ""),
            "addresses": [row(a, filters["type"]) for a in shown], "selected": None,
        }
        if simulation.selected:
            address = next(a for a in shown if a["addressKey"] == simulation.selected)
            detail = details[simulation.selected] if simulation.detail == "ready" else None
            step["selected"] = {
                "key": simulation.selected, "index": simulation.keys().index(simulation.selected),
                "heading": f"{address['block']} {address['streetName']}", "detail": simulation.detail,
                "metrics": metrics(address, filters["type"], detail, latest),
                "lease": lease(address, year), "location": location(address),
                "status": "" if detail else "Could not load this address's registrations. The HDB Resale API answered 503.",
                "recent": recent(detail) if detail else [], "trend": trend(detail, latest) if detail else None,
            }
        for action in actions:
            if action["do"] == "viewport":
                zoom = [s for s in action["sequence"] if s[0] == "zoom"][-1][1]
                center = [s for s in action["sequence"] if s[0] == "center"][-1]
                step["camera"] = {"zoom": zoom, "latitude": center[1], "longitude": center[2]}
            if action["do"] == "marker":
                step["camera"] = {"zoom": action["zoom"], "latitude": action["latitude"], "longitude": action["longitude"]}
        steps.append(step)
    for previous, step in zip(steps, steps[1:]):
        # A scrolled details pane must return to its top whenever the selected address changes or clears.
        moved = previous["selected"] and previous["selected"]["detail"] == "ready" and (
            step["selected"] is None or step["selected"]["key"] != previous["selected"]["key"])
        previous["scrollDetailsBeforeNext"] = bool(moved)
        step["detailsAtTop"] = bool(moved)
    if [s["name"] for s in steps] != list(STEPS):
        raise AssertionError("Scenario and STEPS disagree")
    return {"latestMonth": latest, "maximumAvailablePrice": highest, "totalAddresses": len(addresses),
            "year": year, "retryKey": RETRY_KEY, "steps": steps}


def apply_fault(plan: dict, fault: str | None) -> dict:
    """A copy of the plan with one deliberate defect, which the native gate must detect."""
    plan = copy.deepcopy(plan)
    steps = {s["name"]: s for s in plan["steps"]}
    if fault == "reorder":
        rows = steps["type"]["addresses"]
        rows[0], rows[1] = rows[1], rows[0]
    elif fault == "trend":
        point = next(p for p in steps["list-selected"]["selected"]["trend"]["Points"] if p["Count"] > 0)
        point["MedianPrice"] += 1000
        point["PriceThousands"] += 1
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
