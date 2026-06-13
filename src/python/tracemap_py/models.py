from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any


@dataclass(frozen=True)
class EvidenceSpan:
    file_path: str
    start_line: int
    end_line: int
    snippet_hash: str | None
    extractor_id: str
    extractor_version: str

    def to_json(self) -> dict[str, Any]:
        return {
            "filePath": self.file_path,
            "startLine": max(1, self.start_line),
            "endLine": max(max(1, self.start_line), self.end_line),
            "snippetHash": self.snippet_hash,
            "extractorId": self.extractor_id,
            "extractorVersion": self.extractor_version,
        }


@dataclass(frozen=True)
class ScanManifest:
    scan_id: str
    repo_name: str
    remote_url: str | None
    branch: str | None
    commit_sha: str
    scanner_version: str
    scanned_at: str
    analysis_level: str
    build_status: str
    solutions: list[str]
    projects: list[str]
    target_frameworks: list[str]
    known_gaps: list[str]
    scan_root_relative_path: str | None = None
    scan_root_path_hash: str | None = None
    git_root_hash: str | None = None

    def to_json(self) -> dict[str, Any]:
        return {
            "scanId": self.scan_id,
            "repoName": self.repo_name,
            "remoteUrl": self.remote_url,
            "branch": self.branch,
            "commitSha": self.commit_sha,
            "scannerVersion": self.scanner_version,
            "scannedAt": self.scanned_at,
            "analysisLevel": self.analysis_level,
            "buildStatus": self.build_status,
            "solutions": self.solutions,
            "projects": self.projects,
            "targetFrameworks": self.target_frameworks,
            "knownGaps": self.known_gaps,
            "scanRootRelativePath": self.scan_root_relative_path,
            "scanRootPathHash": self.scan_root_path_hash,
            "gitRootHash": self.git_root_hash,
        }


@dataclass(frozen=True)
class CodeFact:
    fact_id: str
    scan_id: str
    repo: str
    commit_sha: str
    project_path: str | None
    fact_type: str
    rule_id: str
    evidence_tier: str
    source_symbol: str | None
    target_symbol: str | None
    contract_element: str | None
    evidence: EvidenceSpan
    properties: dict[str, str] = field(default_factory=dict)

    def to_json(self) -> dict[str, Any]:
        return {
            "factId": self.fact_id,
            "scanId": self.scan_id,
            "repo": self.repo,
            "commitSha": self.commit_sha,
            "projectPath": self.project_path,
            "factType": self.fact_type,
            "ruleId": self.rule_id,
            "evidenceTier": self.evidence_tier,
            "sourceSymbol": self.source_symbol,
            "targetSymbol": self.target_symbol,
            "contractElement": self.contract_element,
            "evidence": self.evidence.to_json(),
            "properties": dict(sorted(self.properties.items())),
        }


@dataclass(frozen=True)
class FileInventoryItem:
    relative_path: str
    absolute_path: str
    kind: str
    size_bytes: int
    skipped: bool = False


@dataclass(frozen=True)
class GitMetadata:
    repo_name: str
    remote_url: str | None
    branch: str | None
    commit_sha: str
    git_root_path: str


@dataclass(frozen=True)
class ScanOptions:
    repo_path: str
    output_path: str
    project_paths: list[str]
    include_globs: list[str]
    exclude_globs: list[str]
    max_file_byte_size: int
    no_metadata: bool = False
