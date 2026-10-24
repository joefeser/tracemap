#!/usr/bin/env python3
"""Read public registry manifest/config metadata only; never pull image layers."""
import argparse
import datetime
import hashlib
import json
from pathlib import Path
import urllib.request

REGISTRY = 'https://mcr.microsoft.com/v2/mssql/server'
TAG = '2022-CU20-ubuntu-22.04'


def digest(data):
    return hashlib.sha256(data).hexdigest()


def resolve():
    request = urllib.request.Request(REGISTRY + '/manifests/' + TAG,
        headers={'Accept': 'application/vnd.docker.distribution.manifest.v2+json'})
    with urllib.request.urlopen(request, timeout=30) as response:
        raw = response.read(1024 * 1024 + 1)
        declared = response.headers.get('Docker-Content-Digest')
    if len(raw) > 1024 * 1024 or declared != 'sha256:' + digest(raw):
        raise ValueError('missing/mismatched registry manifest digest or over limit')
    manifest = json.loads(raw)
    if manifest.get('mediaType') != 'application/vnd.docker.distribution.manifest.v2+json':
        raise ValueError('single-platform v2 manifest required; review index explicitly')
    config_digest = manifest['config']['digest']
    import re
    if not re.fullmatch('sha256:[0-9a-f]{64}', config_digest):
        raise ValueError('invalid configuration digest')
    with urllib.request.urlopen(REGISTRY + '/blobs/' + config_digest, timeout=30) as response:
        config_raw = response.read(1024 * 1024 + 1)
    if len(config_raw) > 1024 * 1024 or config_digest != 'sha256:' + digest(config_raw):
        raise ValueError('configuration digest mismatch or over limit')
    config = json.loads(config_raw)
    if config.get('os') != 'linux' or config.get('architecture') != 'amd64':
        raise ValueError('Linux amd64 configuration required')
    projection = dict(registry=REGISTRY, tag=TAG, manifestDigest=declared,
        configDigest=config_digest, os=config['os'], architecture=config['architecture'])
    return dict(**projection, image='mcr.microsoft.com/mssql/server@' + declared,
        resolvedAt=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        generatorSha256=digest(Path(__file__).read_bytes()),
        boundedInputSha256=digest(json.dumps(projection, sort_keys=True, separators=(',', ':')).encode()),
        status='registry metadata verified; image NOT PULLED; live SQL NOT RUN', testedVersion=None)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--resolve', action='store_true')
    args = parser.parse_args()
    print(json.dumps(resolve(), indent=2) if args.resolve else 'NOT RESOLVED: use --resolve for public metadata network access')
