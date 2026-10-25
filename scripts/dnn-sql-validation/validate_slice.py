#!/usr/bin/env python3
"""Offline bounded DNN expectation/observation check; no builds or network.
This pins the documented partial scan, not DNN execution or schema compatibility.
"""
# Short source expectation markers below are from DNN Platform v9.13.8,
# commit 33b8fbe440ab665b8423599789db1f34d2a500fc. No implementation file is vendored.
# The MIT License (MIT)
#
# Copyright (c) .NET Foundation and Contributors
# All Rights Reserved
#
# Permission is hereby granted, free of charge, to any person obtaining a copy
# of this software and associated documentation files (the "Software"), to deal
# in the Software without restriction, including without limitation the rights
# to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
# copies of the Software, and to permit persons to whom the Software is
# furnished to do so, subject to the following conditions:
#
# The above copyright notice and this permission notice shall be included in all
# copies or substantial portions of the Software.
#
# THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
# IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
# FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
# AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
# LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
# OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
# SOFTWARE.
#
import argparse
import hashlib
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from public_slice import UPSTREAM, PIN, RELEASE, FILES


def sha(data):
    return hashlib.sha256(data).hexdigest()


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(',', ':')).encode()


def verify_inventory(slice_root):
    # Only the root Git metadata directory and setup receipt are non-source inputs.
    # Do not follow links or silently ignore additional source/metadata files.
    actual = set()
    pending = [slice_root]
    while pending:
        for path in pending.pop().iterdir():
            relative = path.relative_to(slice_root).as_posix()
            if path.is_symlink():
                raise ValueError('public slice inventory contains a symbolic link')
            if relative == '.git' and path.is_dir():
                continue
            if path.is_dir():
                pending.append(path)
            elif path.is_file():
                actual.add(relative)
            else:
                raise ValueError('public slice inventory contains a non-regular file')
    if actual != set(FILES) | {'public-slice.json'}:
        raise ValueError('public slice inventory has missing or unexpected files')
    # This pinned slice has nine .cs files plus the setup JSON in native inventory.
    # LICENSE/NOTICE and the unchanged .SqlDataProvider extension are not included.
    return sum(Path(name).suffix == '.cs' for name in actual) + 1


def verify_scan_inventory(facts, expected_count):
    scanned = [fact for fact in facts if fact.get('factType') == 'RepoScanned']
    if (len(scanned) != 1
            or scanned[0].get('commitSha') != PIN
            or scanned[0].get('properties', {}).get('fileCount') != str(expected_count)):
        raise ValueError('native scan inventory count or identity mismatch')


def verify(slice_root, scan_root):
    setup = json.loads((slice_root / 'public-slice.json').read_text())
    if (setup.get('upstream'), setup.get('revision'), setup.get('release')) != (UPSTREAM, PIN, RELEASE):
        raise ValueError('public source identity mismatch')
    projection = {name: setup[name] for name in ('upstream', 'revision', 'release', 'files')}
    if setup.get('generatorSha256') != sha((Path(__file__).parent / 'public_slice.py').read_bytes()):
        raise ValueError('public slice generator mismatch')
    if setup.get('boundedInputSha256') != sha(canonical(projection)) or set(setup['files']) != set(FILES):
        raise ValueError('public slice bounded input mismatch')
    expected_count = verify_inventory(slice_root)
    for name in FILES:
        if sha((slice_root / name).read_bytes()) != setup['files'][name]:
            raise ValueError('public slice bytes changed')
    # Independent hand-inspected expectations, not learned from TraceMap facts.
    prefix = 'DNN Platform/Library/'
    markers = {
        prefix + 'Security/Roles/DNNRoleProvider.cs': [
            'return new ArrayList(this.GetRoleGroupsInternal(portalId).ToList());',
            'CBO.FillCollection<RoleGroupInfo>(this.dataProvider.GetRoleGroups(portalId))'],
        prefix + 'Data/DataProvider.cs': ['this.ExecuteReader("GetRoleGroups", portalId)'],
        prefix + 'Data/SqlDataProvider.cs': ['CommandType.StoredProcedure, 0, this.DatabaseOwner + this.ObjectQualifier + procedureName'],
        prefix + 'Data/SqlDatabaseConnectionProvider.cs': ['return PetaPocoHelper.ExecuteReader(connectionString, commandType, commandTimeout, procedureName, commandParameters);'],
        prefix + 'Data/PetaPoco/PetaPocoHelper.cs': ['sql = DataUtil.GenerateExecuteStoredProcedureSql(sql, args);', 'return database.ExecuteReader(sql, args);'],
        prefix + 'Data/DataUtil.cs': ['new StringBuilder(";Exec ")'],
        prefix + 'Security/Roles/RoleGroupInfo.cs': ['dr["RoleGroupId"]', 'dr["RoleGroupName"]', 'this.FillInternal(dr);'],
        prefix + 'Entities/BaseEntityInfo.cs': ['dr["CreatedByUserID"]', 'dr["LastModifiedOnDate"]'],
        prefix + 'Common/Utilities/CBO.cs': ['objHydratable.Fill(dr);'],
        'DNN Platform/Website/Providers/DataProviders/SqlDataProvider/DotNetNuke.Schema.SqlDataProvider': [
            'CREATE PROCEDURE {databaseOwner}[{objectQualifier}GetRoleGroups]',
            'FROM {databaseOwner}{objectQualifier}RoleGroups'],
    }
    for name, expected in markers.items():
        text = (slice_root / name).read_text(encoding='utf-8-sig')
        if not all(marker in text for marker in expected):
            raise ValueError('independent source expectation changed')
    manifest = json.loads((scan_root / 'scan-manifest.json').read_text())
    if manifest.get('commitSha') != PIN or manifest.get('remoteUrl') != UPSTREAM:
        raise ValueError('native scan repository/revision mismatch')
    if manifest.get('buildStatus') != 'NotRun' or manifest.get('analysisLevel') != 'Level3SyntaxAnalysis':
        raise ValueError('expected partial projectless coverage changed; inspect before updating baseline')
    facts = [json.loads(line) for line in (scan_root / 'facts.ndjson').read_text().splitlines()]
    verify_scan_inventory(facts, expected_count)
    calls = [f for f in facts if f.get('factType') == 'DatabaseOperationCandidate']
    expected_calls = {
        (prefix + 'Data/PetaPoco/PetaPocoHelper.cs', 80),
        (prefix + 'Data/PetaPoco/PetaPocoHelper.cs', 125),
        (prefix + 'Data/SqlDatabaseConnectionProvider.cs', 28),
    }
    if len(calls) != 3 or {(f['evidence']['filePath'], f['evidence']['startLine']) for f in calls} != expected_calls:
        raise ValueError('native candidate denominator changed; inspect the selected route')
    for fact in calls:
        evidence = fact['evidence']
        if (fact.get('ruleId') != 'database.operation.call-pattern.v1'
                or fact.get('evidenceTier') != 'Tier3SyntaxOrTextual'
                or fact.get('commitSha') != PIN
                or fact.get('properties', {}).get('methodName') != 'ExecuteNonQuery'
                or not evidence.get('extractorId') or not evidence.get('extractorVersion')
                or evidence['endLine'] < evidence['startLine']):
            raise ValueError('native candidate provenance changed or incomplete')
    gaps = {f.get('ruleId') for f in facts if f.get('factType') == 'AnalysisGap'}
    if not {'repo.manifest.v1', 'csharp.semantic.workspace.v1', 'database.operation.call-pattern.v1'} <= gaps:
        raise ValueError('required partial-coverage gaps missing')
    if any(not (scan_root / name).is_file() for name in ('index.sqlite', 'report.md', 'logs/analyzer.log')):
        raise ValueError('required native report/index missing')
    return 'PASS bounded static DNN expectations: 1 inspected operation; 3 incidental Tier3 candidates; selected reader join UNAVAILABLE; runtime NOT RUN'


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--slice', type=Path, required=True)
    parser.add_argument('--scan', type=Path, required=True)
    args = parser.parse_args()
    try:
        print(verify(args.slice, args.scan))
    except (ValueError, KeyError, OSError) as error:
        print(f'Static DNN check failed: {error}', file=sys.stderr)
        sys.exit(1)
