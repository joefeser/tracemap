"""Offline guards; isolated local Git only, no network, .NET, SQL or container."""
import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import Mock, patch

SPEC = importlib.util.spec_from_file_location('public_acceptance', Path(__file__).resolve().parents[1] / 'dnn-sql-validation/public_acceptance.py')
A = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(A)


class PublicAcceptanceTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.root = Path(temp.name).resolve()
        report = self.root / 'csharp/case/report/report.json'
        report.parent.mkdir(parents=True)
        report.write_text('{}')
        self.revision = '1' * 40
        rows = []
        expected = []
        for i, status in enumerate([A.M] * 8 + [A.X] * 12 + [A.U] * 32):
            path = f'case{i}/report.json'
            target = self.root / path
            target.parent.mkdir()
            target.write_text(json.dumps({'coverage': 'partial', 'verdict': 'no-aggregate-clean-verdict',
                                         'comparisons': [{'factId': f'fact{i}', 'status': status}]}))
            rows.append({'language': 'csharp' if i < 26 else 'vb', 'case': f'Case{i}',
                         'factId': f'fact{i}', 'status': status, 'reportSha256': A.sha(target)})
            expected.append(({k: rows[-1][k] for k in ('language', 'case', 'factId')}, path, [status]))
        self.receipt = {'version': 'sqlserver-parameter-smoke/1', 'revision': self.revision,
                        'generatorSha256': A.sha(A.ROOT / 'scripts/sqlserver-parameter-evidence/smoke.py'),
                        'boundedInputSha256': '2' * 64, 'sqlExecution': 'not-run', 'results': rows}
        self.helpers = Mock()
        self.contracts = A.load('contracts_test', A.ROOT / 'scripts/dnn-sql-validation/acceptance_contracts.py')
        self.addCleanup(patch.stopall)
        patch.object(self.contracts, 'reconstruct', return_value=('2' * 64, expected)).start()
        patch.object(A, 'load', return_value=self.contracts).start()

    def verify(self, receipt=None):
        (self.root / 'smoke-receipt.json').write_text(json.dumps(self.receipt if receipt is None else receipt))
        return A.verify_gate(self.root, 'parameter', self.revision, self.helpers)

    def test_valid_denominator_and_scan_verification(self):
        self.assertEqual(self.verify()['caseReports'], 52)
        self.helpers.verify_scan_hashes.assert_called_once()

    def test_stale_identity_generator_and_missing_input_refused(self):
        for field, value in [('version', 'old'), ('revision', 'f' * 40), ('generatorSha256', '0' * 64),
                             ('boundedInputSha256', ''), ('sqlExecution', 'executed'), ('gaps', ['missing'])]:
            with self.subTest(field=field):
                receipt = copy.deepcopy(self.receipt)
                receipt[field] = value
                with self.assertRaises(ValueError):
                    self.verify(receipt)

    def test_missing_extra_duplicate_cases_refused(self):
        for mode in ('missing', 'extra', 'duplicate'):
            receipt = copy.deepcopy(self.receipt)
            if mode == 'missing':
                receipt['results'].pop()
            elif mode == 'extra':
                receipt['results'].append(receipt['results'][0])
            else:
                receipt['results'][1] = receipt['results'][0]
            with self.subTest(mode=mode), self.assertRaises(ValueError):
                self.verify(receipt)

    def test_wrong_counts_language_and_report_hash_refused(self):
        for field, value in [('status', A.U), ('language', 'other'), ('reportSha256', '0' * 64)]:
            receipt = copy.deepcopy(self.receipt)
            receipt['results'][0][field] = value
            with self.subTest(field=field), self.assertRaises(ValueError):
                self.verify(receipt)

    def test_refusal_denominator_not_ignored(self):
        self.receipt['refusals'] = [{'refused': True}]
        with self.assertRaises(ValueError):
            self.verify()

    def test_field_refusal_contract_includes_both_languages(self):
        self.assertEqual(A.GATES['field'], ('field-read', 'sqlserver-field-read-smoke/1',
                                          150, (78, 36, 42), 52))

    def test_artifact_changes_and_links_refused(self):
        report = self.root / 'case0/report.json'
        report.write_text('{"changed":true}')
        with self.assertRaises(ValueError):
            self.verify()
        report.unlink()
        report.symlink_to(self.root / 'missing')
        with self.assertRaises(ValueError):
            self.verify()

    def test_same_execution_does_not_join_by_procedure_name(self):
        identity = {'server': 'SyntheticServer', 'database': 'SyntheticDatabase', 'schema': 'dbo', 'procedure': 'ReadItems'}
        row = {'factId': 'same', 'sourceTier': 'Tier1Semantic'}
        A.require_same_execution(row, row, {'factId': 'same'}, identity)
        for changed in (dict(row, factId='other'), dict(row, sourceTier='Tier3SyntaxOrTextual')):
            with self.assertRaises(ValueError):
                A.require_same_execution(row, changed, {'factId': 'same'}, identity)
        with self.assertRaises(ValueError):
            A.require_same_execution(row, row, {'factId': 'same'}, dict(identity, database='OtherDatabase'))

    def test_dnn_blockers_and_link_denominator_are_finite(self):
        self.assertEqual(len(A.DNN_BLOCKERS), 4)
        self.assertEqual(A.LINK_CASES, {'Good': (A.M, A.M), 'ParameterTypeMismatch': (A.X, A.M),
                                     'RenamedColumn': (A.M, A.X), 'AbsentIdentity': (A.U, A.U)})

    def test_write_never_replaces_existing_evidence(self):
        target = self.root / 'receipt.json'
        A.write(target, {'first': True})
        before = target.read_bytes()
        with self.assertRaises(FileExistsError):
            A.write(target, {'second': True})
        self.assertEqual(target.read_bytes(), before)

    def test_intentional_refusal_inputs_are_not_promoted_or_followed(self):
        refusal = self.root / 'refusals'
        refusal.mkdir()
        (refusal / 'source.sql').symlink_to(self.root / 'does-not-exist')
        self.verify()
        with self.assertRaises(ValueError):
            A.regular_tree(self.root)

    def test_well_formed_but_wrong_input_digest_refused(self):
        self.receipt['boundedInputSha256'] = '3' * 64
        with self.assertRaisesRegex(ValueError, 'input digest'):
            self.verify()

    def test_another_valid_report_cannot_substitute(self):
        self.receipt['results'][0]['reportSha256'] = self.receipt['results'][1]['reportSha256']
        with self.assertRaisesRegex(ValueError, 'case-specific'):
            self.verify()

    def test_case_rename_or_fact_substitution_refused(self):
        for key in ('case', 'factId'):
            receipt = copy.deepcopy(self.receipt)
            receipt['results'][0][key] = 'forged'
            with self.subTest(key=key), self.assertRaisesRegex(ValueError, 'identity'):
                self.verify(receipt)

    def test_refusal_names_unique_and_each_language_required(self):
        for label in ('reader', 'helper', 'field'):
            languages = ('csharp', 'vb') if label == 'field' else (None,)
            rows = [dict(case=name, refused=True, **({'language': lang} if lang else {}))
                    for lang in languages for name in self.contracts.REFUSALS[label].split()]
            self.contracts.verify_refusals(A, label, rows)
            rows[-1] = rows[0]
            with self.subTest(label=label), self.assertRaises(ValueError):
                self.contracts.verify_refusals(A, label, rows)

    def test_artifact_provenance_covers_effective_inputs(self):
        doc, base, source = {'identity': 'one'}, {'columns': ['Id']}, {'readerState': 'one'}
        original = A.artifact_provenance('results', 'Good', base, source, doc)
        for kind, changed_base, changed_source, changed_doc in (
                ('result-bindings', base, source, doc),
                ('results', {'columns': ['Name']}, source, doc),
                ('results', base, {'readerState': 'two'}, doc),
                ('results', base, source, {'identity': 'two'})):
            self.assertNotEqual(original['boundedInputSha256'],
                                A.artifact_provenance(kind, 'Good', changed_base, changed_source, changed_doc)['boundedInputSha256'])
        self.assertEqual(original, A.artifact_provenance('results', 'Good', base, source, dict(doc, provenance=original)))

    def test_reader_scan_is_in_evidence_tree(self):
        scan = self.root / 'reader/csharp/scan'
        scan.mkdir(parents=True)
        facts = scan / 'facts.ndjson'
        facts.write_text('first')
        before = A.regular_tree(self.root / 'reader')
        self.assertIn('csharp/scan/facts.ndjson', before)
        facts.write_text('changed')
        self.assertNotEqual(A.digest(before), A.digest(A.regular_tree(self.root / 'reader')))

    def test_failed_publication_leaves_no_partial_receipt(self):
        target = self.root / 'new.json'
        with patch.object(A.os, 'fsync', side_effect=OSError('interrupted')):
            with self.assertRaises(OSError):
                A.write(target, {'value': 'complete'})
        self.assertFalse(target.exists())
        self.assertEqual(list(self.root.glob('.receipt-*')), [])

    def test_subprocess_paths_are_literal_arguments(self):
        with patch.object(A.subprocess, 'run', return_value=Mock(returncode=0, stdout='', stderr='')) as call:
            A.command('dotnet', '/tmp/a; echo injected.dll')
        self.assertEqual(call.call_args.args[0], ['dotnet', '/tmp/a; echo injected.dll'])
        self.assertIs(call.call_args.kwargs['shell'], False)

    def test_committed_replay_is_canonical_and_isolates_transient_edits_and_builds(self):
        source = self.root / 'source'
        source.mkdir()
        with patch.object(A, 'ROOT', source):
            A.command('git', 'init', '-q')
            A.command('git', 'config', 'user.email', 'synthetic@example.invalid')
            A.command('git', 'config', 'user.name', 'Synthetic')
            A.command('git', 'config', 'core.autocrlf', 'true')
            (source / 'fixture.txt').write_bytes(b'one\r\ntwo\r\n')
            A.command('git', 'add', 'fixture.txt')
            A.command('git', 'commit', '-qm', 'public synthetic test')
            revision = A.command('git', 'rev-parse', 'HEAD').strip()
            hashes = A.committed_hashes(revision, ['fixture.txt'])
            with A.isolated_source():
                first = A.ROOT
                self.assertEqual((first / 'fixture.txt').read_bytes(), b'one\ntwo\n')
                self.assertEqual(A.sha(first / 'fixture.txt'), hashes['fixture.txt'])
                (source / 'fixture.txt').write_bytes(b'transient source edit')
                self.assertEqual((first / 'fixture.txt').read_bytes(), b'one\ntwo\n')
                with A.isolated_source():
                    second = A.ROOT
                    self.assertNotEqual(first, second)
                    (second / 'bin').mkdir()
                    self.assertFalse((first / 'bin').exists())
                self.assertFalse(second.exists())
            self.assertFalse(first.exists())
            self.assertEqual(A.ROOT, source)
            self.assertFalse((source / 'bin').exists())


if __name__ == '__main__':
    unittest.main()
