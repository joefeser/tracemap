#!/usr/bin/env python3
"""Opt-in public fetch; no upstream builds, package restore or application execution."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile

UPSTREAM = 'https://github.com/dnnsoftware/Dnn.Platform.git'
PIN = '33b8fbe440ab665b8423599789db1f34d2a500fc'
RELEASE = 'v9.13.8'
BASE = 'DNN Platform/'
FILES = ['LICENSE', 'NOTICE.md'] + [BASE + name for name in (
    'Library/Security/Roles/DNNRoleProvider.cs',
    'Library/Security/Roles/RoleGroupInfo.cs',
    'Library/Entities/BaseEntityInfo.cs',
    'Library/Data/DataProvider.cs',
    'Library/Data/SqlDataProvider.cs',
    'Library/Data/SqlDatabaseConnectionProvider.cs',
    'Library/Data/PetaPoco/PetaPocoHelper.cs',
    'Library/Data/DataUtil.cs',
    'Library/Common/Utilities/CBO.cs',
    'Website/Providers/DataProviders/SqlDataProvider/DotNetNuke.Schema.SqlDataProvider')]


def fetch(destination):
    if destination.exists():
        raise ValueError('destination must be new; existing inputs are preserved')
    with tempfile.TemporaryDirectory(prefix='tracemap-dnn-upstream-') as temp:
        def git(*args):
            return subprocess.check_output(['git', '-C', temp, *args], timeout=180)
        git('init', '-q')
        git('remote', 'add', 'origin', UPSTREAM)
        git('fetch', '--depth=1', 'origin', PIN)
        if git('rev-parse', 'FETCH_HEAD').decode().strip() != PIN:
            raise ValueError('upstream revision mismatch')
        # Read exact git objects, never evaluate upstream MSBuild/imports.
        contents = {name: git('show', PIN + ':' + name) for name in FILES}
        if b'The MIT License' not in contents['LICENSE']:
            raise ValueError('license changed; review before redistributing')
        destination.mkdir()
        projection = {}
        for name, data in contents.items():
            path = destination / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
            projection[name] = hashlib.sha256(data).hexdigest()
        # SQL extension is intentionally unchanged; native scanner treats it as unknown.
        # No implicit preprocessing/substitution of DNN install placeholders.
        bounded = dict(upstream=UPSTREAM, revision=PIN, release=RELEASE, files=projection)
        receipt = dict(**bounded, generatorSha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
            boundedInputSha256=hashlib.sha256(json.dumps(bounded, sort_keys=True, separators=(',', ':')).encode()).hexdigest())
        (destination / 'public-slice.json').write_text(json.dumps(receipt, indent=2) + '\n')
        # Keep the upstream revision available to the native scanner without changing it.
        subprocess.run(['git', '-C', temp, 'checkout', '--detach', PIN], check=True, capture_output=True)
        # A sparse worktree with the original commit prevents a fabricated slice revision.
        git('sparse-checkout', 'init', '--no-cone')
        git('sparse-checkout', 'set', '--no-cone', *('/' + name for name in FILES))
        import shutil
        shutil.copytree(Path(temp) / '.git', destination / '.git')
        # Remove worktree-specific config left by the temporary checkout, if any.
        subprocess.run(['git', '-C', str(destination), 'status', '--porcelain'], check=True, capture_output=True)
    return receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--fetch', action='store_true')
    parser.add_argument('--destination', type=Path)
    args = parser.parse_args()
    if not args.fetch:
        print(f'NOT FETCHED: {UPSTREAM} at {PIN} ({RELEASE}); {len(FILES)} public files')
        return
    if not args.destination:
        parser.error('--destination is required with --fetch')
    fetch(args.destination)
    print('Pinned public slice fetched; no build or DNN execution performed.')


if __name__ == '__main__':
    main()
