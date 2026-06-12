import { CodeFact, EvidenceSpan, ScanManifest } from "./Models";
import { hash } from "../util/Hash";

export interface CreateFactOptions {
  projectPath?: string | null;
  sourceSymbol?: string | null;
  targetSymbol?: string | null;
  contractElement?: string | null;
  properties?: Record<string, string | number | boolean | null | undefined>;
}

export function createEvidence(
  filePath: string,
  startLine: number,
  endLine: number,
  extractorId: string,
  extractorVersion: string,
  snippetHash: string | null = null
): EvidenceSpan {
  return {
    filePath,
    startLine: Math.max(1, startLine),
    endLine: Math.max(Math.max(1, startLine), endLine),
    snippetHash,
    extractorId,
    extractorVersion
  };
}

export function createFact(
  manifest: ScanManifest,
  factType: string,
  ruleId: string,
  evidenceTier: string,
  evidence: EvidenceSpan,
  options: CreateFactOptions = {}
): CodeFact {
  const properties = sortProperties(options.properties ?? {});
  const factId = createFactId(
    manifest.scanId,
    factType,
    ruleId,
    evidence.filePath,
    evidence.startLine,
    evidence.endLine,
    options.projectPath ?? null,
    options.sourceSymbol ?? null,
    options.targetSymbol ?? null,
    options.contractElement ?? null,
    properties
  );

  return {
    factId,
    scanId: manifest.scanId,
    repo: manifest.repoName,
    commitSha: manifest.commitSha,
    projectPath: options.projectPath ?? null,
    factType,
    ruleId,
    evidenceTier,
    sourceSymbol: options.sourceSymbol ?? null,
    targetSymbol: options.targetSymbol ?? null,
    contractElement: options.contractElement ?? null,
    evidence,
    properties
  };
}

export function createFactId(
  scanId: string,
  factType: string,
  ruleId: string,
  filePath: string,
  startLine: number,
  endLine: number,
  projectPath: string | null,
  sourceSymbol: string | null,
  targetSymbol: string | null,
  contractElement: string | null,
  properties: Record<string, string>
): string {
  const parts = [
    scanId,
    factType,
    ruleId,
    filePath,
    String(startLine),
    String(endLine),
    projectPath ?? "",
    sourceSymbol ?? "",
    targetSymbol ?? "",
    contractElement ?? ""
  ];
  for (const key of Object.keys(properties).sort()) {
    parts.push(`${key}=${properties[key]}`);
  }
  return `fact-${hash(parts.join("|"), 20)}`;
}

export function sortProperties(input: Record<string, string | number | boolean | null | undefined>): Record<string, string> {
  const sorted: Record<string, string> = {};
  for (const key of Object.keys(input).sort()) {
    const value = input[key];
    if (value !== undefined && value !== null) {
      sorted[key] = String(value);
    }
  }
  return sorted;
}
