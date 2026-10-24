import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

SCRIPT = Path(__file__).resolve().parents[1] / 'dnn-sql-validation/harness.py'
spec = importlib.util.spec_from_file_location('dnn_harness', SCRIPT)
h = importlib.util.module_from_spec(spec)
spec.loader.exec_module(h)


class HarnessTests(unittest.TestCase):
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
                return container
            if args[0] == 'inspect':
                return owner
            if args[0] == 'exec':
                return 'synthetic-mock-result'
            return ''
        fake.run.side_effect = respond
        with tempfile.TemporaryDirectory() as temp, patch.object(h, 'Docker', return_value=fake), patch.object(h, 'preflight', return_value='image-id'), patch.object(h.getpass, 'getpass', return_value=password), patch.object(h.uuid, 'uuid4', return_value=Mock(hex=owner)), patch('builtins.print'):
            output = Path(temp) / 'new'
            self.assertEqual(0, h.main(['--activate', '--context', 'reviewed', '--expected-endpoint', 'reviewed', '--output', str(output), '--approve-host', '--approve-resources', '--accept-eula', '--approve-disposable']))
            receipt = json.loads((output / 'receipt.json').read_text())
            self.assertEqual('owned-container-removed', receipt['cleanup'])
            self.assertEqual('completed', receipt['metadataDescription'])
            self.assertTrue((output / 'metadata.sql.log').exists())
            self.assertTrue((output / 'execution.sql.log').exists())
            self.assertEqual(h.provenance(), receipt['provenance'])
            self.assertNotIn(password, (output / 'receipt.json').read_text())
        fake.run.assert_any_call('rm', '-f', container)

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
