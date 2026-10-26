#!/usr/bin/env python3
"""Explicit opt-in static/runtime matrix; no effect without all activation gates."""
import argparse
from collections import Counter
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]
SAMPLE = ROOT / 'samples/sqlserver-live-provider'
CASES = {
    'ReaderName': ('reader', 'bounded-match'),
    'TableName': ('datatable', 'bounded-match'),
    'SetName': ('dataset', 'bounded-match'),
    'MissingName': ('datatable', 'mismatch'),
    'CaseOnly': ('datatable', 'unknown'),
    'LaterTable': ('dataset', 'unknown'),
}

def require(value, message):
    if not value:
        raise ValueError(message)

def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module

def inputs():
    paths = [p for p in SAMPLE.rglob('*') if p.is_file() and not {'bin', 'obj'} & set(p.relative_to(SAMPLE).parts)]
    paths += [Path(__file__), ROOT / 'scripts/dnn-sql-validation/harness.py',
              ROOT / 'samples/sqlserver-result-column-evidence/result-declarations-base.json']
    paths += list((ROOT / 'samples/dnn-sql-validation/sql').glob('*.sql'))
    paths += [ROOT / f'scripts/sqlserver-{kind}-evidence/smoke.py'
              for kind in ('parameter', 'result-column', 'datatable', 'dataset')]
    require(not any(p.is_symlink() for p in paths), 'source links refused')
    return {p.relative_to(ROOT).as_posix(): sha(p) for p in sorted(paths)}

def verify_committed(revision, snapshot):
    require(not subprocess.check_output(['git', 'status', '--porcelain', '--untracked-files=all'], cwd=ROOT),
            'clean committed worktree required; preserve changes and commit before validation')
    require(subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip() == revision,
            'HEAD changed during validation')
    for name, digest in snapshot.items():
        blob = subprocess.check_output(['git', 'show', f'{revision}:{name}'], cwd=ROOT)
        require(hashlib.sha256(blob).hexdigest() == digest, 'input differs from committed blob: ' + name)

def run_runtime(args, publish, runtime):
    # The harness bounds each Docker operation and readiness attempt itself.
    # Invoke in-process: an outer subprocess timeout must never kill its finally
    # block before ownership-checked cleanup and partial receipt persistence.
    harness = load('linked_runtime', ROOT / 'scripts/dnn-sql-validation/harness.py')
    harness.run(argparse.Namespace(activate=True, context=args.context,
        expected_endpoint=args.expected_endpoint, approve_host=True, approve_resources=True,
        accept_eula=True, approve_disposable=True, generate_password=True,
        provider_directory=publish, output=runtime))

def summarize(rows):
    return dict(sorted(Counter(row['static'] for row in rows).items()))

def verify_runtime(runtime, rows):
    receipt = json.loads((runtime / 'receipt.json').read_bytes())
    require(receipt['status'] == 'PASSED synthetic SQL checks' and receipt['cleanup'] == 'owned-container-removed', 'runtime failed/unclean')
    log = (runtime / 'provider.log').read_text().splitlines()
    require(sha(runtime / 'provider.log') == receipt['outputs']['provider.log'], 'runtime log hash mismatch')
    require(log.count('PROVIDER_CHECKS_PASSED 32') == 1, 'runtime denominator mismatch')
    expected = {f'PASS linked-{language}-{name}' for language in ('csharp', 'vb') for name in CASES}
    observed = [line for line in log if line.startswith('PASS linked-')]
    require(len(observed) == len(expected) and set(observed) == expected, 'runtime counterpart missing/duplicate/extra')
    require({(r['language'], r['case']) for r in rows} == {(language, name) for language in ('csharp', 'vb') for name in CASES}
            and len(rows) == 12, 'static denominator mismatch')

def run(args):
    gates = ('activate', 'approve_host', 'approve_resources', 'accept_eula', 'approve_disposable')
    require(all(getattr(args, g) for g in gates), 'all activation approvals required')
    require(args.context and args.expected_endpoint, 'explicit reviewed context and endpoint required')
    args.output = args.output.resolve()
    require(not args.output.is_relative_to(SAMPLE.resolve()), 'output must be outside sample tree')
    require(not args.output.exists(), 'new output directory required')
    before = inputs()
    revision = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
    verify_committed(revision, before)
    args.output.mkdir(parents=True)
    helper = load('linked_reader', ROOT / 'scripts/sqlserver-result-column-evidence/smoke.py')
    table = load('linked_table', ROOT / 'scripts/sqlserver-datatable-evidence/smoke.py')
    dataset = load('linked_dataset', ROOT / 'scripts/sqlserver-dataset-evidence/smoke.py')
    cli = ROOT / 'src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll'
    require(cli.exists(), 'build CLI before this run')
    cli_files = {p.name: sha(p) for p in cli.parent.glob('*.dll')}
    def command(*parts):
        result = subprocess.run([str(p) for p in parts], cwd=ROOT, capture_output=True, text=True, timeout=600)
        require(result.returncode == 0, 'command failed: ' + result.stdout[-2000:] + result.stderr[-2000:])
        return result.stdout
    publish = args.output / 'publish'
    command('dotnet', 'publish', SAMPLE / 'ProviderChecks.csproj', '-c', 'Release', '-r', 'linux-x64',
            '--self-contained', 'true', '-warnaserror', '-o', publish)
    require(inputs() == before, 'source changed during publish')
    rows = []
    evidence_hashes = {}
    for language, extension in (('csharp', 'csproj'), ('vb', 'vbproj')):
        work = args.output / language
        work.mkdir()
        scan = work / 'scan'
        command('dotnet', cli, 'scan', '--repo', SAMPLE / language, '--project', f'Linked.{extension}', '--out', scan)
        manifest = json.loads((scan / 'scan-manifest.json').read_bytes())
        require(manifest['buildStatus'] == 'Succeeded' and manifest['commitSha'] == revision, 'scan build/revision mismatch')
        facts = [json.loads(line) for line in (scan / 'facts.ndjson').read_text().splitlines()]
        facts = [f for f in facts if f['factType'] == 'DatabaseOperationCandidate' and f['evidenceTier'] == 'Tier1Semantic']
        keyed = {k: f for f, k in helper.case_keys(facts)}
        require(len(facts) == 6 and set(keyed) == set(CASES), 'missing/extra modern provider execution sites')
        base = json.loads((ROOT / 'samples/sqlserver-result-column-evidence/result-declarations-base.json').read_bytes())
        declarations = helper.declarations(base, None, facts[0]['repo'], revision, 'independent-public-shapes.json')
        decl = work / 'declarations.json'
        helper.HELPERS.write(decl, declarations)
        schema = work / 'schema.json'
        command('dotnet', cli, 'sqlserver-result-schema', 'produce', '--declarations', decl, '--out', schema)
        for name, (route, expected) in CASES.items():
            fact = keyed[name]
            projection, binding = {
                'reader': (helper.reader_projection, helper.binding),
                'datatable': (table.datatable_projection, table.binding),
                'dataset': (dataset.dataset_projection, dataset.binding),
            }[route]
            case = work / name
            case.mkdir()
            bindings = case / 'bindings.json'
            helper.HELPERS.write(bindings, binding(fact, name, revision, projection(fact)))
            cli_route = 'sqlserver-result-schema' if route == 'reader' else f'sqlserver-{route}-result'
            origin = [] if route == 'reader' else ['--origin', 'result-schema']
            filename = 'result-column-comparison.json' if route == 'reader' else f'{route}-column-comparison.json'
            for target in ('report', 'repeat'):
                command('dotnet', cli, cli_route, 'compare', *origin, '--declarations', decl, '--schema', schema,
                        '--bindings', bindings, '--facts', scan / 'facts.ndjson', '--repo', fact['repo'],
                        '--revision', revision, '--out', case / target)
            report = json.loads((case / 'report' / filename).read_bytes())
            for file in (filename, 'report.md'):
                require((case / 'report' / file).read_bytes() == (case / 'repeat' / file).read_bytes(), 'report is not deterministic')
            selected = [r for r in report['comparisons'] if r['factId'] == fact['factId']]
            require([r['status'] for r in selected] == [expected], f'{language}/{name}: expected {expected}, observed {selected}')
            require(report['coverage'] == 'partial' and report['verdict'] == 'no-aggregate-clean-verdict', 'coverage widened')
            require(selected[0]['sourceTier'] == 'Tier1Semantic' and
                    selected[0]['tier'] == ('Tier4Unknown' if expected == 'unknown' else 'Tier2Structural'), 'tier widened')
            rows.append({'language': language, 'case': name, 'static': selected[0]['status'],
                         'comparison': selected[0], 'limitations': report['limitations'],
                         'reportPath': (case / 'report' / filename).relative_to(args.output).as_posix()})
        for path in sorted(work.rglob('*')):
            if path.is_file():
                evidence_hashes[path.relative_to(args.output).as_posix()] = sha(path)
    runtime = args.output / 'runtime'
    run_runtime(args, publish, runtime)
    verify_runtime(runtime, rows)
    for row in rows:
        row['runtime'] = 'expected exception' if row['case'] == 'MissingName' else '99' if row['case'] == 'LaterTable' else 'sample'
    require(inputs() == before, 'source changed during validation')
    verify_committed(revision, before)
    require(cli_files == {p.name: sha(p) for p in cli.parent.glob('*.dll')}, 'CLI changed during validation')
    evidence_hashes['runtime/receipt.json'] = sha(runtime / 'receipt.json')
    result = {'version': 'sqlserver-linked-provider-validation/1', 'generatorSha256': sha(__file__),
              'boundedInputSha256': helper.HELPERS.digest({'sources': before, 'cli': cli_files, 'evidence': evidence_hashes}),
              'revision': revision, 'coverage': 'partial', 'verdict': 'bounded-matrix-verified-not-application-acceptance',
              'sources': before, 'cli': cli_files, 'evidence': evidence_hashes, 'rows': rows,
              'runtimeChecks': 32, 'staticCounts': summarize(rows)}
    helper.HELPERS.write(args.output / 'linked-receipt.json', result)
    print(f'Verified {len(rows)} linked cases: {summarize(rows)}; 32 runtime checks; owned container removed')

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for gate in ('activate', 'approve-host', 'approve-resources', 'accept-eula', 'approve-disposable'):
        parser.add_argument('--' + gate, action='store_true')
    parser.add_argument('--context')
    parser.add_argument('--expected-endpoint')
    parser.add_argument('--output', type=Path, required=True)
    run(parser.parse_args())

if __name__ == '__main__':
    main()
