#!/usr/bin/env python3
"""Read-only byte identity check; not receipt authentication or semantic validation."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import stat
import sys

HANDOFF = "compiled-paths.handoff.local.json"
MAX_JSON = 32 * 1024 * 1024
MAX_RECEIPT = 4 * 1024 * 1024
MAX_BINARY = 256 * 1024 * 1024


def require(condition):
    if not condition:
        raise ValueError("PROVENANCE_NOT_VERIFIED")


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result)
        result[key] = value
    return result


def regular_file(path, limit):
    info = path.stat()
    require(stat.S_ISREG(info.st_mode) and info.st_size <= limit)


def invalid_constant(_):
    raise ValueError("PROVENANCE_NOT_VERIFIED")


def read_bytes(path, limit):
    regular_file(path, limit)
    with path.open("rb") as stream:
        data = stream.read(limit + 1)
    require(len(data) <= limit)
    return data


def read_json(path, limit=MAX_JSON):
    data = read_bytes(path, limit)
    value = json.loads(data, object_pairs_hook=unique_object, parse_constant=invalid_constant)
    require(isinstance(value, dict))
    return value, data


def digest(path):
    regular_file(path, MAX_BINARY)
    with path.open("rb") as stream:
        result = hashlib.sha256()
        total = 0
        while chunk := stream.read(1024 * 1024):
            total += len(chunk)
            require(total <= MAX_BINARY)
            result.update(chunk)
        return result.hexdigest()


def hash_field(obj, key):
    value = obj.get(key)
    require(isinstance(value, str) and re.fullmatch(r"[0-9a-f]{64}", value))
    return value


def verify(receipt_path, cli_dll):
    # Use the reporting component beside the operator-selected invoked CLI;
    # never select a different assembly using untrusted receipt paths.
    reporting_dll = cli_dll.parent / "TraceMap.Reporting.dll"
    before = (digest(cli_dll), digest(reporting_dll))
    receipt, receipt_raw = read_json(receipt_path, MAX_RECEIPT)
    require(receipt.get("schemaVersion") == "webforms-handler-requery.v1")
    require(receipt.get("ruleId") == "workflow.webforms.retained-handler-requery.v1")
    require(receipt.get("reportVariant", "grouped-paths") == "grouped-paths")
    require(receipt.get("query", {}).get("toSurface") == "http-client")
    require(hash_field(receipt, "generatorSha256") == before[0])
    require(hash_field(receipt, "reportingGeneratorSha256") == before[1])
    handoff, raw = read_json(receipt_path.parent / HANDOFF)
    require(handoff.get("schemaVersion") == "webforms-compiled-grouped-handoff.v1")
    require(handoff.get("ruleId") == "workflow.webforms.compiled-grouped-handoff.v1")
    require(handoff.get("header", {}).get("query", {}).get("toSurface") == "http-client")
    require(hash_field(handoff, "generatorSha256") == before[1])
    require(hash_field(handoff, "inputIndexSha256") == hash_field(receipt, "combinedIndexSha256"))
    artifacts = receipt.get("artifacts")
    require(isinstance(artifacts, list) and all(isinstance(a, dict) for a in artifacts))
    matches = [a for a in artifacts if a.get("relativePath") == HANDOFF]
    require(len(matches) == 1)
    require(type(matches[0].get("bytes")) is int and matches[0]["bytes"] == len(raw))
    require(hash_field(matches[0], "sha256") == hashlib.sha256(raw).hexdigest())
    require(before == (digest(cli_dll), digest(reporting_dll)))
    require(read_bytes(receipt_path, MAX_RECEIPT) == receipt_raw)
    require(read_bytes(receipt_path.parent / HANDOFF, MAX_JSON) == raw)


def main(argv=None):
    class PrivateArgumentParser(argparse.ArgumentParser):
        def error(self, message):
            self.exit(2, "PROVENANCE_ARGUMENTS_INVALID\n")

    parser = PrivateArgumentParser(description=__doc__, allow_abbrev=False)
    parser.add_argument("--receipt", type=Path, required=True)
    parser.add_argument("--cli-dll", type=Path, required=True)
    args = parser.parse_args(argv)
    try:
        verify(args.receipt, args.cli_dll)
    except (OSError, ValueError, TypeError, AttributeError, RecursionError, MemoryError):
        # Never leak private paths, JSON contents, symbols, or hashes.
        print("PROVENANCE_NOT_VERIFIED", file=sys.stderr)
        return 1
    print("CLI_AND_REPORTING_BYTES_MATCH; HANDOFF_LINK_MATCH")
    print("Byte identity only: not authenticated build, bounded-input validation, or runtime proof.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
