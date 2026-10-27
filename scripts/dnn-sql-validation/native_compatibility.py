#!/usr/bin/env python3
"""Bounded native producer/acceptance compatibility. No SQL or upstream fetch.

Uses the existing CI-built CLI and emits only the native provenance-bound receipts.
This does not run a test suite, publish an aggregate clean verdict, or execute fixtures.
"""
import argparse
from pathlib import Path
import public_acceptance as acceptance


def run(output):
    acceptance.require(not output.is_relative_to(acceptance.ROOT.resolve()), 'external output required')
    revision, sources = acceptance.source_snapshot()
    cli = acceptance.ROOT / 'src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll'
    acceptance.require(cli.is_file(), 'build the Debug CLI before native compatibility')
    binaries = {p.name: acceptance.sha(p) for p in cli.parent.glob('*.dll')}
    output.mkdir(parents=True, exist_ok=False)
    helpers = acceptance.load('compatibility_helpers', acceptance.ROOT / 'scripts/sqlserver-parameter-evidence/smoke.py')
    acceptance.native_gates(output, revision, helpers)
    acceptance.require(acceptance.source_snapshot() == (revision, sources), 'source drift during compatibility')
    acceptance.require(binaries == {p.name: acceptance.sha(p) for p in cli.parent.glob('*.dll')}, 'binary drift during compatibility')
    print('Four native serialized producer contracts verified; partial evidence; SQL NOT RUN')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    run(parser.parse_args().output.resolve())
