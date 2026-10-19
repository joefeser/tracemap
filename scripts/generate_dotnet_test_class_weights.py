#!/usr/bin/env python3
"""Project a full-suite .NET trx into the per-class weight table the PR shard matrix packs from.

`scripts/select_dotnet_test_lane.py` splits a pull request's hosted `.NET adapter` job
into parallel shards by test class, packed by measured duration. The durations come
from `scripts/dotnet-test-class-weights.json`, which this script derives from one
full-suite run's trx (`dotnet test --logger trx`): per test class, the sum of its test
durations and its test count. Only class names, seconds and counts are kept; the trx
itself (machine name, local paths, test output) is never copied or hashed. The table
carries this generator's SHA-256 and the SHA-256 of its bounded, privacy-projected
input (the class map), following the repository's derived-artifact rule. The selector
recomputes the input digest when it loads the table, so a hand edit fails loudly;
the generator digest is provenance only.

Regenerate from a run whose durations should drive the packing (a new or split test
class, a materially different suite), naming the commit that run tested:

    dotnet test src/dotnet/TraceMap.sln --logger trx
    python3 scripts/generate_dotnet_test_class_weights.py <run.trx> --commit <sha> --runner <label>

A class missing from the table is never lost: the selector routes every class it does
not list to the catch-all shard. xunit writes one trx result per test case, so a
class's `tests` value is its case count, skipped cases included.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
import xml.etree.ElementTree as ElementTree
from pathlib import Path

SCHEMA_VERSION = "dotnet-test-class-weights.v1"
BOUNDED_INPUT_KIND = "privacy-projected-class-durations"
TRX_NAMESPACE = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"
DEFAULT_OUT = Path(__file__).with_name("dotnet-test-class-weights.json")
COUNTERS = ("total", "executed", "passed", "failed")


def canonical(value: object) -> bytes:
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True).encode()


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def duration_seconds(text: str) -> float:
    hours, minutes, seconds = text.split(":")
    return int(hours) * 3600 + int(minutes) * 60 + float(seconds)


def _tag(name: str) -> str:
    return f"{{{TRX_NAMESPACE}}}{name}"


def project_trx(trx_path: Path) -> tuple[dict[str, dict[str, float | int]], dict[str, object]]:
    """Return (class map, run facts) from a trx; the class map is the bounded input."""
    root = ElementTree.parse(trx_path).getroot()
    class_of: dict[str, str] = {}
    for unit_test in root.iter(_tag("UnitTest")):
        method = unit_test.find(_tag("TestMethod"))
        test_id = unit_test.get("id")
        if method is None or not test_id or not method.get("className"):
            raise ValueError(f"trx UnitTest without a TestMethod className: {unit_test.attrib}")
        class_of[test_id] = method.get("className", "")
    seconds: dict[str, float] = {}
    tests: dict[str, int] = {}
    for result in root.iter(_tag("UnitTestResult")):
        test_id = result.get("testId", "")
        if test_id not in class_of:
            raise ValueError(f"trx UnitTestResult for an undefined test id: {test_id}")
        name = class_of[test_id]
        seconds[name] = seconds.get(name, 0.0) + duration_seconds(result.get("duration", "00:00:00"))
        tests[name] = tests.get(name, 0) + 1
    if not tests:
        raise ValueError(f"no test results in {trx_path.name}")
    classes = {name: {"seconds": round(seconds[name], 3), "tests": tests[name]} for name in sorted(tests)}
    times = root.find(_tag("Times"))
    counters = root.find(f"{_tag('ResultSummary')}/{_tag('Counters')}")
    run: dict[str, object] = {
        "started": times.get("start") if times is not None else None,
        "finished": times.get("finish") if times is not None else None,
    }
    for key in COUNTERS:
        run[key] = int(counters.get(key, "0")) if counters is not None else None
    return classes, run


def build_table(classes: dict[str, dict[str, float | int]], run: dict[str, object],
                commit: str, runner: str, generator: Path = Path(__file__)) -> dict[str, object]:
    return {
        "schemaVersion": SCHEMA_VERSION,
        "generatorSha256": digest(generator.read_bytes()),
        "boundedInputKind": BOUNDED_INPUT_KIND,
        # Only the projected class map is hashed, never the trx (machine name, paths, output).
        "boundedInputSha256": digest(canonical(classes)),
        "source": {"commit": commit, "runner": runner, **run},
        "classes": classes,
    }


def summary(table: dict[str, object]) -> str:
    classes = table["classes"]
    assert isinstance(classes, dict)
    total_seconds = sum(float(entry["seconds"]) for entry in classes.values())
    total_tests = sum(int(entry["tests"]) for entry in classes.values())
    lines = [
        f"{len(classes)} classes, {total_tests} test cases, {total_seconds / 60:.1f} test-minutes"
        f" (commit {table['source']['commit']}, runner {table['source']['runner']})",  # type: ignore[index]
        "heaviest classes:",
    ]
    heaviest = sorted(classes.items(), key=lambda item: (-float(item[1]["seconds"]), item[0]))[:10]
    lines.extend(f"  {float(entry['seconds']) / 60:5.1f} min  {entry['tests']:4d} tests  {name}"
                 for name, entry in heaviest)
    return "\n".join(lines)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("trx", type=Path, help="trx from a full-suite `dotnet test --logger trx` run")
    parser.add_argument("--commit", required=True, help="commit the trx run tested")
    parser.add_argument("--runner", required=True, help="where it ran, e.g. the runner label")
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT, help=f"table path (default {DEFAULT_OUT.name})")
    args = parser.parse_args(argv)

    classes, run = project_trx(args.trx)
    table = build_table(classes, run, args.commit, args.runner)
    args.out.write_text(json.dumps(table, indent=2) + "\n", encoding="utf-8")
    print(f"wrote {args.out}")
    print(summary(table))
    return 0


if __name__ == "__main__":
    sys.exit(main())
