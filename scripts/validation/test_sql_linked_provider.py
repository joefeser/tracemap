import argparse
from collections import Counter
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from unittest.mock import Mock

SCRIPT = Path(__file__).resolve().parents[1] / 'dnn-sql-validation/linked_provider.py'
spec = importlib.util.spec_from_file_location('linked_provider', SCRIPT)
h = importlib.util.module_from_spec(spec)
spec.loader.exec_module(h)


class LinkedProviderTests(unittest.TestCase):
    def test_committed_source_identity_rejects_dirty_head_and_blob_drift(self):
        import hashlib
        snapshot = {'fixture.cs': hashlib.sha256(b'committed').hexdigest()}
        for responses in ([b' M fixture.cs'], [b'?? extra.cs'], [b'', 'different\n'],
                          [b'', 'head\n', b'mutated']):
            with patch.object(h.subprocess, 'check_output', side_effect=responses):
                with self.assertRaises(ValueError):
                    h.verify_committed('head', snapshot)
        with patch.object(h.subprocess, 'check_output', side_effect=[b'', 'head\n', b'committed']):
            h.verify_committed('head', snapshot)

    def test_output_boundary_and_existing_failure_evidence_are_preserved(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            repo = root / 'repo'
            sample = repo / 'sample'
            sample.mkdir(parents=True)
            alias = root / 'alias'
            alias.symlink_to(sample, target_is_directory=True)
            old = root / 'failed'
            old.mkdir()
            (old / 'receipt.json').write_text('failure evidence')
            for output in (repo, repo / 'reports', sample, sample / 'new', alias / 'new', old):
                args = argparse.Namespace(activate=True, approve_host=True, approve_resources=True,
                    accept_eula=True, approve_disposable=True, context='reviewed', expected_endpoint='ssh://reviewed', output=output)
                with patch.object(h, 'ROOT', repo), patch.object(h, 'SAMPLE', sample), patch.object(h.subprocess, 'check_output', side_effect=AssertionError('effect')):
                    with self.assertRaises(ValueError):
                        h.run(args)
            self.assertFalse((sample / 'new').exists())
            self.assertEqual('failure evidence', (old / 'receipt.json').read_text())

    def test_source_links_are_rejected_before_file_filtering(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            sample = root / 'sample'
            sample.mkdir()
            target = root / 'external'
            target.mkdir()
            file = target / 'fixture.cs'
            file.write_text('public class Fixture {}')
            for destination in (target, file, root / 'missing'):
                link = sample / 'linked'
                link.symlink_to(destination, target_is_directory=destination == target)
                with patch.object(h, 'SAMPLE', sample), self.assertRaisesRegex(ValueError, 'source links'):
                    h.inputs()
                link.unlink()
            root_link = root / 'sample-alias'
            root_link.symlink_to(sample, target_is_directory=True)
            with patch.object(h, 'SAMPLE', root_link), self.assertRaisesRegex(ValueError, 'source links'):
                h.inputs()

    def test_runtime_does_not_use_killing_outer_subprocess_timeout(self):
        harness = Mock()
        cleaned = []
        def failed_run(args):
            try:
                raise RuntimeError('bounded readiness exhausted')
            finally:
                cleaned.append(True)
        harness.run.side_effect = failed_run
        args = argparse.Namespace(context='reviewed', expected_endpoint='ssh://reviewed')
        with patch.object(h, 'load', return_value=harness), patch.object(h.subprocess, 'run', side_effect=AssertionError('outer process')):
            with self.assertRaisesRegex(RuntimeError, 'readiness'):
                h.run_runtime(args, Path('/publish'), Path('/runtime'))
        self.assertEqual([True], cleaned)

    def test_summary_is_derived_from_rows(self):
        self.assertEqual({'mismatch': 1, 'unknown': 2}, h.summarize([
            {'static': 'unknown'}, {'static': 'mismatch'}, {'static': 'unknown'}]))

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
