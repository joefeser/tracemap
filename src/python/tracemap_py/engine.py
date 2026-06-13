from __future__ import annotations

import json
import shutil
from datetime import UTC, datetime
from pathlib import Path

from .ast_extractor import extract_python_files
from .config_extractor import extract_config_files
from .constants import EvidenceTiers, FactTypes, RuleIds, ScannerVersions
from .fact_factory import create_fact, evidence
from .git_metadata import read_git_metadata
from .hashes import sha256_hex
from .inventory import create_scan_id, discover_inventory, package_roots
from .metadata import read_package_metadata
from .models import CodeFact, FileInventoryItem, ScanManifest, ScanOptions
from .pathing import parse_byte_size
from .report import render_report
from .sql_extractor import extract_sql_files
from .writers import write_facts, write_manifest, write_sqlite


def scan(options: ScanOptions) -> tuple[ScanManifest, list[CodeFact]]:
    repo = Path(options.repo_path).resolve()
    out = Path(options.output_path).resolve()
    if not repo.exists():
        raise FileNotFoundError(f"Repository path does not exist: {repo}")
    git = read_git_metadata(repo)
    inventory = discover_inventory(repo, options)
    repo_identity = git.remote_url or git.repo_name
    scan_id = create_scan_id(repo_identity, git.commit_sha, inventory, sha256_hex)
    roots = package_roots(repo, inventory)
    base_manifest = ScanManifest(
        scan_id,
        git.repo_name,
        git.remote_url,
        git.branch,
        git.commit_sha,
        ScannerVersions.TRACE_MAP_PY,
        datetime.now(UTC).isoformat(),
        "Level1SemanticAnalysisReduced",
        "FailedOrPartial",
        [],
        [item.relative_path for item in inventory if item.kind == "PythonMetadata"],
        ["python"],
        [],
        _scan_root_relative(repo, git.git_root_path),
        sha256_hex(str(repo), 32),
        sha256_hex(str(Path(git.git_root_path).resolve()), 32),
    )
    gaps: list[str] = []
    facts: list[CodeFact] = []
    facts.extend(_repo_facts(base_manifest))
    facts.extend(_inventory_facts(base_manifest, inventory, gaps))
    usable = [Path(item.absolute_path) for item in inventory if not item.skipped]
    metadata_files = [path for path in usable if _kind(path, inventory) == "PythonMetadata"]
    config_files = [path for path in usable if _kind(path, inventory) == "ConfigFile"]
    sql_files = [path for path in usable if _kind(path, inventory) == "SqlFile"]
    py_files = [path for path in usable if _kind(path, inventory) in {"PythonSource", "PythonStub"}]
    deps: dict[str, str] = {}
    if not options.no_metadata:
        deps, metadata_facts = read_package_metadata(repo, base_manifest, metadata_files, gaps)
        facts.extend(metadata_facts)
    facts.extend(extract_config_files(repo, base_manifest, config_files, gaps))
    facts.extend(extract_sql_files(repo, base_manifest, sql_files, gaps))
    facts.extend(extract_python_files(repo, base_manifest, py_files, roots, deps, gaps))
    facts.extend(_gap_facts(base_manifest, gaps))
    facts = _dedupe_facts(facts)
    analysis_level = "Level3SyntaxAnalysis" if not py_files else "Level1SemanticAnalysisReduced"
    build_status = "NotRun" if not py_files else "FailedOrPartial"
    manifest = base_manifest.__class__(
        base_manifest.scan_id,
        base_manifest.repo_name,
        base_manifest.remote_url,
        base_manifest.branch,
        base_manifest.commit_sha,
        base_manifest.scanner_version,
        base_manifest.scanned_at,
        analysis_level,
        build_status,
        base_manifest.solutions,
        base_manifest.projects,
        base_manifest.target_frameworks,
        sorted(set(gaps)),
        base_manifest.scan_root_relative_path,
        base_manifest.scan_root_path_hash,
        base_manifest.git_root_hash,
    )
    _write_outputs(out, manifest, facts, gaps)
    return manifest, facts


def make_options(repo: str, out: str, project: list[str] | None = None, include: list[str] | None = None, exclude: list[str] | None = None, max_file_byte_size: str | int | None = None, no_metadata: bool = False) -> ScanOptions:
    return ScanOptions(repo, out, project or [], include or [], exclude or [], parse_byte_size(max_file_byte_size), no_metadata)


def _write_outputs(out: Path, manifest: ScanManifest, facts: list[CodeFact], gaps: list[str]) -> None:
    if out.exists():
        shutil.rmtree(out)
    (out / "logs").mkdir(parents=True, exist_ok=True)
    write_manifest(out / "scan-manifest.json", manifest)
    write_facts(out / "facts.ndjson", facts)
    write_sqlite(out / "index.sqlite", manifest, facts)
    (out / "report.md").write_text(render_report(manifest, facts), encoding="utf-8")
    (out / "logs" / "analyzer.log").write_text("\n".join(gaps) + ("\n" if gaps else ""), encoding="utf-8")


def _repo_facts(manifest: ScanManifest) -> list[CodeFact]:
    span = evidence(".", 1, 1, "PythonRepoExtractor", ScannerVersions.REPO)
    return [
        create_fact(manifest, FactTypes.REPO_SCANNED, RuleIds.REPO_MANIFEST, EvidenceTiers.TIER2, span, target_symbol=manifest.repo_name, properties={"repoName": manifest.repo_name, "commitSha": manifest.commit_sha}),
        create_fact(manifest, FactTypes.BUILD_STATUS, RuleIds.REPO_MANIFEST, EvidenceTiers.TIER2, span, target_symbol=manifest.repo_name, properties={"analysisLevel": manifest.analysis_level, "buildStatus": manifest.build_status}),
    ]


def _inventory_facts(manifest: ScanManifest, inventory: list[FileInventoryItem], gaps: list[str]) -> list[CodeFact]:
    facts: list[CodeFact] = []
    for item in inventory:
        facts.append(
            create_fact(
                manifest,
                FactTypes.FILE_INVENTORIED,
                RuleIds.FILE_INVENTORY,
                EvidenceTiers.TIER2,
                evidence(item.relative_path, 1, 1, "PythonInventoryExtractor", ScannerVersions.INVENTORY),
                target_symbol=item.relative_path,
                contract_element=item.relative_path,
                properties={"path": item.relative_path, "kind": item.kind, "sizeBytes": item.size_bytes, "skipped": str(item.skipped).lower()},
            )
        )
        if item.skipped:
            gaps.append(f"FileSkippedMaxSize: {item.relative_path}")
    return facts


def _gap_facts(manifest: ScanManifest, gaps: list[str]) -> list[CodeFact]:
    result = []
    for index, gap in enumerate(sorted(set(gaps)), start=1):
        result.append(
            create_fact(
                manifest,
                FactTypes.ANALYSIS_GAP,
                RuleIds.REPO_MANIFEST,
                EvidenceTiers.TIER4,
                evidence(".", index, index, "PythonGapCollector", ScannerVersions.REPO),
                target_symbol="analysis-gap",
                properties={"gapKind": gap.split(":", 1)[0], "messageHash": sha256_hex(gap, 32)},
            )
        )
    return result


def _kind(path: Path, inventory: list[FileInventoryItem]) -> str:
    resolved = str(path.resolve())
    for item in inventory:
        if item.absolute_path == resolved:
            return item.kind
    return ""


def _scan_root_relative(repo: Path, git_root: str) -> str:
    try:
        return str(repo.resolve().relative_to(Path(git_root).resolve())).replace("\\", "/") or "."
    except ValueError:
        return "."


def _dedupe_facts(facts: list[CodeFact]) -> list[CodeFact]:
    by_id: dict[str, CodeFact] = {}
    for fact in facts:
        by_id.setdefault(fact.fact_id, fact)
    return [by_id[fact_id] for fact_id in sorted(by_id)]
