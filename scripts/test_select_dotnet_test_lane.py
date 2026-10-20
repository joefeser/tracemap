#!/usr/bin/env python3

from __future__ import annotations

import hashlib
import importlib.util
import io
import json
import sys
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path
from unittest import mock


SCRIPT = Path(__file__).with_name("select_dotnet_test_lane.py")
SPEC = importlib.util.spec_from_file_location("select_dotnet_test_lane", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
# Dataclass annotation resolution looks the module up by name, so register it first.
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)

ALL_FAMILIES = ("OutboundHttp", "SqlDatabaseMatrix", "WebFormsReview")
ALL_FAMILY_TERMS = "&FullyQualifiedName!~OutboundHttp&FullyQualifiedName!~SqlDatabaseMatrixTests&FullyQualifiedName!~WebFormsReview"
FULL = MODULE.LaneDecision("full", "test")
PR = "pull_request"


def weights(*entries: tuple[str, float, int]) -> tuple[MODULE.ClassWeight, ...]:
    return tuple(MODULE.ClassWeight("TraceMap.Tests." + name, seconds, tests) for name, seconds, tests in entries)


# Longest-first into the lightest of four bins: bin 1 takes Legacy(340) then the three
# light classes; bin 2 Preparation(330)+OutboundHttpCoverage(100), both campaign classes;
# bin 3 IlBody(320)+Relocation(150); the catch-all bin SqlNet100(260)+Execution(200).
SYNTHETIC = weights(
    ("OutboundHttpLegacyCoverageTests", 340.0, 15),
    ("WebFormsReviewPreparationTests", 330.0, 70),
    ("IlBodyEvidenceExtractorTests", 320.0, 95),
    ("SqlDatabaseMatrixTestsNet100", 260.0, 2),
    ("WebFormsReviewExecutionTests", 200.0, 20),
    ("WebFormsReviewExecutionTestsRelocation", 150.0, 7),
    ("OutboundHttpCoverageTests", 100.0, 14),
    ("EvidenceDocsExportTests", 50.0, 25),
    ("ScanManifestTests", 10.0, 4),
    ("RuleCatalogTests", 5.0, 3),
)


def write_table(directory: Path, classes: dict[str, dict[str, float | int]], **overrides: object) -> Path:
    table: dict[str, object] = {
        "schemaVersion": MODULE.WEIGHTS_SCHEMA,
        "generatorSha256": "0" * 64,
        "boundedInputKind": "privacy-projected-class-durations",
        "boundedInputSha256": hashlib.sha256(MODULE._canonical(classes)).hexdigest(),
        "source": {"commit": "abc1234", "runner": "test"},
        "classes": classes,
    }
    table.update(overrides)
    path = directory / "weights.json"
    path.write_text(json.dumps(table), encoding="utf-8")
    return path


class SelectDotnetTestLaneTests(unittest.TestCase):
    def select(self, *paths: str, event: str = "pull_request") -> MODULE.LaneDecision:
        return MODULE.select_lane(list(paths), event)

    def test_leaf_only_change_yields_reduced_lane_excluding_every_family(self) -> None:
        decision = self.select("docs/VALIDATION.md", "site/src/index.html", ".kiro/specs/x/tasks.md")
        self.assertEqual("reduced", decision.lane)
        self.assertTrue(decision.partial)
        self.assertEqual(ALL_FAMILIES, decision.excluded)
        self.assertEqual((), decision.triggered)
        self.assertEqual(
            "FullyQualifiedName!~OutboundHttp&FullyQualifiedName!~SqlDatabaseMatrixTests&FullyQualifiedName!~WebFormsReview",
            decision.filter)

    def test_non_campaign_test_file_stays_reduced(self) -> None:
        decision = self.select("src/dotnet/tests/TraceMap.Tests/EvidenceDocsExportTests.cs")
        self.assertEqual("reduced", decision.lane)
        self.assertEqual(ALL_FAMILIES, decision.excluded)

    def test_shared_test_helper_forces_full_suite(self) -> None:
        for helper in ("TempDirectory.cs", "DirectoryLinkFixture.cs", "GitMetadataSensitiveCollection.cs"):
            decision = self.select("src/dotnet/tests/TraceMap.Tests/" + helper)
            self.assertEqual("full", decision.lane, helper)
            self.assertIn(helper, decision.reason)

    def test_shared_core_paths_force_full_suite(self) -> None:
        for path in (
            "src/dotnet/TraceMap.Core/ScanEngine.cs",
            "src/dotnet/TraceMap.Cli/Program.cs",
            "src/dotnet/TraceMap.Storage/SqliteIndexWriter.cs",
            "src/dotnet/TraceMap.Combine/Combiner.cs",
            "src/dotnet/TraceMap.Reporting/EvidenceDocsExport.cs",
            "src/dotnet/TraceMap.Reduction/Reducer.cs",
            "rules/rule-catalog.yml",
            "src/dotnet/Directory.Build.props",
            "global.json",
            "src/dotnet/TraceMap.sln",
            "src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj",
            "AGENTS.md",
            "scripts/select_dotnet_test_lane.py",
            ".github/workflows/adapter-validation.yml",
        ):
            decision = self.select("docs/README.md", path)
            self.assertEqual("full", decision.lane, path)
            self.assertFalse(decision.partial)
            self.assertEqual("", decision.filter)

    def test_campaign_sample_triggers_only_its_family(self) -> None:
        decision = self.select("samples/sql-database-matrix/net48/csharp/Program.cs")
        self.assertEqual("reduced", decision.lane)
        self.assertEqual(("SqlDatabaseMatrix",), decision.triggered)
        self.assertEqual(("OutboundHttp", "WebFormsReview"), decision.excluded)
        self.assertEqual("FullyQualifiedName!~OutboundHttp&FullyQualifiedName!~WebFormsReview", decision.filter)

    def test_campaign_test_file_triggers_its_family(self) -> None:
        decision = self.select("src/dotnet/tests/TraceMap.Tests/WebFormsReviewExecutionTests.cs")
        self.assertEqual(("WebFormsReview",), decision.triggered)
        self.assertEqual(("OutboundHttp", "SqlDatabaseMatrix"), decision.excluded)
        self.assertEqual("FullyQualifiedName!~OutboundHttp&FullyQualifiedName!~SqlDatabaseMatrixTests", decision.filter)
        decision = self.select("src/dotnet/tests/TraceMap.Tests/OutboundHttpLegacyCoverageTests.cs")
        self.assertEqual(("OutboundHttp",), decision.triggered)
        self.assertEqual("FullyQualifiedName!~SqlDatabaseMatrixTests&FullyQualifiedName!~WebFormsReview", decision.filter)

    def test_every_directory_level_msbuild_file_forces_full_suite(self) -> None:
        for name in ("Directory.Build.props", "Directory.Build.targets", "Directory.Build.rsp",
                     "Directory.Build.custom.props", "Directory.Packages.props"):
            decision = self.select("docs/README.md", "src/dotnet/" + name)
            self.assertEqual("full", decision.lane, name)

    def test_rename_out_of_a_trigger_area_still_counts_the_old_path(self) -> None:
        # The workflow passes both names of a rename; the old path keeps its classification.
        decision = self.select("docs/moved/ScanEngine.cs", "src/dotnet/TraceMap.Core/ScanEngine.cs")
        self.assertEqual("full", decision.lane)
        decision = self.select("docs/moved/Sql.cs", "samples/sql-database-matrix/net48/Sql.cs")
        self.assertEqual(("SqlDatabaseMatrix",), decision.triggered)

    def test_every_family_triggered_is_the_full_suite(self) -> None:
        decision = self.select(
            "samples/sql-database-matrix/x.cs",
            "samples/outbound-http-coverage/x.cs",
            "samples/fixture-build/x.vbproj")
        self.assertEqual("full", decision.lane)
        self.assertEqual("", decision.filter)

    def test_non_pull_request_events_run_full_suite(self) -> None:
        for event in ("push", "workflow_dispatch", "", "schedule"):
            decision = self.select("docs/README.md", event=event)
            self.assertEqual("full", decision.lane, event)

    def test_missing_change_data_runs_full_suite(self) -> None:
        self.assertEqual("full", self.select().lane)
        self.assertEqual("full", self.select("", "   ").lane)

    def test_large_change_set_runs_full_suite(self) -> None:
        paths = [f"docs/generated/file-{index}.md" for index in range(MODULE.LARGE_CHANGE_SET)]
        decision = MODULE.select_lane(paths, "pull_request")
        self.assertEqual("full", decision.lane)
        self.assertIn("large change set", decision.reason)

    def test_windows_separators_and_duplicates_are_normalised(self) -> None:
        decision = self.select("src\\dotnet\\TraceMap.Core\\ScanEngine.cs", "src/dotnet/TraceMap.Core/ScanEngine.cs")
        self.assertEqual("full", decision.lane)

    def test_main_writes_github_outputs_and_summary(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            output = Path(temp) / "github-output"
            stdin = io.StringIO("docs/README.md\n")
            stdin.isatty = lambda: False  # type: ignore[method-assign]
            stdout = io.StringIO()
            with mock.patch("sys.stdin", stdin), redirect_stdout(stdout):
                exit_code = MODULE.main(["--event", "pull_request", "--github-output", str(output)])
            self.assertEqual(0, exit_code)
            written = output.read_text(encoding="utf-8").splitlines()
            self.assertIn("lane=reduced", written)
            self.assertIn("partial=true", written)
            self.assertIn(
                "filter=FullyQualifiedName!~OutboundHttp&FullyQualifiedName!~SqlDatabaseMatrixTests&FullyQualifiedName!~WebFormsReview",
                written)
            matrix_lines = [line for line in written if line.startswith("matrix=")]
            self.assertEqual(1, len(matrix_lines))
            matrix = json.loads(matrix_lines[0][len("matrix="):])
            self.assertEqual(["shard-1", "shard-2", "shard-3", "rest"], [entry["name"] for entry in matrix["include"]])
            # The reason quotes a pull-request-authored path, so it is written in the
            # delimited form rather than as a `reason=` line another record could follow.
            opener = next(line for line in written if line.startswith("reason<<ghadelimiter_"))
            delimiter = opener[len("reason<<"):]
            start = written.index(opener)
            self.assertEqual(delimiter, written[start + 2])
            self.assertIn("changes are confined to leaf areas", written[start + 1])
            self.assertFalse(any(line.startswith("reason=") for line in written))
            self.assertIn("reduced (partial)", stdout.getvalue())
            self.assertIn("excluded families: OutboundHttp, SqlDatabaseMatrix, WebFormsReview", stdout.getvalue())
            self.assertIn("| rest | ubuntu-latest | everything not listed", stdout.getvalue())

    def test_json_output_keeps_the_top_level_decision_fields_and_adds_the_matrix(self) -> None:
        stdin = io.StringIO("src/dotnet/TraceMap.Core/ScanEngine.cs\n")
        stdin.isatty = lambda: False  # type: ignore[method-assign]
        stdout = io.StringIO()
        with mock.patch("sys.stdin", stdin), redirect_stdout(stdout):
            self.assertEqual(0, MODULE.main(["--event", "pull_request", "--json"]))
        payload = json.loads(stdout.getvalue())
        self.assertEqual("full", payload["lane"])
        self.assertEqual("", payload["filter"])
        self.assertEqual([], payload["excluded"])
        self.assertIn("shared-core path changed", payload["reason"])
        self.assertEqual(["shard-1", "shard-2", "shard-3", "rest"], [entry["name"] for entry in payload["matrix"]["include"]])

    def test_selector_inputs_force_full_suite(self) -> None:
        for path in ("scripts/dotnet-test-class-weights.json", "scripts/generate_dotnet_test_class_weights.py",
                     "scripts/test_generate_dotnet_test_class_weights.py"):
            self.assertEqual("full", self.select("docs/README.md", path).lane, path)


class ShardMatrixTests(unittest.TestCase):
    def shards(self, decision: MODULE.LaneDecision, event: str = PR) -> tuple[MODULE.Shard, ...]:
        return MODULE.build_shards(decision, event, SYNTHETIC)

    def test_push_is_one_self_hosted_entry_with_no_shard_filter(self) -> None:
        (shard,) = self.shards(MODULE.select_lane([], "push"), event="push")
        self.assertEqual("full", shard.name)
        self.assertEqual(MODULE.SELF_HOSTED_RUNNER, shard.runner)
        self.assertEqual("", shard.filter)
        self.assertEqual(MODULE.ARTIFACT, shard.artifact)
        self.assertTrue(shard.catch_all)
        self.assertEqual(sum(w.tests for w in SYNTHETIC), shard.expected_tests)
        self.assertEqual("", shard.trx)  # that trx would name the box's user and paths
        self.assertEqual({"name": "full", "runner": ["self-hosted", "linux-x64", "amd5950x"], "filter": "",
                          "artifact": MODULE.ARTIFACT, "trx": ""}, shard.matrix_entry())

    def test_full_lane_pull_request_packs_listed_shards_and_a_catch_all(self) -> None:
        shards = self.shards(FULL)
        self.assertEqual(["shard-1", "shard-2", "shard-3", "rest"], [s.name for s in shards])
        self.assertTrue(all(s.runner == MODULE.HOSTED_RUNNER for s in shards))
        self.assertEqual(
            ("TraceMap.Tests.OutboundHttpLegacyCoverageTests", "TraceMap.Tests.EvidenceDocsExportTests",
             "TraceMap.Tests.ScanManifestTests", "TraceMap.Tests.RuleCatalogTests"), shards[0].classes)
        self.assertEqual(
            ("TraceMap.Tests.WebFormsReviewPreparationTests", "TraceMap.Tests.OutboundHttpCoverageTests"), shards[1].classes)
        self.assertEqual(
            ("TraceMap.Tests.IlBodyEvidenceExtractorTests", "TraceMap.Tests.WebFormsReviewExecutionTestsRelocation"),
            shards[2].classes)
        self.assertEqual(
            ("TraceMap.Tests.SqlDatabaseMatrixTestsNet100", "TraceMap.Tests.WebFormsReviewExecutionTests"), shards[3].classes)
        self.assertEqual(
            "FullyQualifiedName~TraceMap.Tests.OutboundHttpLegacyCoverageTests."
            "|FullyQualifiedName~TraceMap.Tests.EvidenceDocsExportTests."
            "|FullyQualifiedName~TraceMap.Tests.ScanManifestTests."
            "|FullyQualifiedName~TraceMap.Tests.RuleCatalogTests.", shards[0].filter)
        listed = [name for shard in shards[:-1] for name in shard.classes]
        self.assertEqual("&".join(f"FullyQualifiedName!~{name}." for name in listed), shards[3].filter)
        self.assertEqual(8, len(listed))
        self.assertEqual(len(set(listed)), len(listed))
        self.assertEqual(set(w.name for w in SYNTHETIC), set(listed) | set(shards[3].classes))
        self.assertEqual(sum(w.tests for w in SYNTHETIC), sum(s.expected_tests for s in shards))
        self.assertEqual([False, False, False, True], [s.catch_all for s in shards])
        self.assertEqual(["", "", "", MODULE.ARTIFACT], [s.artifact for s in shards])
        self.assertEqual(["dotnet-trx-shard-1", "dotnet-trx-shard-2", "dotnet-trx-shard-3", "dotnet-trx-rest"],
                         [s.trx for s in shards])

    def test_workflow_dispatch_runs_hosted_shards(self) -> None:
        shards = self.shards(MODULE.select_lane([], "workflow_dispatch"), event="workflow_dispatch")
        self.assertEqual(4, len(shards))
        self.assertTrue(all(s.runner == MODULE.HOSTED_RUNNER for s in shards))

    def test_reduced_lane_ands_family_exclusions_into_every_shard_and_drops_empty_ones(self) -> None:
        shards = self.shards(MODULE.select_lane(["docs/README.md"], PR))
        # shard-2 held only campaign classes and is gone; the catch-all stays even with no known member.
        self.assertEqual(["shard-1", "shard-3", "rest"], [s.name for s in shards])
        self.assertEqual(
            ("TraceMap.Tests.EvidenceDocsExportTests", "TraceMap.Tests.ScanManifestTests", "TraceMap.Tests.RuleCatalogTests"),
            shards[0].classes)
        self.assertEqual(
            "|".join(f"FullyQualifiedName~{name}.{ALL_FAMILY_TERMS}" for name in shards[0].classes), shards[0].filter)
        self.assertEqual(("TraceMap.Tests.IlBodyEvidenceExtractorTests",), shards[1].classes)
        self.assertEqual(f"FullyQualifiedName~TraceMap.Tests.IlBodyEvidenceExtractorTests.{ALL_FAMILY_TERMS}", shards[1].filter)
        self.assertEqual(
            "&".join(f"FullyQualifiedName!~{name}." for name in shards[0].classes + shards[1].classes) + ALL_FAMILY_TERMS,
            shards[2].filter)
        self.assertEqual((), shards[2].classes)
        self.assertEqual(0, shards[2].expected_tests)
        self.assertEqual(MODULE.ARTIFACT, shards[2].artifact)
        self.assertEqual(25 + 4 + 3 + 95, sum(s.expected_tests for s in shards))

    def test_triggered_family_stays_listed_and_only_the_others_are_excluded(self) -> None:
        shards = self.shards(MODULE.select_lane(["samples/outbound-http-coverage/Program.cs"], PR))
        self.assertEqual(["shard-1", "shard-2", "shard-3", "rest"], [s.name for s in shards])
        terms = "&FullyQualifiedName!~SqlDatabaseMatrixTests&FullyQualifiedName!~WebFormsReview"
        self.assertEqual(("TraceMap.Tests.OutboundHttpCoverageTests",), shards[1].classes)
        self.assertEqual(f"FullyQualifiedName~TraceMap.Tests.OutboundHttpCoverageTests.{terms}", shards[1].filter)
        self.assertTrue(shards[0].filter.startswith(f"FullyQualifiedName~TraceMap.Tests.OutboundHttpLegacyCoverageTests.{terms}|"))
        rest_terms = shards[3].filter.split("&")
        self.assertIn("FullyQualifiedName!~TraceMap.Tests.OutboundHttpLegacyCoverageTests.", rest_terms)
        self.assertNotIn("FullyQualifiedName!~OutboundHttp", rest_terms)
        self.assertEqual(["FullyQualifiedName!~SqlDatabaseMatrixTests", "FullyQualifiedName!~WebFormsReview"], rest_terms[-2:])

    def test_class_token_boundary_keeps_a_sibling_from_matching(self) -> None:
        token = MODULE.ClassWeight("TraceMap.Tests.WebFormsReviewExecutionTests", 1.0, 1).token
        self.assertEqual("TraceMap.Tests.WebFormsReviewExecutionTests.", token)
        self.assertIn(token, "TraceMap.Tests.WebFormsReviewExecutionTests.Scan_lifecycle")
        self.assertNotIn(token, "TraceMap.Tests.WebFormsReviewExecutionTestsRelocation.Scan_lifecycle")

    def test_packing_is_deterministic_and_balanced(self) -> None:
        first = MODULE.pack_classes(SYNTHETIC, 4)
        self.assertEqual(first, MODULE.pack_classes(tuple(reversed(SYNTHETIC)), 4))
        loads = [sum(w.seconds for w in members) for members in first]
        self.assertEqual([405.0, 430.0, 470.0, 460.0], loads)
        with self.assertRaises(ValueError):
            MODULE.pack_classes(SYNTHETIC, 1)

    def test_matrix_json_is_one_line_of_include_entries(self) -> None:
        text = MODULE.matrix_json(self.shards(FULL))
        self.assertNotIn("\n", text)
        entries = json.loads(text)["include"]
        self.assertEqual(4, len(entries))
        for entry in entries:
            self.assertEqual({"name", "runner", "filter", "artifact", "trx"}, set(entry))
            self.assertIsInstance(entry["runner"], list)
            for token in entry["filter"].replace("|", "&").split("&"):
                self.assertRegex(token, r"^FullyQualifiedName!?~[A-Za-z0-9_.]+$")

    def test_load_class_weights_validates_the_table(self) -> None:
        classes = {"TraceMap.Tests.A": {"seconds": 1.5, "tests": 2}, "TraceMap.Tests.B": {"seconds": 0.5, "tests": 1}}
        with tempfile.TemporaryDirectory() as temp:
            directory = Path(temp)
            loaded = MODULE.load_class_weights(write_table(directory, classes))
            self.assertEqual((MODULE.ClassWeight("TraceMap.Tests.A", 1.5, 2), MODULE.ClassWeight("TraceMap.Tests.B", 0.5, 1)),
                             loaded.classes)
            self.assertEqual("abc1234", loaded.commit)
            cases = {
                "hand-edited class map": dict(boundedInputSha256="0" * 64),
                "wrong schema": dict(schemaVersion="dotnet-test-class-weights.v0"),
            }
            for label, overrides in cases.items():
                with self.assertRaises(ValueError, msg=label):
                    MODULE.load_class_weights(write_table(directory, classes, **overrides))
            for label, bad in {
                "empty": {},
                "ambiguous token": {"TraceMap.Tests.A": {"seconds": 1, "tests": 1}, "TraceMap.Tests.A.B": {"seconds": 1, "tests": 1}},
                "filter operator": {"TraceMap.Tests.A|B": {"seconds": 1, "tests": 1}},
                "no tests": {"TraceMap.Tests.A": {"seconds": 1, "tests": 0}},
                "negative seconds": {"TraceMap.Tests.A": {"seconds": -1, "tests": 1}},
            }.items():
                with self.assertRaises(ValueError, msg=label):
                    MODULE.load_class_weights(write_table(directory, bad))

    def test_checked_in_table_loads_and_packs_evenly(self) -> None:
        table = MODULE.load_class_weights().classes
        total_tests = sum(w.tests for w in table)
        total_seconds = sum(w.seconds for w in table)
        self.assertGreater(len(table), MODULE.HOSTED_SHARDS)
        shards = MODULE.build_shards(FULL, PR, table)
        self.assertEqual(MODULE.HOSTED_SHARDS, len(shards))
        self.assertEqual(total_tests, sum(s.expected_tests for s in shards))
        names = [name for shard in shards for name in shard.classes]
        self.assertEqual(len(set(names)), len(names))
        self.assertEqual(len(table), len(names))
        for shard in shards:
            self.assertLess(abs(shard.seconds - total_seconds / MODULE.HOSTED_SHARDS), 0.15 * total_seconds / MODULE.HOSTED_SHARDS)
        reduced = MODULE.build_shards(MODULE.select_lane(["docs/README.md"], PR), PR, table)
        self.assertEqual("rest", reduced[-1].name)
        self.assertTrue(all("WebFormsReview" not in name and "SqlDatabaseMatrixTests" not in name and "OutboundHttp" not in name
                            for shard in reduced for name in shard.classes))


if __name__ == "__main__":
    unittest.main()
