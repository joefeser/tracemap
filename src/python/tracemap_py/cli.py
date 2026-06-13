from __future__ import annotations

import argparse
import sys

from . import __version__
from .engine import make_options, scan


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(prog="tracemap-py")
    parser.add_argument("--version", action="store_true", help="Print version and exit.")
    sub = parser.add_subparsers(dest="command")
    scan_parser = sub.add_parser("scan", help="Scan a Python repository.")
    scan_parser.add_argument("--repo", required=True)
    scan_parser.add_argument("--out", required=True)
    scan_parser.add_argument("--project", action="append", default=[])
    scan_parser.add_argument("--include", action="append", default=[])
    scan_parser.add_argument("--exclude", action="append", default=[])
    scan_parser.add_argument("--max-file-byte-size", default="1mb")
    scan_parser.add_argument("--no-metadata", action="store_true")
    args = parser.parse_args(argv)
    if args.version:
        print(f"tracemap-py {__version__}")
        return 0
    if args.command == "scan":
        try:
            manifest, facts = scan(make_options(args.repo, args.out, args.project, args.include, args.exclude, args.max_file_byte_size, args.no_metadata))
        except Exception as exc:
            print(f"tracemap-py scan failed: {exc}", file=sys.stderr)
            return 1
        print(f"TraceMap Python scan complete: {len(facts)} facts, {manifest.analysis_level}, {manifest.build_status}")
        return 0
    parser.print_help()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
