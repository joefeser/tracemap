"""Reconstruct the four finite native smoke contracts without executing a CLI."""
import json
import re
import importlib.util
from pathlib import Path
from collections import Counter

SPEC = importlib.util.spec_from_file_location('independent_acceptance_matrix', Path(__file__).with_name('acceptance_matrix.py'))
MATRIX = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MATRIX)


def member(symbol):
    match = re.search(r'\.([A-Za-z]+)(?:<[^>]*>|\(Of [^)]*\))?\(', symbol)
    if match is None:
        raise ValueError('unrecognized source member')
    return match[1]


def keyed(items):
    items = list(items)
    counts, seen = Counter(name for name, _ in items), Counter()
    result = []
    for name, item in items:
        key = f'{name}#{seen[name]}' if counts[name] > 1 else name
        seen[name] += 1
        result.append((key, item))
    return result

REFUSALS = {
    'parameter': '',
    'reader': '''stale-revision stale-reader-digest mutated-declaration-bytes mutated-export-bytes
parameter-export-as-result-schema parameter-bindings-as-result-bindings mutated-source-facts
oversized-declarations existing-output-not-replaced mutated-ddl-source malformed-sql
sqlcmd-in-comment sqlcmd-in-string oversized-sql symlinked-source parameter-manifest-version
ambiguous-identity mutated-ddl-export json-export-as-ddl-schema parameter-ddl-export-as-result-schema''',
    'helper': '''datatable-bindings-as-helper-bindings dataset-bindings-as-helper-bindings
reader-bindings-as-helper-bindings parameter-bindings-as-helper-bindings datatable-digest-substitution
dataset-digest-substitution stale-helper-digest caller-index-substitution caller-digest-substitution
out-of-range-caller-index duplicate-binding stale-revision mutated-caller-access mutated-caller-procedure-hash
table-export-as-result-schema constant-export-as-table-schema unknown-origin origin-option-mismatch
mutated-json-declarations helper-bindings-in-datatable-route helper-bindings-in-dataset-route
helper-bindings-in-reader-route oversized-bindings existing-output-not-replaced''',
    'field': '''datatable-bindings-as-field-bindings dataset-bindings-as-field-bindings
helper-bindings-as-field-bindings internal-helper-bindings-as-field-bindings reader-bindings-as-field-bindings
parameter-bindings-as-field-bindings datatable-digest-substitution internal-helper-digest-substitution
stale-field-digest site-index-substitution site-digest-substitution out-of-range-site-index duplicate-binding
stale-revision mutated-site-access mutated-type-argument mutated-site-procedure-hash
field-bindings-in-datatable-route field-bindings-in-dataset-route field-bindings-in-helper-route
field-bindings-in-internal-helper-route field-bindings-in-reader-route table-export-as-result-schema
unknown-origin oversized-bindings existing-output-not-replaced''',
}


def verify_refusals(api, label, rows):
    languages = ('csharp', 'vb') if label == 'field' else (None,)
    expected = {(language, name) for language in languages for name in REFUSALS[label].split()}
    actual = [(r.get('language'), r.get('case')) for r in rows]
    api.require(len(actual) == len(set(actual)) and set(actual) == expected
                and all(r.get('refused') is True for r in rows), 'named refusal contract changed')


def reconstruct(api, root, label):
    kind = api.GATES[label][0]
    native = api.load('acceptance_contract_' + label, api.ROOT / f'scripts/sqlserver-{kind}-evidence/smoke.py')
    helpers = native if label == 'parameter' else native.HELPERS
    sample = api.ROOT / ('samples/sqlserver-parameter-evidence' if label == 'parameter'
                        else 'samples/sqlserver-result-column-evidence')
    base = json.loads((sample / ('declarations-base.json' if label == 'parameter'
                                else 'result-declarations-base.json')).read_bytes())
    inputs, expected = [], []

    def add(language, path, fact, statuses, **fields):
        expected.append((dict(language=language, factId=fact['factId'], **fields), path, statuses))

    for language in MATRIX.LANGUAGES:
        facts = [json.loads(line) for line in (root / language / 'scan/facts.ndjson').read_text().splitlines()]
        calls = [f for f in facts if f['factType'] == 'DatabaseOperationCandidate' and f['evidenceTier'] == 'Tier1Semantic'
                 and (label in ('parameter', 'reader') or f['properties'].get('operationKind') == 'data-adapter-fill')]
        if label == 'parameter':
            ordered = keyed((member(f['sourceSymbol']), f) for f in sorted(calls, key=lambda f: f['evidence']['startLine']))
            api.require({k for k, _ in ordered} == set(MATRIX.PARAMETER), 'parameter source case contract changed')
            for number, (key, fact) in enumerate(ordered):
                name = key.split('#')[0]
                status = MATRIX.PARAMETER[key][0][0]
                inputs.append(helpers.projection(fact))
                add(language, f'{language}/{number:02}-{name}/report/parameter-comparison.json', fact, [status], case=name)
        elif label == 'reader':
            ordered = keyed((member(f['sourceSymbol']), f) for f in sorted(calls, key=lambda f: f['evidence']['startLine']))
            api.require({k for k, _ in ordered} == set(MATRIX.READER), 'reader source case contract changed')
            for number, (key, fact) in enumerate(ordered):
                inputs.append(native.reader_projection(fact))
                origins = [('json', None), ('ddl', MATRIX.READER_TEMPLATES.get(key, 'base.sql'))]
                if key == 'NameMatch':
                    origins += [('ddl', t) for t in MATRIX.READER_BODIES]
                for origin, template in origins:
                    path = f"{language}/{origin}/{number:02}-{key.replace('#', '-')}/{template or 'declarations'}/report/result-column-comparison.json"
                    add(language, path, fact, [api.U] if template in MATRIX.READER_BODIES else MATRIX.READER[key][0],
                        case=key, origin=origin, template=template)
        else:
            ordering = (lambda f: (f['evidence']['filePath'], f['evidence']['startLine'])) if label == 'field' else (lambda f: f['evidence']['startLine'])
            fills = keyed((member(f['sourceSymbol']), f) for f in sorted(calls, key=ordering))
            known = (set(MATRIX.BOUNDED_HELPERS) | set(MATRIX.HELPER_UNKNOWN)) if label == 'helper' else (
                set(MATRIX.FIELD_FILLS) | set(MATRIX.FIELD_UNKNOWN) | set(MATRIX.FIELD_HELPERS))
            api.require({k for k, _ in fills} == known, 'consumer source case contract changed')
            selected = {}
            sites = []
            for key, fact in fills:
                if label == 'helper':
                    selected[('helper', key)] = (fact, None, None)
                    state = json.loads(fact['properties']['sqlServerHelperResultState'])
                    inputs.extend(native.helper_projection(fact, i) for i in range(len(state['callers'])))
                    if key in MATRIX.BOUNDED_HELPERS:
                        sites.extend((member(site['caller']), (fact, i, site)) for i, site in enumerate(state['callers']))
                else:
                    state = json.loads(fact['properties']['sqlServerFieldReadState'])
                    if key not in MATRIX.FIELD_HELPERS:
                        index = 0 if key in MATRIX.FIELD_FILLS or key in MATRIX.FIELD_HAS_UNKNOWN_SITE else None
                        api.require(len(state['sites']) == (1 if index == 0 else 0), 'direct Field site contract changed')
                        if index == 0:
                            api.require(member(state['sites'][0]['caller']) == key, 'direct Field caller changed')
                        selected[('fill', key)] = (fact, index, None)
                    else:
                        api.require(tuple(member(s['caller']) for s in state['sites']) == MATRIX.FIELD_HELPERS[key], 'Field helper caller contract changed')
                        sites.extend((member(site['caller']), (fact, i, site)) for i, site in enumerate(state['sites']))
                    inputs.extend(native.field_projection(fact, i) for i in range(len(state['sites'])))
            if label == 'helper':
                sites.sort(key=lambda pair: pair[1][2]['line'])
            for key, (fact, index, site) in keyed(sites):
                selected[('caller' if label == 'helper' else 'site', key)] = (fact, index, site.get('path'))
            for number, (kind, key, origin, variant, statuses) in enumerate(MATRIX.plan(label)):
                api.require((kind, key) in selected, 'missing native case')
                fact, index, site_path = selected[(kind, key)]
                path_key = key.replace('#', '-') if label == 'helper' else key
                path = f'{language}/{number:03}-{kind}-{path_key}-{origin}-{variant or "base"}/report/{MATRIX.REPORTS[label]}'
                extra = {'callerIndex': index} if label == 'helper' else {'siteIndex': index, 'sitePath': site_path}
                add(language, path, fact, statuses, kind=kind, case=key, origin=origin, variant=variant, **extra)
    bounded = {'base': base}
    if label == 'reader':
        bounded['templates'] = {p.name: api.sha(p) for p in sorted((sample / 'ddl').glob('*.sql'))}
    elif label in ('helper', 'field'):
        bounded['templates'] = {d: {p.name: api.sha(p) for p in sorted((sample / d).glob('*.sql'))}
                                for d in ('ddl', 'ddl-table')}
    bounded[{'parameter': 'calls', 'reader': 'calls', 'helper': 'callers', 'field': 'sites'}[label]] = inputs
    return helpers.digest(bounded), expected


def verify(api, root, label, receipt):
    bounded_hash, expected = reconstruct(api, root, label)
    api.require(receipt['boundedInputSha256'] == bounded_hash, 'native bounded input digest mismatch')
    rows = receipt['results']
    api.require(len(rows) == len(expected), 'native case manifest mismatch')
    for row, (identity, relative, statuses) in zip(rows, expected):
        api.require(all(row.get(k) == value for k, value in identity.items()), 'native case identity mismatch')
        path = root / relative
        api.require(path.is_file() and api.sha(path) == row.get('reportSha256'), 'case-specific native report mismatch')
        report = json.loads(path.read_bytes())
        if label == 'reader' and identity['origin'] == 'ddl':
            report = report['comparison']
        api.require(report['coverage'] == 'partial' and report['verdict'] == 'no-aggregate-clean-verdict', 'native coverage widened')
        selected = [r for r in report['comparisons'] if r['factId'] == identity['factId']
                    and (label != 'helper' or identity['callerIndex'] is None or r['callerIndex'] == identity['callerIndex'])
                    and (label != 'field' or r['siteIndex'] == identity['siteIndex'])]
        observed = [r['status'] for r in selected]
        declared = [row['status']] if label == 'parameter' else row['statuses']
        api.require(observed == declared == statuses, 'case-specific native statuses mismatch')
    verify_refusals(api, label, receipt.get('refusals', []))
