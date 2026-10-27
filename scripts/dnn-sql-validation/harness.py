#!/usr/bin/env python3
"""Offline plan by default. Disposable SQL activation requires every explicit gate.
No default context, pull, published ports, source mounts or persistent volumes.
"""
import argparse
import getpass
import hashlib
import json
import os
from pathlib import Path
import re
import secrets
import subprocess
import sys
import time
import uuid

ROOT = Path(__file__).resolve().parents[2]
SQL = ROOT / 'samples/dnn-sql-validation/sql'
IMAGE = 'mcr.microsoft.com/mssql/server@sha256:7c29dfbac885ad7519e219c7fe4aee0e67283e21a10e9c252d13b0fbde1866f8'
TAG = '2022-CU20-ubuntu-22.04'
LABEL = 'tools.tracemap.dnn-sql-validation'
FILES = ('schema.sql', 'seed.sql', 'metadata.sql', 'execution.sql')
# Provider oracle denominator: 20 original checks plus 34 linked static/runtime method checks (22 + 12 Phase 6 Field reads).
PROVIDER_CHECKS = 54


def sha(data):
    return hashlib.sha256(data).hexdigest()


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(',', ':')).encode()


def parse_version(output):
    version = output.strip()
    if not re.fullmatch(r'[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+', version):
        raise ValueError('expected exactly one SQL Server ProductVersion scalar')
    return version


def provenance(payloads=None):
    # Only public fixed fixture bytes are in this bounded input projection.
    payloads = payloads if payloads is not None else {name: (SQL / name).read_bytes() for name in FILES}
    inputs = {name: sha(payloads[name]) for name in FILES}
    return dict(generatorSha256=sha(Path(__file__).read_bytes()),
                boundedInputSha256=sha(canonical(inputs)), inputs=inputs)


def plan(payloads=None):
    return dict(status='NOT RUN', image=IMAGE, versionTag=TAG,
                testedVersion=None, network='none', publishedPorts=[],
                cpus=2, memoryMiB=6144, sqlMemoryMiB=2048, tmpfsMiB=2048,
                storage='owned container tmpfs; no persistent volume',
                phases=['metadata-description', 'synthetic-execution'],
                limitations='No DNN execution, private acceptance or deployed parity.',
                provenance=provenance(payloads))


def validate(args):
    if not args.activate:
        raise ValueError('activation not requested')
    if not args.context or not args.expected_endpoint:
        raise ValueError('explicit context and exact reviewed endpoint required')
    if not all((args.approve_host, args.approve_resources, args.accept_eula, args.approve_disposable)):
        raise ValueError('host, resource/disk, Developer EULA and disposable database approvals required')
    if not args.output or Path(args.output).exists():
        raise ValueError('new local output directory required (existing outputs are preserved)')


def provider_inputs(directory):
    """Only a bounded, link-free self-contained public test publish is admitted."""
    root = Path(directory)
    if root.is_symlink() or not root.is_dir():
        raise ValueError('provider publish directory required; no links')
    files = sorted(root.rglob('*'))
    if any(p.is_symlink() for p in files):
        raise ValueError('provider publish links refused')
    files = [p for p in files if p.is_file()]
    if len(files) > 512 or sum(p.stat().st_size for p in files) > 256 * 1024**2:
        raise ValueError('provider publish exceeds bounds')
    names = {p.relative_to(root).as_posix(): sha(p.read_bytes()) for p in files}
    if not {'ProviderChecks', 'ProviderChecks.dll', 'Microsoft.Data.SqlClient.dll'} <= names.keys():
        raise ValueError('self-contained provider test publish incomplete')
    return names


class Docker:
    def __init__(self, context, password=''):
        self.context, self.password = context, password

    def run(self, *args, input=None, timeout=60):
        # shell=False; never pass a password value in argv or log argv/env.
        env = os.environ.copy()
        env['MSSQL_SA_PASSWORD'] = self.password
        env['SQLCMDPASSWORD'] = self.password
        try:
            result = subprocess.run(['docker', '--context', self.context, *args], input=input,
                                    text=True, capture_output=True, timeout=timeout, env=env, check=False)
        except (OSError, subprocess.TimeoutExpired) as error:
            raise RuntimeError('Docker command unavailable or timed out; inspect the approved context') from error
        stdout, stderr = result.stdout, result.stderr
        if self.password:
            stdout, stderr = stdout.replace(self.password, '[REDACTED]'), stderr.replace(self.password, '[REDACTED]')
        if result.returncode:
            # Do not echo daemon messages which may include environment or credential data.
            raise RuntimeError(f'Docker operation {args[0]} failed with exit {result.returncode}; inspect the approved resource')
        return (stdout + stderr if args[0] == 'logs' else stdout).strip()


def preflight(docker, endpoint):
    observed = docker.run('context', 'inspect', docker.context, '--format', '{{.Endpoints.docker.Host}}')
    if observed != endpoint:
        raise ValueError('Docker context endpoint differs from reviewed endpoint')
    info = json.loads(docker.run('info', '--format', '{{json .}}'))
    if info.get('OSType') != 'linux' or info.get('Architecture') not in ('x86_64', 'amd64'):
        raise ValueError('native Linux x86-64 daemon required; ARM emulation is not accepted')
    if info.get('NCPU', 0) < 2 or info.get('MemTotal', 0) < 8 * 1024**3:
        raise ValueError('host needs >=2 CPUs and >=8 GiB RAM plus reviewed >=10 GiB free disk')
    if any(info.get(capability) is not True for capability in ('MemoryLimit', 'SwapLimit', 'CpuCfsQuota', 'PidsLimit')):
        raise ValueError('daemon cannot enforce the reviewed memory/swap/CPU/PID bounds')
    image = json.loads(docker.run('image', 'inspect', IMAGE, '--format', '{{json .}}'))
    if IMAGE not in image.get('RepoDigests', []) or image.get('Os') != 'linux' or image.get('Architecture') != 'amd64':
        raise ValueError('locally admitted image RepoDigests/platform mismatch; no automatic pull')
    return image['Id']


def run(args):
    validate(args)  # Must run before any subprocess, password prompt, or mutation.
    provider = getattr(args, 'provider_directory', None)
    provider_hashes = provider_inputs(provider) if provider else None
    payloads = {name: (SQL / name).read_bytes() for name in FILES}
    docker = Docker(args.context)
    image_id = preflight(docker, args.expected_endpoint)
    password = ('Aa1!' + secrets.token_urlsafe(32) if getattr(args, 'generate_password', False)
                else getpass.getpass('Disposable SA password (not stored): '))
    if len(password) < 16 or not all(re.search(pattern, password) for pattern in ('[A-Z]', '[a-z]', '[0-9]', '[^A-Za-z0-9]')):
        raise ValueError('use at least 16 characters with upper/lower/digit/symbol')
    docker.password = password
    output = Path(args.output)
    output.mkdir(mode=0o700)  # Exclusive; never replace prior evidence.
    owner = uuid.uuid4().hex
    name = 'tracemap-dnn-' + owner
    container = None
    receipt = plan(payloads)
    if provider_hashes:
        receipt['providerInputs'] = provider_hashes
        receipt['providerBoundedInputSha256'] = sha(canonical(provider_hashes))
        receipt['provenance']['boundedInputSha256'] = sha(canonical({
            'sql': receipt['provenance']['inputs'], 'provider': provider_hashes}))
    receipt.update(status='FAILED', owner=owner, imageId=image_id, cleanup='not-yet-created')
    (output / 'receipt.json').write_text(json.dumps(receipt, indent=2) + '\n')
    try:
        container = docker.run('create', '--pull=never', '--name', name,
            '--label', f'{LABEL}={owner}', '--network', 'none', '--cpus', '2',
            '--memory', '6g', '--memory-swap', '6g', '--pids-limit', '512',
            '--cap-drop', 'ALL', '--cap-add', 'NET_BIND_SERVICE', '--security-opt', 'no-new-privileges',
            '--tmpfs', '/var/opt/mssql:rw,nosuid,nodev,size=2147483648,uid=10001,gid=0,mode=0770',
            '-e', 'ACCEPT_EULA=Y', '-e', 'MSSQL_PID=Developer',
            '-e', 'MSSQL_MEMORY_LIMIT_MB=2048', '-e', 'MSSQL_SA_PASSWORD', IMAGE)
        if not re.fullmatch('[0-9a-f]{64}', container):
            raise ValueError('invalid created container ID; manual ownership inspection required')
        receipt.update(containerId=container, cleanup='required')
        (output / 'receipt.json').write_text(json.dumps(receipt, indent=2) + '\n')
        docker.run('start', container)
        def sql(text, readiness=False, scalar=False):
            return docker.run('exec', '-i', '-e', 'SQLCMDPASSWORD', container,
                '/opt/mssql-tools18/bin/sqlcmd', '-S', 'tcp:127.0.0.1,1433', '-U', 'sa', '-C',
                '-b', '-l', '3' if readiness else '5', '-t', '3' if readiness else '20',
                *(['-h', '-1', '-W'] if scalar else []),
                input=text, timeout=10 if readiness else 45)
        receipt['phase'] = 'readiness'
        ready = False
        for _ in range(60):
            if docker.run('inspect', container, '--format', '{{.State.Running}}') != 'true':
                raise RuntimeError('SQL container exited before readiness; inspect container.log')
            try:
                sql('SELECT 1;\nGO\n', readiness=True)
                ready = True
                break
            except RuntimeError:
                time.sleep(2)
        if not ready:
            raise RuntimeError('SQL readiness failed after 60 bounded attempts; inspect container.log')
        receipt['phase'] = 'create-owned-database'
        sql('CREATE DATABASE TraceMapSynthetic908;\nGO\n')
        prefix = 'USE TraceMapSynthetic908;\nGO\n'
        for phase in FILES:
            receipt['phase'] = phase
            result = sql(prefix + payloads[phase].decode('utf-8'))
            (output / (phase + '.log')).write_text(result + '\n')
        if provider:
            receipt['phase'] = 'microsoft-data-sqlclient-runtime'
            if provider_inputs(provider) != provider_hashes:
                raise ValueError('provider publish changed before copy')
            docker.run('cp', str(Path(provider).resolve()), container + ':/tmp/provider', timeout=120)
            result = docker.run('exec', '-e', 'SQLCMDPASSWORD', container,
                                '/tmp/provider/ProviderChecks', timeout=120)
            if provider_inputs(provider) != provider_hashes:
                raise ValueError('provider publish changed during execution')
            lines = result.splitlines()
            if lines[-1:] != [f'PROVIDER_CHECKS_PASSED {PROVIDER_CHECKS}'] or sum(s.startswith('PASS ') for s in lines) != PROVIDER_CHECKS:
                raise ValueError('provider test denominator mismatch')
            (output / 'provider.log').write_text(result + '\n')
            receipt['providerRuntime'] = f'{PROVIDER_CHECKS} checks passed; observed execution only, not static acceptance'
        receipt['phase'] = 'server-version'
        version = parse_version(sql("SET NOCOUNT ON;\nSELECT CONVERT(nvarchar(128),SERVERPROPERTY('ProductVersion'));\nGO\n", scalar=True))
        receipt.update(status='PASSED synthetic SQL checks', testedVersion=version,
                       metadataDescription='completed', syntheticExecution='completed')
    finally:
        cleanup_error = None
        try:
            if container and re.fullmatch('[0-9a-f]{64}', container):
                # Inspect the returned ID, never a pre-existing name; preserve unknowns.
                actual_owner = docker.run('inspect', container, '--format', '{{index .Config.Labels "' + LABEL + '"}}')
                if actual_owner != owner:
                    receipt['cleanup'] = 'unknown-owner-preserved'
                    raise RuntimeError('ownership mismatch; resource preserved for operator inspection')
                try:
                    (output / 'container.log').write_text(docker.run('logs', '--tail', '150', container) + '\n')
                finally:
                    docker.run('rm', '-f', container)
                    receipt['cleanup'] = 'owned-container-removed'
        except (RuntimeError, OSError) as error:
            cleanup_error = error
            receipt['status'] = 'FAILED cleanup or log capture; inspect owned resource'
        finally:
            # Save partial results even if cleanup or diagnostics fail.
            receipt['outputs'] = {p.name: sha(p.read_bytes()) for p in sorted(output.glob('*.log'))}
            (output / 'receipt.json').write_text(json.dumps(receipt, indent=2) + '\n')
        if cleanup_error:
            raise cleanup_error
    return receipt


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--activate', action='store_true')
    parser.add_argument('--context')
    parser.add_argument('--expected-endpoint')
    parser.add_argument('--output')
    parser.add_argument('--provider-directory', help='Optional reviewed self-contained linux-x64 ProviderChecks publish')
    parser.add_argument('--generate-password', action='store_true', help='Generate a throwaway secret in memory; never print or persist it')
    for gate in ('approve-host', 'approve-resources', 'accept-eula', 'approve-disposable'):
        parser.add_argument('--' + gate, action='store_true')
    args = parser.parse_args(argv)
    if not args.activate:
        print(json.dumps(plan(), indent=2))
        return 0
    try:
        print(json.dumps(run(args), indent=2))
        return 0
    except (ValueError, RuntimeError, OSError) as error:
        print(f'Harness refused/failed: {error}', file=sys.stderr)
        return 1


if __name__ == '__main__':
    sys.exit(main())
