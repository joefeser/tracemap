from __future__ import annotations

import configparser
import re
import tomllib
from pathlib import Path

from .constants import EvidenceTiers, FactTypes, RuleIds, ScannerVersions
from .fact_factory import create_fact, evidence
from .hashes import sha256_hex
from .models import CodeFact, ScanManifest


def read_package_metadata(repo: Path, manifest: ScanManifest, files: list[Path], gaps: list[str]) -> tuple[dict[str, str], list[CodeFact]]:
    deps: dict[str, str] = {}
    facts: list[CodeFact] = []
    for path in sorted(files):
        rel = _rel(path, repo)
        try:
            if path.name == "pyproject.toml":
                parsed = tomllib.loads(path.read_text(encoding="utf-8"))
                project = parsed.get("project", {})
                deps.update(_deps_from_list(project.get("dependencies", [])))
                for optional in project.get("optional-dependencies", {}).values():
                    deps.update(_deps_from_list(optional))
                poetry = parsed.get("tool", {}).get("poetry", {})
                deps.update(_deps_from_mapping(poetry.get("dependencies", {})))
                name = project.get("name") or poetry.get("name") or ""
                version = project.get("version") or poetry.get("version") or ""
                facts.append(_package_fact(manifest, rel, 1, name, version, "pyproject.toml"))
            elif path.name == "setup.cfg":
                parser = configparser.ConfigParser()
                parser.read(path, encoding="utf-8")
                name = parser.get("metadata", "name", fallback="")
                version = parser.get("metadata", "version", fallback="")
                facts.append(_package_fact(manifest, rel, 1, name, version, "setup.cfg"))
                install_requires = parser.get("options", "install_requires", fallback="")
                deps.update(_deps_from_list(install_requires.splitlines()))
            elif path.name.startswith("requirements") and path.suffix == ".txt":
                deps.update(_deps_from_requirements(path, repo, manifest, facts))
            elif path.name == "setup.py":
                gaps.append(f"Dynamic setup.py metadata not executed: {rel}")
                facts.append(_gap_fact(manifest, rel, "dynamic-setup", "setup.py was not executed"))
        except Exception as exc:
            gaps.append(f"PythonMetadataParseFailed: {rel}: {type(exc).__name__}")
            facts.append(_gap_fact(manifest, rel, "metadata-parse", type(exc).__name__))
    for name, version in sorted(deps.items()):
        facts.append(
            create_fact(
                manifest,
                FactTypes.PACKAGE_REFERENCED,
                RuleIds.PY_PACKAGE,
                EvidenceTiers.TIER2,
                evidence("pyproject.toml" if Path(repo, "pyproject.toml").exists() else ".", 1, 1, "PythonMetadataExtractor", ScannerVersions.METADATA),
                target_symbol=name,
                contract_element=name,
                properties={"name": name, "packageName": name, "packageVersion": version, "version": version},
            )
        )
    return deps, facts


def _package_fact(manifest: ScanManifest, rel: str, line: int, name: str, version: str, source: str) -> CodeFact:
    return create_fact(
        manifest,
        FactTypes.PACKAGE_REFERENCED,
        RuleIds.PY_PACKAGE,
        EvidenceTiers.TIER2,
        evidence(rel, line, line, "PythonMetadataExtractor", ScannerVersions.METADATA),
        target_symbol=name or source,
        contract_element=name or source,
        properties={"name": name, "packageName": name, "packageVersion": version, "version": version, "metadataSource": source},
    )


def _gap_fact(manifest: ScanManifest, rel: str, kind: str, message: str) -> CodeFact:
    return create_fact(
        manifest,
        FactTypes.ANALYSIS_GAP,
        RuleIds.PY_PACKAGE,
        EvidenceTiers.TIER4,
        evidence(rel, 1, 1, "PythonMetadataExtractor", ScannerVersions.METADATA),
        target_symbol=rel,
        properties={"gapKind": kind, "messageHash": sha256_hex(message, 32)},
    )


def _deps_from_list(values: object) -> dict[str, str]:
    deps: dict[str, str] = {}
    if not isinstance(values, list):
        return deps
    for value in values:
        name, version = _parse_requirement(str(value))
        if name:
            deps[name] = version
    return deps


def _deps_from_mapping(values: object) -> dict[str, str]:
    deps: dict[str, str] = {}
    if not isinstance(values, dict):
        return deps
    for key, value in values.items():
        if str(key).lower() == "python":
            continue
        deps[str(key).lower()] = "" if isinstance(value, dict) else str(value)
    return deps


def _deps_from_requirements(path: Path, repo: Path, manifest: ScanManifest, facts: list[CodeFact]) -> dict[str, str]:
    deps: dict[str, str] = {}
    for line_no, raw in enumerate(path.read_text(encoding="utf-8").splitlines(), start=1):
        text = raw.strip()
        if not text or text.startswith("#") or text.startswith(_REQUIREMENTS_OPTIONS_TO_SKIP):
            continue
        if text.startswith("-e ") or "git+" in text or text.startswith(("./", "../", "/")):
            ref_hash = sha256_hex(text, 32)
            facts.append(
                create_fact(
                    manifest,
                    FactTypes.PACKAGE_REFERENCED,
                    RuleIds.PY_PACKAGE,
                    EvidenceTiers.TIER3,
                    evidence(_rel(path, repo), line_no, line_no, "PythonMetadataExtractor", ScannerVersions.METADATA),
                    target_symbol="dependency-boundary",
                    properties={"referenceKind": "dynamic", "referenceHash": ref_hash},
                )
            )
            continue
        name, version = _parse_requirement(text)
        if name:
            deps[name] = version
    return deps


def _parse_requirement(value: str) -> tuple[str, str]:
    value = value.split("#", 1)[0].strip()
    match = re.match(r"([A-Za-z0-9_.-]+)\s*([<>=!~].*)?$", value)
    if not match:
        return "", ""
    return match.group(1).lower(), (match.group(2) or "").strip()


_REQUIREMENTS_OPTIONS_TO_SKIP = (
    "-r ",
    "--requirement ",
    "-c ",
    "--constraint ",
    "--index-url ",
    "--extra-index-url ",
    "--find-links ",
    "--trusted-host ",
    "--pre",
    "--no-binary ",
    "--only-binary ",
)


def _rel(path: Path, repo: Path) -> str:
    return str(path.resolve().relative_to(repo.resolve())).replace("\\", "/")
