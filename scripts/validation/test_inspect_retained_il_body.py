import importlib.util
import hashlib
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

    def graph(self, calls, digest=None):
        path = self.path.parent / "method-graph.local.json"
        path.write_text(json.dumps(dict(schemaVersion="retained-method-graph.v1",
            indexSha256=digest or hashlib.sha256(self.path.read_bytes()).hexdigest(),
            graph=dict(calls=calls, nodes=[]))), encoding="utf-8")
        return path

    def test_graph_comparison_uses_exact_body_not_overload_name(self):
        self.call()
        graph = self.graph([dict(bodyFactId="OtherBody", factId="OtherCall")])
        counts, _ = inspection.inspect(self.path, "Abody", graph)
        self.assertEqual(0, counts["exact_body_calls_in_graph"])
        self.assertEqual(1, counts["exact_body_calls_outside_graph"])
        self.assertEqual(0, counts["exact_body_owner_matches"])

    def test_exact_body_graph_call_is_counted(self):
        self.call()
        graph = self.graph([dict(bodyFactId="Abody", factId="Acall")])
        counts, _ = inspection.inspect(self.path, "Abody", graph)
        self.assertEqual(1, counts["exact_body_calls_in_graph"])
        self.assertEqual(0, counts["exact_body_calls_outside_graph"])

    def test_stale_graph_refused(self):
        with self.assertRaises(ValueError):
            inspection.inspect(self.path, "Abody", self.graph([], "wrong"))

    def test_unknown_graph_call_refused(self):
        with self.assertRaises(ValueError):
            inspection.inspect(self.path, "Abody", self.graph([dict(bodyFactId="Abody", factId="missing")]))

    def test_duplicate_graph_call_refused(self):
        self.call()
        call = dict(bodyFactId="Abody", factId="Acall")
        with self.assertRaises(ValueError):
            inspection.inspect(self.path, "Abody", self.graph([call, call]))

    def test_owner_join_is_source_index_local_and_hash_checked(self):
        with closing(sqlite3.connect(self.path)) as db, db:
            db.execute("UPDATE combined_facts SET properties_json=? WHERE combined_fact_id='Abody'",
                       (json.dumps(dict(rawFileSha256="hash", compiledFactId="owner")),))
        self.add("owner", "ManagedMethodDeclared", dict(rawFileSha256="hash"))
        self.add("owner", "ManagedMethodDeclared", dict(rawFileSha256="different"), "B")
        counts, _ = inspection.inspect(self.path, "Abody", self.graph([]))
        self.assertEqual(1, counts["exact_body_owner_matches"])
        self.assertEqual(1, counts["owner_artifact_hash_matches"])


if __name__ == "__main__": unittest.main()
