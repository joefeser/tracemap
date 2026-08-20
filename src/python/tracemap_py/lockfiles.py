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
from pathlib import Path, PurePosixPath
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
    for path in sorted(files):
        rel = _rel(path, repo)
        try:
            raw = path.read_bytes()
            text = raw.decode("utf-8")
            data = tomllib.loads(text)
            lockfile_hash = sha256_bytes(raw, 32)
            header_lines = _package_header_lines(text)
            if path.name == UV_LOCK:
                facts.extend(
                    _uv_lock_facts(
                        manifest,
                        rel,
                        data,
                        header_lines,
                        lockfile_hash,
                        _uv_workspace_source_paths(path, pyproject_files),
                        gaps,
                    )
                )
            elif path.name == POETRY_LOCK:
                declared, declaration_complete = _declared_names_for_lockfile(path, pyproject_files)
                facts.extend(
                    _poetry_lock_facts(
                        manifest,
                        rel,
                        data,
                        header_lines,
                        lockfile_hash,
                        declared,
                        declaration_complete,
                        gaps,
                    )
                )
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
    workspace_source_paths: set[str],
    gaps: list[str],
) -> list[CodeFact]:
    version = data.get("version")
    packages = data.get("package")
    if version not in SUPPORTED_UV_LOCK_VERSIONS or not isinstance(packages, list):
        gaps.append(f"PythonLockUnsupported: {rel}: uv.lock format version {version!r}")
        return [_gap_fact(manifest, rel, 1, "python-lock-unsupported", f"uv.lock format version {version!r} is not supported")]
    direct_descriptors: list[dict] = []
    declarations_complete = True
    roots_present = False
    for package in packages:
        if not isinstance(package, dict):
            continue
        source = package.get("source")
        if not _is_uv_workspace_source(source, workspace_source_paths):
            continue
        roots_present = True
        descriptors, complete = _root_dependency_descriptors(package)
        direct_descriptors.extend(descriptors)
        declarations_complete = declarations_complete and complete
    declarations_complete = roots_present and declarations_complete
    facts: list[CodeFact] = []
    for index, package in enumerate(packages):
        line = _entry_line(header_lines, index, len(packages))
        if not isinstance(package, dict):
            gaps.append(f"PythonLockEntryUnsafe: {rel}: entry {index}")
            facts.append(_gap_fact(manifest, rel, line, "python-lock-entry-unsafe", f"uv.lock entry {index} is not a package table"))
            continue
        source = package.get("source")
        if _is_uv_workspace_source(source, workspace_source_paths):
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
                dependency_relation=_uv_dependency_relation(index, package, packages, direct_descriptors, declarations_complete),
            )
        )
    relation_proven = declarations_complete and all(
        fact.properties.get("dependencyRelation") in {"direct", "transitive"}
        for fact in facts
        if fact.fact_type == FactTypes.PACKAGE_REFERENCED
    )
    facts.extend(_capability_gaps(manifest, rel, facts, gaps, relation_proven=relation_proven))
    return facts


def _poetry_lock_facts(
    manifest: ScanManifest,
    rel: str,
    data: dict,
    header_lines: list[int],
    lockfile_hash: str,
    declared: set[str],
    declaration_complete: bool,
    gaps: list[str],
) -> list[CodeFact]:
    metadata = data.get("metadata")
    lock_version = metadata.get("lock-version") if isinstance(metadata, dict) else None
    packages = data.get("package")
    if lock_version not in SUPPORTED_POETRY_LOCK_VERSIONS or not isinstance(packages, list):
        gaps.append(f"PythonLockUnsupported: {rel}: poetry.lock lock-version {lock_version!r}")
        return [_gap_fact(manifest, rel, 1, "python-lock-unsupported", f"poetry.lock lock-version {lock_version!r} is not supported")]
    facts: list[CodeFact] = []
    for index, package in enumerate(packages):
        line = _entry_line(header_lines, index, len(packages))
        if not isinstance(package, dict):
            gaps.append(f"PythonLockEntryUnsafe: {rel}: entry {index}")
            facts.append(_gap_fact(manifest, rel, line, "python-lock-entry-unsafe", f"poetry.lock entry {index} is not a package table"))
            continue
        source_supported, registry_origin = _poetry_registry_source(package.get("source"))
        if not source_supported:
            gaps.append(f"PythonLockEntrySourceUnsupported: {rel}: entry {index}")
            facts.append(
                _gap_fact(
                    manifest,
                    rel,
                    line,
                    "python-lock-entry-source-unsupported",
                    f"poetry.lock entry {index} does not resolve from a supported registry source",
                )
            )
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
                registry_origin=registry_origin,
                dependency_relation=_poetry_dependency_relation(index, package, packages, declared, declaration_complete),
            )
        )
    relation_proven = declaration_complete and all(
        fact.properties.get("dependencyRelation") in {"direct", "transitive"}
        for fact in facts
        if fact.fact_type == FactTypes.PACKAGE_REFERENCED
    )
    facts.extend(_capability_gaps(manifest, rel, facts, gaps, relation_proven=relation_proven))
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


def _capability_gaps(
    manifest: ScanManifest,
    rel: str,
    facts: list[CodeFact],
    gaps: list[str],
    *,
    relation_proven: bool,
) -> list[CodeFact]:
    if not any(fact.fact_type == FactTypes.PACKAGE_REFERENCED for fact in facts):
        return []
    gaps.append(f"LockfileDigestUnavailable: {rel}")
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
        gaps.append(f"DirectTransitiveUnavailable: {rel}")
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


def _declared_names_for_lockfile(lockfile: Path, pyproject_files: list[Path]) -> tuple[set[str], bool]:
    manifest = lockfile.with_name("pyproject.toml").resolve()
    available = {path.resolve() for path in pyproject_files}
    return _declared_pyproject_names([manifest]) if manifest in available else (set(), False)


def _declared_pyproject_names(pyproject_files: list[Path]) -> tuple[set[str], bool]:
    declared: set[str] = set()
    complete = True
    declaration_surface_present = False
    for path in sorted(pyproject_files):
        try:
            data = tomllib.loads(path.read_text(encoding="utf-8"))
        except Exception:
            return declared, False
        if not isinstance(data, dict):
            return declared, False
        project = data.get("project")
        if project is not None and not isinstance(project, dict):
            complete = False
            project = {}
        project = project or {}
        if "dependencies" in project:
            declaration_surface_present = True
            names, valid = _names_from_requirement_list(project.get("dependencies"))
            declared.update(names)
            complete = complete and valid
        dynamic = project.get("dynamic")
        if dynamic is not None:
            if not isinstance(dynamic, list) or any(not isinstance(value, str) for value in dynamic):
                complete = False
            elif "dependencies" in dynamic:
                complete = False
        optional = project.get("optional-dependencies")
        if optional is not None:
            declaration_surface_present = True
            if not isinstance(optional, dict):
                complete = False
                optional = {}
            for values in optional.values():
                names, valid = _names_from_requirement_list(values)
                declared.update(names)
                complete = complete and valid
        tool = data.get("tool")
        if tool is not None and not isinstance(tool, dict):
            complete = False
            tool = {}
        tool = tool or {}
        poetry = tool.get("poetry")
        if poetry is not None and not isinstance(poetry, dict):
            complete = False
            poetry = {}
        poetry = poetry or {}
        for key in ("dependencies", "dev-dependencies"):
            if key not in poetry:
                continue
            declaration_surface_present = True
            complete = _add_poetry_dependency_table(declared, poetry.get(key)) and complete
        groups = poetry.get("group")
        if groups is not None:
            declaration_surface_present = True
            if not isinstance(groups, dict):
                complete = False
                groups = {}
            for group in groups.values():
                if not isinstance(group, dict) or "dependencies" not in group:
                    complete = False
                    continue
                complete = _add_poetry_dependency_table(declared, group.get("dependencies")) and complete
    return declared, declaration_surface_present and complete


def _names_from_requirement_list(values: object) -> tuple[set[str], bool]:
    names: set[str] = set()
    if not isinstance(values, list):
        return names, False
    for value in values:
        if not isinstance(value, str):
            return names, False
        name, _ = _parse_requirement(value)
        if not name:
            return names, False
        names.add(_normalize_name(name))
    return names, True


def _add_poetry_dependency_table(declared: set[str], values: object) -> bool:
    if not isinstance(values, dict):
        return False
    valid = True
    for key in values:
        if not isinstance(key, str) or not _SAFE_NAME.match(key):
            valid = False
            continue
        normalized = _normalize_name(key)
        if normalized != "python":
            declared.add(normalized)
    return valid


def _root_dependency_descriptors(package: dict) -> tuple[list[dict], bool]:
    descriptors: list[dict] = []
    present = False
    complete = True
    for key in ("dependencies", "dev-dependencies", "optional-dependencies"):
        if key not in package:
            continue
        present = True
        values = package.get(key)
        groups = values.values() if isinstance(values, dict) else [values]
        for group in groups:
            if not isinstance(group, list):
                complete = False
                continue
            for value in group:
                if not isinstance(value, dict):
                    complete = False
                    continue
                name = value.get("name")
                if not isinstance(name, str) or not _SAFE_NAME.match(name):
                    complete = False
                    continue
                descriptors.append(value)
    return descriptors, present and complete


def _uv_dependency_relation(
    index: int,
    package: dict,
    packages: list,
    descriptors: list[dict],
    declarations_complete: bool,
) -> str | None:
    name = package.get("name")
    if not isinstance(name, str):
        return None
    normalized = _normalize_name(name)
    same_name_descriptors = [
        descriptor
        for descriptor in descriptors
        if isinstance(descriptor.get("name"), str) and _normalize_name(descriptor["name"]) == normalized
    ]
    if not same_name_descriptors:
        return "transitive" if declarations_complete else None

    candidate_indexes = [
        candidate_index
        for candidate_index, candidate in enumerate(packages)
        if isinstance(candidate, dict)
        and isinstance(candidate.get("name"), str)
        and _normalize_name(candidate["name"]) == normalized
        and _is_registry_source(candidate.get("source"))
    ]
    ambiguous_indexes: set[int] = set()
    for descriptor in same_name_descriptors:
        matching = [
            candidate_index
            for candidate_index in candidate_indexes
            if _uv_descriptor_matches(descriptor, packages[candidate_index])
        ]
        if len(matching) == 1 and matching[0] == index:
            return "direct"
        if len(matching) > 1:
            ambiguous_indexes.update(matching)
    if index in ambiguous_indexes:
        return None
    return "transitive" if declarations_complete else None


def _uv_descriptor_matches(descriptor: dict, package: dict) -> bool:
    descriptor_version = descriptor.get("version")
    if descriptor_version is not None:
        if not isinstance(descriptor_version, str) or package.get("version") != descriptor_version:
            return False
    descriptor_source = descriptor.get("source")
    if descriptor_source is not None:
        if not isinstance(descriptor_source, dict):
            return False
        descriptor_registry = descriptor_source.get("registry")
        package_source = package.get("source")
        package_registry = package_source.get("registry") if isinstance(package_source, dict) else None
        if not isinstance(descriptor_registry, str) or not isinstance(package_registry, str):
            return False
        descriptor_origin = _registry_origin(descriptor_registry)
        package_origin = _registry_origin(package_registry)
        if descriptor_origin is None or package_origin is None or descriptor_origin != package_origin:
            return False
    return True


def _poetry_dependency_relation(
    index: int,
    package: dict,
    packages: list,
    declared: set[str],
    declaration_complete: bool,
) -> str | None:
    if not declaration_complete:
        return None
    name = package.get("name")
    if not isinstance(name, str):
        return None
    normalized = _normalize_name(name)
    if normalized not in declared:
        return "transitive"
    matching_indexes = [
        candidate_index
        for candidate_index, candidate in enumerate(packages)
        if isinstance(candidate, dict)
        and isinstance(candidate.get("name"), str)
        and _normalize_name(candidate["name"]) == normalized
        and isinstance(candidate.get("version"), str)
        and bool(candidate["version"].strip())
        and _poetry_registry_source(candidate.get("source"))[0]
    ]
    return "direct" if matching_indexes == [index] else None


def _is_registry_source(source: object) -> bool:
    return isinstance(source, dict) and isinstance(source.get("registry"), str)


def _poetry_registry_source(source: object) -> tuple[bool, str | None]:
    if source is None:
        return True, None
    if not isinstance(source, dict) or source.get("type") != "legacy":
        return False, None
    url = source.get("url")
    if not isinstance(url, str):
        return False, None
    origin = _registry_origin(url)
    return (True, origin) if origin else (False, None)


def _uv_workspace_source_paths(lockfile: Path, pyproject_files: list[Path]) -> set[str]:
    result = {"."}
    lock_root = lockfile.parent.resolve()
    available = {path.resolve() for path in pyproject_files}
    root_manifest = lockfile.with_name("pyproject.toml").resolve()
    if root_manifest not in available:
        return result
    try:
        data = tomllib.loads(root_manifest.read_text(encoding="utf-8"))
    except Exception:
        return result
    tool = data.get("tool") if isinstance(data, dict) else None
    uv = tool.get("uv") if isinstance(tool, dict) else None
    workspace = uv.get("workspace") if isinstance(uv, dict) else None
    member_patterns = _workspace_patterns(workspace, "members")
    exclude_patterns = _workspace_patterns(workspace, "exclude")
    for path in available:
        try:
            relative = path.parent.relative_to(lock_root)
        except ValueError:
            continue
        normalized = relative.as_posix()
        member = PurePosixPath(normalized)
        if (
            normalized
            and any(member.match(pattern) for pattern in member_patterns)
            and not any(member.match(pattern) for pattern in exclude_patterns)
        ):
            result.add(normalized)
    return result


def _is_uv_workspace_source(source: object, workspace_source_paths: set[str]) -> bool:
    if not isinstance(source, dict):
        return False
    for key in ("virtual", "editable"):
        value = source.get(key)
        if not isinstance(value, str):
            continue
        normalized = _normalize_relative_source_path(value)
        if normalized is None:
            continue
        if normalized in workspace_source_paths:
            return True
    return False


def _workspace_patterns(workspace: object, key: str) -> list[str]:
    values = workspace.get(key) if isinstance(workspace, dict) else None
    if not isinstance(values, list):
        return []
    result: list[str] = []
    for value in values:
        if not isinstance(value, str):
            continue
        normalized = value.replace("\\", "/").removeprefix("./").rstrip("/")
        if normalized and not normalized.startswith("/") and all(part != ".." for part in normalized.split("/")):
            result.append(normalized)
    return result


def _normalize_relative_source_path(value: str) -> str | None:
    normalized = value.replace("\\", "/").removeprefix("./").rstrip("/") or "."
    if normalized.startswith("/") or any(part == ".." for part in normalized.split("/")):
        return None
    return normalized


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
