import fs from "node:fs/promises";
import path from "node:path";
import { createHash } from "node:crypto";
import { createReadStream } from "node:fs";
import { CodeFact, EvidenceTiers, FactTypes, ScanOptions, ScanResult } from "../facts/Models";
import { scan } from "../scan/ScanEngine";

export const base44PacketSchemaVersion = "tracemap.base44.static-evidence.v1";
export const base44CoverageGapSchemaVersion = "tracemap.base44.coverage-gap.v1";

export const base44CoverageGapCategories = [
  "entity",
  "function",
  "auth",
  "http-integration",
  "storage",
  "provider",
  "unknown"
] as const;

export type Base44CoverageGapCategory = typeof base44CoverageGapCategories[number];

export interface Base44CoverageGap {
  gapId: string;
  surface: string;
  category: Base44CoverageGapCategory;
  factId: string;
  ruleId: string;
  evidenceTier: typeof EvidenceTiers.Tier4Unknown;
}

type CoverageGapFact = Pick<CodeFact,
  "factId" | "factType" | "ruleId" | "evidenceTier" | "targetSymbol" | "contractElement" | "properties">;

export interface Base44EvidenceOptions extends ScanOptions {
  acceptedSourceSha256: string;
  acceptedTreeSha256: string;
  coverageLabel: string;
}

export interface Base44EvidencePacket {
  schemaVersion: typeof base44PacketSchemaVersion;
  source: {
    repo: string;
    commitSha: string;
    acceptedSourceSha256: string;
    acceptedTreeSha256: string;
    scanRootRelativePath: string;
  };
  scanner: { version: string; scanId: string };
  coverage: {
    label: string;
    analysisLevel: string;
    buildStatus: string;
    knownGaps: string[];
    gapSchemaVersion: typeof base44CoverageGapSchemaVersion;
    gaps: Base44CoverageGap[];
    ruleIds: string[];
    extractorIdentities: string[];
    evidenceTiers: string[];
  };
  artifacts: Record<string, { sha256: string }>;
  facts: Base44PacketFact[];
  limitations: string[];
}

export interface Base44PacketFact {
  factId: string;
  repo: string;
  commitSha: string;
  factType: string;
  ruleId: string;
  evidenceTier: string;
  targetSymbol: string | null;
  contractElement: string | null;
  lineSpan: { startLine: number; endLine: number };
  evidence: CodeFact["evidence"];
  properties: Record<string, string>;
}

export interface Base44Diff {
  schemaVersion: "tracemap.base44.static-diff.v1";
  before: Base44EvidencePacket["source"] & { coverageLabel: string };
  after: Base44EvidencePacket["source"] & { coverageLabel: string };
  coverageReduced: boolean;
  coverageEvidence: {
    beforeAnalysisLevel: string;
    afterAnalysisLevel: string;
    addedKnownGaps: string[];
    removedKnownGaps: string[];
  };
  added: Base44PacketFact[];
  removed: Base44PacketFact[];
  unchangedCount: number;
  limitations: string[];
}

export async function buildBase44Evidence(options: Base44EvidenceOptions): Promise<{ packet: Base44EvidencePacket; result: ScanResult }> {
  requireSha(options.acceptedSourceSha256, "--accepted-source-sha256");
  requireSha(options.acceptedTreeSha256, "--accepted-tree-sha256");
  if (!options.coverageLabel.trim()) throw new Error("--coverage-label must be non-empty");
  const result = await scan(options);
  const base44Facts = result.facts.filter((fact) => fact.factType.startsWith("Base44"));
  const artifacts: Base44EvidencePacket["artifacts"] = {};
  for (const name of ["scan-manifest.json", "facts.ndjson", "index.sqlite", "report.md", "logs/analyzer.log"]) {
    artifacts[name] = { sha256: await sha256File(path.join(options.outputPath, name)) };
  }
  const packet: Base44EvidencePacket = {
    schemaVersion: base44PacketSchemaVersion,
    source: {
      repo: result.manifest.repoName,
      commitSha: result.manifest.commitSha,
      acceptedSourceSha256: options.acceptedSourceSha256.toLowerCase(),
      acceptedTreeSha256: options.acceptedTreeSha256.toLowerCase(),
      scanRootRelativePath: result.manifest.scanRootRelativePath ?? "."
    },
    scanner: { version: result.manifest.scannerVersion, scanId: result.manifest.scanId },
    coverage: {
      label: options.coverageLabel,
      analysisLevel: result.manifest.analysisLevel,
      buildStatus: result.manifest.buildStatus,
      knownGaps: [...result.manifest.knownGaps].sort(),
      gapSchemaVersion: base44CoverageGapSchemaVersion,
      gaps: buildCoverageGaps(base44Facts, {
        repo: result.manifest.repoName,
        commitSha: result.manifest.commitSha,
        acceptedSourceSha256: options.acceptedSourceSha256.toLowerCase(),
        acceptedTreeSha256: options.acceptedTreeSha256.toLowerCase()
      }),
      ruleIds: unique(base44Facts.map((fact) => fact.ruleId)),
      extractorIdentities: unique(base44Facts.map((fact) => `${fact.evidence.extractorId}@${fact.evidence.extractorVersion}`)),
      evidenceTiers: unique(base44Facts.map((fact) => fact.evidenceTier))
    },
    artifacts,
    facts: base44Facts.map(packetFact),
    limitations: [
      "Static evidence does not prove bundling, browser execution, route reachability, runtime behavior, IAM or secret access, provider delivery, or migration completion.",
      "Absence is meaningful only within the declared coverage label and known-gap set.",
      "Dynamic imports, computed names, dynamic URLs, generated files outside inventory, and runtime-created bindings may require runtime evidence."
    ]
  };
  validateCoverageGaps(packet);
  await fs.writeFile(path.join(options.outputPath, "base44-evidence.json"), stableJson(packet), "utf8");
  await fs.writeFile(path.join(options.outputPath, "base44-evidence.md"), packetMarkdown(packet), "utf8");
  await fs.writeFile(path.join(options.outputPath, "base44-evidence.html"), packetHtml(packet), "utf8");
  return { packet, result };
}

export async function diffBase44Evidence(beforePath: string, afterPath: string, outputPath: string): Promise<Base44Diff> {
  const before = await readPacket(beforePath);
  const after = await readPacket(afterPath);
  const beforeByKey = new Map(before.facts.map((fact) => [factKey(fact), fact]));
  const afterByKey = new Map(after.facts.map((fact) => [factKey(fact), fact]));
  const added = [...afterByKey.entries()].filter(([key]) => !beforeByKey.has(key)).map(([, fact]) => fact);
  const removed = [...beforeByKey.entries()].filter(([key]) => !afterByKey.has(key)).map(([, fact]) => fact);
  const addedKnownGaps = after.coverage.knownGaps.filter((gap) => !before.coverage.knownGaps.includes(gap)).sort();
  const removedKnownGaps = before.coverage.knownGaps.filter((gap) => !after.coverage.knownGaps.includes(gap)).sort();
  const coverageReduced = addedKnownGaps.length > 0 || tierRank(after.coverage.analysisLevel) > tierRank(before.coverage.analysisLevel);
  const coverageEvidence = {
    beforeAnalysisLevel: before.coverage.analysisLevel,
    afterAnalysisLevel: after.coverage.analysisLevel,
    addedKnownGaps,
    removedKnownGaps
  };
  const diff: Base44Diff = {
    schemaVersion: "tracemap.base44.static-diff.v1",
    before: { ...before.source, coverageLabel: before.coverage.label },
    after: { ...after.source, coverageLabel: after.coverage.label },
    coverageReduced,
    coverageEvidence,
    added,
    removed,
    unchangedCount: [...afterByKey.keys()].filter((key) => beforeByKey.has(key)).length,
    limitations: [
      "This is a deterministic static-fact delta, not runtime proof.",
      ...(coverageReduced ? ["Coverage was reduced; missing facts must not be interpreted as clean absence."] : [])
    ]
  };
  await fs.mkdir(path.dirname(path.resolve(outputPath)), { recursive: true });
  await fs.writeFile(outputPath, stableJson(diff), "utf8");
  const markdownPath = outputPath.toLowerCase().endsWith(".json") ? outputPath.replace(/\.json$/i, ".md") : `${outputPath}.md`;
  await fs.writeFile(markdownPath, diffMarkdown(diff), "utf8");
  return diff;
}

function packetFact(fact: CodeFact): Base44PacketFact {
  return {
    factId: fact.factId,
    repo: fact.repo,
    commitSha: fact.commitSha,
    factType: fact.factType,
    ruleId: fact.ruleId,
    evidenceTier: fact.evidenceTier,
    targetSymbol: fact.targetSymbol,
    contractElement: fact.contractElement,
    lineSpan: { startLine: fact.evidence.startLine, endLine: fact.evidence.endLine },
    evidence: fact.evidence,
    properties: fact.properties
  };
}

async function readPacket(filePath: string): Promise<Base44EvidencePacket> {
  const value = JSON.parse(await fs.readFile(filePath, "utf8")) as Base44EvidencePacket;
  if (value.schemaVersion !== base44PacketSchemaVersion || !Array.isArray(value.facts)) throw new Error(`Unsupported Base44 evidence packet: ${filePath}`);
  validateCoverageGaps(value);
  return value;
}

function buildCoverageGaps(
  facts: readonly CoverageGapFact[],
  source: Pick<Base44EvidencePacket["source"], "repo" | "commitSha" | "acceptedSourceSha256" | "acceptedTreeSha256">
): Base44CoverageGap[] {
  const gaps = facts
    .filter((fact) => fact.evidenceTier === EvidenceTiers.Tier4Unknown)
    .map((fact) => coverageGapForFact(fact, source))
    .sort(compareCoverageGaps);
  if (new Set(gaps.map((gap) => gap.gapId)).size !== gaps.length) {
    throw new Error("Base44 coverage gaps contain a duplicate gapId");
  }
  if (new Set(gaps.map((gap) => gap.factId)).size !== gaps.length) {
    throw new Error("Base44 coverage gaps contain a duplicate factId");
  }
  return gaps;
}

function coverageGapForFact(
  fact: CoverageGapFact,
  source: Pick<Base44EvidencePacket["source"], "repo" | "commitSha" | "acceptedSourceSha256" | "acceptedTreeSha256">
): Base44CoverageGap {
  const category = coverageGapCategory(fact);
  const surface = coverageGapSurface(fact, category);
  const identity = JSON.stringify([
    base44CoverageGapSchemaVersion,
    source.repo,
    source.commitSha,
    source.acceptedSourceSha256,
    source.acceptedTreeSha256,
    fact.factId,
    category,
    surface
  ]);
  return {
    gapId: `gap-${createHash("sha256").update(identity, "utf8").digest("hex")}`,
    surface,
    category,
    factId: fact.factId,
    ruleId: fact.ruleId,
    evidenceTier: EvidenceTiers.Tier4Unknown
  };
}

function coverageGapCategory(fact: Pick<CodeFact, "factType" | "properties">): Base44CoverageGapCategory {
  switch (fact.factType) {
    case FactTypes.Base44EntityOperation:
    case FactTypes.Base44EntityPayload:
    case FactTypes.Base44EntityQuery:
      return "entity";
    case FactTypes.Base44FunctionInvocation:
    case FactTypes.Base44FunctionSurface:
      return "function";
    case FactTypes.Base44HttpTarget:
      return "http-integration";
    case FactTypes.Base44MigrationSurface:
      return "storage";
    case FactTypes.Base44CustomerBoundary:
      if (fact.properties.surfaceKind === "entity") return "entity";
      if (fact.properties.surfaceKind === "function") return "function";
      return "unknown";
    case FactTypes.Base44SdkPrimitive:
      return capabilityGapCategory(fact.properties.capability ?? "");
    default:
      return "unknown";
  }
}

function capabilityGapCategory(capability: string): Base44CoverageGapCategory {
  const normalized = capability.replace(/^asServiceRole\./, "");
  const parts = normalized.split(".");
  const root = parts[0]?.toLowerCase() ?? "";
  if (root === "entities") return "entity";
  if (root === "functions") return "function";
  if (["auth", "users", "sso"].includes(root)) return "auth";
  if (root === "storage") return "storage";
  if (root === "integrations" && parts[1]?.toLowerCase() === "core") {
    const operation = parts[2]?.toLowerCase() ?? "";
    if (["uploadfile", "uploadprivatefile", "createfilesignedurl", "extractdatafromuploadedfile"].includes(operation)) {
      return "storage";
    }
    if (["invokellm", "generateimage", "generatespeech", "generatevideo", "sendemail", "texttospeech", "transcribe"].includes(operation)) {
      return "provider";
    }
  }
  if (["integrations", "connectors", "analytics", "applogs"].includes(root)) return "http-integration";
  return "unknown";
}

function coverageGapSurface(
  fact: Pick<CodeFact, "factType" | "targetSymbol" | "contractElement" | "properties">,
  category: Base44CoverageGapCategory
): string {
  const capability = fact.properties.capability?.trim();
  if (capability) return capability;
  if (category === "entity") {
    const entity = fact.properties.entityName?.trim() || fact.targetSymbol?.trim() || "unknown";
    const operation = fact.properties.operationName?.trim() || "unknown";
    return `entities.${entity}.${operation}`;
  }
  if (category === "function") {
    const name = fact.properties.functionName?.trim() || fact.targetSymbol?.trim() || "unknown";
    return `functions.${name}`;
  }
  if (category === "storage" && fact.factType === FactTypes.Base44MigrationSurface) {
    return `storage.migration.${fact.targetSymbol?.trim() || "unknown"}`;
  }
  const target = fact.targetSymbol?.trim() || fact.contractElement?.trim() || "unknown";
  return `${category}.${fact.factType}.${target}`;
}

function validateCoverageGaps(packet: Base44EvidencePacket): void {
  if (packet.coverage?.gapSchemaVersion !== base44CoverageGapSchemaVersion) {
    throw new Error(`Unsupported Base44 coverage gap schema: ${packet.coverage?.gapSchemaVersion ?? "missing"}`);
  }
  if (!Array.isArray(packet.coverage.gaps)) {
    throw new Error("Base44 evidence packet coverage.gaps must be an array");
  }
  const tier4Facts = packet.facts.filter((fact) => fact.evidenceTier === EvidenceTiers.Tier4Unknown);
  const expected = buildCoverageGaps(tier4Facts, packet.source);
  const actual = [...packet.coverage.gaps].sort(compareCoverageGaps);
  if (new Set(actual.map((gap) => gap.gapId)).size !== actual.length) {
    throw new Error("Base44 coverage gaps contain a duplicate gapId");
  }
  if (new Set(actual.map((gap) => gap.factId)).size !== actual.length) {
    throw new Error("Base44 coverage gaps contain a duplicate factId");
  }
  for (const [index, gap] of actual.entries()) {
    if (!/^gap-[0-9a-f]{64}$/.test(gap.gapId)) throw new Error(`Base44 coverage gap ${index} has an invalid gapId`);
    if (!base44CoverageGapCategories.includes(gap.category)) throw new Error(`Base44 coverage gap ${index} has an invalid category`);
    if (typeof gap.surface !== "string" || gap.surface.length === 0) throw new Error(`Base44 coverage gap ${index} has an invalid surface`);
    if (!/^fact-[0-9a-f]{20}$/.test(gap.factId)) throw new Error(`Base44 coverage gap ${index} has an invalid factId`);
    if (!gap.ruleId.startsWith("base44.")) throw new Error(`Base44 coverage gap ${index} has an invalid ruleId`);
    if (gap.evidenceTier !== EvidenceTiers.Tier4Unknown) throw new Error(`Base44 coverage gap ${index} must retain Tier4Unknown`);
  }
  if (JSON.stringify(actual) !== JSON.stringify(expected)) {
    throw new Error("Base44 coverage gaps do not match the source-bound Tier4 fact set exactly once");
  }
}

function compareCoverageGaps(left: Base44CoverageGap, right: Base44CoverageGap): number {
  return left.gapId.localeCompare(right.gapId);
}

function factKey(fact: Base44PacketFact): string {
  const sortedProperties = Object.fromEntries(Object.entries(fact.properties).sort(([left], [right]) => left.localeCompare(right)));
  return JSON.stringify([fact.factType, fact.ruleId, fact.evidenceTier, fact.targetSymbol, fact.contractElement, fact.evidence.filePath, fact.evidence.startLine, fact.evidence.endLine, sortedProperties]);
}

function packetMarkdown(packet: Base44EvidencePacket): string {
  const counts = countFacts(packet.facts);
  return `${[
    "# TraceMap Base44 Static Evidence",
    "",
    `- Repo: \`${packet.source.repo}\``,
    `- Commit: \`${packet.source.commitSha}\``,
    `- Accepted source SHA-256: \`${packet.source.acceptedSourceSha256}\``,
    `- Accepted tree SHA-256: \`${packet.source.acceptedTreeSha256}\``,
    `- Coverage: \`${packet.coverage.label}\``,
    `- Analysis: \`${packet.coverage.analysisLevel}\``,
    "",
    "## Static fact counts",
    "",
    "| Fact type | Count |",
    "| --- | ---: |",
    ...Object.entries(counts).map(([name, count]) => `| \`${name}\` | ${count} |`),
    "",
    "## Known gaps",
    "",
    ...(packet.coverage.knownGaps.length ? packet.coverage.knownGaps.map((gap) => `- ${gap}`) : ["- None recorded by this scan."]),
    "",
    "## Limitations",
    "",
    ...packet.limitations.map((item) => `- ${item}`),
    ""
  ].join("\n")}`;
}

function packetHtml(packet: Base44EvidencePacket): string {
  const rows = Object.entries(countFacts(packet.facts)).map(([name, count]) => `<tr><td>${escapeHtml(name)}</td><td>${count}</td></tr>`).join("");
  return `<!doctype html><html lang="en"><meta charset="utf-8"><title>TraceMap Base44 Static Evidence</title><style>body{font:16px system-ui;max-width:72rem;margin:2rem auto;padding:0 1rem;color:#18202a}code{word-break:break-all}table{border-collapse:collapse}th,td{border:1px solid #ccd3da;padding:.5rem;text-align:left}.warning{background:#fff4ce;padding:1rem}</style><main><h1>TraceMap Base44 Static Evidence</h1><p><strong>Repo:</strong> ${escapeHtml(packet.source.repo)}</p><p><strong>Commit:</strong> <code>${escapeHtml(packet.source.commitSha)}</code></p><p><strong>Accepted source:</strong> <code>${escapeHtml(packet.source.acceptedSourceSha256)}</code></p><p><strong>Accepted tree:</strong> <code>${escapeHtml(packet.source.acceptedTreeSha256)}</code></p><p><strong>Coverage:</strong> ${escapeHtml(packet.coverage.label)}</p><p class="warning">Static evidence is not runtime proof. Absence is coverage-qualified.</p><table><thead><tr><th>Fact type</th><th>Count</th></tr></thead><tbody>${rows}</tbody></table><h2>Known gaps</h2><ul>${packet.coverage.knownGaps.map((gap) => `<li>${escapeHtml(gap)}</li>`).join("") || "<li>None recorded by this scan.</li>"}</ul></main></html>\n`;
}

function diffMarkdown(diff: Base44Diff): string {
  return `${["# TraceMap Base44 Static Diff", "", `- Before commit: \`${diff.before.commitSha}\``, `- After commit: \`${diff.after.commitSha}\``, `- Coverage reduced: \`${diff.coverageReduced}\``, `- Before analysis: \`${diff.coverageEvidence.beforeAnalysisLevel}\``, `- After analysis: \`${diff.coverageEvidence.afterAnalysisLevel}\``, `- Added known gaps: ${diff.coverageEvidence.addedKnownGaps.length}`, `- Removed known gaps: ${diff.coverageEvidence.removedKnownGaps.length}`, `- Added facts: ${diff.added.length}`, `- Removed facts: ${diff.removed.length}`, `- Unchanged facts: ${diff.unchangedCount}`, "", ...diff.limitations.map((item) => `- ${item}`), ""].join("\n")}`;
}

function countFacts(facts: Base44PacketFact[]): Record<string, number> {
  const counts: Record<string, number> = {};
  for (const fact of facts) counts[fact.factType] = (counts[fact.factType] ?? 0) + 1;
  return Object.fromEntries(Object.entries(counts).sort(([a], [b]) => a.localeCompare(b)));
}

function stableJson(value: unknown): string {
  return `${JSON.stringify(value, null, 2)}\n`;
}

function unique(values: string[]): string[] { return [...new Set(values)].sort(); }
function requireSha(value: string, option: string): void { if (!/^[0-9a-f]{64}$/i.test(value)) throw new Error(`${option} must be a 64-character SHA-256`); }
function tierRank(value: string): number { return value === "Level1SemanticAnalysis" ? 0 : value === "Level1SemanticAnalysisReduced" ? 1 : 2; }
function escapeHtml(value: string): string { return value.replace(/[&<>"']/g, (char) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[char] ?? char); }
async function sha256File(filePath: string): Promise<string> {
  return await new Promise((resolve, reject) => {
    const digest = createHash("sha256");
    const stream = createReadStream(filePath);
    stream.on("data", (chunk) => digest.update(chunk));
    stream.on("end", () => resolve(digest.digest("hex")));
    stream.on("error", reject);
  });
}
