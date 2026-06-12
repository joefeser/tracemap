import fs from "node:fs/promises";
import path from "node:path";
import { CodeFact, EvidenceTiers, FactTypes, FileInventoryItem, ScanManifest } from "../facts/Models";
import { createEvidence, createFact } from "../facts/FactFactory";
import { RuleIds, ScannerVersions } from "../facts/RuleIds";
import { hash } from "../util/Hash";

export interface PackageIdentity {
  name: string;
  version: string;
  rootPath: string;
}

export async function findNearestPackageIdentity(repoPath: string, filePath: string): Promise<PackageIdentity> {
  let current = path.dirname(filePath);
  while (current.startsWith(repoPath)) {
    const packagePath = path.join(current, "package.json");
    try {
      const parsed = JSON.parse(await fs.readFile(packagePath, "utf8")) as { name?: unknown; version?: unknown };
      return {
        name: typeof parsed.name === "string" ? parsed.name : path.basename(repoPath),
        version: typeof parsed.version === "string" ? parsed.version : "HEAD",
        rootPath: current
      };
    } catch {
      const parent = path.dirname(current);
      if (parent === current) {
        break;
      }
      current = parent;
    }
  }
  return { name: path.basename(repoPath), version: "HEAD", rootPath: repoPath };
}

export async function extractPackageFacts(manifest: ScanManifest, repoPath: string, inventory: readonly FileInventoryItem[]): Promise<CodeFact[]> {
  const facts: CodeFact[] = [];
  for (const item of inventory.filter((file) => path.basename(file.relativePath) === "package.json")) {
    try {
      const text = await fs.readFile(item.absolutePath, "utf8");
      const json = JSON.parse(text) as Record<string, unknown>;
      const packageName = typeof json.name === "string" ? json.name : path.basename(repoPath);
      const packageVersion = typeof json.version === "string" ? json.version : "HEAD";
      facts.push(
        createFact(
          manifest,
          FactTypes.ProjectDeclared,
          RuleIds.TypeScriptPackage,
          EvidenceTiers.Tier2Structural,
          createEvidence(item.relativePath, 1, 1, "typescript-package", ScannerVersions.TypeScriptPackageExtractor),
          {
            targetSymbol: packageName,
            properties: {
              name: packageName,
              packageName,
              packageVersion,
              type: "package-json"
            }
          }
        )
      );
      for (const section of ["dependencies", "devDependencies", "peerDependencies", "optionalDependencies"]) {
        const dependencies = json[section];
        if (!dependencies || typeof dependencies !== "object" || Array.isArray(dependencies)) {
          continue;
        }
        for (const dependencyName of Object.keys(dependencies as Record<string, unknown>).sort()) {
          const version = (dependencies as Record<string, unknown>)[dependencyName];
          facts.push(
            createFact(
              manifest,
              FactTypes.PackageReferenced,
              RuleIds.TypeScriptPackage,
              EvidenceTiers.Tier2Structural,
              createEvidence(item.relativePath, lineOf(text, dependencyName), lineOf(text, dependencyName), "typescript-package", ScannerVersions.TypeScriptPackageExtractor),
              {
                targetSymbol: dependencyName,
                properties: {
                  name: dependencyName,
                  packageName: dependencyName,
                  packageVersion: typeof version === "string" ? version : "unknown",
                  dependencySection: section
                }
              }
            )
          );
        }
      }
      const scripts = json.scripts;
      if (scripts && typeof scripts === "object" && !Array.isArray(scripts)) {
        for (const scriptName of Object.keys(scripts as Record<string, unknown>).sort()) {
          const value = (scripts as Record<string, unknown>)[scriptName];
          facts.push(
            createFact(
              manifest,
              FactTypes.ConfigKeyDeclared,
              RuleIds.TypeScriptPackage,
              EvidenceTiers.Tier2Structural,
              createEvidence(item.relativePath, lineOf(text, scriptName), lineOf(text, scriptName), "typescript-package", ScannerVersions.TypeScriptPackageExtractor),
              {
                targetSymbol: `scripts:${scriptName}`,
                contractElement: `scripts:${scriptName}`,
                properties: {
                  keyPath: `scripts:${scriptName}`,
                  name: scriptName,
                  valueHash: hash(typeof value === "string" ? value : JSON.stringify(value)),
                  valueKind: typeof value
                }
              }
            )
          );
        }
      }
    } catch {
      facts.push(
        createFact(
          manifest,
          FactTypes.AnalysisGap,
          RuleIds.TypeScriptPackage,
          EvidenceTiers.Tier4Unknown,
          createEvidence(item.relativePath, 1, 1, "typescript-package", ScannerVersions.TypeScriptPackageExtractor),
          { properties: { category: "package-json-parse" } }
        )
      );
    }
  }
  return facts;
}

function lineOf(text: string, needle: string): number {
  const index = text.indexOf(`"${needle}"`);
  if (index < 0) {
    return 1;
  }
  return text.slice(0, index).split(/\r?\n/).length;
}
