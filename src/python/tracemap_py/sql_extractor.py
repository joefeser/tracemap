from __future__ import annotations

from pathlib import Path

from .constants import EvidenceTiers, FactTypes, RuleIds, ScannerVersions
from .fact_factory import create_fact, evidence
from .models import CodeFact, ScanManifest
from .sql_text import is_sql_like, operation_name, query_shape_properties, text_hash


def extract_sql_files(repo: Path, manifest: ScanManifest, files: list[Path], gaps: list[str]) -> list[CodeFact]:
    facts: list[CodeFact] = []
    for path in sorted(files):
        rel = str(path.resolve().relative_to(repo.resolve())).replace("\\", "/")
        try:
            text = path.read_text(encoding="utf-8")
        except (OSError, UnicodeDecodeError) as exc:
            gaps.append(f"SqlFileReadFailed: {rel}: {type(exc).__name__}")
            continue
        lines = max(1, text.count("\n") + 1)
        facts.append(
            create_fact(
                manifest,
                FactTypes.SQL_FILE_DECLARED,
                RuleIds.SQL,
                EvidenceTiers.TIER2,
                evidence(rel, 1, lines, "PythonSqlExtractor", ScannerVersions.SQL, text_hash(text)),
                target_symbol=rel,
                contract_element=rel,
                properties={"name": rel, "fileKind": "Sql"},
            )
        )
        if is_sql_like(text):
            span = evidence(rel, 1, lines, "PythonSqlExtractor", ScannerVersions.SQL, text_hash(text))
            facts.append(
                create_fact(
                    manifest,
                    FactTypes.SQL_TEXT_USED,
                    RuleIds.SQL,
                    EvidenceTiers.TIER2,
                    span,
                    target_symbol=rel,
                    contract_element=rel,
                    properties={"textHash": text_hash(text), "textLength": len(text), "operationName": operation_name(text), "sqlSourceKind": "sql-file"},
                )
            )
            pattern_props = query_shape_properties(text, "sql-file")
            if pattern_props.get("operationName") or pattern_props.get("tableName") or pattern_props.get("columnNames"):
                facts.append(
                    create_fact(
                        manifest,
                        FactTypes.QUERY_PATTERN_DETECTED,
                        RuleIds.SQL,
                        EvidenceTiers.TIER2,
                        span,
                        target_symbol=pattern_props.get("tableName") or rel,
                        contract_element=pattern_props.get("tableName") or rel,
                        properties={**pattern_props, "targetSymbol": pattern_props.get("tableName") or rel},
                    )
                )
    return facts
