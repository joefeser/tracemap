"""Offline guards; isolated local Git only, no network, .NET, SQL or container."""
import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import shutil
from types import SimpleNamespace
from unittest.mock import Mock, patch

SPEC = importlib.util.spec_from_file_location('public_acceptance', Path(__file__).resolve().parents[1] / 'dnn-sql-validation/public_acceptance.py')
A = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(A)


class PublicAcceptanceTests(unittest.TestCase):
    """Small verifier unit tests; real reconstruction is covered separately below."""
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


class AcceptanceReconstructionTests(unittest.TestCase):
    """Real reconstruction over authored source-fact fixtures; no CLI or reconstruction mocks."""

    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.root = Path(temp.name).resolve()
        self.contracts = A.load('real_contracts_test', A.ROOT / 'scripts/dnn-sql-validation/acceptance_contracts.py')
        self.matrix = self.contracts.MATRIX

    def fixture(self, label):
        root = self.root / label
        matrix = self.matrix
        for language in matrix.LANGUAGES:
            facts = []

            def fact(key, properties):
                number = len(facts) + 1
                facts.append({'factId': f'{language}-{number}', 'factType': 'DatabaseOperationCandidate',
                              'repo': 'public-synthetic-fixture', 'commitSha': '1' * 40,
                              'sourceSymbol': 'Fixture.' + key.split('#')[0] + '()',
                              'ruleId': 'database.operation.invocation.v1', 'evidenceTier': 'Tier1Semantic',
                              'evidence': {'filePath': 'Fixture.' + ('cs' if language == 'csharp' else 'vb'),
                                           'startLine': number, 'endLine': number,
                                           'extractorId': language + '-semantic', 'extractorVersion': 'synthetic'},
                              'properties': properties})

            if label in ('parameter', 'reader'):
                for key in matrix.PARAMETER if label == 'parameter' else matrix.READER:
                    fact(key, {'sqlServerParameterState': '{"coverage":"bounded","parameters":[]}',
                               'sqlServerReaderState': '{"coverage":"bounded","accesses":[]}'})
            elif label == 'helper':
                callers = [dict(caller='Fixture.' + key.split('#')[0] + '()', line=100 + i)
                           for i, key in enumerate([*matrix.HELPER_CALLERS, *matrix.CALLER_UNKNOWN])]
                for key in (*matrix.BOUNDED_HELPERS, *matrix.HELPER_UNKNOWN):
                    fact(key, {'operationKind': 'data-adapter-fill', 'sqlServerHelperResultState':
                               json.dumps({'callers': callers if key == 'ReadTable' else []})})
            else:
                for key in (*matrix.FIELD_FILLS, *matrix.FIELD_UNKNOWN, *matrix.FIELD_HELPERS):
                    names = matrix.FIELD_HELPERS.get(key, (key,) if key in matrix.FIELD_FILLS or key in matrix.FIELD_HAS_UNKNOWN_SITE else ())
                    sites = [dict(caller='Fixture.' + name + '()', line=100 + i,
                                  path=f'{language}/Callers' if key.startswith('ReadShared') else None)
                             for i, name in enumerate(names)]
                    fact(key, {'operationKind': 'data-adapter-fill', 'sqlServerFieldReadState': json.dumps({'sites': sites})})
            scan = root / language / 'scan'
            scan.mkdir(parents=True)
            for name in ('scan-manifest.json', 'index.sqlite', 'report.md', 'logs/analyzer.log'):
                path = scan / name
                path.parent.mkdir(exist_ok=True)
                path.write_text('public synthetic guard input')
            (scan / 'facts.ndjson').write_text(''.join(json.dumps(f) + '\n' for f in facts))
        bounded, expected = self.contracts.reconstruct(A, root, label)
        rows = []
        for identity, relative, statuses in expected:
            path = root / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            report = {'coverage': 'partial', 'verdict': 'no-aggregate-clean-verdict', 'comparisons': [
                dict(factId=identity['factId'], status=s,
                     **{k: identity[k] for k in ('callerIndex', 'siteIndex') if k in identity}) for s in statuses]}
            path.write_text(json.dumps({'comparison': report} if label == 'reader' and identity['origin'] == 'ddl' else report))
            rows.append(dict(identity, reportSha256=A.sha(path),
                             **({'status': statuses[0]} if label == 'parameter' else {'statuses': statuses})))
        kind, version, *_ = A.GATES[label]
        helpers = A.load('real_scan_hashes', A.ROOT / 'scripts/sqlserver-parameter-evidence/smoke.py')
        languages = matrix.LANGUAGES if label == 'field' else (None,)
        receipt = {'version': version, 'revision': '1' * 40,
                   'generatorSha256': A.sha(A.ROOT / f'scripts/sqlserver-{kind}-evidence/smoke.py'),
                   'boundedInputSha256': bounded, 'results': rows, 'sqlExecution': 'not-run',
                   'scanArtifactSha256': {lang: helpers.scan_hashes(root, lang) for lang in matrix.LANGUAGES},
                   'refusals': [dict(case=name, refused=True, **({'language': lang} if lang else {}))
                                for lang in languages for name in self.contracts.REFUSALS[label].split()]}
        (root / 'smoke-receipt.json').write_text(json.dumps(receipt))
        return root, receipt, expected, helpers

    def test_real_reconstruction_all_routes_and_report_paths(self):
        totals = {'parameter': (52, 8, 12, 32), 'reader': (158, 72, 20, 94),
                  'helper': (230, 84, 30, 116), 'field': (150, 78, 36, 42)}
        for label, (cases, matches, mismatches, unknowns) in totals.items():
            with self.subTest(label=label):
                root, receipt, expected, helpers = self.fixture(label)
                result = A.verify_gate(root, label, '1' * 40, helpers)
                self.assertEqual(result['caseReports'], cases)
                self.assertEqual(result['rowCounts'], {A.M: matches, A.X: mismatches, A.U: unknowns})
                self.assertEqual(len({path for _, path, _ in expected}), cases)
                if label == 'parameter':
                    self.assertEqual(expected[0][1], 'csharp/00-Good/report/parameter-comparison.json')
                elif label == 'reader':
                    self.assertEqual(expected[0][1], 'csharp/json/00-NameMatch/declarations/report/result-column-comparison.json')
                else:
                    self.assertIn('/000-', expected[0][1])

    def test_regenerated_count_preserving_status_swaps_are_rejected(self):
        for label in A.GATES:
            root, receipt, expected, helpers = self.fixture(label)
            positions = [next(i for i, (_, _, statuses) in enumerate(expected) if statuses == [s]) for s in (A.M, A.X)]
            for i, status in zip(positions, (A.X, A.M)):
                row = receipt['results'][i]
                row['status' if label == 'parameter' else 'statuses'] = status if label == 'parameter' else [status]
                path = root / expected[i][1]
                report = json.loads(path.read_bytes())
                inner = report.get('comparison', report)
                inner['comparisons'][0]['status'] = status
                path.write_text(json.dumps(report))
                row['reportSha256'] = A.sha(path)
            (root / 'smoke-receipt.json').write_text(json.dumps(receipt))
            with self.subTest(label=label), self.assertRaisesRegex(ValueError, 'statuses'):
                A.verify_gate(root, label, '1' * 40, helpers)

    def test_native_expectation_and_keying_changes_do_not_change_acceptance_oracle(self):
        for label, (kind, *_) in A.GATES.items():
            root, _, expected, _ = self.fixture(label)
            native = A.load('mutated_native', A.ROOT / f'scripts/sqlserver-{kind}-evidence/smoke.py')
            for name in ('EXPECTED', 'SOURCE_CASES', 'BODY_MATRIX', 'DDL_TEMPLATES'):
                setattr(native, name, {'forged': [A.M]})
            native.REPORT = 'forged-comparison.json'
            for name in ('case_plan', 'case_keys', 'fill_keys', 'caller_keys', 'site_keys', 'check_fill_state'):
                setattr(native, name, Mock(side_effect=AssertionError('native oracle must not be consulted')))
            api = SimpleNamespace(**vars(A))
            api.load = lambda *_: native
            self.assertEqual(self.contracts.reconstruct(api, root, label)[1], expected)

    def test_real_input_digest_tracks_source_bases_and_templates(self):
        for label in A.GATES:
            root, receipt, _, _ = self.fixture(label)
            path = root / 'csharp/scan/facts.ndjson'
            facts = [json.loads(line) for line in path.read_text().splitlines()]
            facts[0]['evidence']['extractorVersion'] = 'changed'
            path.write_text(''.join(json.dumps(f) + '\n' for f in facts))
            self.assertNotEqual(self.contracts.reconstruct(A, root, label)[0], receipt['boundedInputSha256'])
            fixture_root = self.root / ('inputs-' + label)
            for sample in ('sqlserver-parameter-evidence', 'sqlserver-result-column-evidence'):
                shutil.copytree(A.ROOT / 'samples' / sample, fixture_root / 'samples' / sample,
                                ignore=shutil.ignore_patterns('bin', 'obj'))
            api = SimpleNamespace(**vars(A))
            api.ROOT = fixture_root
            api.load = lambda name, path: A.load(name, A.ROOT / path.relative_to(fixture_root))
            before = self.contracts.reconstruct(api, root, label)[0]
            sample, filename = ('sqlserver-parameter-evidence', 'declarations-base.json') if label == 'parameter' else (
                'sqlserver-result-column-evidence', 'result-declarations-base.json')
            base = fixture_root / 'samples' / sample / filename
            doc = json.loads(base.read_bytes())
            doc['revision'] = 'changed'
            base.write_text(json.dumps(doc))
            self.assertNotEqual(self.contracts.reconstruct(api, root, label)[0], before)
            if label != 'parameter':
                before = self.contracts.reconstruct(api, root, label)[0]
                template = fixture_root / 'samples/sqlserver-result-column-evidence/ddl/base.sql'
                template.write_text(template.read_text() + '\n-- changed public template\n')
                self.assertNotEqual(self.contracts.reconstruct(api, root, label)[0], before)


if __name__ == '__main__':
    unittest.main()
