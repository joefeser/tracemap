import fs from "node:fs/promises";
import path from "node:path";
import { CodeFact, EvidenceTiers, FactTypes, FileInventoryItem, ScanManifest } from "../facts/Models";
import { createEvidence, createFact } from "../facts/FactFactory";
import { RuleIds, ScannerVersions } from "../facts/RuleIds";
import { hash } from "../util/Hash";

export async function extractConfigFacts(manifest: ScanManifest, inventory: readonly FileInventoryItem[]): Promise<CodeFact[]> {
  const facts: CodeFact[] = [];
  for (const item of inventory.filter((file) => file.kind === "json-config" || file.relativePath.startsWith("tsconfig"))) {
    const text = await fs.readFile(item.absolutePath, "utf8");
    facts.push(
      createFact(
        manifest,
        FactTypes.ConfigFileDeclared,
        RuleIds.TypeScriptConfig,
        EvidenceTiers.Tier2Structural,
        createEvidence(item.relativePath, 1, Math.max(1, text.split(/\r?\n/).length), "typescript-config", ScannerVersions.TypeScriptConfigExtractor),
        { targetSymbol: item.relativePath, properties: { name: path.basename(item.relativePath), fileKind: item.kind } }
      )
    );
    try {
      const json = JSON.parse(text) as unknown;
      walkJson(manifest, facts, item.relativePath, text, json, []);
    } catch {
      facts.push(
        createFact(
          manifest,
          FactTypes.AnalysisGap,
          RuleIds.TypeScriptConfig,
          EvidenceTiers.Tier4Unknown,
          createEvidence(item.relativePath, 1, 1, "typescript-config", ScannerVersions.TypeScriptConfigExtractor),
          { properties: { category: "json-parse" } }
        )
      );
    }
  }
  return facts;
}

function walkJson(manifest: ScanManifest, facts: CodeFact[], filePath: string, text: string, value: unknown, pathParts: string[]): void {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    return;
  }
  const obj = value as Record<string, unknown>;
  for (const key of Object.keys(obj).sort()) {
    const next = [...pathParts, key];
    const keyPath = next.join(":");
    const child = obj[key];
    const line = lineOf(text, key);
    const valueKind = Array.isArray(child) ? "array" : child === null ? "null" : typeof child;
    facts.push(
      createFact(
        manifest,
        FactTypes.ConfigKeyDeclared,
        RuleIds.TypeScriptConfig,
        EvidenceTiers.Tier2Structural,
        createEvidence(filePath, line, line, "typescript-config", ScannerVersions.TypeScriptConfigExtractor),
        {
          targetSymbol: keyPath,
          contractElement: keyPath,
          properties: {
            keyPath,
            name: key,
            valueKind,
            valueHash: typeof child === "string" ? hash(child) : hash(JSON.stringify(child))
          }
        }
      )
    );
    walkJson(manifest, facts, filePath, text, child, next);
  }
}

function lineOf(text: string, needle: string): number {
  const index = text.indexOf(`"${needle}"`);
  return index < 0 ? 1 : text.slice(0, index).split(/\r?\n/).length;
}
