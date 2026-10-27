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
        lines = [f"PASS linked-{r['language']}-{r['case']}" for r in rows] + [f'PROVIDER_CHECKS_PASSED {h.RUNTIME_CHECKS}']
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
        self.assertEqual({'bounded-match': 10, 'mismatch': 3, 'unknown': 4}, counts)
        self.assertEqual(('datatable', 'unknown'), h.CASES['CaseOnly'])
        self.assertEqual(('dataset', 'unknown'), h.CASES['LaterTable'])
        # A runtime-successful later table through the helper stays a static unknown with its authored reason.
        self.assertEqual(('helper', 'unknown'), h.CASES['HelperLaterTable'])
        self.assertEqual('99', h.RUNTIME['HelperLaterTable'])
        self.assertEqual('dataset-later-table-unsupported', h.REASONS['HelperLaterTable'])
        self.assertEqual(54, h.RUNTIME_CHECKS)
        self.assertEqual(20 + 2 * len(h.CASES), h.RUNTIME_CHECKS)
        helper_cases = {name for name, (route, _) in h.CASES.items() if route == 'helper'}
        self.assertEqual(helper_cases, set(h.HELPER_SITES))
        # Two call sites of one helper with different procedures are separate cases.
        self.assertEqual(('ReadTable', 'ReadItems'), h.HELPER_SITES['HelperTableName'])
        self.assertEqual(('ReadTable', 'ReadOther'), h.HELPER_SITES['HelperOtherProcedure'])
        self.assertEqual('other', h.RUNTIME['HelperOtherProcedure'])
        # Phase 6: Field reads keep a runtime-successful case-only read a static unknown; one site is a cross-document internal helper.
        field_cases = {name for name, (route, _) in h.CASES.items() if route == 'field'}
        self.assertEqual({'FieldTableName', 'FieldOrdinal', 'FieldSetName', 'FieldMissingName', 'FieldCaseOnly', 'InternalFieldName'}, field_cases)
        self.assertEqual(('field', 'unknown'), h.CASES['FieldCaseOnly'])
        self.assertEqual('sample', h.RUNTIME['FieldCaseOnly'])
        self.assertEqual(('1', 'expected exception'), (h.RUNTIME['FieldOrdinal'], h.RUNTIME['FieldMissingName']))
        self.assertEqual({'InternalFieldName': ('ReadShared', 'ReadItems')}, h.FIELD_SITES)

    def test_helper_call_sites_bind_separately_and_refuse_missing_or_duplicate_sites(self):
        route = h.load('linked_helper_test', h.ROOT / 'scripts/sqlserver-helper-result-evidence/smoke.py')
        caller = {'line': 3, 'endLine': 3, 'coverage': 'bounded', 'gap': None, 'commandTextHash': 'a' * 64, 'resultLine': 3,
                  'selectors': [], 'guards': [], 'accesses': []}
        state = {'version': 'sqlserver-helper-result/2', 'coverage': 'bounded', 'gap': None, 'callers': [
            dict(caller, caller='LinkedCSharp.Cases.HelperTableName(Microsoft.Data.SqlClient.SqlConnection)'),
            dict(caller, line=7, caller='LinkedCSharp.Cases.HelperOtherProcedure(Microsoft.Data.SqlClient.SqlConnection)')]}
        fact = {'factId': 'f', 'repo': 'r', 'commitSha': '1' * 40, 'ruleId': 'x', 'evidenceTier': 'Tier1Semantic',
                'evidence': {'filePath': 'Linked.cs', 'startLine': 1, 'endLine': 1, 'extractorId': 'e', 'extractorVersion': 'v'},
                'properties': {'sqlServerHelperResultState': json.dumps(state)}}
        self.assertEqual(0, h.helper_site(route, fact, 'HelperTableName'))
        self.assertEqual(1, h.helper_site(route, fact, 'HelperOtherProcedure'))
        first = h.helper_binding(route, fact, 0, 'ReadItems', '1' * 40)['calls'][0]
        second = h.helper_binding(route, fact, 1, 'ReadOther', '1' * 40)['calls'][0]
        self.assertEqual((0, 'dbo.ReadItems', 1, 'dbo.ReadOther'), (first['callerIndex'], first['commandText'], second['callerIndex'], second['commandText']))
        self.assertNotEqual(first['helperResultEvidenceSha256'], second['helperResultEvidenceSha256'])
        with self.assertRaisesRegex(ValueError, 'exactly one call site'):
            h.helper_site(route, fact, 'HelperSetName')
        duplicate = dict(fact, properties={'sqlServerHelperResultState': json.dumps(dict(state, callers=state['callers'][:1] * 2))})
        with self.assertRaisesRegex(ValueError, 'exactly one call site'):
            h.helper_site(route, duplicate, 'HelperTableName')
        unknown = dict(fact, properties={'sqlServerHelperResultState': json.dumps(dict(state, coverage='unknown', gap='helper-recursion'))})
        with self.assertRaisesRegex(ValueError, 'not bounded'):
            h.helper_site(route, unknown, 'HelperTableName')

    def test_field_sites_bind_separately_and_refuse_unbounded_missing_or_duplicate_sites(self):
        route = h.load('linked_field_test', h.ROOT / 'scripts/sqlserver-field-read-evidence/smoke.py')
        site = {'path': 'Linked.cs', 'line': 3, 'endLine': 3, 'coverage': 'bounded', 'gap': None, 'commandTextHash': 'a' * 64,
                'selectors': [], 'guards': [], 'accesses': [{'member': 'Field'}]}
        state = {'version': 'sqlserver-field-read/1', 'coverage': 'bounded', 'gap': None, 'origin': 'internal-helper', 'originGaps': [], 'sites': [
            dict(site, caller='LinkedCSharp.Cases.InternalFieldName(Microsoft.Data.SqlClient.SqlConnection)'),
            dict(site, line=7, coverage='unknown', gap='field-read-absent', caller='LinkedCSharp.Cases.Other(Microsoft.Data.SqlClient.SqlConnection)')]}
        fact = {'factId': 'f', 'repo': 'r', 'commitSha': '1' * 40, 'ruleId': 'x', 'evidenceTier': 'Tier1Semantic',
                'evidence': {'filePath': 'Shared.cs', 'startLine': 1, 'endLine': 1, 'extractorId': 'e', 'extractorVersion': 'v'},
                'properties': {'sqlServerFieldReadState': json.dumps(state)}}
        self.assertEqual(0, h.field_site(route, fact, 'InternalFieldName'))
        call = h.field_binding(route, fact, 0, 'ReadItems', '1' * 40)['calls'][0]
        self.assertEqual((0, 'dbo.ReadItems'), (call['siteIndex'], call['commandText']))
        self.assertEqual(route.HELPERS.digest(route.field_projection(fact, 0)), call['fieldReadEvidenceSha256'])
        self.assertNotIn('callerIndex', call)
        for name in ('Other', 'Missing', None):  # field-read-absent, absent and ambiguous sites never bind
            with self.assertRaisesRegex(ValueError, 'exactly one bounded field site'):
                h.field_site(route, fact, name)
        direct = dict(fact, properties={'sqlServerFieldReadState': json.dumps(dict(state, origin='datatable', sites=state['sites'][:1]))})
        self.assertEqual(0, h.field_site(route, direct))
        unknown = dict(fact, properties={'sqlServerFieldReadState': json.dumps(dict(state, coverage='unknown', gap='field-read-origin-unbound', sites=[]))})
        with self.assertRaisesRegex(ValueError, 'not bounded'):
            h.field_site(route, unknown)
    def test_shared_internal_helper_has_one_rule_specific_suppression_and_constant_callers(self):
        import re
        text = (h.SAMPLE / 'csharp/Shared.cs').read_text()
        lines = [line for line in text.splitlines() if 'nosemgrep' in line]
        self.assertEqual(['using var command = new SqlCommand(procedure); // nosemgrep: csharp.lang.security.sqli.csharp-sqli'], [l.strip() for l in lines])
        self.assertIn('internal static DataTable ReadShared(SqlConnection connection, string procedure)', text)
        for language, extension in (('csharp', 'cs'), ('vb', 'vb')):
            linked = (h.SAMPLE / language / f'Linked.{extension}').read_text()
            self.assertEqual(['connection, "dbo.ReadItems"'], re.findall(r'SharedData\.ReadShared\(([^)]*)\)', linked))
        self.assertIn('Friend Function ReadShared(connection As SqlConnection, procedure As String) As DataTable', (h.SAMPLE / 'vb/Shared.vb').read_text())
    def test_both_fixture_assemblies_are_runtime_project_references(self):
        project = (h.SAMPLE / 'ProviderChecks.csproj').read_text()
        for language, extension in (('csharp', 'csproj'), ('vb', 'vbproj')):
            self.assertIn(f'{language}/Linked.{extension}', project)
        program = (h.SAMPLE / 'Program.cs').read_text()
        self.assertIn('typeof(LinkedCSharp.Cases)', program)
        self.assertIn('typeof(LinkedVisualBasic.Cases)', program)
        self.assertIn('PROVIDER_CHECKS_PASSED 54', program)
        for name in h.CASES:
            self.assertIn(f'["{name}"]', program)

    def test_input_inventory_excludes_generated_build_outputs(self):
        inputs = h.inputs()
        self.assertIn('samples/sqlserver-live-provider/csharp/Linked.cs', inputs)
        self.assertIn('samples/sqlserver-live-provider/vb/Linked.vb', inputs)
        self.assertIn('samples/sqlserver-live-provider/csharp/Shared.cs', inputs)
        self.assertIn('samples/sqlserver-live-provider/vb/Shared.vb', inputs)
        self.assertIn('scripts/sqlserver-field-read-evidence/smoke.py', inputs)
        self.assertIn('samples/sqlserver-live-provider/matrix.md', inputs)
        self.assertFalse(any({'bin', 'obj'} & set(Path(p).parts) for p in inputs))


if __name__ == '__main__':
    unittest.main()
