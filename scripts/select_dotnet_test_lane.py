#!/usr/bin/env python3
"""Select the .NET test lane for a pull request from its changed paths, and shard it.

Deterministic change-impact v1 for the `.NET adapter` CI job: a directory-level
trigger table decides whether a pull request runs the full solution suite or a
reduced lane that excludes the campaign families (the heavy, self-contained
WebForms-review, SQL-database-matrix and outbound-HTTP lanes, which together
are ~58% of the suite's test-minutes and which a change confined to leaf areas
cannot reach). Every ambiguity resolves to the full suite:

- a shared-core path (TraceMap.Core/Cli/Storage/Combine/Reporting/Reduction,
  the rule catalog, build props, the solution, the test project file, shared
  test helpers, the AGENTS.md root marker fixtures walk up to, this selector,
  its weights table or its workflow) forces the full suite;
- a campaign family's own sources, samples or test files trigger that family;
- anything else (other test files, docs, site, scripts, workflows, adapters,
  the Oracle project, the Access / SqlValidation / EndpointAlignment projects,
  non-campaign samples) yields the reduced lane, labelled partial;
- non-pull-request events, missing change data or very large change sets run
  the full suite.

The lane then becomes a job matrix. A push (the trigger limits pushes to
dev/main) is one entry on the self-hosted runner with no shard filter. Every
other event runs on hosted runners as HOSTED_SHARDS parallel shards packed by
test class from the measured durations in dotnet-test-class-weights.json:
every shard but the last is an explicit class list (`FullyQualifiedName~A.|…`)
and the last, `rest`, is the catch-all that excludes every listed class with
`!~`, so a class the table does not know (new or renamed) always runs there.
In the reduced lane the family exclusions are ANDed into every shard and a
shard left with no classes is dropped. Exactly one shard, `rest`, uploads the
adapter-index artifact the combine job consumes.

A reduced run is a partial result. Pushes to dev/main always run the full suite
in CI, so a regression in an excluded family surfaces on the dev push, before
promotion to main. Renamed files contribute both their old and new paths (the
workflow passes `previous_filename` too). Usage (changed paths on stdin, one per
line):

    python3 scripts/select_dotnet_test_lane.py --event pull_request \\
        [--github-output "$GITHUB_OUTPUT"] [--json] [--shards 4] [--weights PATH]
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
import uuid
from dataclasses import dataclass, field
from pathlib import Path

TESTS_DIR = "src/dotnet/tests/TraceMap.Tests/"

FULL_SUITE_PREFIXES: tuple[str, ...] = (
    "src/dotnet/TraceMap.Core/",
    "src/dotnet/TraceMap.Cli/",
    "src/dotnet/TraceMap.Storage/",
    "src/dotnet/TraceMap.Combine/",
    "src/dotnet/TraceMap.Reporting/",
    "src/dotnet/TraceMap.Reduction/",
    "rules/",
    "scripts/select_dotnet_test_lane.py",
    "scripts/test_select_dotnet_test_lane.py",
    "scripts/dotnet-test-class-weights.json",
    "scripts/generate_dotnet_test_class_weights.py",
    "scripts/test_generate_dotnet_test_class_weights.py",
    ".github/workflows/adapter-validation.yml",
)

FULL_SUITE_FILENAMES: frozenset[str] = frozenset({
    "global.json",
    "TraceMap.sln",
    "TraceMap.Tests.csproj",
    # Repository-root marker: campaign fixtures locate the repo by walking up to it.
    "AGENTS.md",
})

# Any MSBuild directory-level file (props, targets, rsp, ...) shapes every build.
FULL_SUITE_FILENAME_PREFIXES: tuple[str, ...] = ("Directory.Build.", "Directory.Packages.")

# Family name -> (FullyQualifiedName substring used for exclusion, trigger prefixes).
FAMILIES: dict[str, tuple[str, tuple[str, ...]]] = {
    "WebFormsReview": (
        "WebFormsReview",
        ("samples/compiled-dotnet-evidence/", "samples/fixture-build/", TESTS_DIR + "WebFormsReview"),
    ),
    "SqlDatabaseMatrix": (
        "SqlDatabaseMatrixTests",
        ("samples/sql-database-matrix/", TESTS_DIR + "SqlDatabaseMatrixTests.cs"),
    ),
    "OutboundHttp": (
        "OutboundHttp",
        ("samples/outbound-http-coverage/", TESTS_DIR + "OutboundHttp"),
    ),
}

# Above this many changed files the compare data may be truncated; run everything.
LARGE_CHANGE_SET = 250

WEIGHTS_PATH = Path(__file__).with_name("dotnet-test-class-weights.json")
WEIGHTS_SCHEMA = "dotnet-test-class-weights.v1"
HOSTED_SHARDS = 4
HOSTED_RUNNER: tuple[str, ...] = ("ubuntu-latest",)
SELF_HOSTED_RUNNER: tuple[str, ...] = ("self-hosted", "linux-x64", "amd5950x")
ARTIFACT = "adapter-index-dotnet"
# A class name containing any of these would change the meaning of its filter token.
FILTER_OPERATORS: frozenset[str] = frozenset("()&|!~=")


@dataclass(frozen=True)
class LaneDecision:
    lane: str  # "full" | "reduced"
    reason: str
    filter: str = ""
    excluded: tuple[str, ...] = field(default_factory=tuple)
    triggered: tuple[str, ...] = field(default_factory=tuple)

    @property
    def partial(self) -> bool:
        return self.lane == "reduced"

    def summary(self) -> str:
        label = ".NET test lane: **reduced (partial)**" if self.partial else ".NET test lane: **full**"
        lines = [f"{label} — {self.reason}"]
        if self.partial:
            lines.append(f"- excluded families: {', '.join(self.excluded)}")
            lines.append(f"- `--filter \"{self.filter}\"`")
        return "\n".join(lines)


@dataclass(frozen=True)
class ClassWeight:
    name: str
    seconds: float
    tests: int

    @property
    def token(self) -> str:
        # A test's FullyQualifiedName is `<class>.<method>`, so the trailing dot keeps the
        # token from also matching a longer sibling (`…ExecutionTests.` does not occur in
        # `…ExecutionTestsRelocation.Method`).
        return self.name + "."


@dataclass(frozen=True)
class Shard:
    name: str
    runner: tuple[str, ...]
    filter: str
    artifact: str  # adapter-index artifact this shard uploads, or "" for none
    classes: tuple[str, ...]  # table classes this shard runs (the catch-all also runs unlisted ones)
    catch_all: bool
    expected_tests: int  # class-level, from the weights table
    seconds: float

    @property
    def trx(self) -> str:
        # Hosted shards publish their trx so the table can be repacked from hosted timings.
        # The self-hosted entry never does: its trx names the box's user and paths, and the
        # public repository's private-path rule keeps those out of anything shareable.
        return "" if self.runner == SELF_HOSTED_RUNNER else f"dotnet-trx-{self.name}"

    def matrix_entry(self) -> dict[str, object]:
        return {"name": self.name, "runner": list(self.runner), "filter": self.filter, "artifact": self.artifact,
                "trx": self.trx}


def _is_shared_core(path: str) -> bool:
    if path.startswith(FULL_SUITE_PREFIXES):
        return True
    name = os.path.basename(path)
    if name in FULL_SUITE_FILENAMES or name.startswith(FULL_SUITE_FILENAME_PREFIXES):
        return True
    # Shared test helpers and fixtures (anything under the test project that is not a
    # *Tests.cs file) can be used by campaign tests, so they force the full suite.
    if path.startswith(TESTS_DIR) and path.endswith(".cs") and not path.endswith("Tests.cs"):
        return True
    return False


def select_lane(changed_paths: list[str], event: str) -> LaneDecision:
    if event != "pull_request":
        return LaneDecision("full", f"{event or 'unknown'} event always runs the full suite")
    paths = sorted({p.strip().replace("\\", "/") for p in changed_paths if p and p.strip()})
    if not paths:
        return LaneDecision("full", "no changed paths available; defaulting to the full suite")
    if len(paths) >= LARGE_CHANGE_SET:
        return LaneDecision("full", f"{len(paths)} changed files is a large change set; running the full suite")
    for path in paths:
        if _is_shared_core(path):
            return LaneDecision("full", f"shared-core path changed: {path}")
    triggered = tuple(sorted(
        name for name, (_, triggers) in FAMILIES.items()
        if any(path.startswith(triggers) for path in paths)))
    excluded = tuple(sorted(set(FAMILIES) - set(triggered)))
    if not excluded:
        return LaneDecision("full", "every campaign family was triggered by the change set")
    lane_filter = "&".join(f"FullyQualifiedName!~{FAMILIES[name][0]}" for name in excluded)
    return LaneDecision(
        "reduced",
        "changes are confined to leaf areas; campaign families not reachable from them are excluded",
        filter=lane_filter,
        excluded=excluded,
        triggered=triggered,
    )


def _canonical(value: object) -> bytes:
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True).encode()


@dataclass(frozen=True)
class WeightsTable:
    classes: tuple[ClassWeight, ...]
    commit: str  # the run the table measured


def load_class_weights(path: Path = WEIGHTS_PATH) -> WeightsTable:
    """Load the weights table, refusing a hand-edited class map or an ambiguous token."""
    table = json.loads(path.read_text(encoding="utf-8"))
    if table.get("schemaVersion") != WEIGHTS_SCHEMA:
        raise ValueError(f"{path.name}: expected schemaVersion {WEIGHTS_SCHEMA}")
    classes = table.get("classes")
    if not isinstance(classes, dict) or not classes:
        raise ValueError(f"{path.name}: no classes")
    if table.get("boundedInputSha256") != hashlib.sha256(_canonical(classes)).hexdigest():
        raise ValueError(f"{path.name}: boundedInputSha256 does not match the class map; regenerate the table")
    weights = tuple(ClassWeight(name, float(entry["seconds"]), int(entry["tests"]))
                    for name, entry in sorted(classes.items()))
    for weight in weights:
        if not weight.name or FILTER_OPERATORS & set(weight.name) or weight.seconds < 0 or weight.tests < 1:
            raise ValueError(f"{path.name}: invalid class entry {weight.name!r}")
    for weight in weights:
        for other in weights:
            if weight is not other and weight.token in other.token:
                raise ValueError(f"{path.name}: token {weight.token!r} would also match {other.name!r}")
    return WeightsTable(weights, str(table.get("source", {}).get("commit", "?")))


def pack_classes(weights: tuple[ClassWeight, ...], shard_count: int) -> tuple[tuple[ClassWeight, ...], ...]:
    """Longest-processing-time packing: heaviest class first, each into the lightest bin.

    Ties resolve to the lowest bin, so the result is a pure function of the table. The
    last bin's classes are the catch-all shard's known members and are never listed.
    """
    if shard_count < 2:
        raise ValueError("a shard matrix needs at least one listed shard and the catch-all")
    bins: list[list[ClassWeight]] = [[] for _ in range(shard_count)]
    loads = [0.0] * shard_count
    for weight in sorted(weights, key=lambda item: (-item.seconds, item.name)):
        index = min(range(shard_count), key=lambda i: (loads[i], i))
        bins[index].append(weight)
        loads[index] += weight.seconds
    return tuple(tuple(members) for members in bins)


def build_shards(decision: LaneDecision, event: str, weights: tuple[ClassWeight, ...],
                 shard_count: int = HOSTED_SHARDS) -> tuple[Shard, ...]:
    """Turn a lane decision into matrix entries; see the module docstring for the rules."""
    if event == "push":
        return (Shard("full", SELF_HOSTED_RUNNER, decision.filter, ARTIFACT, tuple(w.name for w in weights),
                      True, sum(w.tests for w in weights), sum(w.seconds for w in weights)),)
    family_tokens = tuple(FAMILIES[name][0] for name in decision.excluded)
    # vstest binds `&` tighter than `|` and has no parentheses, so the family exclusions
    # ride on every class term; the catch-all is a pure conjunction and takes them once.
    family_terms = "".join(f"&FullyQualifiedName!~{token}" for token in family_tokens)

    def allowed(weight: ClassWeight) -> bool:
        return not any(token in weight.name for token in family_tokens)

    bins = pack_classes(weights, shard_count)
    shards: list[Shard] = []
    listed: list[ClassWeight] = []
    for number, members in enumerate(bins[:-1], start=1):
        kept = tuple(weight for weight in members if allowed(weight))
        if not kept:
            continue  # nothing left to list once the excluded families are removed
        shard_filter = "|".join(f"FullyQualifiedName~{weight.token}{family_terms}" for weight in kept)
        shards.append(Shard(f"shard-{number}", HOSTED_RUNNER, shard_filter, "", tuple(w.name for w in kept), False,
                            sum(w.tests for w in kept), sum(w.seconds for w in kept)))
        listed.extend(kept)
    rest_filter = "&".join([f"FullyQualifiedName!~{weight.token}" for weight in listed]
                           + [f"FullyQualifiedName!~{token}" for token in family_tokens])
    rest = tuple(weight for weight in bins[-1] if allowed(weight))
    shards.append(Shard("rest", HOSTED_RUNNER, rest_filter, ARTIFACT, tuple(w.name for w in rest), True,
                        sum(w.tests for w in rest), sum(w.seconds for w in rest)))
    return tuple(shards)


def matrix_json(shards: tuple[Shard, ...]) -> str:
    return json.dumps({"include": [shard.matrix_entry() for shard in shards]}, separators=(",", ":"))


def shard_summary(shards: tuple[Shard, ...], decision: LaneDecision, source_commit: str) -> str:
    lines = [
        f"- matrix: {len(shards)} job(s), packed from `{WEIGHTS_PATH.name}` measured at `{source_commit}`"
        + ("; expected tests are class-level, so a family token inside a listed class's method name"
           " trims that class slightly" if decision.partial else ""),
        "",
        "| shard | runner | classes | expected tests | table weight | artifact |",
        "| --- | --- | --- | ---: | ---: | --- |",
    ]
    for shard in shards:
        if shard.name == "full":
            classes = f"every class ({len(shard.classes)} known)"
        elif shard.catch_all:
            classes = f"everything not listed ({len(shard.classes)} known)"
        else:
            classes = f"{len(shard.classes)} listed"
        lines.append(f"| {shard.name} | {', '.join(shard.runner)} | {classes} | {shard.expected_tests:,} "
                     f"| {shard.seconds / 60:.1f} min | {shard.artifact or '—'} |")
    return "\n".join(lines)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--event", default=os.environ.get("GITHUB_EVENT_NAME", ""), help="GitHub event name")
    parser.add_argument("--github-output", help="append lane/filter/reason/partial/matrix outputs to this file")
    parser.add_argument("--json", action="store_true", help="print the decision and matrix as JSON instead of a summary")
    parser.add_argument("--shards", type=int, default=HOSTED_SHARDS, help="hosted shards including the catch-all")
    parser.add_argument("--weights", type=Path, default=WEIGHTS_PATH, help="class weights table")
    args = parser.parse_args(argv)

    changed = [] if sys.stdin.isatty() else sys.stdin.read().splitlines()
    decision = select_lane(changed, args.event)
    table = load_class_weights(args.weights)
    shards = build_shards(decision, args.event, table.classes, args.shards)

    if args.github_output:
        with open(args.github_output, "a", encoding="utf-8") as handle:
            handle.write(f"lane={decision.lane}\n")
            handle.write(f"filter={decision.filter}\n")
            handle.write(f"partial={'true' if decision.partial else 'false'}\n")
            # The reason quotes a changed path, the one value here a pull request authors;
            # the delimited form keeps it from ever reading as another output record.
            delimiter = f"ghadelimiter_{uuid.uuid4()}"
            handle.write(f"reason<<{delimiter}\n{decision.reason}\n{delimiter}\n")
            handle.write(f"matrix={matrix_json(shards)}\n")

    if args.json:
        print(json.dumps({**decision.__dict__, "matrix": json.loads(matrix_json(shards))}, indent=2))
    else:
        print(decision.summary())
        print(shard_summary(shards, decision, table.commit))
    return 0


if __name__ == "__main__":
    sys.exit(main())
