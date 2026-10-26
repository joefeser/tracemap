"""Public synthetic DataTable consumer smoke guards; no CLI, network, or SQL execution."""
import ast
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest import mock

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("datatable_smoke", ROOT / "scripts/sqlserver-datatable-evidence/smoke.py")
SMOKE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SMOKE)
M, X, U = "bounded-match", "mismatch", "unknown"


def fact(name, line, state=None):
    properties = {"operationKind": "data-adapter-fill"}
    if state is not None:
        properties["sqlServerDataTableState"] = json.dumps(state)
    return {"factId": f"id-{name}-{line}", "repo": "synthetic", "commitSha": "1" * 40, "ruleId": "database.operation.call-pattern.v1",
            "evidenceTier": "Tier1Semantic", "sourceSymbol": f"DataTableCases.{name}(string, int, bool)",
            "evidence": {"filePath": "DataTables.cs", "startLine": line, "endLine": line, "extractorId": "CSharpSemanticExtractor",
                         "extractorVersion": "csharp-semantic/0.22.14"}, "properties": properties}


class DataTableSmokeGuards(unittest.TestCase):
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
        self.assertEqual({M: 54, X: 12, U: 88}, SMOKE.expected_totals())
        plan = SMOKE.case_plan()
        self.assertEqual(148, len(plan))
        self.assertEqual(28 * 3, sum(1 for key, *_ in plan if key in SMOKE.CORE_CASES))
        self.assertTrue(all(origin == "ddl-table-result" and statuses == [U] for key, origin, _, statuses in plan if key in SMOKE.STATE_CASES))
        self.assertFalse(set(SMOKE.CORE_CASES) & set(SMOKE.STATE_CASES))

    def test_missing_unexpected_and_duplicate_cases_are_gaps(self):
        observed = [k for k in list(SMOKE.CORE_CASES) + list(SMOKE.STATE_CASES) if k != "CtorName"] + ["Surprise", "TextCommand"]
        gaps = SMOKE.case_gaps(observed)
        self.assertIn("missing:CtorName", gaps)
        self.assertIn("unexpected:Surprise", gaps)
        self.assertIn("duplicate:TextCommand", gaps)
        keyed = SMOKE.case_keys([fact("RepeatedFill", 20), fact("CtorName", 10), fact("RepeatedFill", 21)])
        self.assertEqual(["CtorName", "RepeatedFill#0", "RepeatedFill#1"], [k for _, k in keyed])

    def test_datatable_digest_is_contract_tagged_and_distinct_from_reader_digest(self):
        state = {"version": "sqlserver-datatable/1", "coverage": "bounded", "gap": None, "accesses": []}
        item = fact("CtorName", 10, state)
        projected = SMOKE.datatable_projection(item)
        self.assertEqual("sqlserver-datatable-bindings/1", projected["contract"])
        self.assertEqual(state, projected["dataTableState"])
        self.assertNotEqual(SMOKE.HELPERS.digest(projected), SMOKE.HELPERS.digest(SMOKE.RESULT.reader_projection(item)))
        bindings = SMOKE.binding(item, "TwoFills#1", "1" * 40, projected)
        self.assertEqual("sqlserver-datatable-bindings/1", bindings["version"])
        self.assertEqual("dbo.ReadOther", bindings["calls"][0]["commandText"])
        self.assertNotIn("readerEvidenceSha256", bindings["calls"][0])

    def test_state_mismatch_and_invalid_aggregate_never_publish_a_receipt(self):
        with self.assertRaises(ValueError):
            SMOKE.check_state("RowAlias", fact("RowAlias", 10, {"version": "sqlserver-datatable/1", "coverage": "unknown",
                                                                 "gap": "row-reassignment", "accesses": []}))
        with self.assertRaises(ValueError):
            SMOKE.check_state("CtorName", fact("CtorName", 10, {"version": "sqlserver-reader/1", "coverage": "bounded", "gap": None,
                                                                 "commandType": "StoredProcedure", "accesses": [{}]}))
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
            report = {"version": "sqlserver-datatable-comparison/1", "consumerVersion": "sqlserver-datatable/1",
                      "bindingsVersion": "sqlserver-datatable-bindings/1", "schemaRoute": "result-schema",
                      "origin": "independent-synthetic-result-declarations", "coverage": "partial", "verdict": "no-aggregate-clean-verdict",
                      "generatorSha256": "a" * 64, "schemaEvidenceSha256": SMOKE.HELPERS.digest({"version": "x"}),
                      "comparisons": [{"factId": "f", "status": X, "reasons": [], "ruleId": SMOKE.RULE, "tier": "Tier2Structural",
                                       "sourceTier": "Tier1Semantic"}]}
            for name in ("report", "repeat"):
                (root / name).mkdir()
                SMOKE.HELPERS.write(root / name / "datatable-column-comparison.json", report)
                (root / name / "report.md").write_text("No aggregate clean verdict")
            self.assertEqual([X], SMOKE.check_report(root, {"factId": "f"}, [X], "result-schema", None, "a" * 64, schema)[0])
            for expected, origin in (([M], "result-schema"), ([X], "ddl-result")):
                with self.subTest(expected=expected, origin=origin), self.assertRaises(ValueError):
                    SMOKE.check_report(root, {"factId": "f"}, expected, origin, None, "a" * 64, schema)


if __name__ == "__main__":
    unittest.main()
