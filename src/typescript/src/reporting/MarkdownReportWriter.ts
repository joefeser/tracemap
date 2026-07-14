import fs from "node:fs/promises";
import path from "node:path";
import { CodeFact, FactTypes, ScanResult } from "../facts/Models";
import { hash } from "../util/Hash";

export async function writeMarkdownReport(filePath: string, result: ScanResult): Promise<void> {
  await fs.mkdir(path.dirname(filePath), { recursive: true });
  const factsByType = countBy(result.facts, (fact) => fact.factType);
  const factsByTier = countBy(result.facts, (fact) => fact.evidenceTier);
  const gaps = result.facts.filter((fact) => fact.factType === "AnalysisGap");
  const lines = [
    "# TraceMap TypeScript Scan Report",
    "",
    `- Repo: \`${result.manifest.repoName}\``,
    `- Commit: \`${result.manifest.commitSha}\``,
    `- Analysis level: \`${result.manifest.analysisLevel}\``,
    `- Build status: \`${result.manifest.buildStatus}\``,
    `- Projects: ${result.manifest.projects.length}`,
    `- Facts: ${result.facts.length}`,
    "",
    "## Coverage",
    "",
    result.manifest.analysisLevel === "Level1SemanticAnalysis"
      ? "Semantic analysis completed without known gaps."
      : "Analysis is reduced; no-evidence conclusions must be treated as reduced coverage.",
    "",
    "## Fact Counts",
    "",
    ...table(factsByType),
    "",
    "## Evidence Tiers",
    "",
    ...table(factsByTier),
    "",
    "## Known Gaps",
    "",
    ...(gaps.length === 0 ? ["No known gaps were recorded."] : gaps.slice(0, 25).map((fact) => `- \`${fact.properties.gapKind ?? fact.properties.category ?? "gap"}\` at \`${fact.evidence.filePath}:${fact.evidence.startLine}\``)),
    ...(gaps.length > 25 ? [`- ${gaps.length - 25} additional gaps are preserved in facts.ndjson.`] : []),
    "",
    "## Query Patterns",
    "",
    ...factList(
      result.facts.filter((fact) => fact.factType === FactTypes.QueryPatternDetected),
      formatQueryPattern
    ),
    "",
    "## Object Shapes",
    "",
    ...factList(
      result.facts.filter((fact) => fact.factType === FactTypes.ObjectShapeInferred),
      (fact) => `- \`${fact.properties.objectKind ?? fact.contractElement ?? "object"}\` fields \`${fact.properties.fieldNames ?? "unknown"}\` (${fact.evidenceTier}) at \`${fact.evidence.filePath}:${fact.evidence.startLine}\``
    ),
    "",
    "## Limitations",
    "",
    "- TypeScript `buildStatus = Succeeded` means semantic analysis succeeded; it does not mean target repo tests or bundlers ran.",
    "- Missing dependencies, unresolved path mappings, decorators, dependency injection, and runtime routing can reduce evidence quality.",
    ...(result.facts.some((fact) => fact.factType === FactTypes.QueryPatternDetected)
      ? ["- Query-pattern rows are static shape evidence. They do not prove runtime execution, database schema existence, SQL dialect validity, generated SQL equivalence, or branch feasibility."]
      : []),
    "- Facts store hashes and spans, not raw source snippets."
  ];
  await fs.writeFile(filePath, `${lines.join("\n")}\n`, "utf8");
}

function countBy(facts: readonly CodeFact[], keySelector: (fact: CodeFact) => string): Map<string, number> {
  const counts = new Map<string, number>();
  for (const fact of facts) {
    const key = keySelector(fact);
    counts.set(key, (counts.get(key) ?? 0) + 1);
  }
  return counts;
}

function table(counts: Map<string, number>): string[] {
  return [
    "| Name | Count |",
    "| --- | ---: |",
    ...[...counts.entries()].sort(([left], [right]) => left.localeCompare(right)).map(([key, value]) => `| \`${key}\` | ${value} |`)
  ];
}

function factList(facts: CodeFact[], format: (fact: CodeFact) => string): string[] {
  const selectedFacts = facts
    .sort((left, right) =>
      left.evidence.filePath.localeCompare(right.evidence.filePath)
      || left.evidence.startLine - right.evidence.startLine
      || (left.contractElement ?? "").localeCompare(right.contractElement ?? "")
    )
    .slice(0, 50);
  return selectedFacts.length === 0 ? ["- None found."] : selectedFacts.map(format);
}

function displayFields(fact: CodeFact): string {
  const fields = [
    fact.properties.filterFields,
    fact.properties.sortFields,
    fact.properties.selectFields,
    fact.properties.includeFields,
    fact.properties.mutationFields
  ].filter((value): value is string => typeof value === "string" && value.length > 0);
  return fields.length === 0 ? "none" : [...new Set(fields)].join(";");
}

function formatQueryPattern(fact: CodeFact): string {
  return isSqlShapeQueryPattern(fact) ? formatSqlShapeQueryPattern(fact) : formatQueryBuilderPattern(fact);
}

function isSqlShapeQueryPattern(fact: CodeFact): boolean {
  const value = fact.properties.sqlSourceKind;
  return typeof value === "string" && value.trim().length > 0;
}

function formatSqlShapeQueryPattern(fact: CodeFact): string {
  const operation = displayCodeValue(fact.properties.operationName ?? "unknown");
  const table = displayIdentifierValue(firstPresent(fact.properties.tableName, fact.properties.tableNames), "table", "unknown");
  const columns = displayIdentifierValue(firstPresent(fact.properties.columnNames, fact.properties.fieldNames), "column", "none");
  const sourceKind = displayCodeValue(fact.properties.sqlSourceKind ?? "unknown");
  const shapeHash = displayCodeValue(fact.properties.queryShapeHash ?? "n/a");
  const evidencePath = safePath(fact.evidence.filePath);
  return `- SQL shape \`${operation}\` table \`${table}\` columns \`${columns}\` source \`${sourceKind}\` shape \`${shapeHash}\` rule \`${fact.ruleId}\` (${fact.evidenceTier}) at \`${evidencePath}:${fact.evidence.startLine}\``;
}

function formatQueryBuilderPattern(fact: CodeFact): string {
  const operation = displayCodeValue(fact.properties.operationName ?? fact.contractElement ?? "unknown");
  const patternHash = fact.properties.patternHash;
  const hashPart = patternHash ? ` pattern \`${displayCodeValue(patternHash)}\`` : "";
  const evidencePath = safePath(fact.evidence.filePath);
  return `- Query builder \`${operation}\` fields \`${displayFields(fact)}\`${hashPart} rule \`${fact.ruleId}\` (${fact.evidenceTier}) at \`${evidencePath}:${fact.evidence.startLine}\``;
}

type IdentifierKind = "table" | "column";

function displayIdentifierValue(rawValue: string | undefined, kind: IdentifierKind, missingValue: string): string {
  if (!rawValue || rawValue.trim().length === 0) {
    return missingValue;
  }

  const values = rawValue
    .split(/[,;|]/)
    .map((value) => value.trim())
    .filter((value) => value.length > 0)
    .map((value) => displayIdentifier(value, kind));
  const distinct = [...new Set(values)];
  if (distinct.length === 0) {
    return missingValue;
  }
  if (distinct.length <= 20) {
    return distinct.join(";");
  }
  return `${distinct.slice(0, 20).join(";")};... and ${distinct.length - 20} more`;
}

function displayIdentifier(value: string, kind: IdentifierKind): string {
  return isSafeIdentifier(value, kind) ? value : `unsafe-identifier-hash:${hash(value, 32)}`;
}

function isSafeIdentifier(value: string, kind: IdentifierKind): boolean {
  if (value.length === 0 || value.length > maxIdentifierLength(kind)) {
    return false;
  }
  if (value.includes("://") || value.includes("--") || value.includes("/*") || value.includes("*/")) {
    return false;
  }

  for (const ch of value) {
    const allowed = /[A-Za-z0-9_.-]/.test(ch) || (kind === "table" && ch === " ");
    if (!allowed) {
      return false;
    }
  }

  const tokens = value.split(/[ .-]+/).filter((token) => token.length > 0);
  return !tokens.some((token) => sqlKeywords.has(token.toLowerCase()));
}

function maxIdentifierLength(kind: IdentifierKind): number {
  return kind === "table" ? 100 : 80;
}

function safePath(filePath: string): string {
  if (!filePath || filePath.trim().length === 0) {
    return "n/a";
  }
  const absolute = filePath.startsWith("/")
    || filePath.startsWith("\\")
    || filePath.includes("://")
    || filePath.includes(":/")
    || filePath.includes(":\\")
    || path.isAbsolute(filePath);
  return absolute ? `absolute-path-hash:${hash(filePath, 16)}` : filePath.replace(/\\/g, "/");
}

function displayCodeValue(value: string): string {
  return value.replace(/`/g, "'").replace(/\r?\n/g, " ");
}

function firstPresent(first: string | undefined, second: string | undefined): string | undefined {
  return first && first.trim().length > 0 ? first : second;
}

const sqlKeywords = new Set([
  "select",
  "from",
  "insert",
  "into",
  "values",
  "update",
  "set",
  "delete",
  "where",
  "join",
  "having",
  "group",
  "order",
  "by",
  "union",
  "create",
  "alter",
  "drop",
  "truncate",
  "merge",
  "exec"
]);
