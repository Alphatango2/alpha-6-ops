#!/usr/bin/env python3
"""Assert JST771's historical evidence, never a new native T1 success.

Python standard library only. Reads the ZIP without extraction. Optional journal
inputs are the 132028 departure and 140712 reconnect JSONL files. A history DB
must be a copied evidence snapshot; it and its WAL are read via a temporary copy
so SQLite cannot modify the evidence or its shared-memory sidecar.

From the repository root (replace python with an available Python 3 executable):
  python scripts/test-jst771-evidence.py --output work/jst771-zip-check.json

Full historical check:
  python scripts/test-jst771-evidence.py \
    --supplemental-journal <departure-132028.jsonl> \
    --supplemental-journal <reconnect-140712.jsonl> \
    --history-db <copied-Alpha6OPS-flights.sqlite> \
    --output <new-result.json>

Existing output files are refused; evidence is never overwritten. Exit status 0
means supplied historical assertions passed, 1 means an assertion/input failure,
and 2 means command-line misuse. Missing optional inputs remain NOT PROVEN.
"""

import argparse
from collections import Counter
from datetime import datetime
import hashlib
import json
import math
from pathlib import Path
import shutil
import sqlite3
import sys
import tempfile
import zipfile


REPO = Path(__file__).resolve().parents[1]
ZIP_RELATIVE = "docs/flight-tracking/evidence/7.24c-jst771-20260929/JST771 - S4TF - 9-29.zip"
ZIP_SHA256 = "c5d0a0cc05eb358865eee2bb67ca38e3b9358e7d4821e8224b204f9a5703ec1c"
PRIMARY = "Alpha6OPS-20260929-140838-d803e26c9d8b4aec8cac7c52986c89b2"
SUPPLEMENTAL = {
    "Alpha6OPS-20260929-132028-8b82910e17c643edb0daaadcaf382fe3": (5630, 2800),
    "Alpha6OPS-20260929-140712-45a372128d6c48869b91cd6e6a66176e": (160, 77),
}
SUPPLEMENTAL_ALIASES = {
    "supplemental-132028": "Alpha6OPS-20260929-132028-8b82910e17c643edb0daaadcaf382fe3",
    "supplemental-140712": "Alpha6OPS-20260929-140712-45a372128d6c48869b91cd6e6a66176e",
}
OOOI = {
    "TaxiOut": ("2026-09-28T21:41:34.3206481+00:00", "Block-out / taxi out"),
    "Airborne": ("2026-09-28T21:52:01.3083724+00:00", "Liftoff"),
    "TaxiIn": ("2026-09-28T22:55:18.3102722+00:00", "Touchdown"),
    "Complete": ("2026-09-28T23:03:55.3247909+00:00", "Block-in / flight complete"),
}
CONFIRM_AT = "2026-09-28T23:03:54.3254623+00:00"
DESTINATION = (-37.673333, 144.843333)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def utc(value):
    # Exact milestone equality uses original strings; datetime is only for
    # intervals, where Python's microsecond resolution is sufficient.
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def timestamp_ticks(value):
    """Normalize omitted trailing fractional zeros without losing 100 ns ticks."""
    if not value.endswith("+00:00"):
        raise ValueError("Expected recorded UTC timestamp: " + value)
    whole, _, fraction = value[:-6].partition(".")
    if len(fraction) > 7:
        raise ValueError("Timestamp exceeds source precision: " + value)
    return whole + "." + fraction.ljust(7, "0") + "+00:00"


def distance_nm(latitude, longitude):
    a, b = map(math.radians, (latitude, DESTINATION[0]))
    dlat = b - a
    dlon = math.radians(DESTINATION[1] - longitude)
    hav = math.sin(dlat / 2) ** 2 + math.cos(a) * math.cos(b) * math.sin(dlon / 2) ** 2
    return 3440.065 * 2 * math.asin(math.sqrt(min(1, max(0, hav))))


class Checks:
    def __init__(self):
        self.items = []

    def check(self, name, condition, detail=None):
        item = {"name": name, "passed": bool(condition)}
        if detail is not None:
            item["detail"] = detail
        self.items.append(item)
        return bool(condition)


def parse_journal(raw):
    return [json.loads(line) for line in raw.decode("utf-8-sig").splitlines() if line.strip()]


def select(events, kind):
    return [event for event in events if event["kind"] == kind]


def one(events, kind):
    matches = select(events, kind)
    if len(matches) != 1:
        raise ValueError(f"Expected one {kind}, observed {len(matches)}")
    return matches[0]


def verify_journal(checks, stem, events, counts):
    telemetry = select(events, "telemetry")
    states = select(events, "tracking_state")
    checks.check(stem + ": literal record/sample counts",
                 (len(events), len(telemetry), len(states)) == (counts[0], counts[1], counts[1]))
    checks.check(stem + ": contiguous integer sequence",
                 all(type(e["sequence"]) is int and e["sequence"] == i for i, e in enumerate(events, 1)))
    assignment = one(events, "flight_assignment")["detail"]
    checks.check(stem + ": literal flown assignment",
                 {k: assignment.get(k) for k in ("FlightNumber", "Origin", "Destination", "AircraftType", "CruiseAltitudeFeet")}
                 == {"FlightNumber": "JST771", "Origin": "YPAD", "Destination": "YMML", "AircraftType": "FENIX A320", "CruiseAltitudeFeet": 35000})
    checks.check(stem + ": historical T1 unsupported on every sample",
                 all(type(t["detail"]["terminalShutdown"]["Support"]) is int
                     and t["detail"]["terminalShutdown"]["Support"] == 0
                     and t["detail"]["terminalShutdown"]["Provider"] == "unsupported"
                     and type(t["detail"]["terminalShutdown"]["ExpectedEngineCount"]) is int
                     and t["detail"]["terminalShutdown"]["ExpectedEngineCount"] == 0
                     and t["detail"]["terminalShutdown"]["Engines"] == []
                     and t["detail"]["terminalShutdown"]["Qualified"] is False
                     for t in telemetry))
    checks.check(stem + ": observed intentional aircraft variant",
                 {t["detail"]["aircraft"] for t in telemetry} == {"FenixA320 IAE WF"})
    checks.check(stem + ": no monitor invalidation or joined reset",
                 not select(events, "monitor_invalidated")
                 and not any(e["detail"].get("Title") == "Tracking joined in progress" for e in select(events, "tracking_event")))
    checks.check(stem + ": geographical progress does not prematurely reach 100",
                 all(type(s["detail"]["progress"]) in (int, float)
                     and math.isfinite(s["detail"]["progress"])
                     and 0 <= s["detail"]["progress"] <= 1
                     and (s["detail"]["phase"] == "Complete" or s["detail"]["progress"] < 1)
                     for s in states))
    checks.check(stem + ": all route states reliable",
                 all(s["detail"]["routeReliable"] is True for s in states))
    latest = None
    distance_ok = True
    for event in events:
        if event["kind"] == "telemetry":
            latest = event
        elif event["kind"] == "tracking_state":
            data = event["detail"]
            if latest is None or latest["simulatorUtc"] != event["simulatorUtc"]:
                distance_ok = False
            elif data["phase"] == "Complete":
                distance_ok &= data["progress"] == 1 and data["remainingNm"] == 0
            else:
                remaining = data["remainingNm"]
                position = latest["detail"]
                distance_ok &= (type(remaining) in (int, float) and math.isfinite(remaining)
                                and remaining > 0
                                and remaining + 1e-8 >= distance_nm(position["latitude"], position["longitude"]))
    checks.check(stem + ": remaining distance covers actual endpoint offset", distance_ok)
    return {"stem": stem, "eventCount": len(events), "telemetryCount": len(telemetry),
            "trackingStateCount": len(states), "completeStateCount": sum(s["detail"]["phase"] == "Complete" for s in states),
            "unsupportedSamples": sum(t["detail"]["terminalShutdown"]["Provider"] == "unsupported" for t in telemetry)}


def verify_primary(checks, events):
    telemetry = select(events, "telemetry")
    states = select(events, "tracking_state")
    by_sim = {t["simulatorUtc"]: t["detail"] for t in telemetry}
    terminal = select(events, "terminal_block_in")
    checks.check("literal terminal decision sequence", [e["sequence"] for e in terminal] == [6, 5915, 5937, 6189])
    confirming = next(e for e in terminal if e["sequence"] == 5915)
    eligible = next(e for e in terminal if e["sequence"] == 5937)
    checks.check("manual fallback only after stable destination parking",
                 confirming["simulatorUtc"] == "2026-09-28T23:01:46.3106231+00:00"
                 and confirming["detail"]["decision"]["CanConfirm"] is False
                 and eligible["simulatorUtc"] == "2026-09-28T23:01:56.3197174+00:00"
                 and (utc(eligible["simulatorUtc"]) - utc(confirming["simulatorUtc"])).total_seconds() >= 10
                 and eligible["detail"]["decision"] == {
                     "TerminalEligible": False, "CanConfirm": True, "PilotConfirmed": False,
                     "Reason": "Automatic shutdown intent unavailable; confirm Block In"}
                 and all(eligible["detail"][key] is True for key in ("HasOn", "AtDestination", "SessionValid")))
    parked = by_sim[eligible["simulatorUtc"]]
    stable = [t["detail"] for t in telemetry
              if confirming["simulatorUtc"] <= t["simulatorUtc"] <= eligible["simulatorUtc"]]
    checks.check("all 11 parking samples in the stable interval retain guards",
                 len(stable) == 11
                 and all(t["onGround"] is True and t["parkingBrake"] is True
                         and t["groundSpeedKnots"] < .5
                         and t["paused"] is False and t["slewing"] is False
                         and t["terminalShutdown"]["OperatingEvidence"] is False
                         and distance_nm(t["latitude"], t["longitude"]) < 1 for t in stable))
    checks.check("manual fallback fresh physical parking guards",
                 parked["onGround"] is True and parked["parkingBrake"] is True
                 and parked["groundSpeedKnots"] < .5 and not parked["paused"] and not parked["slewing"]
                 and parked["terminalShutdown"]["OperatingEvidence"] is False
                 and distance_nm(parked["latitude"], parked["longitude"]) < 1)
    confirmation = one(events, "block_in_confirmation")
    checks.check("literal pilot confirmation", confirmation["sequence"] == 6175
                 and confirmation["simulatorUtc"] == CONFIRM_AT
                 and confirmation["detail"] == {"source": "Pilot", "destination": "YMML"})
    parked_states = [s for s in states if "2026-09-28T23:00:11" <= s["simulatorUtc"] <= CONFIRM_AT]
    checks.check("both-running and staggered-shutdown intervals never complete automatically",
                 len(parked_states) == 224
                 and all(s["detail"]["phase"] == "TaxiIn"
                         and s["detail"]["TerminalBlockInEligible"] is False
                         and s["detail"]["PilotConfirmedBlockIn"] is False for s in parked_states))
    for title, stamp in (("Parking brake set", "2026-09-28T23:00:11.3273696+00:00"),
                         ("Engine 1 shutdown", "2026-09-28T23:01:20.3297424+00:00"),
                         ("Engine 2 shutdown", "2026-09-28T23:01:49.3184509+00:00")):
        found = [e["simulatorUtc"] for e in select(events, "tracking_event") if e["detail"]["Title"] == title]
        checks.check("literal historical event: " + title, found == [stamp])
    complete = [s for s in states if s["detail"]["phase"] == "Complete"]
    checks.check("first complete follows pilot confirmation and closes route",
                 len(complete) == 45 and complete[0]["sequence"] == 6187
                 and complete[0]["simulatorUtc"] == "2026-09-28T23:03:59.3148422+00:00"
                 and complete[0]["detail"]["TerminalBlockInEligible"] is True
                 and complete[0]["detail"]["PilotConfirmedBlockIn"] is True
                 and all(s["detail"]["estimatedIn"] == OOOI["Complete"][0] for s in complete))
    checks.check("known historical descent omission retained",
                 not any(e["detail"]["Title"] in ("Top of descent", "Top of climb / cruise established")
                         for e in select(events, "tracking_event")))
    checks.check("independent descent 10000-foot crossing retained",
                 [(e["simulatorUtc"], e["detail"]["Summary"]) for e in select(events, "tracking_event")
                  if e["detail"]["Title"] == "10,000 feet crossed"]
                 == [("2026-09-28T22:41:18.3048553+00:00", "Descending through 10,000 ft")])
    closeout, ended = one(events, "pirep_closeout_complete"), one(events, "session_ended")
    checks.check("exactly one historical PIREP closeout then session end",
                 closeout["sequence"] == 6277 and ended["sequence"] == 6278
                 and closeout["recordedAtUtc"] == "2026-09-29T15:00:38.5588559+00:00"
                 and closeout["detail"] == {"FlightNumber": "JST771"}
                 and ended["detail"] == {"reason": "pirep_submitted"})


def verify_oooi(checks, events, full):
    milestones = select(events, "flight_milestone")
    expected = OOOI if full else {key: OOOI[key] for key in ("TaxiIn", "Complete")}
    if full:
        checks.check("full journals exactly four OOOI milestones", len(milestones) == 4)
    for phase, (stamp, title) in expected.items():
        checks.check("exactly once milestone " + phase,
                     [e["simulatorUtc"] for e in milestones if e["detail"]["phase"] == phase] == [stamp])
        checks.check("exactly once displayed milestone " + title,
                     [e["simulatorUtc"] for e in select(events, "tracking_event") if e["detail"]["Title"] == title] == [stamp])


def verify_database(checks, source, combined, full):
    with tempfile.TemporaryDirectory(prefix="jst771-readonly-") as temporary:
        copied = Path(temporary) / source.name
        shutil.copyfile(source, copied)
        wal = Path(str(source) + "-wal")
        if wal.exists():
            shutil.copyfile(wal, Path(str(copied) + "-wal"))
        connection = sqlite3.connect(copied.as_uri() + "?mode=ro", uri=True)
        connection.row_factory = sqlite3.Row
        try:
            connection.execute("PRAGMA query_only=ON")
            checks.check("copied history database integrity", connection.execute("PRAGMA integrity_check").fetchone()[0] == "ok")
            rows = [dict(r) for r in connection.execute(
                "SELECT * FROM flight WHERE flight_number=? AND started_utc>=? AND started_utc<?",
                ("JST771", "2026-09-29", "2026-09-30"))]
            checks.check("exactly one JST771 history row on test date", len(rows) == 1)
            if len(rows) != 1:
                return {"historyRows": len(rows), "pirepSubmissions": None}
            row = rows[0]
            checks.check("literal completed operational flight identity",
                         (row["id"], row["source"], row["origin"], row["destination"], row["final_phase"])
                         == ("4208cd32c171441c891cb4429b5f19d8", "live", "YPAD", "YMML", "Complete"))
            db_events = [dict(r) for r in connection.execute("SELECT * FROM flight_event WHERE flight_id=? ORDER BY sequence", (row["id"],))]
            submissions = [e for e in db_events if e["kind"] == "pirep_submitted"]
            checks.check("exactly one durable PIREP submission", len(submissions) == 1)
            if len(submissions) == 1:
                payload = json.loads(submissions[0]["detail_json"])
                checks.check("literal durable PIREP OOOI and pilot-confirmed completion",
                             all(payload.get(key) == OOOI[phase][0] for key, phase in (
                                 ("actualOut", "TaxiOut"), ("actualOff", "Airborne"),
                                 ("actualOn", "TaxiIn"), ("actualIn", "Complete")))
                             and payload.get("pilotConfirmedBlockIn") is True
                             and payload.get("status") == "Completed"
                             and payload.get("FlightNumber") == "JST771"
                             and payload.get("eventCount") == 45
                             and payload.get("submittedUtc") == "2026-09-29T15:00:38.5503212+00:00")
            tracking = [e for e in db_events if e["kind"] == "tracking_event"]
            checks.check("45 retained operational events plus one PIREP", len(tracking) == 45 and len(db_events) == 46)
            for _, (stamp, title) in OOOI.items():
                checks.check("database exactly once " + title,
                             [e["simulator_utc"] for e in tracking if json.loads(e["detail_json"])["Title"] == title] == [stamp])
            if full:
                canonical = lambda stamp, detail: (timestamp_ticks(stamp), json.dumps(detail, sort_keys=True, ensure_ascii=False))
                observed = Counter(canonical(e["simulatorUtc"], e["detail"]) for e in select(combined, "tracking_event"))
                persisted = Counter(canonical(e["simulator_utc"], json.loads(e["detail_json"])) for e in tracking)
                checks.check("database retains the exact full-journal operational history", observed == persisted)
            return {"historyRows": len(rows), "trackingEvents": len(tracking), "pirepSubmissions": len(submissions), "flightId": row["id"]}
        finally:
            connection.close()


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--zip", type=Path, default=REPO / ZIP_RELATIVE, help="Original preserved JST771 ZIP")
    parser.add_argument("--supplemental-journal", type=Path, action="append", default=[], help="Optional departure or reconnect JSONL; repeat for both")
    parser.add_argument("--history-db", type=Path, help="Optional copied flight-history SQLite evidence snapshot")
    parser.add_argument("--output", type=Path, required=True, help="New JSON result file; existing files are refused")
    args = parser.parse_args()
    if args.output.exists():
        parser.error("Output already exists; choose a new artifact path")
    checks = Checks()
    report = {"scope": "Historical JST771 evidence assertions only; not a replay of changed production code or new native success",
              "historicalAutomaticT1": "FAIL: provider unsupported; manual fallback required",
              "fullJournalOOOI": "NOT PROVEN: both supplemental flight journals required",
              "databasePersistence": "NOT PROVEN: copied history database not supplied",
              "coldStartAfterSubmission": "NOT PROVEN by this evidence check",
              "inputs": [], "journals": [], "checks": checks.items}
    sources = [args.zip, *args.supplemental_journal]
    if args.history_db:
        sources.append(args.history_db)
        sources += [Path(str(args.history_db) + suffix) for suffix in ("-wal", "-shm") if Path(str(args.history_db) + suffix).exists()]
    before = {}
    try:
        before = {p.resolve(): digest(p) for p in sources}
        report["inputs"] = [{"path": str(p), "sha256": value} for p, value in before.items()]
        checks.check("original supplied ZIP SHA256", digest(args.zip) == ZIP_SHA256)
        with zipfile.ZipFile(args.zip) as archive:
            members = archive.namelist()
            checks.check("six screenshot members retained", len([n for n in members if n.lower().endswith(".jpg")]) == 6)
            journal_name = next(n for n in members if n.endswith(PRIMARY + ".jsonl"))
            export_name = next(n for n in members if n.endswith(PRIMARY + ".json"))
            primary = parse_journal(archive.read(journal_name))
            exported = json.loads(archive.read(export_name).decode("utf-8-sig"))
            checks.check("supplied JSON and JSONL contain identical events", exported["events"] == primary
                         and exported["eventCount"] == 6278 and exported["sessionEnded"] is True
                         and exported["incompleteLastLine"] is False)
        report["journals"].append(verify_journal(checks, PRIMARY, primary, (6278, 3121)))
        verify_primary(checks, primary)
        combined = list(primary)
        seen = set()
        for path in args.supplemental_journal:
            stem = SUPPLEMENTAL_ALIASES.get(path.stem, path.stem)
            if stem not in SUPPLEMENTAL or stem in seen:
                raise ValueError("Supplemental journal must be one unique 132028 departure or 140712 reconnect JSONL: " + str(path))
            seen.add(stem)
            events = parse_journal(path.read_bytes())
            report["journals"].append(verify_journal(checks, stem, events, SUPPLEMENTAL[stem]))
            combined.extend(events)
        full = seen == set(SUPPLEMENTAL)
        verify_oooi(checks, combined, full)
        if full:
            report["fullJournalOOOI"] = "CHECKED: literal OUT/OFF/ON/IN exactly once across all three flight journals"
        if args.history_db:
            report["databasePersistence"] = verify_database(checks, args.history_db.resolve(), combined, full)
        report["sampleCounts"] = {"journalCount": len(report["journals"]),
                                  "eventCount": sum(j["eventCount"] for j in report["journals"]),
                                  "telemetryCount": sum(j["telemetryCount"] for j in report["journals"]),
                                  "trackingStateCount": sum(j["trackingStateCount"] for j in report["journals"]),
                                  "historicalUnsupportedSamples": sum(j["unsupportedSamples"] for j in report["journals"])}
    except (OSError, ValueError, KeyError, TypeError, StopIteration, sqlite3.Error, zipfile.BadZipFile) as error:
        checks.check("input parsing and evidence structure", False, f"{type(error).__name__}: {error}")
    finally:
        for path, original in before.items():
            try:
                checks.check("input unchanged: " + str(path), digest(path) == original)
            except OSError as error:
                checks.check("input unchanged: " + str(path), False, str(error))
    passed = sum(item["passed"] for item in checks.items)
    report["checkCounts"] = {"total": len(checks.items), "passed": passed, "failed": len(checks.items) - passed}
    report["result"] = "PASS" if passed == len(checks.items) else "FAIL"
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("x", encoding="utf-8") as output:
        json.dump(report, output, indent=2, ensure_ascii=False)
        output.write("\n")
    print(json.dumps({"result": report["result"], "checkCounts": report["checkCounts"],
                      "sampleCounts": report.get("sampleCounts"), "artifact": str(args.output.resolve()),
                      "scope": report["scope"]}, indent=2))
    return 0 if report["result"] == "PASS" else 1


if __name__ == "__main__":
    sys.exit(main())
