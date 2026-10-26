import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

SCRIPT = Path(__file__).resolve().parents[1] / 'dnn-sql-validation/harness.py'
spec = importlib.util.spec_from_file_location('dnn_harness', SCRIPT)
h = importlib.util.module_from_spec(spec)
spec.loader.exec_module(h)


def load_script(name):
    spec = importlib.util.spec_from_file_location(name, SCRIPT.parent / (name + '.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class HarnessTests(unittest.TestCase):
    def test_version_requires_one_four_part_scalar(self):
        self.assertEqual('16.0.4205.1', h.parse_version(' \r\n16.0.4205.1 \r\n'))
        for output in ('', 'NULL', '16.0.1', '16.0.1.2\n16.0.1.2',
                       'ProductVersion\n----\n16.0.1.2\n(1 rows affected)',
                       '16.0.1.2\n(1 rows affected)', 'warning\n16.0.1.2'):
            with self.subTest(output=output), self.assertRaises(ValueError):
                h.parse_version(output)

    def test_registry_resolution_is_byte_repeatable_for_identical_metadata(self):
        import json
        from unittest.mock import Mock
        resolver = load_script('resolve_image')
        config = h.canonical({'os': 'linux', 'architecture': 'amd64'})
        manifest = h.canonical({'mediaType': 'application/vnd.docker.distribution.manifest.v2+json',
                               'config': {'digest': 'sha256:' + h.sha(config)}})
        def response(url, **kwargs):
            result = Mock()
            result.__enter__ = Mock(return_value=result)
            result.__exit__ = Mock(return_value=False)
            result.read.return_value = config if isinstance(url, str) else manifest
            result.headers = {'Docker-Content-Digest': 'sha256:' + h.sha(manifest)}
            return result
        with patch.object(resolver.urllib.request, 'urlopen', side_effect=response):
            first, second = resolver.resolve(), resolver.resolve()
        self.assertEqual(json.dumps(first), json.dumps(second))
        self.assertNotIn('resolvedAt', first)

    def test_slice_inventory_rejects_extra_missing_and_renamed_files(self):
        static = load_script('validate_slice')
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            for name in [*static.FILES, 'public-slice.json', '.git/config']:
                path = root / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text('synthetic')
            self.assertEqual(10, static.verify_inventory(root))
            for name in ('extra.cs', 'nested/extra.sql', 'extra.json', '.hidden.cs'):
                extra = root / name
                extra.parent.mkdir(parents=True, exist_ok=True)
                extra.write_text('')
                with self.subTest(name=name), self.assertRaisesRegex(ValueError, 'inventory'):
                    static.verify_inventory(root)
                extra.unlink()
            original = root / static.FILES[0]
            original.rename(root / 'renamed')
            with self.assertRaisesRegex(ValueError, 'inventory'):
                static.verify_inventory(root)
            (root / 'renamed').unlink()
            with self.assertRaisesRegex(ValueError, 'inventory'):
                static.verify_inventory(root)

    def test_native_inventory_requires_unique_matching_count_and_revision(self):
        static = load_script('validate_slice')
        fact = {'factType': 'RepoScanned', 'commitSha': static.PIN, 'properties': {'fileCount': '10'}}
        static.verify_scan_inventory([fact], 10)
        for facts in ([], [fact, fact], [dict(fact, commitSha='wrong')],
                      [dict(fact, properties={'fileCount': '11'})],
                      [dict(fact, properties={})]):
            with self.subTest(facts=facts), self.assertRaisesRegex(ValueError, 'inventory'):
                static.verify_scan_inventory(facts, 10)

    def test_default_never_calls_docker_or_network(self):
        with patch.object(h.subprocess, 'run', side_effect=AssertionError('external effect')), patch('builtins.print'):
            self.assertEqual(0, h.main([]))
            self.assertEqual('NOT RUN', h.plan()['status'])
            self.assertIsNone(h.plan()['testedVersion'])

    def test_activation_gates_fail_before_any_effect(self):
        for args in (['--activate'], ['--activate', '--context', 'reviewed', '--expected-endpoint', 'unix:///reviewed']):
            with patch.object(h.subprocess, 'run', side_effect=AssertionError('external effect')), patch('builtins.print'):
                self.assertEqual(1, h.main(args))

    def test_all_approvals_are_individually_required(self):
        gates = ['--approve-host', '--approve-resources', '--accept-eula', '--approve-disposable']
        for missing in gates:
            with patch.object(h.subprocess, 'run', side_effect=AssertionError('external effect')), patch('builtins.print'):
                self.assertEqual(1, h.main(['--activate', '--context', 'reviewed', '--expected-endpoint', 'unix:///reviewed', '--output', '/unused'] + [g for g in gates if g != missing]))

    def test_exact_generator_and_bounded_public_input_are_recomputable(self):
        p = h.provenance()
        self.assertEqual(h.sha(SCRIPT.read_bytes()), p['generatorSha256'])
        self.assertEqual(h.sha(h.canonical(p['inputs'])), p['boundedInputSha256'])
        self.assertEqual(set(h.FILES), set(p['inputs']))

    def test_endpoint_mismatch_and_arm_fail_closed(self):
        from unittest.mock import Mock
        docker = Mock(context='reviewed')
        docker.run.return_value = 'unix:///other'
        with self.assertRaisesRegex(ValueError, 'endpoint'):
            h.preflight(docker, 'unix:///reviewed')
        docker.run.side_effect = ['unix:///reviewed', '{"OSType":"linux","Architecture":"aarch64"}']
        with self.assertRaisesRegex(ValueError, 'x86-64'):
            h.preflight(docker, 'unix:///reviewed')

    def test_missing_digest_does_not_pull(self):
        from unittest.mock import Mock
        docker = Mock(context='reviewed')
        docker.run.side_effect = ['reviewed', '{"OSType":"linux","Architecture":"amd64","NCPU":4,"MemTotal":17179869184,"MemoryLimit":true,"SwapLimit":true,"CpuCfsQuota":true,"PidsLimit":true}', '{"RepoDigests":[],"Os":"linux","Architecture":"amd64"}']
        with self.assertRaisesRegex(ValueError, 'RepoDigests'):
            h.preflight(docker, 'reviewed')
        self.assertFalse(any(c.args[0] in ('pull', 'create', 'run') for c in docker.run.call_args_list))

    def test_unenforceable_resource_bounds_refuse_before_image_or_creation(self):
        from unittest.mock import Mock
        docker = Mock(context='reviewed')
        docker.run.side_effect = ['reviewed', '{"OSType":"linux","Architecture":"amd64","NCPU":4,"MemTotal":17179869184,"MemoryLimit":false}']
        with self.assertRaisesRegex(ValueError, 'cannot enforce'):
            h.preflight(docker, 'reviewed')
        self.assertEqual(2, docker.run.call_count)

    def test_existing_output_is_preserved_before_preflight(self):
        with tempfile.TemporaryDirectory() as temp, patch.object(h.subprocess, 'run', side_effect=AssertionError('external effect')), patch('builtins.print'):
            args = ['--activate', '--context', 'reviewed', '--expected-endpoint', 'reviewed', '--output', temp,
                    '--approve-host', '--approve-resources', '--accept-eula', '--approve-disposable']
            self.assertEqual(1, h.main(args))


    def test_static_dnn_check_refuses_missing_input_and_wrong_source_identity(self):
        import json
        static_spec = importlib.util.spec_from_file_location('dnn_static', SCRIPT.parent / 'validate_slice.py')
        static = importlib.util.module_from_spec(static_spec)
        static_spec.loader.exec_module(static)
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            with self.assertRaises(FileNotFoundError):
                static.verify(root, root)
            (root / 'public-slice.json').write_text(json.dumps({'revision': 'wrong'}))
            with self.assertRaisesRegex(ValueError, 'source identity'):
                static.verify(root, root)

    def test_public_fetch_and_registry_resolver_defaults_are_offline(self):
        import contextlib
        import io
        import runpy
        for name in ('public_slice.py', 'resolve_image.py'):
            with patch('sys.argv', [name]), patch('subprocess.check_output', side_effect=AssertionError('unexpected git/network')), patch('urllib.request.urlopen', side_effect=AssertionError('unexpected registry/network')), contextlib.redirect_stdout(io.StringIO()) as output:
                runpy.run_path(str(SCRIPT.parent / name), run_name='__main__')
                self.assertIn('NOT ', output.getvalue())

    def test_recorded_registry_provenance_and_runtime_pin_agree(self):
        import json
        receipt = json.loads((h.SQL.parent / 'image-resolution.json').read_text())
        resolver = SCRIPT.parent / 'resolve_image.py'
        self.assertEqual(h.sha(resolver.read_bytes()), receipt['generatorSha256'])
        projection = {key: receipt[key] for key in ('registry', 'tag', 'manifestDigest', 'configDigest', 'os', 'architecture')}
        self.assertEqual(h.sha(h.canonical(projection)), receipt['boundedInputSha256'])
        self.assertEqual(h.IMAGE, receipt['image'])
        self.assertIsNone(receipt['testedVersion'])
        self.assertNotIn('resolvedAt', receipt)

    def test_mocked_activation_keeps_sql_phases_and_owned_cleanup_separate(self):
        from unittest.mock import Mock
        import json
        import secrets
        password = secrets.token_urlsafe(24) + 'Aa1!'
        fake = Mock()
        container = 'a' * 64
        owner = 'b' * 32
        def respond(*args, **kwargs):
            if args[0] == 'create':
                self.assertIn('--pull=never', args)
                self.assertIn('none', args)
                self.assertNotIn('--privileged', args)
                self.assertNotIn('-p', args)
                self.assertNotIn('-v', args)
                self.assertEqual('ALL', args[args.index('--cap-drop') + 1])
                self.assertEqual('NET_BIND_SERVICE', args[args.index('--cap-add') + 1])
                self.assertIn('no-new-privileges', args)
                return container
            if args[0] == 'inspect':
                if args[-1] == '{{.State.Running}}':
                    return 'true'
                return owner
            if args[0] == 'exec':
                self.assertEqual('tcp:127.0.0.1,1433', args[args.index('-S') + 1])
                if kwargs.get('input', '').startswith('SET NOCOUNT ON;\nSELECT CONVERT(nvarchar(128),SERVERPROPERTY('):
                    self.assertIn('-h', args)
                    self.assertIn('-1', args)
                    self.assertIn('-W', args)
                    self.assertIn('SET NOCOUNT ON;', kwargs['input'])
                    return '16.0.4205.1'
                return 'synthetic-mock-result'
            return ''
        fake.run.side_effect = respond
        with tempfile.TemporaryDirectory() as temp, patch.object(h, 'Docker', return_value=fake), patch.object(h, 'preflight', return_value='image-id'), patch.object(h.getpass, 'getpass', return_value=password), patch.object(h.uuid, 'uuid4', return_value=Mock(hex=owner)), patch('builtins.print'):
            output = Path(temp) / 'new'
            self.assertEqual(0, h.main(['--activate', '--context', 'reviewed', '--expected-endpoint', 'reviewed', '--output', str(output), '--approve-host', '--approve-resources', '--accept-eula', '--approve-disposable']))
            receipt = json.loads((output / 'receipt.json').read_text())
            self.assertEqual('owned-container-removed', receipt['cleanup'])
            self.assertEqual('completed', receipt['metadataDescription'])
            self.assertEqual('16.0.4205.1', receipt['testedVersion'])
            self.assertTrue((output / 'metadata.sql.log').exists())
            self.assertTrue((output / 'execution.sql.log').exists())
            self.assertEqual(h.provenance(), receipt['provenance'])
            self.assertNotIn(password, (output / 'receipt.json').read_text())
        fake.run.assert_any_call('rm', '-f', container)

    def test_provider_publish_rejects_incomplete_or_linked_payload(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            with self.assertRaisesRegex(ValueError, 'incomplete'):
                h.provider_inputs(root)
            for name in ('ProviderChecks', 'ProviderChecks.dll', 'Microsoft.Data.SqlClient.dll'):
                (root / name).write_bytes(b'public-test-bytes')
            first = h.provider_inputs(root)
            self.assertEqual(3, len(first))
            (root / 'ProviderChecks.dll').write_bytes(b'changed')
            self.assertNotEqual(first, h.provider_inputs(root))
            (root / 'link').symlink_to(root / 'ProviderChecks.dll')
            with self.assertRaisesRegex(ValueError, 'links'):
                h.provider_inputs(root)

    def test_provider_denominator_matches_the_linked_runner_and_client(self):
        self.assertEqual(42, h.PROVIDER_CHECKS)
        self.assertEqual(h.PROVIDER_CHECKS, load_script('linked_provider').RUNTIME_CHECKS)
        program = (h.ROOT / 'samples/sqlserver-live-provider/Program.cs').read_text()
        self.assertIn(f'PROVIDER_CHECKS_PASSED {h.PROVIDER_CHECKS}', program)
        self.assertIn(f'count != {h.PROVIDER_CHECKS}', program)
        self.assertNotIn('PROVIDER_CHECKS_PASSED 32', SCRIPT.read_text())
    def test_container_stderr_logs_are_retained_and_secret_redacted(self):
        from unittest.mock import Mock
        result = Mock(returncode=0, stdout='banner\n', stderr='startup failed secret-value\n')
        with patch.object(h.subprocess, 'run', return_value=result):
            logs = h.Docker('test', 'secret-value').run('logs', 'owned')
        self.assertIn('startup failed [REDACTED]', logs)
        self.assertNotIn('secret-value', logs)

    def test_mocked_unknown_owner_is_never_removed(self):
        from unittest.mock import Mock
        import secrets
        password = secrets.token_urlsafe(24) + 'Aa1!'
        fake = Mock()
        fake.run.side_effect = lambda *args, **kw: ('a' * 64 if args[0] == 'create' else 'different-owner' if args[0] == 'inspect' else '')
        with tempfile.TemporaryDirectory() as temp, patch.object(h, 'Docker', return_value=fake), patch.object(h, 'preflight', return_value='image-id'), patch.object(h.getpass, 'getpass', return_value=password), patch('builtins.print'):
            self.assertEqual(1, h.main(['--activate', '--context', 'reviewed', '--expected-endpoint', 'reviewed', '--output', str(Path(temp) / 'new'), '--approve-host', '--approve-resources', '--accept-eula', '--approve-disposable']))
        self.assertFalse(any(c.args[0] == 'rm' for c in fake.run.call_args_list))


if __name__ == '__main__':
    unittest.main()
