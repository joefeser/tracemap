"""Public synthetic regression guards for the independent HTTP smoke oracle."""
import json
import fnmatch
import pathlib
import tempfile
import unittest
from http_matrix_expectations import load_expectations, assert_calls, assert_compiled_cases


class MatrixOracleTests(unittest.TestCase):
    def test_workflow_triggers_cover_producers_consumers_and_oracle(self):
        root = pathlib.Path(__file__).resolve().parents[2]
        workflow = (root / ".github/workflows/outbound-http-compatibility.yml").read_text(encoding="utf-8")
        for section in [workflow.split("  push:\n", 1)[1].split("  pull_request:\n", 1)[0],
                        workflow.split("  pull_request:\n", 1)[1].split("  workflow_dispatch:", 1)[0]]:
            paths = [line.strip()[3:-1] for line in section.splitlines() if line.strip().startswith("- '")]
            for path in ["src/dotnet/TraceMap.Core/CSharpSemanticExtractor.cs",
                         "src/dotnet/TraceMap.Core/VisualBasicSemanticExtractor.cs",
                         "src/dotnet/TraceMap.Core/CombinedSurfaceProjection.cs",
                         "src/dotnet/TraceMap.Reporting/GroupedCompiledPathHandoff.cs",
                         "src/dotnet/TraceMap.Reporting/GroupedCompiledPathReportWriter.cs",
                         "scripts/validation/http_matrix_expectations.py",
                         "scripts/validation/test_http_matrix_expectations.py",
                         "samples/outbound-http-coverage/CONTROL-EXPECTATIONS.json"]:
                self.assertTrue(any(fnmatch.fnmatchcase(path, pattern) for pattern in paths), path)

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = pathlib.Path(self.temp.name)
        self.inventory = self.root / "OVERLOADS.md"
        self.inventory.write_text("| Case000 | API | signature |\n", encoding="utf-8")
        self.source = self.root / "Matrix.cs"
        self.source.write_text("public void Case000() {}\n", encoding="utf-8")
        self.controls = self.root / "controls.json"
        self.controls.write_text(json.dumps({"controls": {}}), encoding="utf-8")

    def load(self):
        return load_expectations(self.root, "csharp", self.inventory, None)

    def test_matching_independent_inventory(self):
        self.assertEqual({"Case000": 1}, self.load()[0])

    def test_added_source_case_requires_inventory_update(self):
        self.source.write_text("public void Case000() {}\npublic void Case001() {}\n", encoding="utf-8")
        with self.assertRaises(ValueError):
            self.load()

    def test_duplicate_inventory_refused(self):
        self.inventory.write_text("| Case000 | a | b |\n" * 2, encoding="utf-8")
        with self.assertRaises(ValueError):
            self.load()

    def test_unlisted_control_refused(self):
        (self.root / "Controls.cs").write_text("public void NewControl() {}", encoding="utf-8")
        with self.assertRaises(ValueError):
            load_expectations(self.root, "csharp", self.inventory, self.controls)

    def test_missing_required_controls_refused(self):
        with self.assertRaises(ValueError):
            load_expectations(self.root, "csharp", self.inventory, self.controls)

    def test_missing_case_is_not_hidden_by_extra_control(self):
        facts = [{"factType": "HttpCallDetected", "evidenceTier": "Tier1Semantic",
                  "evidence": {"filePath": "Controls.cs", "startLine": 1}}]
        with self.assertRaises(AssertionError):
            assert_calls(facts, "csharp", "semantic", {"Case000": 1}, {"Control": 1},
                         {"Control": {"semantic": 0}})

    def test_compiled_new_case_must_be_verified(self):
        facts = [{"factType": "ManagedIlCallObserved", "targetSymbol": "x|method:7:Case000|x"}]
        with self.assertRaises(AssertionError):
            assert_compiled_cases(facts, {"Case000": 1, "Case001": 2})
        with self.assertRaises(AssertionError):
            assert_compiled_cases(facts, {})


if __name__ == "__main__":
    unittest.main()
