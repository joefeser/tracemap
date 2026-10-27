"""Reconstruct the four finite native smoke contracts without executing a CLI."""
import json
import re

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

    for language in ('csharp', 'vb'):
        facts = [json.loads(line) for line in (root / language / 'scan/facts.ndjson').read_text().splitlines()]
        calls = [f for f in facts if f['factType'] == 'DatabaseOperationCandidate' and f['evidenceTier'] == 'Tier1Semantic'
                 and (label in ('parameter', 'reader') or f['properties'].get('operationKind') == 'data-adapter-fill')]
        if label == 'parameter':
            repeated = 0
            for number, fact in enumerate(sorted(calls, key=lambda f: f['evidence']['startLine'])):
                name = re.search(r'\.([A-Za-z]+)\(', fact['sourceSymbol'])[1]
                status = native.EXPECTED.get(name)
                if name == 'TwoCommands':
                    status = api.X if repeated == 0 else api.M
                    repeated += 1
                api.require(status is not None, 'unknown parameter case')
                inputs.append(helpers.projection(fact))
                add(language, f'{language}/{number:02}-{name}/report/parameter-comparison.json', fact, [status], case=name)
        elif label == 'reader':
            for number, (fact, key) in enumerate(native.case_keys(calls)):
                api.require(key in native.SOURCE_CASES, 'unknown reader case')
                inputs.append(native.reader_projection(fact))
                origins = [('json', None), ('ddl', native.DDL_TEMPLATES.get(key, 'base.sql'))]
                if key == 'NameMatch':
                    origins += [('ddl', t) for t in native.BODY_MATRIX]
                for origin, template in origins:
                    path = f"{language}/{origin}/{number:02}-{key.replace('#', '-')}/{template or 'declarations'}/report/result-column-comparison.json"
                    add(language, path, fact, [api.U] if template in native.BODY_MATRIX else native.SOURCE_CASES[key],
                        case=key, origin=origin, template=template)
        else:
            fills = native.fill_keys(calls)
            selected = {}
            for key, fact in fills:
                if label == 'helper':
                    selected[('helper', key)] = (fact, None, None)
                    state = json.loads(fact['properties']['sqlServerHelperResultState'])
                    inputs.extend(native.helper_projection(fact, i) for i in range(len(state['callers'])))
                else:
                    index, _ = native.check_fill_state(key, fact)
                    if key not in native.HELPER_FILLS:
                        selected[('fill', key)] = (fact, index, None)
                    state = json.loads(fact['properties'][native.STATE])
                    inputs.extend(native.field_projection(fact, i) for i in range(len(state['sites'])))
            sites = native.caller_keys(fills) if label == 'helper' else native.site_keys(fills)
            for key, (fact, index, site) in sites:
                selected[('caller' if label == 'helper' else 'site', key)] = (fact, index, site.get('path'))
            for number, (kind, key, origin, variant, statuses) in enumerate(native.case_plan()):
                api.require((kind, key) in selected, 'missing native case')
                fact, index, site_path = selected[(kind, key)]
                path_key = key.replace('#', '-') if label == 'helper' else key
                path = f'{language}/{number:03}-{kind}-{path_key}-{origin}-{variant or "base"}/report/{native.REPORT}'
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
