"""Public synthetic guards for the local selected-root SQL report."""
import argparse
import importlib.util
import json
import pathlib
import subprocess
import tempfile
import unittest
from unittest.mock import patch

SPEC = importlib.util.spec_from_file_location('sql_wrapper_report', pathlib.Path(__file__).parents[1] / 'sql-wrapper-report.py')
REPORT = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(REPORT)


def native(selector, paths=None):
    return {'query': {'fromSymbol': selector, 'exactFromSymbol': True, 'toSurface': 'database-api'},
            'summary': {'selectorCandidateCount': 1, 'truncated': False},
            'reportCoverage': 'partial', 'paths': paths or [], 'gaps': [],
            'coverageWarnings': ['Not runtime proof'], 'limitations': []}


class SqlWrapperReportTests(unittest.TestCase):
    def test_markdown_controls_are_inert(self):
        value=REPORT.cell('![remote](https://example.invalid/a) [link](https://example.invalid) \\ * _')
        for token in ['![', '](', '\\', '*', '_']:
            self.assertNotIn(token,value)

    def test_malformed_gaps_and_truncation_refused(self):
        for gap in [None,'bad',3,[]]:
            report=native('First');report['gaps']=[gap]
            with self.subTest(gap=gap), self.assertRaises(ValueError): REPORT.render_root(1,'First',report)
        for flag in [None,'false',0,1,{}]:
            report=native('First');report['summary']['truncated']=flag
            with self.subTest(flag=flag), self.assertRaises(ValueError): REPORT.render_root(1,'First',report)

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = pathlib.Path(self.temp.name)
        self.index = self.root / 'index.sqlite'
        self.index.write_bytes(b'synthetic index')
        self.cli = self.root / 'tracemap.dll'
        self.cli.write_bytes(b'synthetic tool')
        self.args = argparse.Namespace(index=self.index, cli=self.cli, root=['First', 'Second'], out=self.root / 'report')

    def runner(self, command, **kwargs):
        folder = pathlib.Path(command[command.index('--out') + 1])
        folder.mkdir(parents=True)
        selector = command[command.index('--from-symbol') + 1]
        (folder / 'paths-report.json').write_text(json.dumps(native(selector)), encoding='utf-8')
        return subprocess.CompletedProcess(command, 0)

    def test_unmatched_roots_retained_in_order_not_unused(self):
        with patch.object(REPORT.subprocess, 'run', side_effect=self.runner):
            self.assertEqual(0, REPORT.run(self.args))
        text = (self.args.out / 'sql-wrapper-inventory.local.md').read_text()
        self.assertLess(text.index('Caller selector: First'), text.index('Caller selector: Second'))
        self.assertEqual(2, text.count('No retained SQL route'))
        self.assertIn('No-path never means unused', text)
        self.assertIn('Input index SHA-256', text)

    def test_failure_and_timeout_are_not_empty(self):
        for outcome in [subprocess.CompletedProcess([], 1), subprocess.TimeoutExpired([], 120)]:
            self.args.out = self.root / ('timeout' if isinstance(outcome, Exception) else 'failed')
            with patch.object(REPORT.subprocess, 'run', side_effect=outcome if isinstance(outcome, Exception) else None, return_value=outcome):
                self.assertEqual(2, REPORT.run(self.args))
            text = (self.args.out / 'sql-wrapper-inventory.local.md').read_text()
            self.assertEqual(2, text.count('QUERY FAILED'))
            self.assertNotIn('No retained SQL route', text)
            if isinstance(outcome, subprocess.TimeoutExpired):
                self.assertIn('Failure category: QueryTimeout', text)

    def test_query_identity_mismatch_rejected(self):
        def wrong(command, **kwargs):
            result = self.runner(command, **kwargs)
            path = pathlib.Path(command[command.index('--out') + 1]) / 'paths-report.json'
            path.write_text(json.dumps(native('wrong')))
            return result
        with patch.object(REPORT.subprocess, 'run', side_effect=wrong):
            self.assertEqual(2, REPORT.run(self.args))

    def test_input_change_refuses_consolidated_report(self):
        def changed(command, **kwargs):
            self.index.write_bytes(b'changed')
            return self.runner(command, **kwargs)
        with patch.object(REPORT.subprocess, 'run', side_effect=changed):
            with self.assertRaises(ValueError):
                REPORT.run(self.args)
        self.assertFalse((self.args.out / 'sql-wrapper-inventory.local.md').exists())

    def test_bounds_duplicates_and_sidecars(self):
        for roots in [[], ['a'] * 2, [str(i) for i in range(33)], ['x' * 8193]]:
            self.args.root = roots
            with self.assertRaises(ValueError):
                REPORT.run(self.args)
        self.args.root = ['a']
        pathlib.Path(str(self.index) + '-wal').touch()
        with self.assertRaises(ValueError):
            REPORT.run(self.args)

    def test_existing_output_refused(self):
        self.args.out.mkdir()
        with self.assertRaises(FileExistsError):
            REPORT.run(self.args)

    def test_large_index_requires_explicit_admission_and_streams_hash(self):
        # Public sparse file: exercises real >512 MiB size and streaming hashes,
        # without constructing a large bytes object or pretending to be SQLite.
        with self.index.open('wb') as stream:
            stream.truncate(768 * 1024 * 1024)
        with patch.object(REPORT.subprocess, 'run', side_effect=self.runner) as runner:
            with self.assertRaisesRegex(REPORT.ReportRefusal, 'IndexSizeLimitExceeded'):
                REPORT.run(self.args)
            runner.assert_not_called()
            self.assertFalse(self.args.out.exists())
            self.args.max_index_mib = 1024
            self.args.query_timeout_seconds = 600
            self.assertEqual(0, REPORT.run(self.args))
            self.assertEqual(600, runner.call_args.kwargs['timeout'])
        text = (self.args.out / 'sql-wrapper-inventory.local.md').read_text()
        self.assertIn('selected maximum: 1024 MiB', text)
        self.assertIn('600-second timeout', text)
        self.assertEqual(768 * 1024 * 1024, self.index.stat().st_size)

    def test_invalid_admission_limits_refused_before_output(self):
        for value in [0, -1, 16385, True, 1.5]:
            self.args.max_index_mib = value
            with self.assertRaises(REPORT.ReportRefusal):
                REPORT.run(self.args)
        self.args.max_index_mib = 512
        for value in [0, -1, 3601, True, 1.5]:
            self.args.query_timeout_seconds = value
            with self.assertRaises(REPORT.ReportRefusal):
                REPORT.run(self.args)
        self.assertFalse(self.args.out.exists())

    def test_size_limit_is_inclusive(self):
        with self.index.open('wb') as stream:
            stream.truncate(1024 * 1024)
        self.args.max_index_mib = 1
        with patch.object(REPORT.subprocess, 'run', side_effect=self.runner):
            self.assertEqual(0, REPORT.run(self.args))
        with self.index.open('ab') as stream:
            stream.write(b'x')
        self.args.out = self.root / 'over-limit'
        with self.assertRaises(REPORT.ReportRefusal):
            REPORT.run(self.args)

    def test_empty_path_nodes_are_invalid_not_an_empty_query(self):
        def malformed(command, **kwargs):
            result = self.runner(command, **kwargs)
            folder = pathlib.Path(command[command.index('--out') + 1])
            selector = command[command.index('--from-symbol') + 1]
            (folder / 'paths-report.json').write_text(json.dumps(native(selector, [{'nodes': []}])))
            return result
        with patch.object(REPORT.subprocess, 'run', side_effect=malformed):
            self.assertEqual(2, REPORT.run(self.args))

    def test_added_tool_component_refuses_report(self):
        def changed(command, **kwargs):
            (self.cli.parent / 'new.dll').write_bytes(b'changed tool set')
            return self.runner(command, **kwargs)
        with patch.object(REPORT.subprocess, 'run', side_effect=changed):
            with self.assertRaises(ValueError):
                REPORT.run(self.args)
        self.assertFalse((self.args.out / 'sql-wrapper-inventory.local.md').exists())

    def test_repeated_output_is_deterministic(self):
        with patch.object(REPORT.subprocess, 'run', side_effect=self.runner):
            REPORT.run(self.args)
            first = (self.args.out / 'sql-wrapper-inventory.local.md').read_bytes()
            self.args.out = self.root / 'repeat'
            REPORT.run(self.args)
        self.assertEqual(first, (self.args.out / 'sql-wrapper-inventory.local.md').read_bytes())

    def test_render_escapes_and_preserves_gap_identity_not_raw_sql(self):
        report = native('Root', [{'classification': 'candidate', 'nodes': [
            {'displayName': '<Caller>|x'}, {'surfaceName': 'SqlCommand.ExecuteNonQuery', 'commandBinding': {
                'commandTextFromPath': {'state': 'constant-on-encoded-call-path', 'origin': {'identity': 'SECRET_SQL'}},
                'commandTypeFromPath': {'state': 'constant-on-encoded-call-path', 'origin': {'kind': 'constant-int32', 'identity': '4'}}}}]}])
        report['gaps'] = [{'gapKind': 'Boundary', 'reason': 'missing', 'message': 'Not bound',
                           'ruleId': 'public.rule.v1', 'evidenceTier': 'Tier4Unknown', 'nodeId': 'node-1', 'combinedFactId': 'fact-1'}]
        rendered = '\n'.join(REPORT.render_root(1, 'Root', report))
        self.assertIn('&lt;Caller&gt;&#124;x', rendered)
        self.assertIn('node&#45;1 / fact&#45;1', rendered)
        self.assertIn('StoredProcedure', rendered)
        self.assertNotIn('SECRET_SQL', rendered)
        self.assertIn('caller relevance unproven', rendered)
        self.assertIn('not proof of a blocking edge', rendered)

    def test_gap_association_does_not_claim_blockage(self):
        report = native('Root', [{'nodes': [{'nodeId': 'n1', 'surfaceName': 'SqlCommand.ExecuteScalar'}], 'supportingFactIds': ['f1']}])
        report['gaps'] = [{'nodeId': 'n1', 'gapKind': 'ProvenanceWarning'}, {'combinedFactId': 'f1'}, {'nodeId': 'unrelated'}]
        rendered = '\n'.join(REPORT.render_root(1, 'Root', report))
        self.assertEqual(2, rendered.count('| retained path node/fact |'))
        self.assertEqual(1, rendered.count('| not associated with a retained path; caller relevance unproven |'))

    def test_truncation_reasons_are_explicit_and_missing_is_unknown(self):
        report = native('Root')
        report['summary']['truncated'] = True
        self.assertIn('unavailable in retained diagnostics', '\n'.join(REPORT.render_root(1, 'Root', report)))
        report['gaps'] = [{'gapKind': 'TruncatedByLimit', 'reason': 'work'}, {'gapKind': 'TruncatedByLimit', 'reason': 'depth'}]
        self.assertIn('diagnostics): depth, work', '\n'.join(REPORT.render_root(1, 'Root', report)))


if __name__ == '__main__':
    unittest.main()
