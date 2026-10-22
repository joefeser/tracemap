import contextlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("verifier", Path(__file__).parents[1] / "verify-http-report-provenance.py")
verifier = importlib.util.module_from_spec(spec)
spec.loader.exec_module(verifier)


class ProvenanceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.cli = self.root / "TraceMap.Cli.dll"
        self.cli.write_bytes(b"synthetic CLI")
        self.reporting = self.root / "TraceMap.Reporting.dll"
        self.reporting.write_bytes(b"synthetic reporting")
        self.handoff = self.root / verifier.HANDOFF
        self.handoff.write_text(json.dumps({
            "schemaVersion": "webforms-compiled-grouped-handoff.v1",
            "ruleId": "workflow.webforms.compiled-grouped-handoff.v1",
            "generatorSha256": verifier.digest(self.reporting), "inputIndexSha256": "a" * 64}))
        self.receipt = self.root / "handler-requery.local.json"
        self.data = dict(schemaVersion="webforms-handler-requery.v1",
                         ruleId="workflow.webforms.retained-handler-requery.v1",
                         reportVariant="grouped-paths", query={"toSurface": "http-client"},
                         generatorSha256=verifier.digest(self.cli),
                         reportingGeneratorSha256=verifier.digest(self.reporting),
                         combinedIndexSha256="a" * 64,
                         artifacts=[dict(relativePath=verifier.HANDOFF,
                                         bytes=self.handoff.stat().st_size,
                                         sha256=verifier.digest(self.handoff))])
        self.save()

    def save(self):
        self.receipt.write_text(json.dumps(self.data))

    def run_check(self):
        output = io.StringIO()
        with contextlib.redirect_stdout(output), contextlib.redirect_stderr(output):
            result = verifier.main(["--receipt", str(self.receipt), "--cli-dll", str(self.cli)])
        self.assertNotIn(str(self.root), output.getvalue())
        return result

    def test_matching_is_read_only(self):
        before = {p: p.read_bytes() for p in self.root.iterdir()}
        self.assertEqual(0, self.run_check())
        self.assertEqual(before, {p: p.read_bytes() for p in self.root.iterdir()})

    def test_missing_malformed_and_mismatched_hashes(self):
        original = self.data.copy()
        for field in ("generatorSha256", "reportingGeneratorSha256", "combinedIndexSha256"):
            for value in (None, "", "x" * 64, "f" * 64):
                with self.subTest(field=field, value=value):
                    self.data = dict(original, **{field: value})
                    self.save()
                    self.assertEqual(1, self.run_check())

    def test_wrong_reporting_binary(self):
        self.reporting.write_bytes(b"different reporting component")
        self.assertEqual(1, self.run_check())

    def test_missing_reporting_binary(self):
        self.reporting.unlink()
        self.assertEqual(1, self.run_check())

    def test_tampered_handoff(self):
        self.handoff.write_bytes(self.handoff.read_bytes() + b" ")
        self.assertEqual(1, self.run_check())

    def test_linked_handoff_with_wrong_component_or_index(self):
        original = self.handoff.read_text()
        for field in ("generatorSha256", "inputIndexSha256"):
            data = json.loads(original)
            data[field] = "f" * 64
            self.handoff.write_text(json.dumps(data))
            self.data["artifacts"][0].update(bytes=self.handoff.stat().st_size,
                                             sha256=verifier.digest(self.handoff))
            self.save()
            self.assertEqual(1, self.run_check())

    def test_binary_changes_during_verification(self):
        real_digest = verifier.digest
        calls = 0

        def changed(path):
            nonlocal calls
            calls += 1
            return "f" * 64 if calls > 2 else real_digest(path)

        with patch.object(verifier, "digest", side_effect=changed):
            self.assertEqual(1, self.run_check())

    def test_duplicate_json_key(self):
        self.receipt.write_text('{"schemaVersion": "x", "schemaVersion": "y"}')
        self.assertEqual(1, self.run_check())

    def test_missing_or_duplicate_artifact(self):
        artifact = self.data["artifacts"][0]
        for artifacts in ([], [artifact, artifact]):
            self.data["artifacts"] = artifacts
            self.save()
            self.assertEqual(1, self.run_check())

    def test_wrong_surface_and_schema(self):
        for field, value in (("query", {"toSurface": "database-api"}),
                             ("schemaVersion", "future-version"), ("reportVariant", "method-graph")):
            old = self.data[field]
            self.data[field] = value
            self.save()
            self.assertEqual(1, self.run_check())
            self.data[field] = old


if __name__ == "__main__":
    unittest.main()
