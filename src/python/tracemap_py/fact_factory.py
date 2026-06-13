from __future__ import annotations

from collections.abc import Mapping

from .hashes import sha256_hex
from .models import CodeFact, EvidenceSpan, ScanManifest


def evidence(file_path: str, start_line: int, end_line: int, extractor_id: str, extractor_version: str, snippet_hash: str | None = None) -> EvidenceSpan:
    return EvidenceSpan(file_path, max(1, start_line), max(max(1, start_line), end_line), snippet_hash, extractor_id, extractor_version)


def create_fact(
    manifest: ScanManifest,
    fact_type: str,
    rule_id: str,
    evidence_tier: str,
    span: EvidenceSpan,
    *,
    project_path: str | None = None,
    source_symbol: str | None = None,
    target_symbol: str | None = None,
    contract_element: str | None = None,
    properties: Mapping[str, object | None] | None = None,
) -> CodeFact:
    stable = {
        key: str(value)
        for key, value in sorted((properties or {}).items())
        if value is not None
    }
    fact_id = create_fact_id(
        manifest.scan_id,
        fact_type,
        rule_id,
        span.file_path,
        span.start_line,
        span.end_line,
        project_path,
        source_symbol,
        target_symbol,
        contract_element,
        stable,
    )
    return CodeFact(
        fact_id,
        manifest.scan_id,
        manifest.repo_name,
        manifest.commit_sha,
        project_path,
        fact_type,
        rule_id,
        evidence_tier,
        source_symbol,
        target_symbol,
        contract_element,
        span,
        stable,
    )


def create_fact_id(
    scan_id: str,
    fact_type: str,
    rule_id: str,
    file_path: str,
    start_line: int,
    end_line: int,
    project_path: str | None,
    source_symbol: str | None,
    target_symbol: str | None,
    contract_element: str | None,
    properties: Mapping[str, str],
) -> str:
    parts = [
        scan_id,
        fact_type,
        rule_id,
        file_path,
        str(start_line),
        str(end_line),
        project_path or "",
        source_symbol or "",
        target_symbol or "",
        contract_element or "",
    ]
    parts.extend(f"{key}={properties[key]}" for key in sorted(properties))
    return "fact-" + sha256_hex("|".join(parts), 20)
