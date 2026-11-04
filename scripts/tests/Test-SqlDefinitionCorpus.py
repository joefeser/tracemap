#!/usr/bin/env python3
"""Opt-in pinned public SQL compatibility smoke. Never builds upstream or executes SQL."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from urllib.parse import quote
from urllib.request import urlopen

DNN_PIN = '33b8fbe440ab665b8423599789db1f34d2a500fc'
WWI_PIN = '3c71696a3942879f0481f3bd65fe112a40023f18'
DNN_SCHEMA = 'DNN Platform/Website/Providers/DataProviders/SqlDataProvider/DotNetNuke.Schema.SqlDataProvider'
WWI_BASE = 'samples/databases/wide-world-importers/wwi-ssdt/wwi-ssdt/'
SOURCES = {
    'dnn': ('dnnsoftware/Dnn.Platform', DNN_PIN, ['LICENSE', 'NOTICE.md', DNN_SCHEMA]),
    'wwi': ('microsoft/sql-server-samples', WWI_PIN, ['license.txt',
        WWI_BASE + 'Website/Stored Procedures/SearchForCustomers.sql',
        WWI_BASE + 'Website/Stored Procedures/SearchForSuppliers.sql',
        WWI_BASE + 'Warehouse/Tables/Colors.sql'])
}


def digest(data):
    return hashlib.sha256(data).hexdigest()


def encoded(value):
    return json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=False).encode()


def save(path, value):
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')


def run(root, cli):
    if root.exists():
        raise ValueError('Choose a new output folder; existing evidence is preserved')
    cli = cli.resolve(strict=True)
    for filename in ('TraceMap.Core.dll', 'Microsoft.SqlServer.TransactSql.ScriptDom.dll'):
        if not (cli.parent / filename).is_file():
            raise ValueError('CLI runtime dependencies missing')
    root.mkdir(parents=True)
    generator = digest(Path(__file__).read_bytes())
    projection = {}
    contents = {}
    for key, (repo, pin, paths) in SOURCES.items():
        for name in paths:
            url = f'https://raw.githubusercontent.com/{repo}/{pin}/{quote(name, safe="/")}'
            with urlopen(url, timeout=30) as response:
                data = response.read(2_097_153)
            if len(data) > 2_097_152:
                raise ValueError('Public corpus download size limit')
            path = root / 'upstream' / key / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
            projection[key + '/' + name] = {'revision': pin, 'url': url, 'sha256': digest(data)}
            contents[key, name] = data
        license_name = 'LICENSE' if key == 'dnn' else 'license.txt'
        if b'MIT' not in contents[key, license_name]:
            raise ValueError('Upstream license changed; inspect before using corpus')
    dnn = contents['dnn', DNN_SCHEMA]
    if digest(dnn) != 'e487db02868ca7eb551f12aaf9b561cde78151e8a1131a2b5bbb782fa22e6f46':
        raise ValueError('Pinned DNN schema bytes changed')
    # Explicit line projection, never qualifier substitution or an invented upstream revision.
    lines = dnn.decode('utf-8-sig').splitlines(keepends=True)
    selected = ''.join(lines[7126:7138] + lines[19150:19158]).encode()
    if b'{databaseOwner}' not in selected or b'{objectQualifier}' not in selected:
        raise ValueError('Pinned DNN slice markers changed')
    cases = [
        ('dnn-template-slice', DNN_PIN, {'selected.sql': selected}, 'ddl-malformed-sql',
            {'source': 'dnn/' + DNN_SCHEMA, 'lineRanges': [[7127, 7138], [19151, 19158]], 'substitutions': []}),
        ('dnn-full-install-over-limit', DNN_PIN, {'schema.sql': dnn}, 'input-size-limit', {'wholeFile': True}),
        ('wwi-procedures', WWI_PIN, {
            name + '.sql': contents['wwi', WWI_BASE + 'Website/Stored Procedures/' + name + '.sql']
            for name in ('SearchForCustomers', 'SearchForSuppliers')}, 'partial', {'wholeFiles': True}),
        ('wwi-table-setup-execution', WWI_PIN, {'Colors.sql': contents['wwi', WWI_BASE + 'Warehouse/Tables/Colors.sql']},
            'sql-local-top-level-statement-not-admitted', {'wholeFile': True})
    ]
    results = []
    for name, revision, files, expected, selection in cases:
        case = root / name
        folder = case / 'input'; folder.mkdir(parents=True)
        manifest = dict(version='sqlserver-local-definitions-input/1', visibility='local-only',
            contextRepository='public-corpus', contextCommitSha=revision, definitionSet=name,
            server='ExampleServer', database='ExampleDatabase',
            files=[dict(path=path, sha256=digest(data)) for path, data in files.items()])
        manifest['provenance'] = dict(generatorSha256=generator, boundedInputSha256=digest(encoded(manifest)))
        for path, data in files.items():
            (folder / path).write_bytes(data)
        manifest_path = case / 'input.local.json'; save(manifest_path, manifest)
        result = subprocess.run(['dotnet', str(cli), 'sqlserver-definitions', 'collect', '--input', str(manifest_path),
            '--source-root', str(folder), '--out', str(case / 'collected')], capture_output=True, text=True, timeout=60)
        observation = dict(name=name, expected=expected, returnCode=result.returncode, selection=selection,
            files=manifest['files'], stdout=result.stdout, stderr=result.stderr)
        if expected == 'partial':
            if result.returncode != 0:
                raise ValueError(name + ': collection failed: ' + result.stderr)
            export = json.loads((case / 'collected/definitions.local.json').read_text())
            objects = export['objects']
            if export['coverage'] != 'partial' or len(objects) != 2 or {o['name'] for o in objects} != {'SearchForCustomers', 'SearchForSuppliers'}:
                raise ValueError('WWI procedure inventory mismatch')
            for obj in objects:
                if obj['kind'] != 'procedure' or obj['schema'] != 'Website' or obj['resultShape']['completeness'] != 'unknown':
                    raise ValueError('Complex WWI procedure shape was overclaimed')
                if [p['name'] for p in obj['parameters']] != ['@SearchText', '@MaximumRowsToReturn']:
                    raise ValueError('WWI parameter inventory mismatch')
                if [p['declaredType'] for p in obj['parameters']] != ['nvarchar(1000)', 'int']:
                    raise ValueError('WWI parameter types mismatch')
            relation_names = {(r['schema'], r['name']) for r in export['dependencies'] if r['kind'] == 'relation'}
            if relation_names != {('Sales', 'Customers'), ('Purchasing', 'Suppliers'), ('Application', 'Cities'), ('Application', 'People')}:
                raise ValueError('WWI relation reference inventory mismatch')
            if any(r['targetId'] is not None for r in export['dependencies']):
                raise ValueError('Unadmitted WWI dependency was resolved')
            emitted = (case / 'collected/definitions.local.json').read_text()
            if any(value in emitted for value in ("N'Customers'", "N'Suppliers'", 'CONCAT(c.CustomerName')):
                raise ValueError('Literal/body output is not admitted')
            observation['outputFiles'] = {p.name: digest(p.read_bytes()) for p in (case / 'collected').iterdir()}
            observation['objects'] = len(objects)
            observation['unknownResultShapes'] = 2
        elif result.returncode == 0 or expected not in result.stderr or (case / 'collected').exists():
            raise ValueError(name + ': expected refusal did not hold: ' + result.stderr)
        results.append(observation)
    bounded = dict(sources=projection, cliSha256=digest(cli.read_bytes()),
        coreSha256=digest((cli.parent / 'TraceMap.Core.dll').read_bytes()),
        parserSha256=digest((cli.parent / 'Microsoft.SqlServer.TransactSql.ScriptDom.dll').read_bytes()),
        cases=[dict(name=c[0], expected=c[3], selection=c[4]) for c in cases])
    save(root / 'public-corpus-receipt.json', dict(version='sql-local-public-corpus-smoke/1', visibility='local-only',
        generatorSha256=generator, boundedInputSha256=digest(encoded(bounded)), input=bounded,
        outcome='passed', coverage='bounded-static-only', sqlExecution='not-run', sourceCallBinding='not-established', cases=results))
    print('Passed: 4 pinned public cases; 2 WWI procedure definitions with unknown result shapes; 3 expected refusals. No SQL executed.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--fetch', action='store_true')
    parser.add_argument('--cli', type=Path)
    parser.add_argument('--out', type=Path)
    args = parser.parse_args()
    if not args.fetch:
        print('NOT FETCHED: pinned DNN and Wide World Importers definitions; use --fetch --cli <tracemap.dll> --out <new-folder>')
        return
    if args.cli is None or args.out is None:
        parser.error('--fetch requires --cli and --out')
    run(args.out.absolute(), args.cli)


if __name__ == '__main__':
    main()
