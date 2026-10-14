import importlib.util
import json
from pathlib import Path
import tempfile
import subprocess
import sys
import unittest
from unittest.mock import patch

SCRIPT = Path(__file__).resolve().parents[1] / "summarize-retained-gaps.py"
spec = importlib.util.spec_from_file_location("inventory", SCRIPT)
inventory = importlib.util.module_from_spec(spec)
spec.loader.exec_module(inventory)


class InventoryTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.catalog = self.root / "catalog.yml"
        self.catalog.write_text("rules:\n  - id: vb.semantic.workspace.v1\n", encoding="utf-8")

    def write(self, facts):
        (self.root / "facts.ndjson").write_text("".join(json.dumps(f) + "\n" for f in facts), encoding="utf-8")

    def gap(self, reason="SemanticAnalysisUnavailable"):
        return dict(factType="AnalysisGap", ruleId="vb.semantic.workspace.v1",
                    evidenceTier="Tier4Unknown", properties={"gapKind": reason})

    def result(self):
        return inventory.summarize(self.root, self.catalog)

    def test_counts_separate_gaps_from_report_calls(self):
        self.write([self.gap(), self.gap(), {"factType": "MethodCalled"}])
        (self.root / "report.md").write_text("Path matching: bounded; 213 calls without a unique supported method target. Analysis gaps in scan: 2.\n")
        result = self.result()
        self.assertEqual(2, result["inventory"]["analysisGapFacts"])
        self.assertEqual(213, result["inventory"]["unresolvedCalls"]["count"])
        self.assertEqual(2, result["inventory"]["gapGroups"][0]["count"])
        self.assertEqual(inventory.digest(inventory.canonical(result["inventory"])), result["boundedInputSha256"])
        self.assertEqual(inventory.digest(SCRIPT.read_bytes()), result["generatorSha256"])

    def test_private_changes_do_not_change_shareable_output_or_hash(self):
        results = []
        for secret in ("private-account-one", "private-account-two"):
            fact = self.gap(secret)
            fact.update(ruleId=secret, evidenceTier=secret, repo=secret, commitSha=secret)
            fact["properties"]["message"] = secret
            self.write([fact])
            result = self.result()
            self.assertNotIn(secret, json.dumps(result))
            results.append(result)
        self.assertEqual(*results)

    def test_no_report_does_not_mean_zero_calls(self):
        self.write([self.gap()])
        self.assertIsNone(self.result()["inventory"]["unresolvedCalls"]["count"])

    def test_mismatch_and_duplicate_report_totals_rejected(self):
        self.write([self.gap()])
        for content in ("Path matching: bounded; 2 calls without a unique supported method target. Analysis gaps in scan: 0.\n",
                        "Path matching: bounded; 2 calls without a unique supported method target. Analysis gaps in scan: 1.\n" * 2):
            (self.root / "report.md").write_text(content)
            with self.assertRaises(inventory.InventoryError):
                self.result()

    def test_invalid_and_duplicate_json_are_rejected(self):
        for raw in ('{"factType":"AnalysisGap","factType":"MethodCalled"}', '[]', '{', '{"factType":"AnalysisGap","properties":[]}'):
            (self.root / "facts.ndjson").write_text(raw)
            with self.assertRaises((inventory.InventoryError, ValueError)):
                self.result()

    def test_limits_do_not_return_partial_counts(self):
        self.write([self.gap(), self.gap()])
        for name, value in (("MAX_FACTS", 1), ("MAX_BYTES", 10), ("MAX_LINE", 10)):
            with patch.object(inventory, name, value), self.assertRaises(inventory.InventoryError):
                self.result()
        with self.assertRaises(inventory.InventoryError):
            inventory.summarize(self.root, self.catalog, max_seconds=-1)

    def test_input_order_does_not_change_summary(self):
        facts = [self.gap(), self.gap("SyntaxParseDiagnostic")]
        self.write(facts)
        first = self.result()
        self.write(list(reversed(facts)))
        self.assertEqual(first, self.result())

    def test_symlink_refused(self):
        target = self.root / "target.ndjson"
        target.write_text(json.dumps(self.gap()))
        try:
            (self.root / "facts.ndjson").symlink_to(target)
        except OSError:
            self.skipTest("Symlink creation unavailable")
        with self.assertRaises(inventory.InventoryError):
            self.result()

    def test_cli_refuses_output_inside_scan_without_echoing_paths(self):
        self.write([self.gap()])
        output = self.root / "private-summary.json"
        run = subprocess.run([sys.executable, str(SCRIPT), "--scan", str(self.root), "--out", str(output)], capture_output=True, text=True)
        self.assertEqual(1, run.returncode)
        self.assertIn("OUTPUT_LOCATION_UNSAFE", run.stderr)
        self.assertNotIn(str(self.root), run.stderr)
        self.assertFalse(output.exists())

    def test_cli_writes_once_and_refuses_overwrite(self):
        self.write([self.gap()])
        with tempfile.TemporaryDirectory() as destination:
            output = Path(destination) / "summary.json"
            command = [sys.executable, str(SCRIPT), "--scan", str(self.root), "--out", str(output)]
            first = subprocess.run(command, capture_output=True, text=True)
            self.assertEqual(0, first.returncode, first.stderr)
            before = output.read_bytes()
            second = subprocess.run(command, capture_output=True, text=True)
            self.assertEqual(1, second.returncode)
            self.assertEqual(before, output.read_bytes())
            self.assertNotIn(destination, second.stderr)


if __name__ == "__main__":
    unittest.main()
