import fs from "node:fs/promises";
import fsSync from "node:fs";
import path from "node:path";
import initSqlJs from "sql.js";

export interface ExportOptions {
  indexPath: string;
  outputPath: string;
  format: "json" | "mermaid";
}

export interface ExportResult {
  outputPath: string;
  format: "json" | "mermaid";
  factCount: number;
  relationshipCount: number;
  callEdgeCount: number;
}

interface CountRow {
  name: string;
  count: number;
}

interface RelationshipRow {
  sourceSymbolId: string;
  sourceDisplayName: string;
  targetSymbolId: string;
  targetDisplayName: string;
  relationshipKind: string;
  ruleId: string;
  evidenceTier: string;
  filePath: string;
  startLine: number;
  endLine: number;
}

interface CallEdgeRow {
  callerSymbol: string | null;
  calleeSymbol: string;
  calleeContainingType: string | null;
  callKind: string | null;
  ruleId: string;
  evidenceTier: string;
  filePath: string;
  startLine: number;
  endLine: number;
}

interface ExportDocument {
  version: string;
  generatedAt: string;
  manifest: unknown;
  factsByType: CountRow[];
  factsByTier: CountRow[];
  factsByRule: CountRow[];
  relationships: RelationshipRow[];
  callEdges: CallEdgeRow[];
}

export async function exportIndex(options: ExportOptions): Promise<ExportResult> {
  const format = normalizeFormat(options.format);
  const sqlJs = await initSqlJs({ locateFile: (file) => findSqlJsFile(file) });
  const bytes = await fs.readFile(options.indexPath);
  const db = new sqlJs.Database(bytes);
  const document = readDocument(db);
  const outputPath = outputFile(options.outputPath, format);
  await fs.mkdir(path.dirname(outputPath), { recursive: true });
  if (format === "json") {
    await fs.writeFile(outputPath, `${JSON.stringify(document, null, 2)}\n`, "utf8");
  } else {
    await fs.writeFile(outputPath, renderMermaid(document), "utf8");
  }
  db.close();
  return {
    outputPath,
    format,
    factCount: document.factsByType.reduce((sum, row) => sum + row.count, 0),
    relationshipCount: document.relationships.length,
    callEdgeCount: document.callEdges.length
  };
}

function readDocument(db: initSqlJs.Database): ExportDocument {
  return {
    version: "1.0",
    generatedAt: new Date().toISOString(),
    manifest: JSON.parse(singleValue(db, "select manifest_json from scan_manifest order by scanned_at desc limit 1;")),
    factsByType: readCounts(db, "fact_type"),
    factsByTier: readCounts(db, "evidence_tier"),
    factsByRule: readCounts(db, "rule_id"),
    relationships: tableExists(db, "symbol_relationships") ? readRelationships(db) : [],
    callEdges: tableExists(db, "call_edges") ? readCallEdges(db) : []
  };
}

function readCounts(db: initSqlJs.Database, column: string): CountRow[] {
  const result = db.exec(`select ${column}, count(*) from facts group by ${column} order by ${column};`)[0];
  if (!result) {
    return [];
  }
  return result.values.map((row) => ({ name: String(row[0]), count: Number(row[1]) }));
}

function readRelationships(db: initSqlJs.Database): RelationshipRow[] {
  const result = db.exec(`
    select
      r.source_symbol_id,
      coalesce(source.display_name, r.source_symbol_id) as source_display_name,
      r.target_symbol_id,
      coalesce(target.display_name, r.target_symbol_id) as target_display_name,
      r.relationship_kind,
      r.rule_id,
      r.evidence_tier,
      r.file_path,
      r.start_line,
      r.end_line
    from symbol_relationships r
    left join symbols source on source.scan_id = r.scan_id and source.symbol_id = r.source_symbol_id
    left join symbols target on target.scan_id = r.scan_id and target.symbol_id = r.target_symbol_id
    order by source_display_name, relationship_kind, target_display_name, file_path, start_line;
  `)[0];
  return (result?.values ?? []).map((row) => ({
    sourceSymbolId: String(row[0]),
    sourceDisplayName: String(row[1]),
    targetSymbolId: String(row[2]),
    targetDisplayName: String(row[3]),
    relationshipKind: String(row[4]),
    ruleId: String(row[5]),
    evidenceTier: String(row[6]),
    filePath: String(row[7]),
    startLine: Number(row[8]),
    endLine: Number(row[9])
  }));
}

function readCallEdges(db: initSqlJs.Database): CallEdgeRow[] {
  const result = db.exec(`
    select caller_symbol,
           callee_symbol,
           callee_containing_type,
           call_kind,
           rule_id,
           evidence_tier,
           file_path,
           start_line,
           end_line
    from call_edges
    order by coalesce(caller_symbol, ''), callee_symbol, file_path, start_line;
  `)[0];
  return (result?.values ?? []).map((row) => ({
    callerSymbol: row[0] === null ? null : String(row[0]),
    calleeSymbol: String(row[1]),
    calleeContainingType: row[2] === null ? null : String(row[2]),
    callKind: row[3] === null ? null : String(row[3]),
    ruleId: String(row[4]),
    evidenceTier: String(row[5]),
    filePath: String(row[6]),
    startLine: Number(row[7]),
    endLine: Number(row[8])
  }));
}

function renderMermaid(document: ExportDocument): string {
  const lines = ["flowchart TD"];
  const ids = new Map<string, string>();
  for (const row of document.relationships) {
    const source = nodeId(ids, row.sourceSymbolId);
    const target = nodeId(ids, row.targetSymbolId);
    lines.push(`  ${source}["${escapeLabel(row.sourceDisplayName)}"] -->|${escapeLabel(row.relationshipKind)}| ${target}["${escapeLabel(row.targetDisplayName)}"]`);
  }
  for (const row of document.callEdges.slice(0, 500)) {
    const callerKey = row.callerSymbol ?? "(unknown caller)";
    const caller = nodeId(ids, callerKey);
    const callee = nodeId(ids, row.calleeSymbol);
    lines.push(`  ${caller}["${escapeLabel(callerKey)}"] -.->|calls| ${callee}["${escapeLabel(row.calleeSymbol)}"]`);
  }
  if (lines.length === 1) {
    lines.push('  empty["No relationship or call-edge rows were exported"]');
  }
  return `${lines.join("\n")}\n`;
}

function singleValue(db: initSqlJs.Database, sql: string): string {
  const result = db.exec(sql)[0];
  const value = result?.values[0]?.[0];
  if (typeof value !== "string") {
    throw new Error("TraceMap index does not contain the expected export data.");
  }
  return value;
}

function tableExists(db: initSqlJs.Database, table: string): boolean {
  const statement = db.prepare("select count(*) from sqlite_master where type = 'table' and name = ?;");
  statement.bind([table]);
  try {
    return statement.step() && Number(statement.get()[0]) > 0;
  } finally {
    statement.free();
  }
}

function nodeId(ids: Map<string, string>, key: string): string {
  const existing = ids.get(key);
  if (existing) {
    return existing;
  }
  const next = `n${ids.size + 1}`;
  ids.set(key, next);
  return next;
}

function outputFile(outputPath: string, format: "json" | "mermaid"): string {
  const fullPath = path.resolve(outputPath);
  return path.extname(fullPath) ? fullPath : path.join(fullPath, format === "json" ? "index-export.json" : "relationships.mmd");
}

function normalizeFormat(format: string): "json" | "mermaid" {
  const normalized = format.toLowerCase();
  if (normalized === "json") {
    return "json";
  }
  if (normalized === "mermaid" || normalized === "mmd") {
    return "mermaid";
  }
  throw new Error("export --format must be json or mermaid");
}

function escapeLabel(value: string): string {
  return value.replaceAll("\\", "\\\\").replaceAll('"', '\\"').replaceAll("\n", " ").replaceAll("\r", " ");
}

function findSqlJsFile(file: string): string {
  const candidates = [
    path.join(process.cwd(), "node_modules", "sql.js", "dist", file),
    path.join(__dirname, "..", "..", "node_modules", "sql.js", "dist", file),
    path.join(__dirname, "..", "..", "..", "node_modules", "sql.js", "dist", file)
  ];
  return candidates.find((candidate) => fsSync.existsSync(candidate)) ?? candidates[0];
}
