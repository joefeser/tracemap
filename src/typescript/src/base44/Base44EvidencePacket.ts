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
  validatePayloadShapeContracts(packet);
  validateEntitySelectorContracts(packet);
  validateSdkIdentityContracts(packet);
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

function validatePayloadShapeContracts(packet: Base44EvidencePacket): void {
  for (const fact of packet.facts.filter((candidate) => candidate.factType === FactTypes.Base44EntityPayload)) {
    if (["base44-evidence/0.8.0", "base44-evidence/0.9.0", "base44-evidence/0.10.0", "base44-evidence/0.11.0", "base44-evidence/0.12.0"].includes(fact.evidence.extractorVersion)
      && fact.properties.shapeVersion !== "2") {
      throw new Error(`Base44 payload ${fact.factId} must use shapeVersion 2 for extractor ${fact.evidence.extractorVersion}`);
    }
    if (fact.properties.shapeVersion !== "2") continue;
    const outerKind = fact.properties.outerKind;
    const referenceAccounting = fact.properties.referenceAccounting;
    if (!new Set(["object", "array", "unknown"]).has(outerKind)) {
      throw new Error(`Base44 payload ${fact.factId} has an invalid outerKind`);
    }
    if (!new Set(["source-bounded", "unresolved"]).has(referenceAccounting)) {
      throw new Error(`Base44 payload ${fact.factId} has invalid referenceAccounting`);
    }
    let obligations: unknown;
    let gaps: unknown;
    try {
      obligations = JSON.parse(fact.properties.runtimeObligationsJson);
      gaps = JSON.parse(fact.properties.analysisGapsJson);
    } catch {
      throw new Error(`Base44 payload ${fact.factId} has malformed payload-shape v2 arrays`);
    }
    if (!Array.isArray(obligations) || obligations.some((item) => item !== "entity-open-object-fields:docker-write-readback-cleanup")
      || new Set(obligations).size !== obligations.length) {
      throw new Error(`Base44 payload ${fact.factId} has invalid runtime obligations`);
    }
    if (!Array.isArray(gaps) || gaps.some((item) => typeof item !== "string")) {
      throw new Error(`Base44 payload ${fact.factId} has invalid analysis gaps`);
    }
    const deferred = gaps.includes("runtime-deferred-object-fields");
    if (fact.properties.completeness === "complete" && outerKind === "unknown") {
      throw new Error(`Base44 payload ${fact.factId} cannot be complete with an unknown outer kind`);
    }
    if (deferred && (outerKind !== "object" || referenceAccounting !== "source-bounded"
      || fact.properties.completeness !== "partial" || fact.evidenceTier !== EvidenceTiers.Tier4Unknown
      || obligations.length !== 1)) {
      throw new Error(`Base44 payload ${fact.factId} has an invalid deferred-object contract`);
    }
    if (!deferred && obligations.length > 0) {
      throw new Error(`Base44 payload ${fact.factId} has an orphaned runtime obligation`);
    }
  }
}

function validateEntitySelectorContracts(packet: Base44EvidencePacket): void {
  const selectorFactTypes = new Set<string>([
    FactTypes.Base44EntityOperation,
    FactTypes.Base44EntityPayload,
    FactTypes.Base44EntityQuery
  ]);
  const facts = packet.facts.filter((fact) => fact.evidence.extractorVersion === "base44-evidence/0.12.0"
    && selectorFactTypes.has(fact.factType));
  const operations = new Map(facts.filter((fact) => fact.factType === FactTypes.Base44EntityOperation)
    .map((fact) => [fact.properties.operationEvidenceId, fact]));
  for (const fact of facts) {
    const gap = fact.properties.entitySelectorGap;
    let selector: Record<string, any>;
    try {
      selector = JSON.parse(fact.properties.entitySelectorJson);
    } catch {
      throw new Error(`Base44 fact ${fact.factId} has malformed entity selector JSON`);
    }
    requireClosedKeys(selector, ["schemaVersion", "kind", "candidates", "evidence", "gap"], `entity selector ${fact.factId}`);
    if (selector.schemaVersion !== "88mph.base44-entity-selector.v1"
      || !["static-member", "finite-source-domain", "unresolved"].includes(selector.kind)
      || !Array.isArray(selector.candidates) || !Array.isArray(selector.evidence)
      || !["", "entity-selector-dynamic-unresolved"].includes(selector.gap)
      || selector.gap !== gap) {
      throw new Error(`Base44 fact ${fact.factId} has an invalid entity selector`);
    }
    const candidates = selector.candidates as unknown[];
    if (candidates.some((candidate) => typeof candidate !== "string" || !/^[A-Z][A-Za-z0-9_]*$/u.test(candidate))
      || JSON.stringify(candidates) !== JSON.stringify([...new Set(candidates as string[])].sort((left, right) => left.localeCompare(right)))) {
      throw new Error(`Base44 fact ${fact.factId} has invalid entity selector candidates`);
    }
    if ((selector.kind === "unresolved") !== Boolean(gap)
      || (selector.kind === "unresolved" && candidates.length !== 0)
      || (selector.kind !== "unresolved" && candidates.length === 0)
      || (selector.kind === "static-member" && (candidates.length !== 1 || candidates[0] !== fact.properties.entityName))
      || (selector.kind === "finite-source-domain" && !candidates.includes(fact.properties.entityName))
      || (selector.kind === "unresolved" && fact.properties.entityName !== "dynamic")) {
      throw new Error(`Base44 fact ${fact.factId} has a contradictory entity selector`);
    }
    const evidence = selector.evidence as Array<Record<string, any>>;
    for (const item of evidence) {
      requireClosedKeys(item, ["filePath", "sourceFileSha256", "startLine", "endLine", "snippetSha256", "derivation"], `entity selector evidence ${fact.factId}`);
      if (typeof item.filePath !== "string" || !item.filePath || item.filePath.startsWith("/") || item.filePath.split("/").includes("..")
        || !Number.isInteger(item.startLine) || item.startLine < 1 || !Number.isInteger(item.endLine) || item.endLine < item.startLine
        || !/^[0-9a-f]{64}$/u.test(item.sourceFileSha256) || !/^[0-9a-f]{64}$/u.test(item.snippetSha256)
        || !["literal", "array-element", "object-property", "caller-argument", "set-membership"].includes(item.derivation)) {
        throw new Error(`Base44 fact ${fact.factId} has invalid entity selector evidence`);
      }
      if (!packet.facts.some((authority) => authority.evidence.filePath === item.filePath
        && authority.properties.sourceFileSha256 === item.sourceFileSha256)) {
        throw new Error(`Base44 fact ${fact.factId} has entity selector evidence outside packet source authority`);
      }
    }
    const sortedEvidence = [...evidence].sort((left, right) => left.filePath.localeCompare(right.filePath)
      || left.sourceFileSha256.localeCompare(right.sourceFileSha256)
      || left.startLine - right.startLine || left.endLine - right.endLine
      || left.derivation.localeCompare(right.derivation) || left.snippetSha256.localeCompare(right.snippetSha256));
    if (JSON.stringify(evidence) !== JSON.stringify(sortedEvidence)
      || new Set(evidence.map((item) => JSON.stringify(item))).size !== evidence.length) {
      throw new Error(`Base44 fact ${fact.factId} has non-deterministic entity selector evidence`);
    }
    if (selector.kind === "static-member" && (evidence.length !== 1
      || evidence[0].filePath !== fact.evidence.filePath
      || evidence[0].sourceFileSha256 !== fact.properties.sourceFileSha256
      || evidence[0].derivation !== "literal")) {
      throw new Error(`Base44 fact ${fact.factId} has invalid static entity selector authority`);
    }
    if (fact.factType === FactTypes.Base44EntityOperation) {
      if (gap && fact.evidenceTier !== EvidenceTiers.Tier4Unknown) {
        throw new Error(`Base44 operation ${fact.factId} has an invalid entity selector tier`);
      }
    } else {
      const operation = operations.get(fact.properties.operationEvidenceId);
      if (!operation || operation.properties.entitySelectorJson !== fact.properties.entitySelectorJson
        || operation.properties.entitySelectorGap !== gap) {
        throw new Error(`Base44 shape ${fact.factId} does not retain its operation entity selector exactly`);
      }
    }
  }
}

const sdkIdentityGapTokens = new Set([
  "sdk-identity-source-root-missing",
  "sdk-identity-source-root-ambiguous",
  "sdk-identity-specifier-unsupported",
  "sdk-identity-package-authority-missing",
  "sdk-identity-package-authority-invalid",
  "sdk-identity-version-unsupported"
]);

function validateSdkIdentityContracts(packet: Base44EvidencePacket): void {
  const identityFactTypes = new Set<string>([
    FactTypes.Base44EntityOperation,
    FactTypes.Base44EntityPayload,
    FactTypes.Base44EntityQuery
  ]);
  const facts = packet.facts.filter((fact) => ["base44-evidence/0.10.0", "base44-evidence/0.11.0", "base44-evidence/0.12.0"].includes(fact.evidence.extractorVersion)
    && identityFactTypes.has(fact.factType));
  const operations = new Map(facts.filter((fact) => fact.factType === FactTypes.Base44EntityOperation)
    .map((fact) => [fact.properties.operationEvidenceId, fact]));
  const runtimeSdkImports = packet.facts.filter((fact) => fact.factType === FactTypes.Base44SdkImport
    && fact.properties.importKind === "runtime");
  for (const fact of facts) {
    const identityJson = fact.properties.sdkIdentityJson;
    const gap = fact.properties.sdkIdentityGap;
    if (gap) {
      if (!sdkIdentityGapTokens.has(gap) || identityJson) throw new Error(`Base44 fact ${fact.factId} has an invalid SDK identity gap`);
      if (fact.factType === FactTypes.Base44EntityOperation && fact.evidenceTier !== EvidenceTiers.Tier4Unknown) {
        throw new Error(`Base44 operation ${fact.factId} must retain Tier4Unknown for an SDK identity gap`);
      }
    } else {
      if (!identityJson) throw new Error(`Base44 fact ${fact.factId} is missing its SDK identity`);
      validateSdkIdentityJson(fact, identityJson, runtimeSdkImports);
      if (fact.factType === FactTypes.Base44EntityOperation && fact.evidenceTier !== (fact.properties.entitySelectorGap
        ? EvidenceTiers.Tier4Unknown : EvidenceTiers.Tier3SyntaxOrTextual)) {
        throw new Error(`Base44 operation ${fact.factId} with an exact SDK identity must retain Tier3SyntaxOrTextual`);
      }
    }
    if (fact.factType !== FactTypes.Base44EntityOperation) {
      const operation = operations.get(fact.properties.operationEvidenceId);
      if (!operation || operation.properties.sdkIdentityJson !== identityJson || operation.properties.sdkIdentityGap !== gap) {
        throw new Error(`Base44 shape ${fact.factId} does not retain its operation SDK identity exactly`);
      }
    }
  }
}

function validateSdkIdentityJson(fact: Base44PacketFact, identityJson: string, runtimeSdkImports: Base44PacketFact[]): void {
  let identity: Record<string, any>;
  try {
    identity = JSON.parse(identityJson);
  } catch {
    throw new Error(`Base44 fact ${fact.factId} has malformed SDK identity JSON`);
  }
  requireClosedKeys(identity, ["schemaVersion", "packageName", "version", "scope", "rawSpecifier", "evidence"], `SDK identity ${fact.factId}`);
  if (identity.schemaVersion !== "88mph.base44-sdk-callsite-identity.v1" || identity.packageName !== "@base44/sdk"
    || !["0.8.4", "0.8.5"].includes(identity.version)
    || !["function-runtime", "frontend-package"].includes(identity.scope)
    || typeof identity.rawSpecifier !== "string" || !Array.isArray(identity.evidence)) {
    throw new Error(`Base44 fact ${fact.factId} has an invalid SDK identity`);
  }
  const evidence = identity.evidence as Array<Record<string, any>>;
  for (const item of evidence) {
    requireClosedKeys(item, ["authorityPath", "authoritySha256", "kind"], `SDK identity evidence ${fact.factId}`);
    if (typeof item.authorityPath !== "string" || !item.authorityPath || item.authorityPath.startsWith("/")
      || item.authorityPath.split("/").includes("..") || !/^[0-9a-f]{64}$/u.test(item.authoritySha256)
      || !["source-import", "package-manifest", "package-lock-resolution"].includes(item.kind)) {
      throw new Error(`Base44 fact ${fact.factId} has invalid SDK identity evidence`);
    }
  }
  const sorted = [...evidence].sort((left, right) => left.kind.localeCompare(right.kind)
    || left.authorityPath.localeCompare(right.authorityPath)
    || left.authoritySha256.localeCompare(right.authoritySha256));
  if (JSON.stringify(evidence) !== JSON.stringify(sorted)
    || new Set(evidence.map((item) => JSON.stringify(item))).size !== evidence.length) {
    throw new Error(`Base44 fact ${fact.factId} has non-deterministic SDK identity evidence`);
  }
  const sourceEvidence = evidence.filter((item) => item.kind === "source-import");
  if (sourceEvidence.length !== 1 || !runtimeSdkImports.some((sdkImport) => (
    sdkImport.evidence.filePath === sourceEvidence[0].authorityPath
    && sdkImport.properties.sourceFileSha256 === sourceEvidence[0].authoritySha256
    && sdkImport.properties.requestedPackage === identity.rawSpecifier
  ))) {
    throw new Error(`Base44 fact ${fact.factId} has SDK identity evidence not bound to an extracted runtime import`);
  }
  if (identity.scope === "function-runtime") {
    if (identity.version !== "0.8.4" || identity.rawSpecifier !== "npm:@base44/sdk@0.8.4"
      || evidence.length !== 1 || evidence[0].kind !== "source-import"
      || evidence[0].authorityPath !== fact.evidence.filePath
      || evidence[0].authoritySha256 !== fact.properties.sourceFileSha256) {
      throw new Error(`Base44 fact ${fact.factId} has an invalid function-runtime SDK identity`);
    }
  } else {
    const kinds = evidence.map((item) => item.kind);
    if (identity.version !== "0.8.5" || identity.rawSpecifier !== "@base44/sdk"
      || JSON.stringify(kinds) !== JSON.stringify(["package-lock-resolution", "package-manifest", "source-import"])
      || evidence.find((item) => item.kind === "package-lock-resolution")?.authorityPath !== "package-lock.json"
      || evidence.find((item) => item.kind === "package-manifest")?.authorityPath !== "package.json") {
      throw new Error(`Base44 fact ${fact.factId} has an invalid frontend-package SDK identity`);
    }
  }
}

function requireClosedKeys(value: Record<string, any>, keys: string[], label: string): void {
  if (!value || typeof value !== "object" || Array.isArray(value)
    || JSON.stringify(Object.keys(value).sort()) !== JSON.stringify([...keys].sort())) {
    throw new Error(`${label} has unexpected or missing properties`);
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
