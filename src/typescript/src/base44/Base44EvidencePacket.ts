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
  filePath: string;
  lineSpan: { startLine: number; endLine: number };
  commitSha: string;
  extractorVersion: string;
}

type CoverageGapFact = Pick<CodeFact,
  "factId" | "factType" | "ruleId" | "evidenceTier" | "targetSymbol" | "contractElement" | "properties" | "evidence">;

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
  // Bind only deterministic source-evidence artifacts into the portable
  // packet. scan-manifest.json carries scannedAt and index.sqlite serializes
  // that operational timestamp, so both remain required scan outputs and are
  // validated separately but cannot perturb the source-bound packet bytes.
  for (const name of ["facts.ndjson", "report.md", "logs/analyzer.log"]) {
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
  const coverageReduced = removed.length > 0 || addedKnownGaps.length > 0
    || tierRank(after.coverage.analysisLevel) > tierRank(before.coverage.analysisLevel);
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
  normalizeLegacyCoverageGaps(value);
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
    evidenceTier: EvidenceTiers.Tier4Unknown,
    filePath: fact.evidence.filePath,
    lineSpan: { startLine: fact.evidence.startLine, endLine: fact.evidence.endLine },
    commitSha: source.commitSha,
    extractorVersion: fact.evidence.extractorVersion
  };
}

function normalizeLegacyCoverageGaps(packet: Base44EvidencePacket): void {
  if (packet.coverage?.gapSchemaVersion === base44CoverageGapSchemaVersion && Array.isArray(packet.coverage.gaps)) return;
  if (!legacyCoverageGapShapeAllowed(packet)) return;
  packet.coverage.gapSchemaVersion = base44CoverageGapSchemaVersion;
  packet.coverage.gaps = buildCoverageGaps(
    packet.facts.filter((fact) => fact.evidenceTier === EvidenceTiers.Tier4Unknown),
    packet.source
  );
}

function legacyCoverageGapShapeAllowed(packet: Base44EvidencePacket): boolean {
  const versions = legacyCoverageGapProducerVersions(packet);
  return versions.length > 0 && versions.every((version) => {
    const match = /^base44-evidence\/0\.(\d+)\.\d+$/u.exec(version);
    return match !== null && Number(match[1]) <= 6;
  });
}

function legacyCoverageGapProducerVersions(packet: Base44EvidencePacket): string[] {
  return unique([
    ...(packet.coverage?.extractorIdentities ?? []).map((identity) => {
      const match = /(?:^|@)(base44-evidence\/\d+\.\d+\.\d+)$/u.exec(identity);
      return match?.[1] ?? identity;
    }),
    ...packet.facts.map((fact) => fact.evidence?.extractorVersion ?? "")
  ].filter(Boolean));
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
  validateEntityCallsiteDispositions(packet);
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
    if (typeof gap.filePath !== "string" || gap.filePath.length === 0) throw new Error(`Base44 coverage gap ${index} has an invalid filePath`);
    if (gap.commitSha !== packet.source.commitSha) throw new Error(`Base44 coverage gap ${index} has an invalid commitSha`);
    if (!gap.lineSpan || !Number.isInteger(gap.lineSpan.startLine) || !Number.isInteger(gap.lineSpan.endLine)
      || gap.lineSpan.startLine < 1 || gap.lineSpan.endLine < gap.lineSpan.startLine) {
      throw new Error(`Base44 coverage gap ${index} has an invalid lineSpan`);
    }
    if (typeof gap.extractorVersion !== "string" || gap.extractorVersion.length === 0) throw new Error(`Base44 coverage gap ${index} has an invalid extractorVersion`);
  }
  if (JSON.stringify(actual) !== JSON.stringify(expected)) {
    throw new Error("Base44 coverage gaps do not match the source-bound Tier4 fact set exactly once");
  }
}

function validatePayloadShapeContracts(packet: Base44EvidencePacket): void {
  const openObjectObligation = "entity-open-object-fields:docker-write-readback-cleanup";
  const sourceExecutionObligation = "entity-execution:docker-workflow-readback-cleanup";
  const runtimeObligations = new Set([openObjectObligation, sourceExecutionObligation]);
  const sourceAuthority = new Set(packet.facts.map((fact) =>
    `${fact.evidence.filePath}\0${fact.properties.sourceFileSha256 ?? ""}`));
  for (const fact of packet.facts.filter((candidate) => candidate.factType === FactTypes.Base44EntityPayload)) {
    if (["base44-evidence/0.8.0", "base44-evidence/0.9.0", "base44-evidence/0.10.0", "base44-evidence/0.11.0", "base44-evidence/0.12.0", "base44-evidence/0.13.0"].includes(fact.evidence.extractorVersion)
      && fact.properties.shapeVersion !== "2") {
      throw new Error(`Base44 payload ${fact.factId} must use shapeVersion 2 for extractor ${fact.evidence.extractorVersion}`);
    }
    if (fact.evidence.extractorVersion === "base44-evidence/0.14.0" && fact.properties.shapeVersion !== "3") {
      throw new Error(`Base44 payload ${fact.factId} must use shapeVersion 3 for extractor ${fact.evidence.extractorVersion}`);
    }
    if (!new Set(["2", "3"]).has(fact.properties.shapeVersion)) continue;
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
    if (!Array.isArray(obligations) || obligations.some((item) => !runtimeObligations.has(item))
      || new Set(obligations).size !== obligations.length) {
      throw new Error(`Base44 payload ${fact.factId} has invalid runtime obligations`);
    }
    if (!Array.isArray(gaps) || gaps.some((item) => typeof item !== "string")) {
      throw new Error(`Base44 payload ${fact.factId} has invalid analysis gaps`);
    }
    const deferredObject = gaps.includes("runtime-deferred-object-fields");
    const deferredExecution = gaps.includes("runtime-deferred-callsite-execution");
    const expectedObligations = [
      ...(deferredExecution ? [sourceExecutionObligation] : []),
      ...(deferredObject ? [openObjectObligation] : [])
    ];
    if (fact.properties.completeness === "complete" && outerKind === "unknown") {
      throw new Error(`Base44 payload ${fact.factId} cannot be complete with an unknown outer kind`);
    }
    if ((deferredObject || deferredExecution) && (outerKind !== "object" || referenceAccounting !== "source-bounded"
      || fact.properties.completeness !== "partial" || fact.evidenceTier !== EvidenceTiers.Tier4Unknown
      || JSON.stringify(obligations) !== JSON.stringify(expectedObligations))) {
      throw new Error(`Base44 payload ${fact.factId} has an invalid runtime-deferred payload contract`);
    }
    if (!deferredObject && !deferredExecution && obligations.length > 0) {
      throw new Error(`Base44 payload ${fact.factId} has an orphaned runtime obligation`);
    }
    if (fact.properties.shapeVersion === "3") validateSemanticPayloadFields(sourceAuthority, fact);
  }
}

const payloadValueTypes = new Set(["string", "number", "integer", "decimal", "boolean", "date", "object", "array", "uuid", "unknown"]);

function validateSemanticPayloadFields(sourceAuthority: Set<string>, fact: Base44PacketFact): void {
  let fields: unknown;
  let semanticFields: unknown;
  try {
    fields = JSON.parse(fact.properties.fieldsJson);
    semanticFields = JSON.parse(fact.properties.semanticFieldsJson);
  } catch {
    throw new Error(`Base44 payload ${fact.factId} has malformed semantic fields JSON`);
  }
  if (!Array.isArray(fields) || !Array.isArray(semanticFields)) {
    throw new Error(`Base44 payload ${fact.factId} semantic fields must be arrays`);
  }
  const syntacticKeys = ["name", "presence", "expressionType", "origin", "evidenceStartLine", "evidenceEndLine",
    "evidenceStartOffset", "evidenceEndOffset", "evidenceFilePath", "evidenceSourceFileSha256", "evidenceSnippetHash"];
  const semanticProvenanceKeys = [...syntacticKeys, "semanticValueType", "semanticExplicitNull"];
  for (const [index, value] of fields.entries()) validatePayloadFieldProvenance(sourceAuthority, value, syntacticKeys, fact.factId, index);
  const sortedFields = [...fields].sort((left: any, right: any) => left.name.localeCompare(right.name)
    || left.evidenceFilePath.localeCompare(right.evidenceFilePath)
    || left.evidenceStartOffset - right.evidenceStartOffset
    || left.evidenceEndOffset - right.evidenceEndOffset
    || left.presence.localeCompare(right.presence)
    || left.expressionType.localeCompare(right.expressionType)
    || left.origin.localeCompare(right.origin));
  const occurrenceKeys = fields.map((field: any) => JSON.stringify([
    field.evidenceFilePath, field.evidenceSourceFileSha256, field.evidenceStartOffset,
    field.evidenceEndOffset, field.name,
  ]));
  if (JSON.stringify(fields) !== JSON.stringify(sortedFields)
    || new Set(occurrenceKeys).size !== occurrenceKeys.length) {
    throw new Error(`Base44 payload ${fact.factId} has non-deterministic or duplicate field occurrences`);
  }
  const names = new Set<string>();
  const flattenedProvenance: Record<string, unknown>[] = [];
  for (const [index, value] of semanticFields.entries()) {
    if (!value || typeof value !== "object" || Array.isArray(value)) {
      throw new Error(`Base44 payload ${fact.factId} semantic field ${index} must be an object`);
    }
    const field = value as Record<string, unknown>;
    requireClosedKeys(field, ["name", "semanticPresence", "valueType", "explicitNull", "provenance"],
      `semantic payload field ${fact.factId}:${index}`);
    if (typeof field.name !== "string" || !field.name || names.has(field.name)
      || !new Set(["always", "conditional", "unknown"]).has(String(field.semanticPresence))
      || !payloadValueTypes.has(String(field.valueType)) || typeof field.explicitNull !== "boolean"
      || !Array.isArray(field.provenance) || field.provenance.length === 0) {
      throw new Error(`Base44 payload ${fact.factId} has invalid semantic field ${index}`);
    }
    const provenance = field.provenance as unknown[];
    for (const [provenanceIndex, item] of provenance.entries()) {
      validatePayloadFieldProvenance(sourceAuthority, item, semanticProvenanceKeys, fact.factId, provenanceIndex);
      const observation = item as Record<string, unknown>;
      if (observation.name !== field.name) throw new Error(`Base44 payload ${fact.factId} has cross-field semantic provenance`);
      if (!payloadValueTypes.has(String(observation.semanticValueType))
        || typeof observation.semanticExplicitNull !== "boolean") {
        throw new Error(`Base44 payload ${fact.factId} has invalid semantic provenance value ${provenanceIndex}`);
      }
      const { semanticValueType: _semanticValueType, semanticExplicitNull: _semanticExplicitNull, ...syntactic } = observation;
      flattenedProvenance.push(syntactic);
    }
    const presenceValues = provenance.map((item) => String((item as Record<string, unknown>).presence));
    const expectedPresence = presenceValues.some((presence) => presence === "dynamic-computed" || presence === "unresolved") ? "unknown"
      : presenceValues.every((presence) => presence === "unconditional") ? "always" : "conditional";
    if (field.semanticPresence !== expectedPresence) {
      throw new Error(`Base44 payload ${fact.factId} has a non-conservative semantic presence aggregate`);
    }
    const semanticTypes = new Set(provenance.map((item) => String((item as Record<string, unknown>).semanticValueType)));
    const nullStates = new Set(provenance.map((item) => Boolean((item as Record<string, unknown>).semanticExplicitNull)));
    const expectedValueType = semanticTypes.size === 1 && nullStates.size === 1 ? [...semanticTypes][0] : "unknown";
    const expectedExplicitNull = provenance.some((item) => Boolean((item as Record<string, unknown>).semanticExplicitNull));
    if (field.valueType !== expectedValueType || field.explicitNull !== expectedExplicitNull) {
      throw new Error(`Base44 payload ${fact.factId} has a contradictory semantic value aggregate`);
    }
    names.add(field.name);
  }
  const sorted = [...semanticFields].sort((left: any, right: any) => left.name.localeCompare(right.name));
  if (JSON.stringify(semanticFields) !== JSON.stringify(sorted)) {
    throw new Error(`Base44 payload ${fact.factId} semantic fields are not deterministic`);
  }
  if (JSON.stringify(flattenedProvenance) !== JSON.stringify(fields)) {
    throw new Error(`Base44 payload ${fact.factId} semantic fields do not preserve syntactic alternatives exactly once`);
  }
}

function validatePayloadFieldProvenance(
  sourceAuthority: Set<string>,
  value: unknown,
  keys: string[],
  factId: string,
  index: number
): void {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    throw new Error(`Base44 payload ${factId} field provenance ${index} must be an object`);
  }
  const field = value as Record<string, unknown>;
  requireClosedKeys(field, keys, `payload field provenance ${factId}:${index}`);
  if (typeof field.name !== "string" || !field.name
    || !new Set(["unconditional", "conditional", "spread-derived", "dynamic-computed", "unresolved"]).has(String(field.presence))
    || typeof field.expressionType !== "string" || !field.expressionType
    || typeof field.origin !== "string" || !field.origin
    || !Number.isSafeInteger(field.evidenceStartLine) || !Number.isSafeInteger(field.evidenceEndLine)
    || Number(field.evidenceStartLine) < 1 || Number(field.evidenceEndLine) < Number(field.evidenceStartLine)
    || !Number.isSafeInteger(field.evidenceStartOffset) || !Number.isSafeInteger(field.evidenceEndOffset)
    || Number(field.evidenceStartOffset) < 0 || Number(field.evidenceEndOffset) <= Number(field.evidenceStartOffset)
    || typeof field.evidenceFilePath !== "string" || !field.evidenceFilePath
    || field.evidenceFilePath.startsWith("/") || field.evidenceFilePath.split("/").includes("..")
    || typeof field.evidenceSourceFileSha256 !== "string" || !/^[0-9a-f]{64}$/u.test(field.evidenceSourceFileSha256)
    || typeof field.evidenceSnippetHash !== "string" || !/^[0-9a-f]{64}$/u.test(field.evidenceSnippetHash)) {
    throw new Error(`Base44 payload ${factId} has invalid field provenance ${index}`);
  }
  if (!sourceAuthority.has(`${field.evidenceFilePath}\0${field.evidenceSourceFileSha256}`)) {
    throw new Error(`Base44 payload ${factId} has field provenance outside packet source authority`);
  }
}

function validateEntitySelectorContracts(packet: Base44EvidencePacket): void {
  const selectorFactTypes = new Set<string>([
    FactTypes.Base44EntityOperation,
    FactTypes.Base44EntityPayload,
    FactTypes.Base44EntityQuery
  ]);
  const facts = packet.facts.filter((fact) => ["base44-evidence/0.12.0", "base44-evidence/0.13.0", "base44-evidence/0.14.0"].includes(fact.evidence.extractorVersion)
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

function validateEntityCallsiteDispositions(packet: Base44EvidencePacket): void {
  const runtimeSdkImports = packet.facts.filter((fact) => fact.factType === FactTypes.Base44SdkImport
    && fact.properties.importKind === "runtime");
  const operationFacts = packet.facts.filter((fact) => fact.factType === FactTypes.Base44EntityOperation);
  const operationIdsInPacket = operationFacts.map((fact) => fact.properties.operationEvidenceId);
  if (operationIdsInPacket.some((id) => !/^operation-[0-9a-f]{20}$/u.test(id ?? ""))
    || new Set(operationIdsInPacket).size !== operationIdsInPacket.length) {
    throw new Error("Base44 entity operations contain invalid or duplicate operation identities");
  }
  const operations = new Map(operationFacts.map((fact) => [fact.properties.operationEvidenceId, fact]));
  const operationIds = new Set(operations.keys());
  const suppressedOperationIds = new Set<string>();
  const dispositionsByOperationId = new Map<string, { fact: Base44PacketFact; disposition: Record<string, any> }>();
  for (const fact of packet.facts.filter((candidate) => candidate.factType === FactTypes.Base44EntityCallsiteDisposition)) {
    let disposition: Record<string, any>;
    try {
      disposition = JSON.parse(fact.properties.callsiteDispositionJson);
    } catch {
      throw new Error(`Base44 fact ${fact.factId} has malformed entity callsite disposition JSON`);
    }
    requireClosedKeys(disposition, ["schemaVersion", "disposition", "callableName", "authorityPath", "authoritySha256",
      "sourceSnapshotDigest", "externalModuleReferences", "ambiguousDynamicModuleReferences", "operationName",
      "operationEvidenceIds", "primitiveCapabilities", "entitySelector", "sdkIdentity", "sdkIdentityGap", "callsite"],
    `entity callsite disposition ${fact.factId}`);
    if (fact.evidence.extractorVersion !== "base44-evidence/0.14.0"
      || disposition.schemaVersion !== "88mph.base44-entity-callsite-disposition.v2"
      || disposition.disposition !== "dormant-unreachable"
      || typeof disposition.callableName !== "string" || !disposition.callableName
      || disposition.authorityPath !== fact.evidence.filePath
      || disposition.authoritySha256 !== fact.properties.sourceFileSha256
      || !/^[0-9a-f]{64}$/u.test(disposition.authoritySha256)
      || !/^[0-9a-f]{64}$/u.test(disposition.sourceSnapshotDigest)
      || disposition.externalModuleReferences !== 0 || disposition.ambiguousDynamicModuleReferences !== 0
      || disposition.operationName !== fact.properties.operationName) {
      throw new Error(`Base44 fact ${fact.factId} has an invalid entity callsite disposition`);
    }
    const selector = disposition.entitySelector as Record<string, any>;
    requireClosedKeys(selector, ["schemaVersion", "kind", "candidates", "evidence", "gap"], `disposition entity selector ${fact.factId}`);
    if (selector.schemaVersion !== "88mph.base44-entity-selector.v1"
      || !["static-member", "finite-source-domain", "unresolved"].includes(selector.kind)
      || !Array.isArray(selector.candidates) || !Array.isArray(selector.evidence)
      || !["", "entity-selector-dynamic-unresolved"].includes(selector.gap)
      || (selector.kind === "unresolved") !== Boolean(selector.gap)
      || selector.candidates.some((candidate: unknown) => typeof candidate !== "string"
        || !/^[A-Z][A-Za-z0-9_]*$/u.test(candidate as string))
      || JSON.stringify(selector.candidates) !== JSON.stringify([...new Set(selector.candidates)].sort())
      || (selector.kind === "static-member" && (selector.candidates.length !== 1 || selector.evidence.length !== 1))
      || (selector.kind === "finite-source-domain" && (selector.candidates.length === 0 || selector.evidence.length === 0))
      || (selector.kind === "unresolved" && (selector.candidates.length !== 0 || selector.evidence.length !== 0))
      || fact.properties.entitySelectorJson !== JSON.stringify(selector)) {
      throw new Error(`Base44 fact ${fact.factId} has an invalid disposition entity selector`);
    }
    for (const item of selector.evidence as Array<Record<string, any>>) {
      requireClosedKeys(item, ["filePath", "sourceFileSha256", "startLine", "endLine", "snippetSha256", "derivation"],
        `disposition selector evidence ${fact.factId}`);
      if (typeof item.filePath !== "string" || !item.filePath || item.filePath.startsWith("/")
        || item.filePath.split("/").includes("..") || !/^[0-9a-f]{64}$/u.test(item.sourceFileSha256 ?? "")
        || !Number.isInteger(item.startLine) || item.startLine < 1
        || !Number.isInteger(item.endLine) || item.endLine < item.startLine
        || !/^[0-9a-f]{64}$/u.test(item.snippetSha256 ?? "")
        || !["literal", "array-element", "object-property", "caller-argument", "set-membership"].includes(item.derivation)
        || !packet.facts.some((authority) => authority.evidence.filePath === item.filePath
          && authority.properties.sourceFileSha256 === item.sourceFileSha256)) {
        throw new Error(`Base44 fact ${fact.factId} has invalid disposition selector evidence`);
      }
    }
    const callsite = disposition.callsite as Record<string, any>;
    requireClosedKeys(callsite, ["filePath", "sourceFileSha256", "startLine", "endLine", "startOffset", "endOffset", "snippetSha256"],
      `disposition callsite ${fact.factId}`);
    if (callsite.filePath !== fact.evidence.filePath || callsite.sourceFileSha256 !== fact.properties.sourceFileSha256
      || callsite.startLine !== fact.evidence.startLine || callsite.endLine !== fact.evidence.endLine
      || callsite.snippetSha256 !== fact.evidence.snippetHash
      || !Number.isSafeInteger(callsite.startOffset) || !Number.isSafeInteger(callsite.endOffset)
      || callsite.startOffset < 0 || callsite.endOffset <= callsite.startOffset) {
      throw new Error(`Base44 fact ${fact.factId} has invalid exact callsite evidence`);
    }
    const entityNames = selector.candidates.length > 0 ? selector.candidates : ["dynamic"];
    const expectedOperationIds = entityNames.map((entityName: string) => `operation-${createHash("sha256").update([
      callsite.filePath,
      String(callsite.startLine),
      String(callsite.endLine),
      String(callsite.startOffset),
      String(callsite.endOffset),
      entityName,
      disposition.operationName,
      callsite.snippetSha256,
    ].join("|"), "utf8").digest("hex").slice(0, 20)}`).sort();
    if (!Array.isArray(disposition.operationEvidenceIds)
      || disposition.operationEvidenceIds.length !== entityNames.length
      || JSON.stringify(disposition.operationEvidenceIds) !== fact.properties.operationEvidenceIdsJson
      || JSON.stringify(disposition.operationEvidenceIds) !== JSON.stringify(expectedOperationIds)
      || JSON.stringify(disposition.operationEvidenceIds) !== JSON.stringify([...new Set(disposition.operationEvidenceIds)].sort())
      || disposition.operationEvidenceIds.some((id: unknown) => typeof id !== "string" || !/^operation-[0-9a-f]{20}$/u.test(id as string)
        || operationIds.has(id as string))) {
      throw new Error(`Base44 fact ${fact.factId} has invalid suppressed operation accounting`);
    }
    for (const id of disposition.operationEvidenceIds as string[]) {
      if (suppressedOperationIds.has(id)) throw new Error(`Base44 fact ${fact.factId} duplicates a suppressed operation identity`);
      suppressedOperationIds.add(id);
      dispositionsByOperationId.set(id, { fact, disposition });
    }
    if (!Array.isArray(disposition.primitiveCapabilities)
      || disposition.primitiveCapabilities.length !== entityNames.length
      || JSON.stringify(disposition.primitiveCapabilities) !== JSON.stringify([...new Set(disposition.primitiveCapabilities)].sort())
      || disposition.primitiveCapabilities.some((capability: unknown) => typeof capability !== "string"
        || !entityNames.some((entity: string) => capability === `entities.${entity}.${disposition.operationName}`
          || capability === `asServiceRole.entities.${entity}.${disposition.operationName}`))) {
      throw new Error(`Base44 fact ${fact.factId} has invalid primitive capability accounting`);
    }
    for (const capability of disposition.primitiveCapabilities as string[]) {
      if (!packet.facts.some((primitive) => primitive.factType === FactTypes.Base44SdkPrimitive
        && primitive.evidence.filePath === fact.evidence.filePath
        && primitive.evidence.startLine === fact.evidence.startLine
        && primitive.evidence.endLine === fact.evidence.endLine
        && primitive.properties.capability === capability)) {
        throw new Error(`Base44 fact ${fact.factId} lacks its retained SDK primitive`);
      }
    }
    if (disposition.sdkIdentityGap) {
      if (!sdkIdentityGapTokens.has(disposition.sdkIdentityGap) || disposition.sdkIdentity !== null
        || fact.properties.sdkIdentityGap !== disposition.sdkIdentityGap || fact.properties.sdkIdentityJson) {
        throw new Error(`Base44 fact ${fact.factId} has invalid disposition SDK identity gap`);
      }
    } else {
      if (!disposition.sdkIdentity || fact.properties.sdkIdentityGap
        || fact.properties.sdkIdentityJson !== JSON.stringify(disposition.sdkIdentity)) {
        throw new Error(`Base44 fact ${fact.factId} has contradictory disposition SDK identity`);
      }
      validateSdkIdentityJson(fact, fact.properties.sdkIdentityJson, runtimeSdkImports);
    }
    // The disposition's claim is exact source reachability, not entity-name
    // resolution. An unresolved selector remains a Tier-4 primitive gap in the
    // raw denominator, but it does not downgrade independently proven dormant
    // reachability or duplicate that gap on the disposition row.
    const expectedTier = disposition.sdkIdentity ? EvidenceTiers.Tier3SyntaxOrTextual : EvidenceTiers.Tier4Unknown;
    if (fact.evidenceTier !== expectedTier) {
      throw new Error(`Base44 fact ${fact.factId} has an invalid disposition evidence tier`);
    }
  }

  validateEntityPrimitiveDenominator(packet, operations, dispositionsByOperationId);
}

function validateEntityPrimitiveDenominator(
  packet: Base44EvidencePacket,
  operations: Map<string, Base44PacketFact>,
  dispositionsByOperationId: Map<string, { fact: Base44PacketFact; disposition: Record<string, any> }>
): void {
  const primitivesByOperationId = new Map<string, Base44PacketFact[]>();
  const entityPrimitivePattern = /^(?:asServiceRole\.)?entities\.([A-Z][A-Za-z0-9_]*|dynamic)\.([A-Za-z][A-Za-z0-9_]*)$/u;
  const primitives = packet.facts.filter((fact) => fact.factType === FactTypes.Base44SdkPrimitive
    && fact.evidence.extractorVersion === "base44-evidence/0.14.0"
    && entityPrimitivePattern.test(fact.properties.capability ?? ""));
  for (const primitive of primitives) {
    const match = entityPrimitivePattern.exec(primitive.properties.capability)!;
    const callsite = exactEntityFactCallsite(primitive);
    const expectedOperationId = entityOperationIdFromCallsite(callsite, match[1], match[2]);
    const active = operations.get(expectedOperationId);
    const suppressed = dispositionsByOperationId.get(expectedOperationId);
    if (Number(Boolean(active)) + Number(Boolean(suppressed)) !== 1) {
      throw new Error(`Base44 SDK primitive ${primitive.factId} is not accounted by exactly one active operation or dormant disposition`);
    }
    if (active) {
      const operationCallsite = exactEntityFactCallsite(active);
      const selector = JSON.parse(active.properties.entitySelectorJson) as Record<string, any>;
      const selectorAccountsForEntity = selector.kind === "unresolved"
        ? match[1] === "dynamic" : selector.candidates.includes(match[1]);
      if (operationCallsite.key !== callsite.key || active.properties.entityName !== match[1]
        || active.properties.operationName !== match[2] || !selectorAccountsForEntity) {
        throw new Error(`Base44 SDK primitive ${primitive.factId} contradicts its active operation`);
      }
    }
    if (suppressed) {
      const dispositionCallsite = suppressed.disposition.callsite as Record<string, any>;
      if (exactCallsiteKey(dispositionCallsite) !== callsite.key
        || !suppressed.disposition.primitiveCapabilities.includes(primitive.properties.capability)) {
        throw new Error(`Base44 SDK primitive ${primitive.factId} contradicts its dormant disposition`);
      }
    }
    const rows = primitivesByOperationId.get(expectedOperationId) ?? [];
    rows.push(primitive);
    primitivesByOperationId.set(expectedOperationId, rows);
  }

  for (const operation of packet.facts.filter((fact) => fact.factType === FactTypes.Base44EntityOperation
    && fact.evidence.extractorVersion === "base44-evidence/0.14.0")) {
    const callsite = exactEntityFactCallsite(operation);
    const expectedOperationId = entityOperationIdFromCallsite(callsite,
      operation.properties.entityName, operation.properties.operationName);
    if (operation.properties.operationEvidenceId !== expectedOperationId) {
      throw new Error(`Base44 operation ${operation.factId} has invalid exact operation identity`);
    }
    const rows = primitivesByOperationId.get(expectedOperationId) ?? [];
    if (rows.length !== 1) {
      throw new Error(`Base44 operation ${operation.factId} does not have exactly one retained SDK primitive`);
    }
  }
  for (const [operationId, disposition] of dispositionsByOperationId) {
    const rows = primitivesByOperationId.get(operationId) ?? [];
    if (rows.length !== 1) {
      throw new Error(`Base44 disposition ${disposition.fact.factId} does not have exactly one retained SDK primitive per suppressed operation`);
    }
  }
}

function exactEntityFactCallsite(fact: Base44PacketFact): {
  filePath: string; sourceFileSha256: string; startLine: number; endLine: number;
  startOffset: number; endOffset: number; snippetSha256: string; key: string;
} {
  const startOffset = Number(fact.properties.callsiteStartOffset);
  const endOffset = Number(fact.properties.callsiteEndOffset);
  const value = {
    filePath: fact.evidence.filePath,
    sourceFileSha256: fact.properties.sourceFileSha256,
    startLine: fact.evidence.startLine,
    endLine: fact.evidence.endLine,
    startOffset,
    endOffset,
    snippetSha256: fact.properties.callsiteSnippetSha256
  };
  if (!Number.isSafeInteger(startOffset) || !Number.isSafeInteger(endOffset)
    || startOffset < 0 || endOffset <= startOffset
    || !/^[0-9a-f]{64}$/u.test(value.sourceFileSha256 ?? "")
    || !/^[0-9a-f]{64}$/u.test(value.snippetSha256 ?? "")
    || value.snippetSha256 !== fact.evidence.snippetHash) {
    throw new Error(`Base44 fact ${fact.factId} has invalid exact entity callsite properties`);
  }
  return { ...value, key: exactCallsiteKey(value) };
}

function exactCallsiteKey(callsite: Record<string, any>): string {
  return [callsite.filePath, callsite.sourceFileSha256, callsite.startLine, callsite.endLine,
    callsite.startOffset, callsite.endOffset, callsite.snippetSha256].join("|");
}

function entityOperationIdFromCallsite(
  callsite: Record<string, any>, entityName: string, operationName: string
): string {
  return `operation-${createHash("sha256").update([
    callsite.filePath,
    String(callsite.startLine),
    String(callsite.endLine),
    String(callsite.startOffset),
    String(callsite.endOffset),
    entityName,
    operationName,
    callsite.snippetSha256,
  ].join("|"), "utf8").digest("hex").slice(0, 20)}`;
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
  const facts = packet.facts.filter((fact) => ["base44-evidence/0.10.0", "base44-evidence/0.11.0", "base44-evidence/0.12.0", "base44-evidence/0.13.0", "base44-evidence/0.14.0"].includes(fact.evidence.extractorVersion)
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
  const gapLines = coverageGapMarkdown(packet);
  return `${[
    "# TraceMap Base44 Static Evidence",
    "",
    `- Repo: \`${packet.source.repo}\``,
    `- Commit: \`${packet.source.commitSha}\``,
    `- Accepted source SHA-256: \`${packet.source.acceptedSourceSha256}\``,
    `- Accepted tree SHA-256: \`${packet.source.acceptedTreeSha256}\``,
    `- Coverage: \`${packet.coverage.label}\``,
    `- Analysis: \`${packet.coverage.analysisLevel}\``,
    `- Coverage status: \`${coverageStatus(packet)}\``,
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
    "## Producer-owned coverage gaps",
    "",
    ...gapLines,
    "",
    "## Limitations",
    "",
    ...packet.limitations.map((item) => `- ${item}`),
    ""
  ].join("\n")}`;
}

function packetHtml(packet: Base44EvidencePacket): string {
  const rows = Object.entries(countFacts(packet.facts)).map(([name, count]) => `<tr><td>${escapeHtml(name)}</td><td>${count}</td></tr>`).join("");
  const gapRows = packet.coverage.gaps.map((gap) => `<tr><td>${escapeHtml(gap.category)}</td><td>${escapeHtml(gap.surface)}</td><td>${escapeHtml(gap.filePath)}:${gap.lineSpan.startLine}-${gap.lineSpan.endLine}</td><td>${escapeHtml(gap.ruleId)}</td></tr>`).join("");
  return `<!doctype html><html lang="en"><meta charset="utf-8"><title>TraceMap Base44 Static Evidence</title><style>body{font:16px system-ui;max-width:72rem;margin:2rem auto;padding:0 1rem;color:#18202a}code{word-break:break-all}table{border-collapse:collapse}th,td{border:1px solid #ccd3da;padding:.5rem;text-align:left}.warning{background:#fff4ce;padding:1rem}</style><main><h1>TraceMap Base44 Static Evidence</h1><p><strong>Repo:</strong> ${escapeHtml(packet.source.repo)}</p><p><strong>Commit:</strong> <code>${escapeHtml(packet.source.commitSha)}</code></p><p><strong>Accepted source:</strong> <code>${escapeHtml(packet.source.acceptedSourceSha256)}</code></p><p><strong>Accepted tree:</strong> <code>${escapeHtml(packet.source.acceptedTreeSha256)}</code></p><p><strong>Coverage:</strong> ${escapeHtml(packet.coverage.label)}</p><p><strong>Coverage status:</strong> ${escapeHtml(coverageStatus(packet))}</p><p class="warning">Static evidence is not runtime proof. Absence is coverage-qualified.</p><table><thead><tr><th>Fact type</th><th>Count</th></tr></thead><tbody>${rows}</tbody></table><h2>Known gaps</h2><ul>${packet.coverage.knownGaps.map((gap) => `<li>${escapeHtml(gap)}</li>`).join("") || "<li>None recorded by this scan.</li>"}</ul><h2>Producer-owned coverage gaps</h2>${gapRows ? `<table><thead><tr><th>Category</th><th>Surface</th><th>Source</th><th>Rule</th></tr></thead><tbody>${gapRows}</tbody></table>` : "<p>None recorded by this scan.</p>"}</main></html>\n`;
}

function coverageStatus(packet: Base44EvidencePacket): string {
  return packet.coverage.gaps.length > 0
    ? `partial:${packet.coverage.gaps.length}:producer-owned-coverage-gaps`
    : "complete-within-declared-coverage";
}

function coverageGapMarkdown(packet: Base44EvidencePacket): string[] {
  if (!packet.coverage.gaps.length) return ["- None recorded by this scan."];
  return packet.coverage.gaps.map((gap) =>
    `- \`${gap.category}\` ${gap.surface} at \`${gap.filePath}:${gap.lineSpan.startLine}-${gap.lineSpan.endLine}\` (${gap.ruleId})`);
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
