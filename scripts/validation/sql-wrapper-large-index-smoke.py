#!/usr/bin/env python3
"""Public-only native size-admission smoke; requires the compiled wrapper fixture.

The padded index exercises file-size admission, not a large logical graph or a
performance benchmark. No fixture methods or database-provider calls execute.
"""
import argparse
import contextlib
import json
import pathlib
import shutil
import sqlite3
import subprocess
import sys


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cli', type=pathlib.Path, required=True)
    parser.add_argument('--out', type=pathlib.Path, required=True)
    args = parser.parse_args()
    repo = pathlib.Path(__file__).resolve().parents[2]
    cli = args.cli.resolve(strict=True)
    output = args.out.resolve()
    output.mkdir(parents=True, exist_ok=False)
    binaries = repo / 'samples/sql-database-matrix/wrapper-build/bin/Debug/net472'
    scan = output / 'scan'
    subprocess.run(['dotnet', str(cli), 'scan', '--repo', str(repo / 'samples/sql-database-matrix/wrapper-website'),
                    '--out', str(scan), '--compiled-input', str(binaries / 'SyntheticWebsite.dll'),
                    '--compiled-input', str(binaries / 'SyntheticStore.dll'), '--il-body-evidence'], check=True, timeout=180)
    facts = [json.loads(line) for line in (scan / 'facts.ndjson').read_text(encoding='utf-8-sig').splitlines()]
    roots = [next(f['targetSymbol'] for f in facts if f['factType'] == 'ManagedMethodDeclared'
                  and f['properties'].get('metadataName') == method) for method in ['Save', 'Search']]
    large = output / 'public-padded.sqlite'
    shutil.copyfile(scan / 'index.sqlite', large)
    with large.open('r+b') as stream:
        stream.truncate(768 * 1024 * 1024)
    with contextlib.closing(sqlite3.connect(large.as_uri() + '?mode=ro', uri=True)) as connection:
        if connection.execute('PRAGMA quick_check').fetchone() != ('ok',):
            raise RuntimeError('Padded public SQLite index failed integrity check')
    command = [sys.executable, str(repo / 'scripts/sql-wrapper-report.py'), '--cli', str(cli), '--index', str(large)]
    for root in roots + ['missing-public-caller']:
        command += ['--root', root]
    rejected = subprocess.run(command + ['--out', str(output / 'default-refused')], capture_output=True, text=True, timeout=30)
    if rejected.returncode != 2 or 'IndexSizeLimitExceeded' not in rejected.stderr or (output / 'default-refused').exists():
        raise RuntimeError('Default size guard failed')
    accepted = output / 'accepted'
    subprocess.run(command + ['--out', str(accepted), '--max-index-mib', '1024', '--query-timeout-seconds', '600'], check=True, timeout=1900)
    counts = [len(json.loads((accepted / f'root-{i:03d}/paths-report.json').read_text(encoding='utf-8-sig'))['paths']) for i in range(1, 4)]
    if counts != [1, 1, 0]:
        raise RuntimeError('Unexpected public route counts')
    print('PASS: 768 MiB public padded SQLite; default refused; explicit 1024 MiB accepted; paths=1,1,0. File-size test only, not graph-scale proof.')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
