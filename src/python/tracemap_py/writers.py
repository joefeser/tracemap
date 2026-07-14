from __future__ import annotations

import json
import sqlite3
from pathlib import Path

from .hashes import sha256_hex
from .models import CodeFact, ScanManifest


def write_manifest(path: Path, manifest: ScanManifest) -> None:
    path.write_text(json.dumps(manifest.to_json(), sort_keys=True, indent=2) + "\n", encoding="utf-8")


def write_facts(path: Path, facts: list[CodeFact]) -> None:
    with path.open("w", encoding="utf-8") as handle:
        for fact in sorted(facts, key=lambda item: item.fact_id):
            handle.write(json.dumps(fact.to_json(), sort_keys=True, separators=(",", ":")) + "\n")


def write_sqlite(path: Path, manifest: ScanManifest, facts: list[CodeFact]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(path.suffix + ".tmp")
    if tmp.exists():
        tmp.unlink()
    con = sqlite3.connect(tmp)
    try:
        create_schema(con)
        insert_manifest(con, manifest)
        for fact in facts:
            insert_fact(con, fact)
        con.commit()
    finally:
        con.close()
    tmp.replace(path)


def create_schema(con: sqlite3.Connection) -> None:
    con.executescript(
        """
        create table scan_manifest (
          scan_id text primary key,
          repo text not null,
          commit_sha text not null,
          scanner_version text not null,
          scanned_at text not null,
          analysis_level text not null,
          build_status text not null,
          manifest_json text not null
        );

        create table facts (
          fact_id text primary key,
          scan_id text not null,
          repo text not null,
          commit_sha text not null,
          project_path text,
          fact_type text not null,
          rule_id text not null,
          evidence_tier text not null,
          source_symbol text,
          target_symbol text,
          contract_element text,
          file_path text not null,
          start_line integer not null,
          end_line integer not null,
          snippet_hash text,
          extractor_id text,
          extractor_version text,
          properties_json text not null
        );

        create table symbols (
          scan_id text not null,
          symbol_id text not null,
          language text not null,
          symbol_kind text not null,
          display_name text not null,
          assembly_name text,
          assembly_version text,
          containing_symbol_id text,
          primary key (scan_id, symbol_id)
        );

        create table symbol_occurrences (
          occurrence_id text primary key,
          scan_id text not null,
          symbol_id text not null,
          fact_id text not null,
          role text not null,
          occurrence_kind text not null,
          evidence_tier text not null,
          rule_id text not null,
          file_path text not null,
          start_line integer not null,
          end_line integer not null
        );

        create table fact_symbols (
          fact_id text not null,
          scan_id text not null,
          symbol_id text not null,
          role text not null,
          primary key (fact_id, symbol_id, role)
        );

        create table symbol_relationships (
          relationship_id text primary key,
          scan_id text not null,
          source_symbol_id text not null,
          target_symbol_id text not null,
          relationship_kind text not null,
          rule_id text not null,
          evidence_tier text not null,
          file_path text not null,
          start_line integer not null,
          end_line integer not null
        );

        create table call_edges (
          fact_id text primary key,
          scan_id text not null,
          repo text not null,
          commit_sha text not null,
          evidence_tier text not null,
          rule_id text not null,
          caller_symbol text,
          caller_assembly_name text,
          caller_assembly_version text,
          callee_symbol text not null,
          callee_assembly_name text,
          callee_assembly_version text,
          callee_containing_type text,
          call_kind text,
          file_path text not null,
          start_line integer not null,
          end_line integer not null
        );

        create table object_creations (
          fact_id text primary key,
          scan_id text not null,
          repo text not null,
          commit_sha text not null,
          evidence_tier text not null,
          rule_id text not null,
          caller_symbol text,
          caller_assembly_name text,
          caller_assembly_version text,
          created_type text not null,
          created_type_assembly_name text,
          created_type_assembly_version text,
          constructor_symbol text,
          assigned_to text,
          file_path text not null,
          start_line integer not null,
          end_line integer not null
        );

        create table argument_flows (
          fact_id text primary key,
          scan_id text not null,
          repo text not null,
          commit_sha text not null,
          evidence_tier text not null,
          rule_id text not null,
          caller_symbol text,
          caller_assembly_name text,
          caller_assembly_version text,
          callee_symbol text not null,
          callee_assembly_name text,
          callee_assembly_version text,
          call_kind text,
          parameter_ordinal integer not null,
          parameter_name text not null,
          parameter_type text,
          argument_ordinal integer not null,
          argument_expression_kind text,
          argument_expression_hash text,
          argument_symbol text,
          argument_symbol_kind text,
          argument_type text,
          argument_assembly_name text,
          argument_assembly_version text,
          argument_source_file text,
          argument_source_start_line integer,
          argument_source_end_line integer,
          file_path text not null,
          start_line integer not null,
          end_line integer not null
        );

        create table local_aliases (
          fact_id text primary key,
          scan_id text not null,
          repo text not null,
          commit_sha text not null,
          evidence_tier text not null,
          rule_id text not null,
          containing_symbol text,
          alias_symbol text not null,
          alias_symbol_kind text,
          alias_type text,
          origin_symbol text not null,
          origin_symbol_kind text,
          origin_type text,
          file_path text not null,
          start_line integer not null,
          end_line integer not null
        );

        create table field_aliases (
          fact_id text primary key,
          scan_id text not null,
          repo text not null,
          commit_sha text not null,
          evidence_tier text not null,
          rule_id text not null,
          containing_symbol text,
          field_symbol text not null,
          field_symbol_kind text,
          field_type text,
          origin_symbol text not null,
          origin_symbol_kind text,
          origin_type text,
          file_path text not null,
          start_line integer not null,
          end_line integer not null
        );

        create table parameter_forward_edges (
          fact_id text primary key,
          scan_id text not null,
          repo text not null,
          commit_sha text not null,
          evidence_tier text not null,
          rule_id text not null,
          source_method_symbol text not null,
          source_parameter_symbol text not null,
          source_node_key text not null,
          target_method_symbol text not null,
          target_parameter_name text not null,
          target_parameter_type text,
          target_parameter_symbol text not null,
          target_node_key text not null,
          target_assembly_name text,
          target_assembly_version text,
          file_path text not null,
          start_line integer not null,
          end_line integer not null
        );

        create index ix_facts_type on facts(fact_type);
        create index ix_facts_target_symbol on facts(target_symbol);
        create index ix_facts_contract_element on facts(contract_element);
        create index ix_facts_file on facts(file_path);
        create index ix_symbols_display on symbols(display_name);
        create index ix_symbol_occurrences_symbol on symbol_occurrences(scan_id, symbol_id);
        create index ix_fact_symbols_symbol on fact_symbols(scan_id, symbol_id);
        create index ix_symbol_relationships_source on symbol_relationships(scan_id, source_symbol_id);
        create index ix_symbol_relationships_target on symbol_relationships(scan_id, target_symbol_id);
        create index ix_call_edges_callee on call_edges(callee_symbol);
        create index ix_object_creations_type on object_creations(created_type);
        create index ix_argument_flows_callee on argument_flows(callee_symbol);
        create index ix_local_aliases_alias on local_aliases(containing_symbol, alias_symbol, start_line);
        create index ix_local_aliases_origin on local_aliases(origin_symbol);
        create index ix_field_aliases_field on field_aliases(containing_symbol, field_symbol, start_line);
        create index ix_field_aliases_origin on field_aliases(origin_symbol);
        create index ix_parameter_forward_edges_source on parameter_forward_edges(source_node_key);
        create index ix_parameter_forward_edges_target on parameter_forward_edges(target_node_key);
        create index ix_parameter_forward_edges_source_method on parameter_forward_edges(source_method_symbol);
        create index ix_parameter_forward_edges_target_method on parameter_forward_edges(target_method_symbol);
        """
    )


def insert_manifest(con: sqlite3.Connection, manifest: ScanManifest) -> None:
    con.execute(
        """insert into scan_manifest
           (scan_id, repo, commit_sha, scanner_version, scanned_at, analysis_level, build_status, manifest_json)
           values (?, ?, ?, ?, ?, ?, ?, ?)""",
        (
            manifest.scan_id,
            manifest.repo_name,
            manifest.commit_sha,
            manifest.scanner_version,
            manifest.scanned_at,
            manifest.analysis_level,
            manifest.build_status,
            json.dumps(manifest.to_json(), sort_keys=True),
        ),
    )


def insert_fact(con: sqlite3.Connection, fact: CodeFact) -> None:
    props_json = json.dumps(dict(sorted(fact.properties.items())), sort_keys=True)
    con.execute(
        """insert into facts
           (fact_id, scan_id, repo, commit_sha, project_path, fact_type, rule_id, evidence_tier,
            source_symbol, target_symbol, contract_element, file_path, start_line, end_line, snippet_hash,
            extractor_id, extractor_version, properties_json)
           values (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
        (
            fact.fact_id,
            fact.scan_id,
            fact.repo,
            fact.commit_sha,
            fact.project_path,
            fact.fact_type,
            fact.rule_id,
            fact.evidence_tier,
            fact.source_symbol,
            fact.target_symbol,
            fact.contract_element,
            fact.evidence.file_path,
            fact.evidence.start_line,
            fact.evidence.end_line,
            fact.evidence.snippet_hash,
            fact.evidence.extractor_id,
            fact.evidence.extractor_version,
            props_json,
        ),
    )
    insert_symbol_rows(con, fact)
    insert_derived_rows(con, fact)


def insert_symbol_rows(con: sqlite3.Connection, fact: CodeFact) -> None:
    for role in ("source", "target", "argument", "parameter", "origin", "constructor"):
        symbol_id = fact.properties.get(f"{role}SymbolId")
        if not symbol_id:
            continue
        con.execute(
            """insert or ignore into symbols
               (scan_id, symbol_id, language, symbol_kind, display_name, assembly_name, assembly_version, containing_symbol_id)
               values (?, ?, ?, ?, ?, ?, ?, ?)""",
            (
                fact.scan_id,
                symbol_id,
                fact.properties.get(f"{role}SymbolLanguage", "python"),
                fact.properties.get(f"{role}SymbolKind", "unknown"),
                fact.properties.get(f"{role}SymbolDisplayName", symbol_id),
                fact.properties.get(f"{role}SymbolAssemblyName"),
                fact.properties.get(f"{role}SymbolAssemblyVersion"),
                fact.properties.get(f"{role}ContainingSymbolId"),
            ),
        )
        occurrence_id = "occ-" + sha256_hex(f"{fact.fact_id}|{role}|{symbol_id}", 20)
        con.execute(
            """insert or ignore into symbol_occurrences
               (occurrence_id, scan_id, symbol_id, fact_id, role, occurrence_kind, evidence_tier, rule_id, file_path, start_line, end_line)
               values (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
            (
                occurrence_id,
                fact.scan_id,
                symbol_id,
                fact.fact_id,
                role,
                fact.fact_type,
                fact.evidence_tier,
                fact.rule_id,
                fact.evidence.file_path,
                fact.evidence.start_line,
                fact.evidence.end_line,
            ),
        )
        con.execute(
            "insert or ignore into fact_symbols (fact_id, scan_id, symbol_id, role) values (?, ?, ?, ?)",
            (fact.fact_id, fact.scan_id, symbol_id, role),
        )


def insert_derived_rows(con: sqlite3.Connection, fact: CodeFact) -> None:
    p = fact.properties
    if fact.fact_type == "SymbolRelationship" and p.get("sourceSymbolId") and p.get("targetSymbolId"):
        con.execute(
            """insert or ignore into symbol_relationships
               (relationship_id, scan_id, source_symbol_id, target_symbol_id, relationship_kind, rule_id, evidence_tier, file_path, start_line, end_line)
               values (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
            (fact.fact_id, fact.scan_id, p["sourceSymbolId"], p["targetSymbolId"], p.get("relationshipKind", "Unknown"), fact.rule_id, fact.evidence_tier, fact.evidence.file_path, fact.evidence.start_line, fact.evidence.end_line),
        )
    if fact.fact_type == "CallEdge" and fact.target_symbol:
        con.execute(
            """insert or ignore into call_edges
               (fact_id, scan_id, repo, commit_sha, evidence_tier, rule_id, caller_symbol, caller_assembly_name, caller_assembly_version, callee_symbol, callee_assembly_name, callee_assembly_version, callee_containing_type, call_kind, file_path, start_line, end_line)
               values (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
            (fact.fact_id, fact.scan_id, fact.repo, fact.commit_sha, fact.evidence_tier, fact.rule_id, fact.source_symbol, p.get("sourceSymbolAssemblyName"), p.get("sourceSymbolAssemblyVersion"), fact.target_symbol, p.get("targetSymbolAssemblyName"), p.get("targetSymbolAssemblyVersion"), p.get("containingType"), p.get("callKind"), fact.evidence.file_path, fact.evidence.start_line, fact.evidence.end_line),
        )
    if fact.fact_type == "ObjectCreated" and fact.target_symbol:
        con.execute(
            """insert or ignore into object_creations
               (fact_id, scan_id, repo, commit_sha, evidence_tier, rule_id, caller_symbol, caller_assembly_name, caller_assembly_version, created_type, created_type_assembly_name, created_type_assembly_version, constructor_symbol, assigned_to, file_path, start_line, end_line)
               values (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
            (fact.fact_id, fact.scan_id, fact.repo, fact.commit_sha, fact.evidence_tier, fact.rule_id, fact.source_symbol, p.get("sourceSymbolAssemblyName"), p.get("sourceSymbolAssemblyVersion"), fact.target_symbol, p.get("targetSymbolAssemblyName"), p.get("targetSymbolAssemblyVersion"), p.get("constructorSymbol"), p.get("assignedTo"), fact.evidence.file_path, fact.evidence.start_line, fact.evidence.end_line),
        )
    if fact.fact_type == "ArgumentPassed" and fact.target_symbol:
        con.execute(
            """insert or ignore into argument_flows
               (fact_id, scan_id, repo, commit_sha, evidence_tier, rule_id, caller_symbol, caller_assembly_name, caller_assembly_version, callee_symbol, callee_assembly_name, callee_assembly_version, call_kind, parameter_ordinal, parameter_name, parameter_type, argument_ordinal, argument_expression_kind, argument_expression_hash, argument_symbol, argument_symbol_kind, argument_type, argument_assembly_name, argument_assembly_version, argument_source_file, argument_source_start_line, argument_source_end_line, file_path, start_line, end_line)
               values (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
            (fact.fact_id, fact.scan_id, fact.repo, fact.commit_sha, fact.evidence_tier, fact.rule_id, fact.source_symbol, p.get("sourceSymbolAssemblyName"), p.get("sourceSymbolAssemblyVersion"), fact.target_symbol, p.get("targetSymbolAssemblyName"), p.get("targetSymbolAssemblyVersion"), p.get("callKind"), _int(p.get("parameterOrdinal"), 0), p.get("parameterName", "unknown"), p.get("parameterType"), _int(p.get("argumentOrdinal"), 0), p.get("argumentExpressionKind"), p.get("argumentExpressionHash"), p.get("argumentSymbol"), p.get("argumentSymbolKind"), p.get("argumentType"), p.get("argumentAssemblyName"), p.get("argumentAssemblyVersion"), p.get("argumentSourceFile"), _nullable_int(p.get("argumentSourceStartLine")), _nullable_int(p.get("argumentSourceEndLine")), fact.evidence.file_path, fact.evidence.start_line, fact.evidence.end_line),
        )
        insert_parameter_forward_row(con, fact)
    if fact.fact_type == "LocalAlias" and fact.target_symbol:
        con.execute(
            """insert or ignore into local_aliases
               (fact_id, scan_id, repo, commit_sha, evidence_tier, rule_id, containing_symbol, alias_symbol, alias_symbol_kind, alias_type, origin_symbol, origin_symbol_kind, origin_type, file_path, start_line, end_line)
               values (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
            (fact.fact_id, fact.scan_id, fact.repo, fact.commit_sha, fact.evidence_tier, fact.rule_id, fact.source_symbol, fact.target_symbol, p.get("aliasSymbolKind"), p.get("aliasType"), p.get("originSymbol", p.get("originSymbolId", "unknown")), p.get("originSymbolKind"), p.get("originType"), fact.evidence.file_path, fact.evidence.start_line, fact.evidence.end_line),
        )
    if fact.fact_type == "FieldAlias" and fact.target_symbol:
        con.execute(
            """insert or ignore into field_aliases
               (fact_id, scan_id, repo, commit_sha, evidence_tier, rule_id, containing_symbol, field_symbol, field_symbol_kind, field_type, origin_symbol, origin_symbol_kind, origin_type, file_path, start_line, end_line)
               values (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
            (fact.fact_id, fact.scan_id, fact.repo, fact.commit_sha, fact.evidence_tier, fact.rule_id, fact.source_symbol, p.get("fieldSymbol", fact.target_symbol), p.get("fieldSymbolKind"), p.get("fieldType"), p.get("originSymbol", p.get("originSymbolId", "unknown")), p.get("originSymbolKind"), p.get("originType"), fact.evidence.file_path, fact.evidence.start_line, fact.evidence.end_line),
        )


def insert_parameter_forward_row(con: sqlite3.Connection, fact: CodeFact) -> None:
    p = fact.properties
    if p.get("argumentSymbolKind") != "parameter" or not fact.source_symbol or not fact.target_symbol:
        return
    argument_symbol = p.get("argumentSymbol")
    parameter_name = p.get("parameterName")
    if not argument_symbol or not parameter_name:
        return
    source_parameter_symbol = p.get("sourceParameterSymbol") or f"{fact.source_symbol}({argument_symbol})"
    target_parameter_symbol = p.get("targetParameterSymbol") or f"{fact.target_symbol}({parameter_name})"
    con.execute(
        """insert or ignore into parameter_forward_edges
           (fact_id, scan_id, repo, commit_sha, evidence_tier, rule_id, source_method_symbol, source_parameter_symbol, source_node_key, target_method_symbol, target_parameter_name, target_parameter_type, target_parameter_symbol, target_node_key, target_assembly_name, target_assembly_version, file_path, start_line, end_line)
           values (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
        (
            fact.fact_id,
            fact.scan_id,
            fact.repo,
            fact.commit_sha,
            fact.evidence_tier,
            fact.rule_id,
            fact.source_symbol,
            source_parameter_symbol,
            source_parameter_symbol,
            fact.target_symbol,
            parameter_name,
            p.get("parameterType"),
            target_parameter_symbol,
            target_parameter_symbol,
            p.get("targetAssemblyName"),
            p.get("targetAssemblyVersion"),
            fact.evidence.file_path,
            fact.evidence.start_line,
            fact.evidence.end_line,
        ),
    )


def _int(value: str | None, fallback: int) -> int:
    try:
        return int(value) if value is not None else fallback
    except ValueError:
        return fallback


def _nullable_int(value: str | None) -> int | None:
    try:
        return int(value) if value is not None else None
    except ValueError:
        return None
