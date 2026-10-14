import importlib.util
from contextlib import closing
import json
from pathlib import Path
import sqlite3
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("inspection", Path(__file__).resolve().parents[1] / "inspect-retained-il-body.py")
inspection = importlib.util.module_from_spec(spec)
spec.loader.exec_module(inspection)


class InspectionTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.path = Path(self.temp.name) / "combined.sqlite"
        with closing(sqlite3.connect(self.path)) as db, db:
            db.execute("CREATE TABLE combined_facts(combined_fact_id TEXT, original_fact_id TEXT, source_index_id TEXT, fact_type TEXT, properties_json TEXT)")
        self.add("body", "ManagedIlBodyDeclared", {"rawFileSha256": "hash"})
        self.add("gap", "AnalysisGap", {"ilBodyFactId": "body"})

    def add(self, identity, kind, props, source="A"):
        with closing(sqlite3.connect(self.path)) as db, db:
            db.execute("INSERT INTO combined_facts VALUES(?,?,?,?,?)", (source + identity, identity, source, kind, json.dumps(props)))

    def call(self, source="A", digest="hash"):
        self.add("call", "ManagedIlCallObserved", dict(ilBodyFactId="body", rawFileSha256=digest,
                 ilOffset="10", opcode="callvirt", targetIdentity="private-target"), source)

    def test_gaps_do_not_hide_compiled_calls_and_file_is_unchanged(self):
        self.call()
        before = self.path.read_bytes()
        counts, calls = inspection.inspect(self.path, "Agap")
        self.assertEqual(1, counts["calls"])
        self.assertEqual(1, counts["gaps"])
        self.assertEqual([(10, "callvirt", "private-target")], calls)
        self.assertEqual(before, self.path.read_bytes())

    def test_same_body_id_in_other_index_is_not_joined(self):
        self.call("B")
        self.assertEqual(0, inspection.inspect(self.path, "Agap")[0]["calls"])

    def test_ambiguous_original_id_refused(self):
        self.add("gap", "AnalysisGap", {"ilBodyFactId": "body"}, "B")
        with self.assertRaises(ValueError): inspection.inspect(self.path, "gap")

    def test_hash_mismatch_refused(self):
        self.call(digest="different")
        with self.assertRaises(ValueError): inspection.inspect(self.path, "Agap")

    def test_missing_body_refused(self):
        self.add("missing", "AnalysisGap", {"ilBodyFactId": "absent"})
        with self.assertRaises(ValueError): inspection.inspect(self.path, "Amissing")

    def test_source_call_edge_is_not_a_compiled_call(self):
        self.add("source", "CallEdge", {"ilBodyFactId": "body"})
        self.assertEqual(0, inspection.inspect(self.path, "Abody")[0]["calls"])

    def test_row_overflow_is_not_partial_success(self):
        with closing(sqlite3.connect(self.path)) as db, db:
            db.executemany("INSERT INTO combined_facts VALUES(?,?,?,?,?)",
                [(f"extra{i}", f"extra{i}", "A", "AnalysisGap", '{"ilBodyFactId":"body"}') for i in range(1024)])
        with self.assertRaises(ValueError): inspection.inspect(self.path, "Abody")

    def test_missing_database_is_not_created(self):
        missing = self.path.parent / "missing.sqlite"
        with self.assertRaises(sqlite3.Error): inspection.inspect(missing, "body")
        self.assertFalse(missing.exists())


if __name__ == "__main__": unittest.main()
