"""Public synthetic table-backed result smoke guards; no CLI, network, or SQL execution."""
import hashlib
import importlib.util
from pathlib import Path
import unittest
ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("table_smoke", ROOT / "scripts/sqlserver-table-result-evidence/smoke.py")
SMOKE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SMOKE)
M, X, U = "bounded-match", "mismatch", "unknown"


class TableResultSmokeGuards(unittest.TestCase):
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
