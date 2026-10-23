import contextlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import subprocess
import sys
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
            "header": {"query": {"toSurface": "http-client"}},
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

    def test_legacy_missing_variant_is_grouped_but_explicit_invalid_is_rejected(self):
        del self.data["reportVariant"]
        self.save()
        self.assertEqual(0, self.run_check())
        for value in (None, "", "method-graph", 0, {}):
            self.data["reportVariant"] = value
            self.save()
            self.assertEqual(1, self.run_check())

    def test_handoff_surface_must_be_http_even_with_valid_artifact_link(self):
        original = json.loads(self.handoff.read_text())
        for header in (None, {}, [], {"query": None}, {"query": {}},
                       {"query": {"toSurface": "database-api"}}):
            with self.subTest(header=header):
                self.handoff.write_text(json.dumps(dict(original, header=header)))
                self.data["artifacts"][0].update(bytes=self.handoff.stat().st_size,
                                               sha256=verifier.digest(self.handoff))
                self.save()
                self.assertEqual(1, self.run_check())

    def test_inputs_changed_after_initial_read_fail(self):
        real_digest = verifier.digest
        for target in (self.receipt, self.handoff):
            original = target.read_bytes()
            calls = 0

            def changed(path):
                nonlocal calls
                calls += 1
                if calls == 3:
                    target.write_bytes(original + b" ")
                return real_digest(path)

            with self.subTest(target=target.name), patch.object(verifier, "digest", side_effect=changed):
                self.assertEqual(1, self.run_check())
            target.write_bytes(original)

    def test_memory_exhaustion_is_private_fail_closed(self):
        for operation in ("read_bytes", "unique_object"):
            with self.subTest(operation=operation), patch.object(verifier, operation, side_effect=MemoryError):
                self.assertEqual(1, self.run_check())

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

    def test_invalid_json_and_shapes_fail_without_leaking_input(self):
        for text in ('{', '[]', 'null', '{"private-secret": NaN}',
                     '{"nested":{"same":1,"same":2}}', '\ufeffnot-json'):
            with self.subTest(text=text):
                self.receipt.write_text(text, encoding="utf-8")
                self.assertEqual(1, self.run_check())
        self.receipt.write_bytes(b'\xff\xfe\xff')
        self.assertEqual(1, self.run_check())

    def test_missing_hash_keys(self):
        for key in ("generatorSha256", "reportingGeneratorSha256", "combinedIndexSha256"):
            saved = self.data.pop(key)
            self.save()
            self.assertEqual(1, self.run_check())
            self.data[key] = saved

    def test_artifact_length_and_paths_cannot_redirect_reads(self):
        original = self.data["artifacts"][0].copy()
        for field, value in (("bytes", True), ("bytes", -1), ("bytes", original["bytes"] + 1),
                             ("relativePath", "../private-secret.json"), ("sha256", "0" * 64)):
            self.data["artifacts"] = [dict(original, **{field: value})]
            self.save()
            self.assertEqual(1, self.run_check())

    def test_input_budgets(self):
        with patch.object(verifier, "MAX_RECEIPT", 8):
            self.assertEqual(1, self.run_check())
        with patch.object(verifier, "MAX_BINARY", 8):
            self.assertEqual(1, self.run_check())
        with self.assertRaises(ValueError):
            verifier.read_json(self.handoff, limit=8)

    def test_missing_and_directory_inputs(self):
        self.handoff.unlink()
        self.assertEqual(1, self.run_check())
        self.handoff.mkdir()
        self.assertEqual(1, self.run_check())

    def test_arbitrary_json_shapes(self):
        for key in ("query", "artifacts"):
            original = self.data[key]
            for value in (None, 42, "private-secret", [None], {"unexpected": True}):
                self.data[key] = value
                self.save()
                self.assertEqual(1, self.run_check())
            self.data[key] = original

    def test_subprocess_paths_with_spaces_and_exit_codes(self):
        # Temporary root is renamed to cover shell-free Windows/Unix argument handling.
        new_root = self.root / "public sample # percent %"
        new_root.mkdir()
        for path in (self.cli, self.reporting, self.handoff, self.receipt):
            (new_root / path.name).write_bytes(path.read_bytes())
        command = [sys.executable, str(Path(verifier.__file__)), "--receipt",
                   str(new_root / self.receipt.name), "--cli-dll", str(new_root / self.cli.name)]
        result = subprocess.run(command, capture_output=True, text=True, timeout=10)
        self.assertEqual(0, result.returncode, result.stderr)
        (new_root / self.reporting.name).unlink()
        result = subprocess.run(command, capture_output=True, text=True, timeout=10)
        self.assertEqual(1, result.returncode)
        self.assertEqual("PROVENANCE_NOT_VERIFIED", result.stderr.strip())
        self.assertNotIn(str(new_root), result.stdout + result.stderr)

    def test_argument_errors_do_not_echo_private_values(self):
        result = subprocess.run([sys.executable, verifier.__file__, "--unexpected-private-path"],
                                capture_output=True, text=True, timeout=10)
        self.assertEqual(2, result.returncode)
        self.assertEqual("PROVENANCE_ARGUMENTS_INVALID", result.stderr.strip())
        self.assertEqual("", result.stdout)


if __name__ == "__main__":
    unittest.main()
