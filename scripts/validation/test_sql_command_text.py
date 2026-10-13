"""Native public-fixture guards. Explicit paths only; never use private inputs."""
import argparse
import hashlib
import json
import pathlib
import subprocess
import tempfile
import unittest

CONFIG = None


class CommandTextTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.folder = pathlib.Path(self.temp.name)
        self.output = self.folder / 'local.md'
        self.report = json.loads(CONFIG.paths.read_text(encoding='utf-8-sig'))
        self.input = self.folder / 'paths.json'
        self.input.write_text(json.dumps(self.report), encoding='utf-8')
        self.sha = hashlib.sha256(CONFIG.assembly.read_bytes()).hexdigest()

    def run_tool(self, consent=True, sha=None, assembly=None, extra_reports=()):
        command = ['dotnet', str(CONFIG.tool), '--paths', str(self.input), '--assembly', str(assembly or CONFIG.assembly),
                   '--expected-sha256', sha or self.sha, '--out', str(self.output)]
        if consent:
            command += ['--allow-local-command-text']
        for report in extra_reports:
            command += ['--paths', str(report)]
        result = subprocess.run(command, capture_output=True, text=True, timeout=30)
        self.assertNotIn('PublicCatalog_Save', result.stdout + result.stderr)
        return result

    def test_native_retained_hash_recovers_only_matching_literal(self):
        self.assertEqual(0, self.run_tool().returncode)
        text = self.output.read_text()
        self.assertIn('hash-matched literal: PublicCatalog_Save', text)
        self.assertIn('StoredProcedure', text)
        self.assertNotIn('SELECT ItemId', text)
        self.assertIn('operator-supplied', text)
        self.assertIn(self.sha, text)

    def test_no_consent_refused_without_output(self):
        self.assertEqual(2, self.run_tool(consent=False).returncode)
        self.assertFalse(self.output.exists())

    def test_wrong_binary_hash_refused_without_output(self):
        self.assertEqual(2, self.run_tool(sha='0' * 64).returncode)
        self.assertFalse(self.output.exists())

    def test_existing_output_not_overwritten(self):
        self.output.write_text('keep')
        self.assertEqual(2, self.run_tool().returncode)
        self.assertEqual('keep', self.output.read_text())

    def test_unmatched_hash_remains_unavailable(self):
        for path in self.report['paths']:
            path['nodes'][-1]['commandBinding']['commandTextFromPath']['origin']['identity'] = 'str:18:' + '0' * 64
        self.input.write_text(json.dumps(self.report))
        self.assertEqual(0, self.run_tool().returncode)
        self.assertIn('unavailable: no unique admitted constant match', self.output.read_text())
        self.assertNotIn('PublicCatalog_Save', self.output.read_text())

    def test_dynamic_state_not_promoted_even_with_matching_origin(self):
        for path in self.report['paths']:
            path['nodes'][-1]['commandBinding']['commandTextFromPath']['state'] = 'symbolic-string-composition'
        self.input.write_text(json.dumps(self.report))
        self.assertEqual(0, self.run_tool().returncode)
        self.assertNotIn('PublicCatalog_Save', self.output.read_text())

    def test_path_label_escaped_and_rows_retained(self):
        self.report['paths'][0]['pathId'] = '<script>|`\n'
        self.input.write_text(json.dumps(self.report))
        self.assertEqual(0, self.run_tool().returncode)
        text = self.output.read_text()
        self.assertIn('&lt;script&gt;&#124;&#96;&#10;', text)
        self.assertNotIn('<script>', text)

    def test_non_database_terminal_not_promoted(self):
        self.report['paths'][0]['nodes'][-1]['surfaceKind'] = 'http-client'
        self.input.write_text(json.dumps(self.report))
        self.assertEqual(0, self.run_tool().returncode)
        self.assertNotIn('PublicCatalog_Save', self.output.read_text())

    def test_invalid_binary_refused_without_output(self):
        invalid = self.folder / 'invalid.dll'
        invalid.write_bytes(b'not an assembly')
        sha = hashlib.sha256(invalid.read_bytes()).hexdigest()
        self.assertEqual(2, self.run_tool(sha=sha, assembly=invalid).returncode)
        self.assertFalse(self.output.exists())

    def test_markdown_images_and_utf16_units_render_as_inert_text(self):
        self.report['paths'][0]['pathId'] = '![x](https://example.invalid) \U0001f600'
        self.input.write_text(json.dumps(self.report))
        self.assertEqual(0, self.run_tool().returncode)
        text = self.output.read_text()
        self.assertNotIn('![x]', text)
        self.assertIn('!&#91;x&#93;', text)
        self.assertIn('\\ud83d\\ude00', text)

    def test_truncated_without_reason_stays_truncated(self):
        self.report['summary']['truncated'] = True
        self.report['gaps'] = []
        self.input.write_text(json.dumps(self.report))
        self.assertEqual(0, self.run_tool().returncode)
        text = self.output.read_text()
        self.assertIn('Original query truncated: true', text)
        self.assertIn('reason unavailable in retained diagnostics', text)
        self.assertNotIn('none recorded', text)

    def test_truncation_reasons_sorted_and_preserved(self):
        self.report['summary']['truncated'] = True
        self.report['gaps'] = [{'gapKind': 'TruncatedByLimit', 'reason': 'work'}, {'gapKind': 'TruncatedByLimit', 'reason': 'depth'}]
        self.input.write_text(json.dumps(self.report))
        self.assertEqual(0, self.run_tool().returncode)
        self.assertIn('Original query truncation reasons: depth, work', self.output.read_text())

    def test_missing_flag_is_unknown(self):
        self.report.pop('summary', None)
        self.report['gaps'] = []
        self.input.write_text(json.dumps(self.report))
        self.assertEqual(0, self.run_tool().returncode)
        self.assertIn('Original query truncated: unknown', self.output.read_text())

    def test_false_flag_with_reason_preserves_conflict(self):
        self.report['summary']['truncated'] = False
        self.report['gaps'] = [{'gapKind': 'TruncatedByLimit', 'reason': 'path'}]
        self.input.write_text(json.dumps(self.report))
        self.assertEqual(0, self.run_tool().returncode)
        text = self.output.read_text()
        self.assertIn('Original query truncated: false', text)
        self.assertIn('Original query truncation reasons: path', text)
        self.assertIn('retain both and reconcile', text)

    def test_multiple_roots_keep_identity_truncation_and_duplicate_path_ids(self):
        self.report['summary']['truncated'] = True
        self.report['coverageWarnings'] = ['public-coverage-warning']
        self.input.write_text(json.dumps(self.report))
        second = self.folder / 'second.json'
        self.report['summary']['truncated'] = False
        self.report['query']['fromSymbol'] = 'another-public-root'
        second.write_text(json.dumps(self.report))
        self.assertEqual(0, self.run_tool(extra_reports=[second]).returncode)
        text = self.output.read_text()
        self.assertIn('## Input 1', text)
        self.assertIn('## Input 2', text)
        self.assertIn('Original query truncated: true', text)
        self.assertIn('Original query truncated: false', text)
        self.assertEqual(2, text.count('hash-matched literal: PublicCatalog_Save'))
        self.assertIn('public-coverage-warning', text)
        self.assertIn('NeedsReviewPath', text)
        self.assertIn('BoundCompiledIlEvidence', text)
        self.assertIn('not supplied / undecided', text)

    def test_empty_root_retained_in_consolidation(self):
        empty = self.folder / 'empty.json'
        self.report['paths'] = []
        empty.write_text(json.dumps(self.report))
        self.assertEqual(0, self.run_tool(extra_reports=[empty]).returncode)
        self.assertIn('No retained paths for this input; not evidence of no SQL use.', self.output.read_text())

    def test_duplicate_file_and_duplicate_content_refused(self):
        duplicate = self.folder / 'duplicate.json'
        duplicate.write_bytes(self.input.read_bytes())
        for second in [self.input, duplicate]:
            self.assertEqual(2, self.run_tool(extra_reports=[second]).returncode)
            self.assertFalse(self.output.exists())

    def test_report_count_bound_refused(self):
        reports = []
        for i in range(32):
            report = self.folder / f'root-{i}.json'
            self.report['query']['fromSymbol'] = f'public-root-{i}'
            report.write_text(json.dumps(self.report))
            reports.append(report)
        self.assertEqual(2, self.run_tool(extra_reports=reports).returncode)
        self.assertFalse(self.output.exists())

    def test_aggregate_path_bound_refused(self):
        reports = []
        self.report['paths'] *= 256
        for i in range(4):
            report = self.folder / f'root-{i}.json'
            self.report['query']['fromSymbol'] = f'public-root-{i}'
            report.write_text(json.dumps(self.report))
            reports.append(report)
        self.assertEqual(2, self.run_tool(extra_reports=reports).returncode)
        self.assertFalse(self.output.exists())

    def test_consolidated_output_deterministic(self):
        second = self.folder / 'second.json'
        self.report['query']['fromSymbol'] = 'another-public-root'
        second.write_text(json.dumps(self.report))
        self.assertEqual(0, self.run_tool(extra_reports=[second]).returncode)
        first = self.output.read_bytes()
        self.output.unlink()
        self.assertEqual(0, self.run_tool(extra_reports=[second]).returncode)
        self.assertEqual(first, self.output.read_bytes())


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['tool', 'paths', 'assembly']:
        parser.add_argument('--' + name, type=pathlib.Path, required=True)
    CONFIG, remaining = parser.parse_known_args()
    unittest.main(argv=['test_sql_command_text.py'] + remaining)
