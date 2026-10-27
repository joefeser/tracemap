#!/usr/bin/env python3
"""Fresh public offline acceptance replay. Never executes application methods or SQL."""
import argparse
from collections import Counter
import copy
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
PARENT = '6aa8c40e975b9277418aa3bc4b561073887c7f3e'
M, X, U = 'bounded-match', 'mismatch', 'unknown'
GATES = {
    'parameter': ('parameter', 'sqlserver-parameter-smoke/1', 52, (8, 12, 32), 0),
    'reader': ('result-column', 'sqlserver-result-column-smoke/1', 158, (72, 20, 94), 20),
    'helper': ('helper-result', 'sqlserver-helper-result-smoke/1', 230, (84, 30, 116), 24),
    'field': ('field-read', 'sqlserver-field-read-smoke/1', 150, (78, 36, 42), 26),
}
# Authored before execution. Same source execution, separately admitted contracts.
LINK_CASES = {'Good': (M, M), 'ParameterTypeMismatch': (X, M),
              'RenamedColumn': (M, X), 'AbsentIdentity': (U, U)}
DNN_BLOCKERS = (
    'selected-reader-binding-unavailable', 'qualified-object-identity-unavailable',
    'sql-install-contract-not-admitted', 'hydration-consumer-binding-unavailable',
)


def require(value, reason):
    if not value:
        raise ValueError(reason)


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(',', ':')).encode()


def digest(value):
    return hashlib.sha256(canonical(value)).hexdigest()


def write(path, value):
    with Path(path).open('xb') as stream:
        stream.write(canonical(value) + b'\n')


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def command(*args, timeout=1200):
    env = dict(os.environ)
    env.pop('PYTHONOPTIMIZE', None)  # Retained native smoke assertions must execute.
    result = subprocess.run([str(a) for a in args], cwd=ROOT, env=env,
                            capture_output=True, text=True, timeout=timeout, shell=False)
    require(result.returncode == 0, 'command failed: ' + result.stdout[-3000:] + result.stderr[-3000:])
    return result.stdout


def regular_tree(root):
    require(root.is_dir() and not any(p.is_symlink() for p in (root, *root.parents)), 'linked root refused')
    files = {}
    for path in sorted(root.rglob('*')):
        require(not path.is_symlink(), 'linked artifact refused')
        if path.is_file():
            files[path.relative_to(root).as_posix()] = sha(path)
        else:
            require(path.is_dir(), 'non-regular artifact refused')
    return files


def source_snapshot():
    require(not command('git', 'status', '--porcelain', '--untracked-files=all').strip(), 'clean committed child required')
    revision = command('git', 'rev-parse', 'HEAD').strip()
    command('git', 'merge-base', '--is-ancestor', PARENT, revision)
    paths = command('git', 'ls-files', 'scripts', 'samples', 'src/dotnet', 'rules',
                    'Directory.Build.*', 'Directory.Packages.props', 'global.json').splitlines()
    values = {}
    for name in paths:
        path = ROOT / name
        require(path.is_file() and not any(p.is_symlink() for p in (path, *path.parents)), 'source link/missing file')
        values[name] = sha(path)
    return revision, values


def verify_gate(root, label, revision, helpers):
    kind, version, cases, counts, refusal_count = GATES[label]
    receipt = json.loads((root / 'smoke-receipt.json').read_bytes())
    require(receipt.get('version') == version and receipt.get('revision') == revision, 'stale gate identity')
    require(receipt.get('generatorSha256') == sha(ROOT / f'scripts/sqlserver-{kind}-evidence/smoke.py'), 'gate generator changed')
    require(re.fullmatch('[0-9a-f]{64}', receipt.get('boundedInputSha256', '')) is not None, 'missing gate input digest')
    require(receipt.get('sqlExecution') == 'not-run' and not receipt.get('gaps'), 'gate scope/gaps changed')
    rows = receipt['results']
    require(len(rows) == cases, 'gate case denominator changed')
    keys = [(r['language'], r.get('kind'), r['case'], r.get('origin'), r.get('variant'), r.get('template'), r['factId']) for r in rows]
    require(len(set(keys)) == cases and {r['language'] for r in rows} == {'csharp', 'vb'}, 'duplicate or missing gate case')
    actual = Counter(s for r in rows for s in ([r['status']] if label == 'parameter' else r['statuses']))
    require(actual == Counter(dict(zip((M, X, U), counts))), 'gate row counts changed')
    refusals = receipt.get('refusals', [])
    require(len(refusals) == refusal_count and all(r.get('refused') is True for r in refusals), 'gate refusal checks incomplete')
    helpers.verify_scan_hashes(root, receipt)
    tree = regular_tree(root)
    report_hashes = {value for name, value in tree.items() if '/report/' in name and name.endswith('.json')}
    require(all(r.get('reportSha256') in report_hashes for r in rows), 'native report missing or changed')
    return {'caseReports': cases, 'rowCounts': dict(actual), 'refusalChecks': refusal_count,
            'receiptSha256': sha(root / 'smoke-receipt.json'), 'artifactsSha256': digest(tree)}


def require_same_execution(parameter, reader, fact, identity):
    for row in (parameter, reader):
        require(row['factId'] == fact['factId'], 'cross-execution join refused')
        require(row['sourceTier'] == 'Tier1Semantic', 'semantic source required')
    # Bindings, not short names, establish the independently authored contract identity.
    require(identity == {'server': 'SyntheticServer', 'database': 'SyntheticDatabase',
                         'schema': 'dbo', 'procedure': 'ReadItems'}, 'unexpected authored operation')


def linked_static(output, reader_root, cli, revision, reader):
    helpers = reader.HELPERS
    rows = []
    base = json.loads((ROOT / 'samples/sqlserver-parameter-evidence/declarations-base.json').read_bytes())
    shape_base = json.loads((ROOT / 'samples/sqlserver-result-column-evidence/result-declarations-base.json').read_bytes())
    for language in ('csharp', 'vb'):
        scan = reader_root / language / 'scan'
        facts = [json.loads(line) for line in (scan / 'facts.ndjson').read_text().splitlines()]
        selected = [f for f in facts if f['factType'] == 'DatabaseOperationCandidate' and '.WithParameter(' in (f.get('sourceSymbol') or '')]
        require(len(selected) == 1, 'same-execution anchor missing/ambiguous')
        fact = selected[0]
        require(fact['commitSha'] == revision, 'same-execution source revision mismatch')
        for name, expected in LINK_CASES.items():
            work = output / language / name
            work.mkdir(parents=True)
            declarations = copy.deepcopy(base)
            declarations.update(repo=fact['repo'], revision=revision)
            proc = declarations['procedures'][0]
            proc.update(identity=dict(reader.IDENTITY), filePath='public-linked-declarations.json', startLine=1, endLine=1)
            proc['parameters'][0].update(name='@Id', sqlType='BigInt' if name == 'ParameterTypeMismatch' else 'Int',
                                         size=None, units='not-applicable', line=1)
            projected = helpers.projection(fact)
            bindings = {'version': 'sqlserver-bindings/1', 'repo': fact['repo'], 'revision': revision,
                        'calls': [{'factId': fact['factId'], 'commandText': 'dbo.ReadItems',
                                   'identity': dict(reader.IDENTITY), 'sourceEvidenceSha256': helpers.digest(projected)}]}
            shapes = reader.declarations(shape_base, 'renamed' if name == 'RenamedColumn' else None,
                                         fact['repo'], revision, 'public-linked-result.json')
            result_binding = reader.binding(fact, 'WithParameter', revision, reader.reader_projection(fact))
            if name == 'AbsentIdentity':
                bindings['calls'][0]['identity']['database'] = 'AbsentDatabase'
                result_binding['calls'][0]['identity']['database'] = 'AbsentDatabase'
            provenance = {'generatorSha256': sha(__file__), 'boundedInputSha256': digest({'case': name, 'fact': projected})}
            for filename, doc in (('parameters', declarations), ('parameter-bindings', bindings),
                                  ('results', shapes), ('result-bindings', result_binding)):
                doc['provenance'] = provenance
                helpers.write(work / f'{filename}.json', doc)
            compared = []
            for route, declaration, binding, filename in (
                    ('sqlserver-schema', 'parameters', 'parameter-bindings', 'parameter-comparison.json'),
                    ('sqlserver-result-schema', 'results', 'result-bindings', 'result-column-comparison.json')):
                schema = work / f'{route}.json'
                command('dotnet', cli, route, 'produce', '--declarations', work / f'{declaration}.json', '--out', schema)
                for target in ('report', 'repeat'):
                    command('dotnet', cli, route, 'compare', '--declarations', work / f'{declaration}.json',
                            '--schema', schema, '--bindings', work / f'{binding}.json', '--facts', scan / 'facts.ndjson',
                            '--repo', fact['repo'], '--revision', revision, '--out', work / f'{route}-{target}')
                path = work / f'{route}-report' / filename
                require(path.read_bytes() == (work / f'{route}-repeat' / filename).read_bytes(), 'linked report nondeterministic')
                report = json.loads(path.read_bytes())
                require(report['coverage'] == 'partial' and report['verdict'] == 'no-aggregate-clean-verdict', 'linked coverage widened')
                chosen = [r for r in report['comparisons'] if r['factId'] == fact['factId']]
                require(len(chosen) == 1, 'linked comparison denominator changed')
                compared.append(chosen[0])
            require_same_execution(*compared, fact, reader.IDENTITY)
            require(tuple(r['status'] for r in compared) == expected, f'{language}/{name}: unexpected linked statuses {compared}')
            rows.append({'language': language, 'case': name, 'factId': fact['factId'], 'revision': revision,
                         'identity': reader.IDENTITY, 'sourceEvidence': fact['evidence'],
                         'parameterComparison': compared[0], 'readerComparison': compared[1],
                         'helper': 'not-required-direct-command', 'runtimeLink': 'not-run'})
    return rows


def run(args):
    require(args.fetch_public_dnn, 'explicit public DNN fetch required; no private-input mode')
    output = args.output.resolve()
    require(not output.is_relative_to(ROOT.resolve()) and not output.exists(), 'new external output required')
    revision, before = source_snapshot()
    cli = ROOT / 'src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll'
    require(cli.is_file(), 'build child CLI first')
    binaries = {p.name: sha(p) for p in cli.parent.glob('*.dll')}
    output.mkdir(parents=True)
    reader = load('acceptance_reader', ROOT / 'scripts/sqlserver-result-column-evidence/smoke.py')
    gates = {}
    for label, (kind, *_rest) in GATES.items():
        print('Running offline gate: ' + label, flush=True)
        # Fresh worktrees have no fixture assets. Restore/build only these public
        # compile-only projects, never a second full solution or upstream DNN.
        sample = ROOT / f'samples/sqlserver-{kind}-evidence'
        for extension in ('csproj', 'vbproj'):
            projects = list(sample.glob(f'*/*.{extension}'))
            require(len(projects) == 1, 'fixture project denominator changed')
            command('dotnet', 'build', projects[0], '-c', 'Debug', '-warnaserror', '--nologo')
        command(sys.executable, ROOT / f'scripts/sqlserver-{kind}-evidence/smoke.py', '--output', output / label)
        gates[label] = verify_gate(output / label, label, revision, reader.HELPERS)
    links = linked_static(output / 'same-execution', output / 'reader', cli, revision, reader)
    print('Checking pinned public DNN operation (no upstream build)', flush=True)
    command(sys.executable, ROOT / 'scripts/dnn-sql-validation/public_slice.py', '--fetch', '--destination', output / 'dnn-source')
    command('dotnet', cli, 'scan', '--repo', output / 'dnn-source', '--out', output / 'dnn-scan')
    command(sys.executable, ROOT / 'scripts/dnn-sql-validation/validate_slice.py', '--slice', output / 'dnn-source', '--scan', output / 'dnn-scan')
    require(source_snapshot() == (revision, before), 'source drift during acceptance')
    require(binaries == {p.name: sha(p) for p in cli.parent.glob('*.dll')}, 'binary drift during acceptance')
    # Hash generated evidence, not the upstream .git database or machine-specific logs.
    evidence = {label: regular_tree(output / label) for label in (*GATES, 'same-execution')}
    dnn_setup = json.loads((output / 'dnn-source/public-slice.json').read_bytes())
    receipt = {'version': 'sqlserver-public-acceptance/1', 'generatorSha256': sha(__file__),
               'boundedInputSha256': digest({'sources': before, 'binaries': binaries, 'evidence': evidence, 'dnn': dnn_setup}),
               'revision': revision, 'parentRevision': PARENT, 'coverage': 'partial', 'verdict': 'no-aggregate-clean-verdict',
               'syntheticGates': gates, 'sameExecutionRoutes': links, 'sourceHashes': before, 'binaryHashes': binaries,
               'dnn': {'operation': 'GetRoleGroups', 'operationCount': 1, 'revision': dnn_setup['revision'],
                       'status': 'blocked', 'blockers': list(DNN_BLOCKERS), 'publicInputSha256': dnn_setup['boundedInputSha256'],
                       'callerHelper': 'source-inspected-not-bound', 'commandParameters': 'unavailable',
                       'admittedContract': 'unavailable', 'resultConsumer': 'unavailable', 'runtimeLink': 'not-run'},
               'sqlExecution': 'not-run', 'fullSuite': 'pending-coordinated-slot', 'liveGate': 'pending-coordinated-slot'}
    write(output / 'public-acceptance.json', receipt)
    print('Verified public synthetic gates and 8 same-execution pairs; DNN remains BLOCKED; SQL NOT RUN')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--fetch-public-dnn', action='store_true')
    try:
        run(parser.parse_args())
    except (ValueError, OSError, KeyError, subprocess.SubprocessError) as error:
        raise SystemExit(str(error)) from error
