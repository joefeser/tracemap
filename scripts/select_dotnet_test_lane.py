#!/usr/bin/env python3
"""Select the .NET test lane for a pull request from its changed paths.

Deterministic change-impact v1 for the `.NET adapter` CI job: a directory-level
trigger table decides whether a pull request runs the full solution suite or a
reduced lane that excludes the campaign families (the heavy, self-contained
WebForms-review, SQL-database-matrix and outbound-HTTP lanes, which together
are ~58% of the suite's test-minutes and which a change confined to leaf areas
cannot reach). Every ambiguity resolves to the full suite:

- a shared-core path (TraceMap.Core/Cli/Storage/Combine/Reporting/Reduction,
  the rule catalog, build props, the solution, the test project file, shared
  test helpers, this selector or its workflow) forces the full suite;
- a campaign family's own sources, samples or test files trigger that family;
- anything else (other test files, docs, site, scripts, workflows, adapters,
  the Oracle project, the Access / SqlValidation / EndpointAlignment projects,
  non-campaign samples) yields the reduced lane, labelled partial;
- non-pull-request events, missing change data or very large change sets run
  the full suite.

A reduced run is a partial result. Pushes to dev/main always run the full suite
in CI, so a regression in an excluded family surfaces on the dev push, before
promotion to main. Renamed files contribute both their old and new paths (the
workflow passes `previous_filename` too). Usage (changed paths on stdin, one per
line):

    python3 scripts/select_dotnet_test_lane.py --event pull_request \\
        [--github-output "$GITHUB_OUTPUT"] [--json]
"""

from __future__ import annotations

import argparse
import json
import os
import sys
from dataclasses import dataclass, field

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
    ".github/workflows/adapter-validation.yml",
)

FULL_SUITE_FILENAMES: frozenset[str] = frozenset({
    "global.json",
    "TraceMap.sln",
    "TraceMap.Tests.csproj",
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


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--event", default=os.environ.get("GITHUB_EVENT_NAME", ""), help="GitHub event name")
    parser.add_argument("--github-output", help="append lane/filter/reason/partial outputs to this file")
    parser.add_argument("--json", action="store_true", help="print the decision as JSON instead of a summary")
    args = parser.parse_args(argv)

    changed = [] if sys.stdin.isatty() else sys.stdin.read().splitlines()
    decision = select_lane(changed, args.event)

    if args.github_output:
        with open(args.github_output, "a", encoding="utf-8") as handle:
            handle.write(f"lane={decision.lane}\n")
            handle.write(f"filter={decision.filter}\n")
            handle.write(f"partial={'true' if decision.partial else 'false'}\n")
            handle.write(f"reason={decision.reason}\n")

    if args.json:
        print(json.dumps(decision.__dict__, indent=2))
    else:
        print(decision.summary())
    return 0


if __name__ == "__main__":
    sys.exit(main())
