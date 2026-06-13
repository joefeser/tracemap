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
      const json = JSON.parse(item.relativePath.startsWith("tsconfig") ? stripJsonComments(text) : text) as unknown;
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

function stripJsonComments(text: string): string {
  let result = "";
  let inString = false;
  let escaped = false;
  for (let index = 0; index < text.length; index++) {
    const current = text[index];
    const next = text[index + 1];
    if (inString) {
      result += current;
      escaped = current === "\\" && !escaped;
      if (current === "\"" && !escaped) {
        inString = false;
      }
      if (current !== "\\") {
        escaped = false;
      }
      continue;
    }
    if (current === "\"") {
      inString = true;
      result += current;
      continue;
    }
    if (current === "/" && next === "/") {
      while (index < text.length && text[index] !== "\n") {
        index++;
      }
      result += "\n";
      continue;
    }
    if (current === "/" && next === "*") {
      index += 2;
      while (index < text.length && !(text[index] === "*" && text[index + 1] === "/")) {
        result += text[index] === "\n" ? "\n" : " ";
        index++;
      }
      index++;
      continue;
    }
    result += current;
  }
  return result;
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
