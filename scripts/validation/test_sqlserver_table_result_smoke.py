"""Public synthetic table-backed result smoke guards; no CLI, network, or SQL execution."""
import hashlib
import ast
import os
import subprocess
import sys
import tempfile
import importlib.util
from pathlib import Path
import unittest
from unittest import mock
ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("table_smoke", ROOT / "scripts/sqlserver-table-result-evidence/smoke.py")
SMOKE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SMOKE)
M, X, U = "bounded-match", "mismatch", "unknown"


class TableResultSmokeGuards(unittest.TestCase):
    def test_optimized_execution_refuses_before_output_or_helpers(self):
        for flag, env_value in (("-O", None), ("-OO", None), (None, "1")):
            with self.subTest(flag=flag, env=env_value), tempfile.TemporaryDirectory() as tmp:
                output = Path(tmp) / "must-not-exist"
                env = dict(os.environ)
                env.pop("PYTHONOPTIMIZE", None)
                if env_value:
                    env["PYTHONOPTIMIZE"] = env_value
                result = subprocess.run([sys.executable, *([flag] if flag else []), SMOKE.__file__,
                                         "--output", str(output)], env=env, capture_output=True, text=True, check=False)
                self.assertNotEqual(0, result.returncode)
                self.assertIn("requires Python optimization disabled", result.stderr)
                self.assertFalse(output.exists())
        source = Path(SMOKE.__file__).read_text()
        self.assertLess(source.index("if sys.flags.optimize:"), source.index("SPEC.loader.exec_module"))
        self.assertFalse(any(isinstance(n, ast.Assert) for n in ast.walk(ast.parse(source))))

    def test_invalid_aggregate_never_publishes_a_receipt(self):
        good = {"gaps": [], "observedCounts": {M: 1}, "expectedCounts": {M: 1},
                "refusals": [{"refused": True} for _ in range(24)]}
        for change in ({"gaps": ["missing:case"]}, {"observedCounts": {M: 2}},
                       {"refusals": []}, {"refusals": [{"refused": False}] * 24}):
            with self.subTest(change=change), tempfile.TemporaryDirectory() as tmp:
                with mock.patch.object(SMOKE.HELPERS, "verify_scan_hashes") as verify:
                    with self.assertRaises(ValueError):
                        SMOKE.publish_receipt(Path(tmp), dict(good, **change))
                    verify.assert_not_called()
                self.assertFalse((Path(tmp) / "smoke-receipt.json").exists())

    def test_bad_export_and_report_status_are_rejected(self):
        with self.assertRaises(ValueError):
            SMOKE.check_export({"version": "wrong"}, "base.sql", "tables.sql", "complete", {}, "a", "b")
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            for name in ("report", "repeat"):
                (root / name).mkdir()
                SMOKE.HELPERS.write(root / name / "result-column-comparison.json", {
                    "comparison": {"coverage": "partial", "verdict": "no-aggregate-clean-verdict",
                                   "comparisons": [{"factId": "f", "status": X}]}})
                (root / name / "report.md").write_text("No aggregate clean verdict")
            with self.assertRaises(ValueError):
                SMOKE.check_report(root, {"factId": "f"}, [M])

    def test_denominator_is_derived_from_the_authored_matrices(self):
        self.assertEqual(SMOKE.EXPECTED_TOTALS, SMOKE.expected_totals())
        self.assertEqual({M: 22, X: 5, U: 44}, SMOKE.expected_totals())
        plan = SMOKE.case_plan()
        self.assertEqual(64, len(plan))
        self.assertEqual(len(SMOKE.RESULT.SOURCE_CASES) + len(SMOKE.EXTRA_CASES), len(plan))
        # The replay keeps the retained #912 per-case expectations unchanged.
        replay = {key: statuses for key, _, _, statuses, _ in plan[:len(SMOKE.RESULT.SOURCE_CASES)]}
        self.assertEqual({k: list(v) for k, v in SMOKE.RESULT.SOURCE_CASES.items()}, replay)

    def test_every_cell_names_existing_public_templates_and_a_known_outcome(self):
        for key, template, tables, statuses, shape in SMOKE.case_plan():
            self.assertIn(key, SMOKE.RESULT.SOURCE_CASES)
            self.assertTrue((SMOKE.TEMPLATES / template).is_file(), template)
            if tables is not None:
                self.assertTrue((SMOKE.TEMPLATES / tables).is_file(), tables)
            self.assertTrue(set(statuses) <= {M, X, U})
            if shape == "complete":
                self.assertIn(template, SMOKE.EXPECTED_PROJECTIONS)
            else:
                # An unknown shape can never produce a match or a mismatch.
                self.assertEqual([U] * len(statuses), statuses, (key, template))
        self.assertEqual(set(SMOKE.REPLAY_TEMPLATES.values()) | {"base.sql"}, set(SMOKE.REPLAY_SHAPES))

    def test_unsupported_families_are_each_covered_once_by_name(self):
        reasons = {shape for *_, shape in SMOKE.EXTRA_CASES if shape != "complete"}
        for family in ("select-star-unsupported", "join-unsupported", "cte-unsupported", "subquery-unsupported",
                       "set-operation-unsupported", "temporary-table-unsupported", "exec-or-dynamic-sql-unsupported",
                       "conditional-body-unsupported", "body-statement-count-unsupported", "table-dependency-missing",
                       "column-dependency-missing", "cross-database-or-server-reference-unsupported",
                       "table-reference-case-policy-unknown", "column-case-policy-unknown", "table-definition-unsupported"):
            self.assertIn(family, reasons)

    def test_manifest_mirrors_core_field_order_and_hashes_public_files_only(self):
        hashes = {"base.sql": "a" * 64, "tables.sql": "b" * 64}
        doc = SMOKE.manifest("synthetic", "1" * 40, ["tables.sql", "base.sql"], hashes)
        self.assertEqual(["version", "repo", "revision", "server", "database", "publicSynthetic", "files", "provenance"], list(doc))
        self.assertEqual(SMOKE.INPUT_VERSION, doc["version"])
        self.assertEqual(["base.sql", "tables.sql"], [f["path"] for f in doc["files"]])
        self.assertEqual(SMOKE.GENERATOR, doc["provenance"]["generatorSha256"])
        self.assertEqual(hashlib.sha256(Path(SMOKE.__file__).read_bytes()).hexdigest(), SMOKE.GENERATOR)

    def test_retained_result_smoke_is_reused_not_modified(self):
        self.assertEqual({"json": {M: 18, X: 5, U: 18}, "ddl": {M: 18, X: 5, U: 29}}, SMOKE.RESULT.expected_totals())
        self.assertEqual(hashlib.sha256((ROOT / "scripts/sqlserver-result-column-evidence/smoke.py").read_bytes()).hexdigest(),
                         SMOKE.RESULT.GENERATOR)


if __name__ == "__main__":
    unittest.main()
