#!/usr/bin/env python3

from __future__ import annotations

import importlib.util
import io
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
        self.assertNotIn("SqlDatabaseMatrixTests", decision.filter)
        self.assertIn("FullyQualifiedName!~WebFormsReview", decision.filter)

    def test_campaign_test_file_triggers_its_family(self) -> None:
        decision = self.select("src/dotnet/tests/TraceMap.Tests/WebFormsReviewExecutionTests.cs")
        self.assertEqual(("WebFormsReview",), decision.triggered)
        self.assertEqual(("OutboundHttp", "SqlDatabaseMatrix"), decision.excluded)
        decision = self.select("src/dotnet/tests/TraceMap.Tests/OutboundHttpLegacyCoverageTests.cs")
        self.assertEqual(("OutboundHttp",), decision.triggered)

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
            self.assertTrue(any(line.startswith("filter=FullyQualifiedName!~") for line in written))
            self.assertIn("reduced (partial)", stdout.getvalue())
            self.assertIn("excluded families: OutboundHttp, SqlDatabaseMatrix, WebFormsReview", stdout.getvalue())


if __name__ == "__main__":
    unittest.main()
