"""Offline guard tests; no native process, network, SQL or container."""
import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import Mock

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
        for i, status in enumerate([A.M] * 8 + [A.X] * 12 + [A.U] * 32):
            rows.append({'language': 'csharp' if i < 26 else 'vb', 'case': f'Case{i}',
                         'factId': f'fact{i}', 'status': status, 'reportSha256': A.sha(report)})
        self.receipt = {'version': 'sqlserver-parameter-smoke/1', 'revision': self.revision,
                        'generatorSha256': A.sha(A.ROOT / 'scripts/sqlserver-parameter-evidence/smoke.py'),
                        'boundedInputSha256': '2' * 64, 'sqlExecution': 'not-run', 'results': rows}
        self.helpers = Mock()

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

    def test_artifact_changes_and_links_refused(self):
        report = self.root / 'csharp/case/report/report.json'
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


if __name__ == '__main__':
    unittest.main()
