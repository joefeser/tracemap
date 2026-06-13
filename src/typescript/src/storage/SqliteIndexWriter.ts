import fs from "node:fs/promises";
import path from "node:path";
import initSqlJs from "sql.js";
import { CodeFact, ScanManifest } from "../facts/Models";
import { hash } from "../util/Hash";

type SqlValue = string | number | null;

export async function writeSqliteIndex(filePath: string, manifest: ScanManifest, facts: readonly CodeFact[]): Promise<void> {
  await fs.mkdir(path.dirname(filePath), { recursive: true });
  const sqlJs = await initSqlJs({
    locateFile: (file) => findSqlJsFile(file)
  });
  const db = new sqlJs.Database();
  let transactionOpen = false;
  try {
    createSchema(db);
    insertManifest(db, manifest);
    db.run("begin transaction;");
    transactionOpen = true;
    for (const fact of facts) {
      insertFact(db, fact);
    }
    db.run("commit;");
    transactionOpen = false;
    const tempPath = `${filePath}.tmp`;
    await fs.writeFile(tempPath, Buffer.from(db.export()));
    await fs.rename(tempPath, filePath);
  } catch (error) {
    if (transactionOpen) {
      try {
        db.run("rollback;");
      } catch {
        // Best-effort rollback only; the database is still closed in finally.
      }
    }
    throw error;
  } finally {
    db.close();
  }
}

function findSqlJsFile(file: string): string {
  const candidates = [
    path.join(process.cwd(), "node_modules", "sql.js", "dist", file),
    path.join(__dirname, "..", "..", "node_modules", "sql.js", "dist", file),
    path.join(__dirname, "..", "..", "..", "node_modules", "sql.js", "dist", file)
  ];
  for (const candidate of candidates) {
    try {
      if (require("node:fs").existsSync(candidate)) {
        return candidate;
      }
    } catch {
      // Continue to the next deterministic lookup path.
    }
  }
  return candidates[0];
}

function createSchema(db: initSqlJs.Database): void {
  db.run(`
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
    create index ix_local_aliases_origin on local_aliases(origin_symbol);
  `);
}

function insertManifest(db: initSqlJs.Database, manifest: ScanManifest): void {
  db.run(
    `insert into scan_manifest (scan_id, repo, commit_sha, scanner_version, scanned_at, analysis_level, build_status, manifest_json)
     values (?, ?, ?, ?, ?, ?, ?, ?);`,
    [
      manifest.scanId,
      manifest.repoName,
      manifest.commitSha,
      manifest.scannerVersion,
      manifest.scannedAt,
      manifest.analysisLevel,
      manifest.buildStatus,
      JSON.stringify(manifest)
    ]
  );
}

function insertFact(db: initSqlJs.Database, fact: CodeFact): void {
  db.run(
    `insert into facts (
      fact_id, scan_id, repo, commit_sha, project_path, fact_type, rule_id, evidence_tier,
      source_symbol, target_symbol, contract_element, file_path, start_line, end_line, snippet_hash, properties_json
    ) values (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);`,
    [
      fact.factId,
      fact.scanId,
      fact.repo,
      fact.commitSha,
      fact.projectPath,
      fact.factType,
      fact.ruleId,
      fact.evidenceTier,
      fact.sourceSymbol,
      fact.targetSymbol,
      fact.contractElement,
      fact.evidence.filePath,
      fact.evidence.startLine,
      fact.evidence.endLine,
      fact.evidence.snippetHash,
      JSON.stringify(fact.properties)
    ]
  );
  insertSymbolRows(db, fact);
  insertDerivedRows(db, fact);
}

function insertSymbolRows(db: initSqlJs.Database, fact: CodeFact): void {
  const roles: Array<["source" | "target" | "argument" | "parameter" | "origin", string]> = [
    ["source", "sourceSymbolId"],
    ["target", "targetSymbolId"],
    ["argument", "argumentSymbolId"],
    ["parameter", "parameterSymbolId"],
    ["origin", "originSymbolId"]
  ];
  for (const [role, key] of roles) {
    const symbolId = fact.properties[key];
    if (!symbolId) {
      continue;
    }
    const displayName = fact.properties[`${role}DisplayName`] ?? fact.properties[`${role}Symbol`] ?? symbolId;
    insertSymbol(db, fact, role, symbolId, displayName);
  }
}

function insertSymbol(db: initSqlJs.Database, fact: CodeFact, role: string, symbolId: string, displayName: string): void {
  db.run(
    `insert or ignore into symbols (scan_id, symbol_id, language, symbol_kind, display_name, assembly_name, assembly_version, containing_symbol_id)
     values (?, ?, ?, ?, ?, ?, ?, ?);`,
    [
      fact.scanId,
      symbolId,
      "typescript",
      fact.properties[`${role}SymbolKind`] ?? "unknown",
      displayName,
      fact.properties[`${role}AssemblyName`] ?? fact.properties.packageName ?? null,
      fact.properties[`${role}AssemblyVersion`] ?? fact.properties.packageVersion ?? null,
      fact.properties[`${role}ContainingSymbolId`] ?? null
    ]
  );
  db.run(
    `insert or ignore into symbol_occurrences (occurrence_id, scan_id, symbol_id, fact_id, role, occurrence_kind, evidence_tier, rule_id, file_path, start_line, end_line)
     values (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);`,
    [
      `occ-${hash(`${fact.factId}|${role}|${symbolId}`, 20)}`,
      fact.scanId,
      symbolId,
      fact.factId,
      role,
      fact.factType,
      fact.evidenceTier,
      fact.ruleId,
      fact.evidence.filePath,
      fact.evidence.startLine,
      fact.evidence.endLine
    ]
  );
  db.run(
    `insert or ignore into fact_symbols (fact_id, scan_id, symbol_id, role) values (?, ?, ?, ?);`,
    [fact.factId, fact.scanId, symbolId, role]
  );
}

function insertDerivedRows(db: initSqlJs.Database, fact: CodeFact): void {
  if (fact.factType === "SymbolRelationship" && fact.properties.sourceSymbolId && fact.properties.targetSymbolId) {
    db.run(
      `insert or ignore into symbol_relationships (relationship_id, scan_id, source_symbol_id, target_symbol_id, relationship_kind, rule_id, evidence_tier, file_path, start_line, end_line)
       values (?, ?, ?, ?, ?, ?, ?, ?, ?, ?);`,
      [
        fact.factId,
        fact.scanId,
        fact.properties.sourceSymbolId,
        fact.properties.targetSymbolId,
        fact.properties.relationshipKind ?? "Unknown",
        fact.ruleId,
        fact.evidenceTier,
        fact.evidence.filePath,
        fact.evidence.startLine,
        fact.evidence.endLine
      ]
    );
  }

  if (fact.factType === "CallEdge" && fact.targetSymbol) {
    db.run(
      `insert or ignore into call_edges (fact_id, scan_id, repo, commit_sha, evidence_tier, rule_id, caller_symbol, caller_assembly_name, caller_assembly_version, callee_symbol, callee_assembly_name, callee_assembly_version, callee_containing_type, call_kind, file_path, start_line, end_line)
       values (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);`,
      [
        fact.factId,
        fact.scanId,
        fact.repo,
        fact.commitSha,
        fact.evidenceTier,
        fact.ruleId,
        fact.sourceSymbol,
        fact.properties.sourceAssemblyName ?? null,
        fact.properties.sourceAssemblyVersion ?? null,
        fact.targetSymbol,
        fact.properties.targetAssemblyName ?? fact.properties.packageName ?? null,
        fact.properties.targetAssemblyVersion ?? fact.properties.packageVersion ?? null,
        fact.properties.containingType ?? null,
        fact.properties.callKind ?? null,
        fact.evidence.filePath,
        fact.evidence.startLine,
        fact.evidence.endLine
      ]
    );
  }

  if (fact.factType === "ObjectCreated" && fact.targetSymbol) {
    db.run(
      `insert or ignore into object_creations (fact_id, scan_id, repo, commit_sha, evidence_tier, rule_id, caller_symbol, caller_assembly_name, caller_assembly_version, created_type, created_type_assembly_name, created_type_assembly_version, constructor_symbol, assigned_to, file_path, start_line, end_line)
       values (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);`,
      [
        fact.factId,
        fact.scanId,
        fact.repo,
        fact.commitSha,
        fact.evidenceTier,
        fact.ruleId,
        fact.sourceSymbol,
        fact.properties.sourceAssemblyName ?? null,
        fact.properties.sourceAssemblyVersion ?? null,
        fact.targetSymbol,
        fact.properties.targetAssemblyName ?? fact.properties.packageName ?? null,
        fact.properties.targetAssemblyVersion ?? fact.properties.packageVersion ?? null,
        fact.properties.constructorSymbol ?? null,
        fact.properties.assignedTo ?? null,
        fact.evidence.filePath,
        fact.evidence.startLine,
        fact.evidence.endLine
      ]
    );
  }

  if (fact.factType === "ArgumentPassed" && fact.targetSymbol) {
    db.run(
      `insert or ignore into argument_flows (fact_id, scan_id, repo, commit_sha, evidence_tier, rule_id, caller_symbol, caller_assembly_name, caller_assembly_version, callee_symbol, callee_assembly_name, callee_assembly_version, call_kind, parameter_ordinal, parameter_name, parameter_type, argument_ordinal, argument_expression_kind, argument_expression_hash, argument_symbol, argument_symbol_kind, argument_type, argument_assembly_name, argument_assembly_version, argument_source_file, argument_source_start_line, argument_source_end_line, file_path, start_line, end_line)
       values (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);`,
      [
        fact.factId,
        fact.scanId,
        fact.repo,
        fact.commitSha,
        fact.evidenceTier,
        fact.ruleId,
        fact.sourceSymbol,
        fact.properties.sourceAssemblyName ?? null,
        fact.properties.sourceAssemblyVersion ?? null,
        fact.targetSymbol,
        fact.properties.targetAssemblyName ?? fact.properties.packageName ?? null,
        fact.properties.targetAssemblyVersion ?? fact.properties.packageVersion ?? null,
        fact.properties.callKind ?? null,
        intProperty(fact, "parameterOrdinal", 0),
        fact.properties.parameterName ?? "unknown",
        fact.properties.parameterType ?? null,
        intProperty(fact, "argumentOrdinal", 0),
        fact.properties.argumentExpressionKind ?? null,
        fact.properties.argumentExpressionHash ?? null,
        fact.properties.argumentSymbol ?? null,
        fact.properties.argumentSymbolKind ?? null,
        fact.properties.argumentType ?? null,
        fact.properties.argumentAssemblyName ?? null,
        fact.properties.argumentAssemblyVersion ?? null,
        fact.properties.argumentSourceFile ?? null,
        nullableIntProperty(fact, "argumentSourceStartLine"),
        nullableIntProperty(fact, "argumentSourceEndLine"),
        fact.evidence.filePath,
        fact.evidence.startLine,
        fact.evidence.endLine
      ]
    );
  }

  if (fact.factType === "LocalAlias" && fact.targetSymbol) {
    db.run(
      `insert or ignore into local_aliases (fact_id, scan_id, repo, commit_sha, evidence_tier, rule_id, containing_symbol, alias_symbol, alias_symbol_kind, alias_type, origin_symbol, origin_symbol_kind, origin_type, file_path, start_line, end_line)
       values (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);`,
      [
        fact.factId,
        fact.scanId,
        fact.repo,
        fact.commitSha,
        fact.evidenceTier,
        fact.ruleId,
        fact.sourceSymbol,
        fact.targetSymbol,
        fact.properties.aliasSymbolKind ?? null,
        fact.properties.aliasType ?? null,
        fact.properties.originSymbol ?? fact.properties.originSymbolId ?? "unknown",
        fact.properties.originSymbolKind ?? null,
        fact.properties.originType ?? null,
        fact.evidence.filePath,
        fact.evidence.startLine,
        fact.evidence.endLine
      ]
    );
  }
}

function intProperty(fact: CodeFact, key: string, fallback: number): number {
  const value = Number.parseInt(fact.properties[key] ?? "", 10);
  return Number.isFinite(value) ? value : fallback;
}

function nullableIntProperty(fact: CodeFact, key: string): SqlValue {
  const value = Number.parseInt(fact.properties[key] ?? "", 10);
  return Number.isFinite(value) ? value : null;
}
