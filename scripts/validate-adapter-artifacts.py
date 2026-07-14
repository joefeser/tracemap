#!/usr/bin/env python3
"""Validate a generated TraceMap adapter output without executing scanned code."""

from __future__ import annotations

import argparse
import json
import re
import sqlite3
import sys
from pathlib import Path, PurePosixPath
from typing import Any, Iterable


REQUIRED_ARTIFACTS = (
    "scan-manifest.json",
    "facts.ndjson",
    "index.sqlite",
    "report.md",
    "logs/analyzer.log",
)
EVIDENCE_TIERS = {
    "Tier1Semantic",
    "Tier2Structural",
    "Tier3SyntaxOrTextual",
    "Tier4Unknown",
}
MANIFEST_FIELDS = {
    "scanId": str,
    "repoName": str,
    "commitSha": str,
    "scannerVersion": str,
    "scannedAt": str,
    "analysisLevel": str,
    "buildStatus": str,
    "knownGaps": list,
}
FACT_FIELDS = {
    "factId": str,
    "scanId": str,
    "repo": str,
    "commitSha": str,
    "factType": str,
    "ruleId": str,
    "evidenceTier": str,
    "evidence": dict,
    "properties": dict,
}
EVIDENCE_FIELDS = {
    "filePath": str,
    "startLine": int,
    "endLine": int,
    "extractorId": str,
    "extractorVersion": str,
}
SQLITE_COLUMNS = {
    "scan_manifest": {
        "scan_id",
        "repo",
        "commit_sha",
        "scanner_version",
        "scanned_at",
        "analysis_level",
        "build_status",
        "manifest_json",
    },
    "facts": {
        "fact_id",
        "scan_id",
        "repo",
        "commit_sha",
        "project_path",
        "fact_type",
        "rule_id",
        "evidence_tier",
        "source_symbol",
        "target_symbol",
        "contract_element",
        "file_path",
        "start_line",
        "end_line",
        "snippet_hash",
        "extractor_id",
        "extractor_version",
        "properties_json",
    },
}
COMMIT_PATTERN = re.compile(r"^[0-9a-fA-F]{40}$")
RULE_PATTERN = re.compile(r"^[a-z0-9][a-z0-9._-]*\.v[0-9]+$")
WINDOWS_PATH_PATTERN = re.compile(r"(?i)(?:^|[^A-Za-z0-9])[A-Za-z]:\\(?:Users|home|opt|var|srv|app|mnt|private|tmp)\\")
UNIX_PATH_PATTERN = re.compile(r"(?i)(?:^|[\s='\"])/(?:Users|home|opt|var|srv|app|mnt|private|tmp)/")
CREDENTIAL_URL_PATTERN = re.compile(r"(?i)\b[a-z][a-z0-9+.-]*://[^\s/@:]+:[^\s/@]+@")
CONNECTION_STRING_PATTERN = re.compile(r"(?i)(?:^|[;\s])(?:password|pwd|user\s*id|uid)\s*=")
PRIVATE_KEY_PATTERN = re.compile(r"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----")


def repo_root() -> Path:
    return Path(__file__).resolve().parent.parent


def unsafe_category(value: str) -> str | None:
    if UNIX_PATH_PATTERN.search(value) or WINDOWS_PATH_PATTERN.search(value):
        return "local-absolute-path"
    if CREDENTIAL_URL_PATTERN.search(value):
        return "credential-url"
    if CONNECTION_STRING_PATTERN.search(value):
        return "connection-string"
    if PRIVATE_KEY_PATTERN.search(value):
        return "private-key"
    return None


def scalar_strings(value: Any) -> Iterable[str]:
    if isinstance(value, str):
        yield value
    elif isinstance(value, dict):
        for key, item in value.items():
            yield str(key)
            yield from scalar_strings(item)
    elif isinstance(value, list):
        for item in value:
            yield from scalar_strings(item)


def rule_ids() -> set[str]:
    catalog = (repo_root() / "rules/rule-catalog.yml").read_text(encoding="utf-8")
    return set(re.findall(r"(?m)^\s*- id:\s*([^\s#]+)\s*$", catalog))


def validate_redaction_corpus(errors: list[str]) -> None:
    path = repo_root() / "contracts/artifacts/redaction-corpus.v1.json"
    corpus = json.loads(path.read_text(encoding="utf-8"))
    for item in corpus["unsafe"]:
        value = item.get("value") or "".join(item.get("segments", []))
        actual = unsafe_category(value)
        if actual != item["category"]:
            errors.append(f"redaction corpus expected {item['category']}, got {actual or 'safe'}")
    for value in corpus["safe"]:
        actual = unsafe_category(value)
        if actual is not None:
            errors.append(f"redaction corpus safe value classified as {actual}")


def require_fields(value: dict[str, Any], expected: dict[str, type], label: str, errors: list[str]) -> None:
    for field, expected_type in expected.items():
        actual = value.get(field)
        if not isinstance(actual, expected_type) or (expected_type is str and not actual.strip()):
            errors.append(f"{label}.{field} must be a non-empty {expected_type.__name__}")


def safe_relative_path(value: str) -> bool:
    normalized = value.replace("\\", "/")
    path = PurePosixPath(normalized)
    is_windows_drive_rooted = re.match(r"^[A-Za-z]:/", normalized) is not None
    return bool(normalized) and not is_windows_drive_rooted and not path.is_absolute() and ".." not in path.parts


def read_facts(path: Path, errors: list[str]) -> list[dict[str, Any]]:
    facts: list[dict[str, Any]] = []
    for line_number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        if not line.strip():
            errors.append(f"facts.ndjson:{line_number} is blank")
            continue
        try:
            value = json.loads(line)
        except json.JSONDecodeError as exc:
            errors.append(f"facts.ndjson:{line_number} is invalid JSON: {exc.msg}")
            continue
        if not isinstance(value, dict):
            errors.append(f"facts.ndjson:{line_number} must be an object")
            continue
        facts.append(value)
    return facts


def sqlite_columns(connection: sqlite3.Connection, table: str) -> set[str]:
    return {str(row[0]) for row in connection.execute("select name from pragma_table_info(?)", (table,))}


def validate_sqlite(output: Path, manifest: dict[str, Any], facts: list[dict[str, Any]], errors: list[str]) -> None:
    try:
        index_uri = (output / "index.sqlite").resolve().as_uri()
        connection = sqlite3.connect(f"{index_uri}?mode=ro", uri=True)
    except sqlite3.Error as exc:
        errors.append(f"index.sqlite cannot be opened read-only: {exc}")
        return
    try:
        for table, expected in SQLITE_COLUMNS.items():
            actual = sqlite_columns(connection, table)
            missing = sorted(expected - actual)
            if missing:
                errors.append(f"index.sqlite {table} missing columns: {', '.join(missing)}")
        if errors:
            return
        manifest_rows = connection.execute(
            "select scan_id, commit_sha from scan_manifest"
        ).fetchall()
        if manifest_rows != [(manifest["scanId"], manifest["commitSha"])]:
            errors.append("index.sqlite scan_manifest does not match scan-manifest.json")
        count = int(connection.execute("select count(*) from facts").fetchone()[0])
        if count != len(facts):
            errors.append(f"index.sqlite contains {count} facts; facts.ndjson contains {len(facts)}")
        rows: dict[str, tuple[Any, ...]] = {}
        for row in connection.execute(
            "select fact_id, scan_id, repo, commit_sha, project_path, fact_type, rule_id, evidence_tier, "
            "source_symbol, target_symbol, contract_element, file_path, start_line, end_line, snippet_hash, "
            "extractor_id, extractor_version, properties_json from facts"
        ):
            try:
                properties = json.loads(row[-1])
            except (json.JSONDecodeError, TypeError):
                errors.append(f"index.sqlite properties_json is invalid for fact {row[0]}")
                properties = None
            rows[str(row[0])] = (*row[1:-1], properties)
        for fact in facts:
            evidence = fact["evidence"]
            expected = (
                fact["scanId"],
                fact["repo"],
                fact["commitSha"],
                fact.get("projectPath"),
                fact["factType"],
                fact["ruleId"],
                fact["evidenceTier"],
                fact.get("sourceSymbol"),
                fact.get("targetSymbol"),
                fact.get("contractElement"),
                evidence["filePath"],
                evidence["startLine"],
                evidence["endLine"],
                evidence.get("snippetHash"),
                evidence["extractorId"],
                evidence["extractorVersion"],
                fact["properties"],
            )
            if rows.get(fact["factId"]) != expected:
                errors.append(f"index.sqlite fact mismatch for fact {fact['factId']}")
    except sqlite3.Error as exc:
        errors.append(f"index.sqlite validation failed: {exc}")
    finally:
        connection.close()


def validate_output(output: Path) -> list[str]:
    errors: list[str] = []
    for relative in REQUIRED_ARTIFACTS:
        if not (output / relative).is_file():
            errors.append(f"missing required artifact: {relative}")
    if errors:
        return errors

    try:
        manifest = json.loads((output / "scan-manifest.json").read_text(encoding="utf-8"))
    except (json.JSONDecodeError, OSError) as exc:
        return [f"scan-manifest.json is unreadable: {exc}"]
    if not isinstance(manifest, dict):
        return ["scan-manifest.json must be an object"]
    require_fields(manifest, MANIFEST_FIELDS, "manifest", errors)
    if not COMMIT_PATTERN.fullmatch(str(manifest.get("commitSha", ""))):
        errors.append("manifest.commitSha must be an exact 40-hex commit SHA")

    facts = read_facts(output / "facts.ndjson", errors)
    registered_rules = rule_ids()
    seen_ids: set[str] = set()
    for index, fact in enumerate(facts, 1):
        label = f"fact[{index}]"
        require_fields(fact, FACT_FIELDS, label, errors)
        evidence = fact.get("evidence")
        if not isinstance(evidence, dict):
            continue
        require_fields(evidence, EVIDENCE_FIELDS, f"{label}.evidence", errors)
        fact_id = fact.get("factId")
        if isinstance(fact_id, str):
            if fact_id in seen_ids:
                errors.append(f"duplicate factId: {fact_id}")
            seen_ids.add(fact_id)
        if fact.get("scanId") != manifest.get("scanId"):
            errors.append(f"{label}.scanId does not match manifest")
        if fact.get("commitSha") != manifest.get("commitSha"):
            errors.append(f"{label}.commitSha does not match manifest")
        rule_id = fact.get("ruleId")
        if not isinstance(rule_id, str) or not RULE_PATTERN.fullmatch(rule_id):
            errors.append(f"{label}.ruleId is not versioned")
        elif rule_id not in registered_rules:
            errors.append(f"{label}.ruleId is absent from rules/rule-catalog.yml: {rule_id}")
        if fact.get("evidenceTier") not in EVIDENCE_TIERS:
            errors.append(f"{label}.evidenceTier is unknown")
        start = evidence.get("startLine")
        end = evidence.get("endLine")
        if isinstance(start, int) and isinstance(end, int) and (start < 1 or end < start):
            errors.append(f"{label}.evidence line span is invalid")
        file_path = evidence.get("filePath")
        if isinstance(file_path, str) and not safe_relative_path(file_path):
            errors.append(f"{label}.evidence.filePath must be repo-relative")
        for value in scalar_strings(fact.get("properties", {})):
            category = unsafe_category(value)
            if category:
                errors.append(f"{label}.properties contains {category}")

    validate_redaction_corpus(errors)
    if not errors:
        validate_sqlite(output, manifest, facts, errors)
    return errors


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("outputs", nargs="+", type=Path, help="generated adapter scan directories")
    args = parser.parse_args()
    failed = False
    for output in args.outputs:
        errors = validate_output(output.resolve())
        if errors:
            failed = True
            for error in errors:
                print(f"{output}: {error}", file=sys.stderr)
        else:
            print(f"Validated adapter artifacts: {output}")
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
