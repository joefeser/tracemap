"""Independent SQL callsite oracle; expected calls never come from scanner output."""
import collections
import json
import re
import sys

if sys.flags.optimize:
    raise RuntimeError('SQL validation requires Python assertions; optimization is unsupported.')


def assert_database_path_evidence(report):
    if not isinstance(report, dict) or not isinstance(report.get('paths'), list):
        raise ValueError('Invalid paths report')
    evidence = []
    for path in report['paths']:
        if not isinstance(path, dict) or not isinstance(path.get('nodes'), list):
            raise ValueError('Invalid retained path')
        for node in path['nodes']:
            if not isinstance(node, dict):
                raise ValueError('Invalid retained node')
            if node.get('databaseCallEvidence') is not None:
                item = node['databaseCallEvidence']
                if not isinstance(item, dict) or any(not isinstance(item.get(key), str) or not item[key]
                        for key in ['methodName', 'operationKind', 'coverageLabel', 'limitations']):
                    raise ValueError('Invalid database evidence')
                evidence.append(item)
    if not evidence:
        raise ValueError('No retained database evidence')


def load_expectations(sample, language, inventory, controls_file):
    rows = re.findall(r'^\| (Case\d+) \| ([^|]+) \| ``([^`]+)`` \|', inventory.read_text(encoding='utf-8'), re.M)
    if not rows or len(rows) != len({r[0] for r in rows}):
        raise ValueError('Missing/duplicate public inventory case')
    extension = '.cs' if language == 'csharp' else '.vb'
    text = (sample / ('Matrix' + extension)).read_text(encoding='utf-8')
    declarations = re.findall(r'(?:public void|Public Sub) (Case\d+)\(', text)
    if len(declarations) != len(set(declarations)) or set(declarations) != {r[0] for r in rows}:
        raise ValueError('Public inventory and source case roster differ')
    # Cross-check declared receiver/member identities independently of scanner output.
    for name, assembly, signature in rows:
        declaration = re.search(r'(?:public void|Public Sub) ' + name + r'\(receiver(?: As |\b)', text) if language == 'vb' else None
        if language == 'vb':
            body = re.search(r'Public Sub ' + name + r'\(receiver As Global\.([\w.]+)[^\n]*\)\s+Call receiver\.(\w+)\(', text)
        else:
            body = re.search(r'public void ' + name + r'\(global::([\w.]+) receiver[^\n]*?\{ receiver\.(\w+)\(', text)
        if not body or not signature.startswith(body[1] + '.' + body[2] + '('):
            raise ValueError('Public overload and fixture declaration identity differ: ' + name)
    controls = json.loads(controls_file.read_text(encoding='utf-8'))
    required = {'ControlLiteral','ControlConstant','ControlDynamic','ControlStoredProcedure','ControlText','ControlParameters','ControlTransaction','ControlReuse','ControlReassignment','ControlAlias','ControlField','ControlFactory','ControlInterface','ControlGeneric','ControlShared','ControlEntry','ControlWrapper','ControlUnconnected','ControlOpenClose','ControlFake','ControlContent','ControlBranch','ControlUnknownCall','ControlNestedAssignment','ControlAliasEscape','ControlConnectionAssignment','ControlConnectionConstructor'}
    if set(controls) != required:
        raise ValueError('Required independent control oracle missing/extra')
    controls_text = (sample / ('Controls' + extension)).read_text(encoding='utf-8')
    names = re.findall(r'(?:public void|Public Sub) (Control\w+)(?:\(|<)', controls_text)
    if len(names) != len(set(names)) or set(names) != set(controls):
        raise ValueError('Duplicate/missing/unlisted control source')
    for values in controls.values():
        if set(values) != {'semantic','projectless'} or any(type(v) is not int or v < 0 for v in values.values()):
            raise ValueError('Invalid control oracle')
    return {r[0]:r[2] for r in rows}, controls


def caller(symbol):
    m = re.search(r'\.(Case\d+|Control\w+)(?:\(|<|$)', symbol or '')
    return m[1] if m else None


def assert_calls(facts, mode, cases, controls):
    tier = 'Tier1Semantic' if mode == 'semantic' else 'Tier3SyntaxOrTextual'
    observed = collections.Counter()
    tiers = {f['evidenceTier'] for f in facts if f['factType'] == 'DatabaseOperationCandidate'}
    assert tiers <= {tier}, (mode, tiers)
    for f in facts:
        if f['factType'] != 'DatabaseOperationCandidate' or f['evidenceTier'] != tier:
            continue
        name = caller(f.get('sourceSymbol'))
        if name not in cases and name not in controls:
            raise AssertionError('Unmapped SQL operation callsite: ' + str(name))
        observed[name] += 1
        if name in cases:
            method = re.search(r'\.(\w+)\(',cases[name])[1]
            assert f['properties']['methodName'] == method, (name, method, f['properties'])
    expected = collections.Counter({name:1 for name in cases})
    expected.update({name:values[mode] for name,values in controls.items()})
    assert observed == expected, (mode, observed, expected)


def assert_compiled_cases(facts, cases):
    observed = collections.Counter()
    for f in facts:
        if f['factType'] == 'ManagedIlCallObserved':
            m = re.search(r'\|method:\d+:(Case\d+)\|', f.get('targetSymbol') or '')
            if m:
                expected_method = re.search(r'\.(\w+)\(', cases[m[1]])[1]
                identity = f['properties']['targetIdentity']
                if '|member:14:GetObjectValue|' in identity:
                    assert 'System.Runtime.CompilerServices' in identity and 'RuntimeHelpers' in identity
                    continue
                observed[m[1]] += 1
                assert re.search(r'\|member:\d+:' + re.search(r'\.(\w+)\(',cases.get(m[1],''))[1] + r'\|', f['properties']['targetIdentity'])
    assert observed == collections.Counter({name:1 for name in cases}), (observed, cases)
