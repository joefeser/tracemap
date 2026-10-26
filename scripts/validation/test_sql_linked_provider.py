import argparse
from collections import Counter
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

SCRIPT = Path(__file__).resolve().parents[1] / 'dnn-sql-validation/linked_provider.py'
spec = importlib.util.spec_from_file_location('linked_provider', SCRIPT)
h = importlib.util.module_from_spec(spec)
spec.loader.exec_module(h)


class LinkedProviderTests(unittest.TestCase):
    def test_runtime_proof_rejects_missing_duplicate_extra_stale_or_unclean(self):
        rows = [{'language': language, 'case': name} for language in ('csharp', 'vb') for name in h.CASES]
        lines = [f"PASS linked-{r['language']}-{r['case']}" for r in rows] + ['PROVIDER_CHECKS_PASSED 32']
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            def write(log, cleanup='owned-container-removed'):
                (root / 'provider.log').write_text('\n'.join(log))
                (root / 'receipt.json').write_text(json.dumps({'status': 'PASSED synthetic SQL checks', 'cleanup': cleanup,
                    'outputs': {'provider.log': h.sha(root / 'provider.log')}}))
            write(lines)
            h.verify_runtime(root, rows)
            for bad in (lines[1:], lines + [lines[0]], lines + ['PASS linked-vb-Unplanned'], lines[:-1]):
                write(bad)
                with self.assertRaises(ValueError):
                    h.verify_runtime(root, rows)
            write(lines, 'required')
            with self.assertRaisesRegex(ValueError, 'unclean'):
                h.verify_runtime(root, rows)
            write(lines)
            (root / 'provider.log').write_text('stale')
            with self.assertRaisesRegex(ValueError, 'hash mismatch'):
                h.verify_runtime(root, rows)
            write(lines)
            with self.assertRaisesRegex(ValueError, 'static denominator'):
                h.verify_runtime(root, rows[:-1])

    def test_each_gate_precedes_filesystem_and_external_effects(self):
        gates = ('activate', 'approve_host', 'approve_resources', 'accept_eula', 'approve_disposable')
        with tempfile.TemporaryDirectory() as temp:
            output = Path(temp) / 'new'
            for missing in gates:
                args = argparse.Namespace(**{g: g != missing for g in gates},
                                          context='reviewed', expected_endpoint='ssh://reviewed', output=output)
                with patch.object(h.subprocess, 'run', side_effect=AssertionError('external effect')):
                    with self.assertRaisesRegex(ValueError, 'all activation'):
                        h.run(args)
                self.assertFalse(output.exists())

    def test_authored_denominator_preserves_unknowns(self):
        counts = Counter(status for _, status in h.CASES.values())
        self.assertEqual({'bounded-match': 3, 'mismatch': 1, 'unknown': 2}, counts)
        self.assertEqual(('datatable', 'unknown'), h.CASES['CaseOnly'])
        self.assertEqual(('dataset', 'unknown'), h.CASES['LaterTable'])

    def test_both_fixture_assemblies_are_runtime_project_references(self):
        project = (h.SAMPLE / 'ProviderChecks.csproj').read_text()
        for language, extension in (('csharp', 'csproj'), ('vb', 'vbproj')):
            self.assertIn(f'{language}/Linked.{extension}', project)
        program = (h.SAMPLE / 'Program.cs').read_text()
        self.assertIn('typeof(LinkedCSharp.Cases)', program)
        self.assertIn('typeof(LinkedVisualBasic.Cases)', program)
        self.assertIn('PROVIDER_CHECKS_PASSED 32', program)

    def test_input_inventory_excludes_generated_build_outputs(self):
        inputs = h.inputs()
        self.assertIn('samples/sqlserver-live-provider/csharp/Linked.cs', inputs)
        self.assertIn('samples/sqlserver-live-provider/vb/Linked.vb', inputs)
        self.assertIn('samples/sqlserver-live-provider/matrix.md', inputs)
        self.assertFalse(any({'bin', 'obj'} & set(Path(p).parts) for p in inputs))


if __name__ == '__main__':
    unittest.main()
