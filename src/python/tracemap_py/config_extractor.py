from __future__ import annotations

import json
import re
import tomllib
from pathlib import Path

from .constants import EvidenceTiers, FactTypes, RuleIds, ScannerVersions
from .fact_factory import create_fact, evidence
from .hashes import sha256_hex
from .models import CodeFact, ScanManifest


def extract_config_files(repo: Path, manifest: ScanManifest, files: list[Path], gaps: list[str]) -> list[CodeFact]:
    facts: list[CodeFact] = []
    for path in sorted(files):
        rel = str(path.resolve().relative_to(repo.resolve())).replace("\\", "/")
        try:
            lines = path.read_text(encoding="utf-8").splitlines()
        except (OSError, UnicodeDecodeError) as exc:
            gaps.append(f"ConfigReadFailed: {rel}: {type(exc).__name__}")
            continue
        facts.append(
            create_fact(
                manifest,
                FactTypes.CONFIG_FILE_DECLARED,
                RuleIds.FILE_INVENTORY,
                EvidenceTiers.TIER2,
                evidence(rel, 1, max(1, len(lines)), "PythonConfigExtractor", ScannerVersions.CONFIG),
                target_symbol=rel,
                properties={"path": rel, "fileKind": "Config"},
            )
        )
        for line_no, key, value in _parse_keys(path, lines):
            facts.append(
                create_fact(
                    manifest,
                    FactTypes.CONFIG_KEY_DECLARED,
                    RuleIds.CONFIG,
                    EvidenceTiers.TIER2,
                    evidence(rel, line_no, line_no, "PythonConfigExtractor", ScannerVersions.CONFIG),
                    target_symbol=key,
                    contract_element=key,
                    properties={"keyPath": key, "name": key.split(".")[-1], "valueKind": _value_kind(value), "valueHash": sha256_hex(value, 32) if value else ""},
                )
            )
    return facts


def _parse_keys(path: Path, lines: list[str]) -> list[tuple[int, str, str]]:
    suffix = path.suffix.lower()
    if suffix == ".json":
        try:
            data = json.loads("\n".join(lines))
        except json.JSONDecodeError:
            return []
        return [(1, key, str(value)) for key, value in _flatten(data)]
    if suffix == ".toml":
        try:
            data = tomllib.loads("\n".join(lines))
        except tomllib.TOMLDecodeError:
            return []
        return [(1, key, str(value)) for key, value in _flatten(data)]
    result: list[tuple[int, str, str]] = []
    for idx, raw in enumerate(lines, start=1):
        text = raw.strip()
        if not text or text.startswith(("#", ";")):
            continue
        if "=" in text:
            key, value = text.split("=", 1)
        elif ":" in text and not text.startswith(("http:", "https:")):
            key, value = text.split(":", 1)
        else:
            continue
        key = re.sub(r"[^A-Za-z0-9_.:-]+", ".", key.strip()).strip(".")
        if key:
            result.append((idx, key, value.strip()))
    return result


def _flatten(value: object, prefix: str = ""):
    if isinstance(value, dict):
        for key, child in value.items():
            yield from _flatten(child, f"{prefix}.{key}" if prefix else str(key))
    else:
        yield prefix, value


def _value_kind(value: str) -> str:
    if not value:
        return "empty"
    lowered = value.lower()
    if lowered in {"true", "false"}:
        return "boolean"
    if re.fullmatch(r"-?\d+(\.\d+)?", value):
        return "number"
    return "string"
