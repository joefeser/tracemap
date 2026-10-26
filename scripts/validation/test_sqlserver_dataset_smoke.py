"""Public synthetic DataSet consumer smoke guards; no CLI, network, or SQL execution."""
import ast
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from unittest import mock

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("dataset_smoke", ROOT / "scripts/sqlserver-dataset-evidence/smoke.py")
SMOKE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SMOKE)
M, X, U = "bounded-match", "mismatch", "unknown"


def fact(name, line, state=None, table=None):
    properties = {"operationKind": "data-adapter-fill",
                  "sqlServerDataTableState": json.dumps(table or {"version": "sqlserver-datatable/1", "coverage": "unknown",
                                                                  "gap": "fill-overload-unsupported", "accesses": []})}
    if state is not None:
        properties["sqlServerDataSetState"] = json.dumps(state)
    return {"factId": f"id-{name}-{line}", "repo": "synthetic", "commitSha": "1" * 40, "ruleId": "database.operation.call-pattern.v1",
            "evidenceTier": "Tier1Semantic", "sourceSymbol": f"DataSetCases.{name}(string, int, bool)",
            "evidence": {"filePath": "DataSets.cs", "startLine": line, "endLine": line, "extractorId": "CSharpSemanticExtractor",
                         "extractorVersion": "csharp-semantic/0.22.15"}, "properties": properties}


class DataSetSmokeGuards(unittest.TestCase):
    def test_native_fixtures_are_in_the_normal_test_build_graph(self):
        project = ROOT / "src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj"
        references = { (project.parent / node.attrib["Include"].replace("\\", "/")).resolve(): node
                       for node in ET.parse(project).iter("ProjectReference") }
        for language, extension in (("csharp", "csproj"), ("vb", "vbproj")):
            fixture = ROOT / "samples/sqlserver-dataset-evidence" / language / f"DataSets.{extension}"
            with self.subTest(language=language):
                self.assertIn(fixture, references)
                self.assertEqual("false", references[fixture].get("ReferenceOutputAssembly"))
                self.assertIsNone(references[fixture].get("Condition"))

    def test_optimized_execution_refuses_before_output_or_helpers(self):
        for flag, env_value in (("-O", None), ("-OO", None), (None, "1")):
            with self.subTest(flag=flag, env=env_value), tempfile.TemporaryDirectory() as tmp:
                output = Path(tmp) / "must-not-exist"
                env = dict(os.environ)
                env.pop("PYTHONOPTIMIZE", None)
                if env_value:
                    env["PYTHONOPTIMIZE"] = env_value
                result = subprocess.run([sys.executable, *([flag] if flag else []), SMOKE.__file__, "--output", str(output)],
                                        env=env, capture_output=True, text=True, check=False)
                self.assertNotEqual(0, result.returncode)
                self.assertIn("requires Python optimization disabled", result.stderr)
                self.assertFalse(output.exists())
        source = Path(SMOKE.__file__).read_text()
        self.assertLess(source.index("if sys.flags.optimize:"), source.index("SPEC.loader.exec_module"))
        self.assertFalse(any(isinstance(n, ast.Assert) for n in ast.walk(ast.parse(source))))

    def test_denominator_is_derived_from_the_authored_matrix(self):
        self.assertEqual(SMOKE.EXPECTED_TOTALS, SMOKE.expected_totals())
        self.assertEqual({M: 78, X: 12, U: 88}, SMOKE.expected_totals())
        plan = SMOKE.case_plan()
        self.assertEqual(169, len(plan))
        self.assertEqual(35 * 3, sum(1 for key, *_ in plan if key in SMOKE.CORE_CASES))
        self.assertEqual(64, len(SMOKE.STATE_CASES))
        self.assertEqual(24, SMOKE.REFUSAL_COUNT)
        self.assertTrue(all(origin == "ddl-table-result" and statuses == [U] for key, origin, _, statuses in plan if key in SMOKE.STATE_CASES))
        self.assertFalse(set(SMOKE.CORE_CASES) & set(SMOKE.STATE_CASES))

    def test_missing_unexpected_and_duplicate_cases_are_gaps(self):
        observed = [k for k in list(SMOKE.CORE_CASES) + list(SMOKE.STATE_CASES) if k != "IndexZero"] + ["Surprise", "TextCommand"]
        gaps = SMOKE.case_gaps(observed)
        self.assertIn("missing:IndexZero", gaps)
        self.assertIn("unexpected:Surprise", gaps)
        self.assertIn("duplicate:TextCommand", gaps)
        keyed = SMOKE.case_keys([fact("RepeatedFill", 20), fact("IndexZero", 10), fact("RepeatedFill", 21)])
        self.assertEqual(["IndexZero", "RepeatedFill#0", "RepeatedFill#1"], [k for _, k in keyed])

    def test_dataset_digest_is_contract_tagged_and_distinct_from_datatable_and_reader_digests(self):
        state = {"version": "sqlserver-dataset/1", "coverage": "bounded", "gap": None, "accesses": []}
        item = fact("IndexZero", 10, state)
        projected = SMOKE.dataset_projection(item)
        self.assertEqual("sqlserver-dataset-bindings/1", projected["contract"])
        self.assertEqual(state, projected["dataSetState"])
        self.assertNotIn("dataTableState", projected)
        table = SMOKE.datatable_projection(item)
        self.assertEqual(("sqlserver-datatable-bindings/1", "fill-overload-unsupported"), (table["contract"], table["dataTableState"]["gap"]))
        self.assertEqual(list(projected)[:-1], list(table)[:-1])
        for other in (table, SMOKE.RESULT.reader_projection(item)):
            self.assertNotEqual(SMOKE.HELPERS.digest(projected), SMOKE.HELPERS.digest(other))
        bindings = SMOKE.binding(item, "TwoFills#1", "1" * 40, projected)
        self.assertEqual("sqlserver-dataset-bindings/1", bindings["version"])
        self.assertEqual("dbo.ReadOther", bindings["calls"][0]["commandText"])
        self.assertIn("dataSetEvidenceSha256", bindings["calls"][0])
        self.assertNotIn("dataTableEvidenceSha256", bindings["calls"][0])

    def test_state_mismatch_and_invalid_aggregate_never_publish_a_receipt(self):
        with self.assertRaises(ValueError):
            SMOKE.check_state("RowAlias", fact("RowAlias", 10, {"version": "sqlserver-dataset/1", "coverage": "unknown",
                                                                 "gap": "row-reassignment", "accesses": []}))
        with self.assertRaises(ValueError):
            SMOKE.check_state("IndexZero", fact("IndexZero", 10, {"version": "sqlserver-datatable/1", "coverage": "bounded", "gap": None,
                                                                   "commandType": "StoredProcedure", "accesses": [{}]}))
        bounded = {"version": "sqlserver-dataset/1", "coverage": "bounded", "gap": None, "commandType": "StoredProcedure", "accesses": [{}]}
        self.assertIsNone(SMOKE.check_state("IndexZero", fact("IndexZero", 10, bounded)))
        # A DataSet site whose sibling DataTable state is bounded (or a DataTable site that is not) is a drift, not a pass.
        with self.assertRaises(ValueError):
            SMOKE.check_state("IndexZero", fact("IndexZero", 10, bounded, {"version": "sqlserver-datatable/1", "coverage": "bounded", "gap": None}))
        with self.assertRaises(ValueError):
            SMOKE.check_state("FillDataTable", fact("FillDataTable", 10, {"version": "sqlserver-dataset/1", "coverage": "unknown",
                                                                           "gap": "fill-overload-unsupported", "accesses": []}))
        good = {"gaps": [], "observedCounts": {M: 1}, "expectedCounts": {M: 1}, "refusals": [{"refused": True}] * SMOKE.REFUSAL_COUNT}
        for change in ({"gaps": ["missing:CtorName"]}, {"observedCounts": {M: 2}}, {"refusals": []},
                       {"refusals": [{"refused": False}] * SMOKE.REFUSAL_COUNT}):
            with self.subTest(change=change), tempfile.TemporaryDirectory() as tmp:
                with mock.patch.object(SMOKE.HELPERS, "verify_scan_hashes") as verify:
                    with self.assertRaises(ValueError):
                        SMOKE.publish_receipt(Path(tmp), dict(good, **change))
                    verify.assert_not_called()
                self.assertFalse((Path(tmp) / "smoke-receipt.json").exists())

    def test_report_status_or_contract_drift_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            schema = root / "schema.json"
            SMOKE.HELPERS.write(schema, {"version": "x"})
            report = {"version": "sqlserver-dataset-comparison/1", "consumerVersion": "sqlserver-dataset/1",
                      "bindingsVersion": "sqlserver-dataset-bindings/1", "schemaRoute": "result-schema",
                      "origin": "independent-synthetic-result-declarations", "coverage": "partial", "verdict": "no-aggregate-clean-verdict",
                      "generatorSha256": "a" * 64, "schemaEvidenceSha256": SMOKE.HELPERS.digest({"version": "x"}),
                      "comparisons": [{"factId": "f", "status": X, "reasons": [], "ruleId": SMOKE.RULE, "tier": "Tier2Structural",
                                       "sourceTier": "Tier1Semantic"}]}
            for name in ("report", "repeat"):
                (root / name).mkdir()
                SMOKE.HELPERS.write(root / name / "dataset-column-comparison.json", report)
                (root / name / "report.md").write_text("No aggregate clean verdict")
            self.assertEqual([X], SMOKE.check_report(root, {"factId": "f"}, [X], "result-schema", None, "a" * 64, schema)[0])
            for expected, origin in (([M], "result-schema"), ([X], "ddl-result")):
                with self.subTest(expected=expected, origin=origin), self.assertRaises(ValueError):
                    SMOKE.check_report(root, {"factId": "f"}, expected, origin, None, "a" * 64, schema)


if __name__ == "__main__":
    unittest.main()
