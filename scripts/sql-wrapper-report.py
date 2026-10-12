#!/usr/bin/env python3
"""Local-only, selected-root SQL report from an existing immutable TraceMap index.

Runs only the committed paths reader. Does not scan/build applications or execute SQL.
No new machine-readable schema: native path reports retain their existing contracts.
"""
import argparse
import hashlib
import html
import json
import pathlib
import subprocess

MAX_INDEX = 512 * 1024 * 1024
MAX_REPORT = 64 * 1024 * 1024


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def cell(value):
    return html.escape(str(value if value is not None else 'unavailable')).replace('|', '&#124;').replace('`', '&#96;').replace('\r', ' ').replace('\n', ' ')


def render_root(ordinal, selector, report):
    summary = report['summary']
    paths = report['paths']
    lines = [f'## Selection {ordinal}', '', f'Caller selector: {cell(selector)}', '',
             f"Coverage: {cell(report['reportCoverage'])}; selector candidates: {summary['selectorCandidateCount']}; retained paths: {len(paths)}; truncated: {summary['truncated']}.",
             f'[Native evidence and limitations](root-{ordinal:03d}/paths-report.json)', '',
             '| Path | Ordered caller / wrapper / terminal | Classification | Command text evidence | Command type evidence | Terminal rule / tier |',
             '| --- | --- | --- | --- | --- | --- |']
    for number, path in enumerate(paths, 1):
        nodes = path['nodes']
        binding = nodes[-1].get('commandBinding') or {}
        text = binding.get('commandTextFromPath') or {}
        kind = binding.get('commandTypeFromPath') or {}
        route = ' → '.join(str(n.get('displayName') or n.get('surfaceName') or n.get('symbolId') or 'unknown') for n in nodes)
        type_identity = (kind.get('origin') or {}).get('identity')
        type_label = {'4': 'StoredProcedure', '1': 'Text', '512': 'TableDirect'}.get(type_identity, 'unavailable') if (kind.get('origin') or {}).get('kind') == 'constant-int32' else 'unavailable'
        lines.append(f"| {number} | {cell(route)} | {cell(path.get('classification'))} | {cell(text.get('state', 'unavailable'))} | {cell(kind.get('state', 'unavailable'))}: {cell(type_label)} | {cell(nodes[-1].get('ruleId'))} / {cell(nodes[-1].get('evidenceTier'))} |")
    if not paths:
        lines.append('| — | No retained SQL route for this supplied selection | unresolved | unavailable | unavailable | unavailable |')
    for number, path in enumerate(paths, 1):
        lines += ['', f'### Path {number} evidence and restrictions', '']
        for edge in path.get('edges', []):
            lines.append(f"- Edge {cell(edge.get('edgeId'))}: {cell(edge.get('ruleId'))} / {cell(edge.get('evidenceTier'))}; {cell(edge.get('fromNodeId'))} → {cell(edge.get('toNodeId'))}.")
            lines += ['  - ' + cell(limit) for limit in edge.get('limitations', [])]
        for node in path['nodes']:
            lines += ['- Node ' + cell(node.get('nodeId')) + ': ' + cell(limit) for limit in node.get('limitations', [])]
        lines += ['- ' + cell((str(note.get('code')) + ': ' + str(note.get('message'))) if isinstance(note, dict) else note) for note in path.get('notes', [])]
    path_nodes = {node.get('nodeId') for path in paths for node in path['nodes']} - {None}
    path_facts = {fact for path in paths for fact in path.get('supportingFactIds', [])}
    lines += ['', '### Retained gaps and coverage warnings', '',
              'Native gaps can be graph-wide. A retained-path association is not proof of a blocking edge; provenance warnings can coexist with a retained route. Unassociated gaps must not be attributed to this caller without further evidence.', '',
              '| Gap | Association | Reason / message | Rule / tier | Node / fact evidence |', '| --- | --- | --- | --- | --- |']
    for gap in report['gaps']:
        association = 'retained path node/fact' if gap.get('nodeId') in path_nodes or gap.get('combinedFactId') in path_facts else 'not associated with a retained path; caller relevance unproven'
        lines.append(f"| {cell(gap.get('gapKind'))} | {association} | {cell(gap.get('reason'))}: {cell(gap.get('message'))} | {cell(gap.get('ruleId'))} / {cell(gap.get('evidenceTier'))} | {cell(gap.get('nodeId'))} / {cell(gap.get('combinedFactId'))} |")
    if not report['gaps']:
        lines.append('| — | — | No retained gap in this bounded query; not a completeness proof | — | — |')
    for warning in report.get('coverageWarnings', []) + report.get('limitations', []):
        lines.append('- ' + cell(warning))
    return lines + ['']


def run(args):
    index = args.index.resolve(strict=True)
    cli = args.cli.resolve(strict=True)
    if index.stat().st_size > MAX_INDEX:
        raise ValueError('Index exceeds the 512 MiB input limit; no report generated.')
    if not 1 <= len(args.root) <= 32 or any(not root.strip() or len(root) > 8192 for root in args.root):
        raise ValueError('Supply 1..32 explicit root identities, each 1..8192 characters.')
    if len(set(args.root)) != len(args.root):
        raise ValueError('Duplicate root selectors are not allowed.')
    # Live SQLite sidecars are outside the admitted immutable-file denominator.
    if any(pathlib.Path(str(index) + suffix).exists() for suffix in ['-wal', '-journal']):
        raise ValueError('Use a closed immutable index without WAL/journal sidecars.')
    args.out.mkdir(parents=True, exist_ok=False)
    original = digest(index)
    components = sorted(p for p in cli.parent.iterdir() if p.is_file() and (p.suffix == '.dll' or p.name.endswith(('.deps.json', '.runtimeconfig.json'))))
    if len(components) > 512 or sum(p.stat().st_size for p in components) > 256 * 1024 * 1024:
        raise ValueError('Tool directory exceeds bounded component inventory.')
    identities = {p.name: digest(p) for p in components}
    selectors_hash = hashlib.sha256(json.dumps(args.root, ensure_ascii=False, separators=(',', ':')).encode()).hexdigest()
    lines = ['# SQL wrapper inventory — local only', '',
             '**Scope: supplied caller selections only, not a whole-site inventory.** Every selection is retained even when no SQL path is found. No-path never means unused. Paths are static candidates, not runtime execution or verified UI wiring.', '',
             'Readable SQL/procedure contracts, parameter values/results, connection identity and transaction scope are unavailable unless separately established in the linked evidence. This report does not reconstruct SQL or decide what to migrate.', '',
             f'- Report script SHA-256: `{digest(pathlib.Path(__file__))}`',
             f'- Input index SHA-256: `{original}`', f'- Ordered selectors SHA-256: `{selectors_hash}`',
             '- Bounds: 32 roots maximum; 20-hop depth, 256 paths and default CLI work/frontier bounds per root; 120-second timeout per root; 64 MiB native report limit.',
             '- These hashes bind the local files, not historical build authenticity. Keep this report, native evidence and private identities on the authorized machine.', '']
    lines += ['Tool inventory below covers top-level managed DLLs and deps/runtimeconfig manifests, not the installed dotnet host, OS or recursively resolved native libraries.', '']
    lines += [f'- Tool component {cell(name)} SHA-256: `{sha}`' for name, sha in identities.items()]
    lines.append('')
    failures = 0
    for ordinal, selector in enumerate(args.root, 1):
        folder = args.out / f'root-{ordinal:03d}'
        command = ['dotnet', str(cli), 'paths', '--index', str(index), '--out', str(folder),
                   '--format', 'json', '--from-symbol', selector, '--exact-from-symbol',
                   '--to-surface', 'database-api', '--max-depth', '20', '--max-paths', '256']
        try:
            completed = subprocess.run(command, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=120, check=False)
            if completed.returncode:
                raise ValueError('native paths command failed')
            artifact = folder / 'paths-report.json'
            if artifact.stat().st_size > MAX_REPORT:
                raise ValueError('native report exceeds size limit')
            report = json.loads(artifact.read_text(encoding='utf-8-sig'))
            if report['query'].get('fromSymbol') != selector or report['query'].get('exactFromSymbol') is not True or report['query'].get('toSurface') != 'database-api':
                raise ValueError('native query identity mismatch')
            lines += render_root(ordinal, selector, report)
            lines += [f'Native report SHA-256: `{digest(artifact)}`', '']
        except (OSError, ValueError, KeyError, TypeError, IndexError, subprocess.TimeoutExpired):
            failures += 1
            lines += [f'## Selection {ordinal}', '', f'Caller selector: {cell(selector)}', '',
                      'QUERY FAILED OR OUTPUT INVALID — no conclusion about SQL use. Inspect the retained local output and rerun the documented native command for diagnostics. This selection is not counted as empty.', '']
    final_components = sorted(p for p in cli.parent.iterdir() if p.is_file() and (p.suffix == '.dll' or p.name.endswith(('.deps.json', '.runtimeconfig.json'))))
    if digest(index) != original or components != final_components or identities != {p.name: digest(p) for p in components} or any(pathlib.Path(str(index) + suffix).exists() for suffix in ['-wal', '-journal']):
        raise ValueError('Input/tool changed during reporting; no consolidated report written. Retained outputs are unaccepted.')
    lines += ['## Delivery status', '', f'{len(args.root)} supplied selections; {failures} failed queries. Coverage is bounded and partial, even when every query succeeds.', '']
    (args.out / 'sql-wrapper-inventory.local.md').write_text('\n'.join(lines), encoding='utf-8')
    print(f'SQL report written; selections={len(args.root)}, failedQueries={failures}, scope=selected-roots-only')
    return 2 if failures else 0


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--index', type=pathlib.Path, required=True)
    parser.add_argument('--cli', type=pathlib.Path, required=True, help='Built tracemap.dll')
    parser.add_argument('--root', action='append', required=True, help='Exact retained symbol identity; repeat for each authorized caller')
    parser.add_argument('--out', type=pathlib.Path, required=True, help='New local output directory')
    args = parser.parse_args()
    try:
        return run(args)
    except (OSError, ValueError) as error:
        parser.exit(2, f'Report refused: {type(error).__name__}. Check input existence, bounds, sidecars and fresh output location.\n')


if __name__ == '__main__':
    raise SystemExit(main())
