"""Offline Python lockfile evidence for uv.lock and poetry.lock.

Bounded, deterministic parsing of checked-in lockfiles only. No package
manager is executed, no network access happens, and no lockfile artifact
hash is ever emitted as an artifact digest: uv.lock/poetry.lock hashes are
per-artifact-form (wheel versus source distribution) and a package-decision
record cannot identify the artifact form, so exact-artifact correlation is
unavailable by construction for this evidence.
"""

from __future__ import annotations

import re
import tomllib
from pathlib import Path
from urllib.parse import urlsplit

from .constants import EvidenceTiers, FactTypes, RuleIds, ScannerVersions
from .fact_factory import create_fact, evidence
from .hashes import sha256_bytes, sha256_hex
from .metadata import _parse_requirement, _unsafe_package_version
from .models import CodeFact, ScanManifest

UV_LOCK = "uv.lock"
POETRY_LOCK = "poetry.lock"
SUPPORTED_UV_LOCK_VERSIONS = {1}
SUPPORTED_POETRY_LOCK_VERSIONS = {"1.0", "1.1", "2.0", "2.1"}

_SAFE_NAME = re.compile(r"^[A-Za-z0-9]([A-Za-z0-9._-]*[A-Za-z0-9])?$")
_SAFE_HOST = re.compile(r"^[A-Za-z0-9.-]+(:[0-9]+)?$")
_PACKAGE_HEADER = re.compile(r"^\s*\[\[package\]\]\s*(?:#.*)?$")


def read_lockfiles(
    repo: Path,
    manifest: ScanManifest,
    files: list[Path],
    pyproject_files: list[Path],
    gaps: list[str],
) -> list[CodeFact]:
    facts: list[CodeFact] = []
    declared = _declared_pyproject_names(repo, pyproject_files)
    for path in sorted(files):
        rel = _rel(path, repo)
        try:
            raw = path.read_bytes()
            text = raw.decode("utf-8")
            data = tomllib.loads(text)
            lockfile_hash = sha256_bytes(raw, 32)
            header_lines = _package_header_lines(text)
            if path.name == UV_LOCK:
                facts.extend(_uv_lock_facts(manifest, rel, data, header_lines, lockfile_hash, gaps))
            elif path.name == POETRY_LOCK:
                facts.extend(_poetry_lock_facts(manifest, rel, data, header_lines, lockfile_hash, declared, gaps))
        except Exception as exc:
            gaps.append(f"PythonLockParseFailed: {rel}: {type(exc).__name__}")
            facts.append(_gap_fact(manifest, rel, 1, "python-lock-parse", f"{path.name} could not be parsed: {type(exc).__name__}"))
    return facts


def _uv_lock_facts(
    manifest: ScanManifest,
    rel: str,
    data: dict,
    header_lines: list[int],
    lockfile_hash: str,
    gaps: list[str],
) -> list[CodeFact]:
    version = data.get("version")
    packages = data.get("package")
    if version not in SUPPORTED_UV_LOCK_VERSIONS or not isinstance(packages, list):
        gaps.append(f"PythonLockUnsupported: {rel}: uv.lock format version {version!r}")
        return [_gap_fact(manifest, rel, 1, "python-lock-unsupported", f"uv.lock format version {version!r} is not supported")]
    direct_names: set[str] = set()
    roots_present = False
    for package in packages:
        if not isinstance(package, dict):
            continue
        source = package.get("source")
        if not isinstance(source, dict) or not ("virtual" in source or "editable" in source):
            continue
        roots_present = True
        for key in ("dependencies", "dev-dependencies"):
            direct_names.update(_root_dependency_names(package.get(key)))
    facts: list[CodeFact] = []
    for index, package in enumerate(packages):
        line = _entry_line(header_lines, index, len(packages))
        if not isinstance(package, dict):
            gaps.append(f"PythonLockEntryUnsafe: {rel}: entry {index}")
            facts.append(_gap_fact(manifest, rel, line, "python-lock-entry-unsafe", f"uv.lock entry {index} is not a package table"))
            continue
        source = package.get("source")
        if isinstance(source, dict) and ("virtual" in source or "editable" in source):
            # The project's own root entry is not a referenced package; its
            # identity is emitted from pyproject.toml manifest evidence.
            continue
        if not _is_registry_source(source):
            gaps.append(f"PythonLockEntrySourceUnsupported: {rel}: entry {index}")
            facts.append(_gap_fact(manifest, rel, line, "python-lock-entry-source-unsupported", f"uv.lock entry {index} does not resolve from a registry"))
            continue
        name = package.get("name")
        if not isinstance(name, str) or not _SAFE_NAME.match(name):
            gaps.append(f"PythonLockEntryUnsafe: {rel}: entry {index}")
            facts.append(_gap_fact(manifest, rel, line, "python-lock-entry-unsafe", f"uv.lock entry {index} has an unsafe package name"))
            continue
        resolved = package.get("version")
        if not isinstance(resolved, str) or not resolved.strip():
            gaps.append(f"PythonLockEntryResolvedMissing: {rel}: entry {index}")
            facts.append(_gap_fact(manifest, rel, line, "python-lock-entry-resolved-missing", f"uv.lock entry {index} did not provide a resolved version"))
            continue
        facts.append(
            _lockfile_fact(
                manifest,
                rel,
                line,
                name,
                resolved,
                UV_LOCK,
                "uv",
                lockfile_hash,
                registry_origin=_registry_origin(source.get("registry")) if isinstance(source.get("registry"), str) else None,
                dependency_relation=(
                    ("direct" if _normalize_name(name) in direct_names else "transitive") if roots_present else None
                ),
            )
        )
    facts.extend(_capability_gaps(manifest, rel, facts, relation_proven=roots_present))
    return facts


def _poetry_lock_facts(
    manifest: ScanManifest,
    rel: str,
    data: dict,
    header_lines: list[int],
    lockfile_hash: str,
    declared: set[str],
    gaps: list[str],
) -> list[CodeFact]:
    metadata = data.get("metadata")
    lock_version = metadata.get("lock-version") if isinstance(metadata, dict) else None
    packages = data.get("package")
    if lock_version not in SUPPORTED_POETRY_LOCK_VERSIONS or not isinstance(packages, list):
        gaps.append(f"PythonLockUnsupported: {rel}: poetry.lock lock-version {lock_version!r}")
        return [_gap_fact(manifest, rel, 1, "python-lock-unsupported", f"poetry.lock lock-version {lock_version!r} is not supported")]
    relation_proven = bool(declared)
    facts: list[CodeFact] = []
    for index, package in enumerate(packages):
        line = _entry_line(header_lines, index, len(packages))
        if not isinstance(package, dict):
            gaps.append(f"PythonLockEntryUnsafe: {rel}: entry {index}")
            facts.append(_gap_fact(manifest, rel, line, "python-lock-entry-unsafe", f"poetry.lock entry {index} is not a package table"))
            continue
        name = package.get("name")
        if not isinstance(name, str) or not _SAFE_NAME.match(name):
            gaps.append(f"PythonLockEntryUnsafe: {rel}: entry {index}")
            facts.append(_gap_fact(manifest, rel, line, "python-lock-entry-unsafe", f"poetry.lock entry {index} has an unsafe package name"))
            continue
        resolved = package.get("version")
        if not isinstance(resolved, str) or not resolved.strip():
            gaps.append(f"PythonLockEntryResolvedMissing: {rel}: entry {index}")
            facts.append(_gap_fact(manifest, rel, line, "python-lock-entry-resolved-missing", f"poetry.lock entry {index} did not provide a resolved version"))
            continue
        facts.append(
            _lockfile_fact(
                manifest,
                rel,
                line,
                name,
                resolved,
                POETRY_LOCK,
                "poetry",
                lockfile_hash,
                registry_origin=None,
                dependency_relation=(
                    ("direct" if _normalize_name(name) in declared else "transitive") if relation_proven else None
                ),
            )
        )
    facts.extend(_capability_gaps(manifest, rel, facts, relation_proven=relation_proven))
    return facts


def _lockfile_fact(
    manifest: ScanManifest,
    rel: str,
    line: int,
    name: str,
    resolved: str,
    manifest_kind: str,
    package_manager: str,
    lockfile_hash: str,
    *,
    registry_origin: str | None,
    dependency_relation: str | None,
) -> CodeFact:
    normalized = name.strip().lower()
    props: dict[str, str] = {
        "dependencyGroup": "lockfile",
        "ecosystem": "python",
        "lockfileHash": lockfile_hash,
        "lockfilePath": rel,
        "manifestKind": manifest_kind,
        "name": normalized,
        "package": normalized,
        "packageManager": package_manager,
        "packageName": normalized,
        "sourceKind": "lockfile",
        "surfaceKind": "package-config",
    }
    trimmed = resolved.strip()
    if _unsafe_package_version(trimmed):
        props["redactionReason"] = "unsafe-package-version"
        props["versionHash"] = f"version-hash:{sha256_hex(trimmed, 32)}"
    else:
        props["resolvedVersion"] = trimmed
        props["version"] = trimmed
    if registry_origin:
        props["registryOrigin"] = registry_origin
    if dependency_relation:
        props["dependencyRelation"] = dependency_relation
    return create_fact(
        manifest,
        FactTypes.PACKAGE_REFERENCED,
        RuleIds.PY_PACKAGE,
        EvidenceTiers.TIER2,
        evidence(rel, line, line, "PythonLockfileExtractor", ScannerVersions.LOCKFILE),
        target_symbol=normalized,
        contract_element=normalized,
        properties=props,
    )


def _capability_gaps(manifest: ScanManifest, rel: str, facts: list[CodeFact], *, relation_proven: bool) -> list[CodeFact]:
    if not any(fact.fact_type == FactTypes.PACKAGE_REFERENCED for fact in facts):
        return []
    result = [
        _gap_fact(
            manifest,
            rel,
            1,
            "LockfileDigestUnavailable",
            "uv.lock/poetry.lock artifact hashes are wheel or source-distribution form-specific and are never emitted as artifact digests",
        )
    ]
    if not relation_proven:
        result.append(
            _gap_fact(
                manifest,
                rel,
                1,
                "DirectTransitiveUnavailable",
                "the lockfile plus its root manifest declarations did not prove a direct versus transitive relation",
            )
        )
    return result


def _gap_fact(manifest: ScanManifest, rel: str, line: int, kind: str, message: str) -> CodeFact:
    return create_fact(
        manifest,
        FactTypes.ANALYSIS_GAP,
        RuleIds.PY_PACKAGE,
        EvidenceTiers.TIER4,
        evidence(rel, line, line, "PythonLockfileExtractor", ScannerVersions.LOCKFILE),
        target_symbol=kind,
        properties={"gapKind": kind, "messageHash": sha256_hex(message, 32)},
    )


def _declared_pyproject_names(repo: Path, pyproject_files: list[Path]) -> set[str]:
    declared: set[str] = set()
    for path in sorted(pyproject_files):
        try:
            data = tomllib.loads(path.read_text(encoding="utf-8"))
        except Exception:
            continue
        project = data.get("project", {})
        declared.update(_names_from_requirement_list(project.get("dependencies")))
        optional = project.get("optional-dependencies", {})
        if isinstance(optional, dict):
            for values in optional.values():
                declared.update(_names_from_requirement_list(values))
        poetry = data.get("tool", {}).get("poetry", {})
        poetry_dependencies = poetry.get("dependencies", {})
        if isinstance(poetry_dependencies, dict):
            for key in poetry_dependencies:
                normalized = _normalize_name(str(key))
                if normalized and normalized != "python":
                    declared.add(normalized)
    return declared


def _names_from_requirement_list(values: object) -> set[str]:
    names: set[str] = set()
    if not isinstance(values, list):
        return names
    for value in values:
        name, _ = _parse_requirement(str(value))
        if name:
            names.add(_normalize_name(name))
    return names


def _root_dependency_names(values: object) -> set[str]:
    names: set[str] = set()
    if not isinstance(values, list):
        return names
    for value in values:
        if isinstance(value, dict):
            name = value.get("name")
            if isinstance(name, str):
                names.add(_normalize_name(name))
    return names


def _is_registry_source(source: object) -> bool:
    return isinstance(source, dict) and isinstance(source.get("registry"), str)


def _registry_origin(url: str) -> str | None:
    try:
        parsed = urlsplit(url)
    except ValueError:
        return None
    if not parsed.hostname:
        return None
    candidate = parsed.hostname.lower() + (f":{parsed.port}" if parsed.port else "")
    return candidate if _SAFE_HOST.match(candidate) else None


def _normalize_name(name: str) -> str:
    return re.sub(r"[-_.]+", "_", name.strip()).lower()


def _package_header_lines(text: str) -> list[int]:
    return [line_no for line_no, raw in enumerate(text.splitlines(), start=1) if _PACKAGE_HEADER.match(raw)]


def _entry_line(header_lines: list[int], index: int, package_count: int) -> int:
    # tomllib returns package tables in document order; when the header-line
    # scan agrees on the count it identifies each entry's line, and otherwise
    # (for example a [[package]] literal inside a multiline string) spans fall
    # back to the file anchor rather than guessing.
    if len(header_lines) == package_count and index < len(header_lines):
        return header_lines[index]
    return 1


def _rel(path: Path, repo: Path) -> str:
    return str(path.resolve().relative_to(repo.resolve())).replace("\\", "/")
