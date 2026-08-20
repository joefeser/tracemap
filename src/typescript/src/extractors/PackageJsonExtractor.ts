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
  const resolvedRepoPath = path.resolve(repoPath);
  let current = path.resolve(path.dirname(filePath));
  while (current === resolvedRepoPath || current.startsWith(resolvedRepoPath + path.sep)) {
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
  return { name: path.basename(resolvedRepoPath), version: "HEAD", rootPath: resolvedRepoPath };
}

export async function extractPackageFacts(manifest: ScanManifest, repoPath: string, inventory: readonly FileInventoryItem[]): Promise<CodeFact[]> {
  const facts: CodeFact[] = [];
  for (const item of inventory.filter((file) => path.basename(file.relativePath) === "package.json")) {
    try {
      const text = await fs.readFile(item.absolutePath, "utf8");
      const json = JSON.parse(text) as Record<string, unknown>;
      const packageName = typeof json.name === "string" ? json.name : path.basename(repoPath);
      const packageVersion = typeof json.version === "string" ? json.version : "HEAD";
      const packageManager = typeof json.packageManager === "string" ? json.packageManager.split("@", 1)[0] : "npm";
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
              ecosystem: "npm",
              manifestKind: "package.json",
              packageManager,
              sourceKind: "manifest",
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
          const versionProperties = packageVersionProperties(typeof version === "string" ? version : "unknown");
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
                  dependencyGroup: section,
                  dependencyScope: dependencyScope(section),
                  dependencySection: section,
                  ecosystem: "npm",
                  manifestKind: "package.json",
                  packageManager,
                  packageName: dependencyName,
                  sourceKind: "manifest",
                  surfaceKind: "package-config",
                  ...versionProperties
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
          const scriptText = typeof value === "string" ? value : (JSON.stringify(value) ?? "");
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
                  redactionReason: "script-command-redacted",
                  valueHash: hash(scriptText),
                  valueKind: typeof value,
                  valueLength: String(scriptText.length)
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
  facts.push(...await extractPackageLockFacts(manifest, repoPath, inventory));
  return facts;
}

/**
 * Reads npm lockfile v2/v3 metadata only.  This intentionally does not resolve,
 * fetch, or verify a tarball: integrity is the registry-declared value copied
 * from package-lock.json and is never treated as content verification.
 */
async function extractPackageLockFacts(manifest: ScanManifest, repoPath: string, inventory: readonly FileInventoryItem[]): Promise<CodeFact[]> {
  const facts: CodeFact[] = [];
  for (const item of inventory.filter((file) => path.basename(file.relativePath) === "package-lock.json")) {
    if (item.skipped) {
      facts.push(lockfileGap(manifest, item, "package-lock-size-limit", "npm package-lock.json exceeded the configured inventory file-size limit."));
      continue;
    }
    try {
      const text = await fs.readFile(item.absolutePath, "utf8");
      const json = JSON.parse(text) as Record<string, unknown>;
      const lockfileVersion = typeof json.lockfileVersion === "number" ? json.lockfileVersion : Number(json.lockfileVersion);
      const packages = json.packages;
      if ((lockfileVersion !== 2 && lockfileVersion !== 3) || !packages || typeof packages !== "object" || Array.isArray(packages)) {
        facts.push(lockfileGap(manifest, item, "package-lock-unsupported", "package-lock.json must be npm lockfile v2 or v3 with a packages map."));
        continue;
      }
      const root = await readRootPackageJson(item.absolutePath, repoPath);
      const directGroups = declaredDependencyGroups(root);
      const lockHash = hash(text, 32);
      for (const packagePath of Object.keys(packages as Record<string, unknown>).sort()) {
        if (!packagePath || packagePath === "") continue;
        const entry = (packages as Record<string, unknown>)[packagePath];
        if (!entry || typeof entry !== "object" || Array.isArray(entry)) continue;
        const packageName = packageNameFromLockPath(packagePath);
        if (!packageName) continue;
        const properties = entry as Record<string, unknown>;
        const evidenceLine = lineOf(text, packagePath.replaceAll("\\", "/"));
        const version = typeof properties.version === "string" ? properties.version.trim() : "";
        if (!version) {
          facts.push(lockfileGap(manifest, item, "package-lock-entry-version-missing", "npm lockfile package entry did not provide a resolved version.", packageName, evidenceLine));
          continue;
        }
        const integrity = typeof properties.integrity === "string" ? properties.integrity.trim() : "";
        const digest = parseSha512Integrity(integrity);
        const resolved = typeof properties.resolved === "string" ? properties.resolved.trim() : "";
        const registryOrigin = hostOnlyOrigin(resolved);
        const depth = dependencyPathDepth(packagePath);
        const direct = isDirectLockEntry(packagePath, packageName, directGroups);
        const declaredGroups = directGroups.get(packageName) ?? [];
        const packageProperties: Record<string, string> = {
          dependencyGroup: direct ? (declaredGroups.length === 1 ? declaredGroups[0] : "multiple") : "lockfile",
          dependencyRelation: direct ? "direct" : "transitive",
          dependencyPathDepth: String(depth),
          ecosystem: "npm",
          manifestKind: "package-lock.json",
          packageManager: "npm",
          packageName,
          resolvedVersion: version,
          lockfilePath: item.relativePath,
          lockfileHash: lockHash,
          sourceKind: "lockfile",
          surfaceKind: "package-config",
          version
        };
        if (direct && declaredGroups.length > 1) packageProperties.dependencyGroups = declaredGroups.join(",");
        if (registryOrigin) packageProperties.registryOrigin = registryOrigin;
        if (digest) {
          packageProperties.artifactDigestAlgorithm = "sha512-base64";
          packageProperties.artifactDigest = digest;
        } else {
          facts.push(lockfileGap(manifest, item, "LockfileDigestUnavailable", "npm lockfile entry did not provide a supported sha512 integrity value.", packageName, evidenceLine));
        }
        facts.push(createFact(
          manifest,
          FactTypes.PackageReferenced,
          RuleIds.TypeScriptPackage,
          EvidenceTiers.Tier2Structural,
          createEvidence(item.relativePath, evidenceLine, evidenceLine, "typescript-package-lock", ScannerVersions.TypeScriptPackageExtractor),
          { targetSymbol: packageName, properties: packageProperties }
        ));
      }
    } catch {
      facts.push(lockfileGap(manifest, item, "package-lock-parse", "npm package-lock.json could not be admitted as bounded metadata."));
    }
  }
  return facts;
}

async function readRootPackageJson(lockfilePath: string, repoPath: string): Promise<Record<string, unknown>> {
  const root = path.dirname(lockfilePath);
  const candidates = [path.join(root, "package.json"), path.join(repoPath, "package.json")];
  for (const candidate of candidates) {
    try {
      return JSON.parse(await fs.readFile(candidate, "utf8")) as Record<string, unknown>;
    } catch {
      // A lockfile can be nested under a workspace; try the repository root.
    }
  }
  return {};
}

function declaredDependencyGroups(json: Record<string, unknown>): Map<string, string[]> {
  const groups = new Map<string, string[]>();
  for (const section of ["dependencies", "devDependencies", "peerDependencies", "optionalDependencies"]) {
    const values = json[section];
    if (!values || typeof values !== "object" || Array.isArray(values)) continue;
    for (const name of Object.keys(values as Record<string, unknown>).sort()) {
      const current = groups.get(name) ?? [];
      if (!current.includes(section)) groups.set(name, [...current, section].sort());
    }
  }
  return groups;
}

function packageNameFromLockPath(packagePath: string): string | null {
  const segments = packagePath.split("/").filter(Boolean);
  for (let i = segments.length - 1; i >= 0; i--) {
    if (segments[i] === "node_modules" && segments[i + 1]) {
      const name = segments[i + 1].startsWith("@") && segments[i + 2] ? `${segments[i + 1]}/${segments[i + 2]}` : segments[i + 1];
      return isSafePackageName(name) ? name : null;
    }
  }
  return null;
}

function dependencyPathDepth(packagePath: string): number {
  return Math.max(1, packagePath.split("/").filter((segment) => segment === "node_modules").length);
}

function isDirectLockEntry(packagePath: string, packageName: string, directGroups: ReadonlyMap<string, readonly string[]>): boolean {
  if (!directGroups.has(packageName) || dependencyPathDepth(packagePath) !== 1) return false;
  return packagePath.replaceAll("\\", "/") === `node_modules/${packageName}`;
}

function parseSha512Integrity(value: string): string | null {
  if (!value.startsWith("sha512-")) return null;
  const digest = value.slice("sha512-".length);
  if (digest.length === 0 || digest.length > 128 || !/^[A-Za-z0-9+/]+={0,2}$/.test(digest)) return null;
  try {
    const decoded = Buffer.from(digest, "base64");
    const canonical = decoded.toString("base64");
    const normalizedInput = digest.replace(/=+$/, "");
    return decoded.length === 64 && canonical.replace(/=+$/, "") === normalizedInput ? canonical : null;
  } catch {
    return null;
  }
}

function hostOnlyOrigin(value: string): string | null {
  if (!value) return null;
  try {
    const url = new URL(value);
    if (url.protocol !== "https:" && url.protocol !== "http:") return null;
    if (url.username || url.password || url.port) return null;
    const host = url.hostname.toLowerCase();
    return /^[a-z0-9.-]+$/.test(host) ? host : null;
  } catch {
    return null;
  }
}

function isSafePackageName(value: string): boolean {
  return value.length > 0 && value.length <= 160 && /^(?:@[a-z0-9._-]+\/)?[a-z0-9._-]+$/i.test(value);
}

function lockfileGap(manifest: ScanManifest, item: FileInventoryItem, category: string, message: string, packageName?: string, line = 1): CodeFact {
  return createFact(
    manifest,
    FactTypes.AnalysisGap,
    RuleIds.TypeScriptPackage,
    EvidenceTiers.Tier4Unknown,
    createEvidence(item.relativePath, line, line, "typescript-package-lock", ScannerVersions.TypeScriptPackageExtractor),
    { targetSymbol: packageName ?? undefined, properties: { category, messageHash: hash(message, 16) } }
  );
}

function dependencyScope(section: string): string {
  switch (section) {
    case "dependencies":
      return "runtime";
    case "devDependencies":
      return "development";
    case "peerDependencies":
      return "peer";
    case "optionalDependencies":
      return "optional";
    default:
      return "unknown";
  }
}

function packageVersionProperties(value: string): Record<string, string> {
  const trimmed = value.trim();
  if (!trimmed) {
    return { packageVersion: "", version: "" };
  }
  if (isUnsafePackageVersion(trimmed)) {
    return {
      versionHash: hash(trimmed, 32),
      redactionReason: "unsafe-package-version"
    };
  }
  return { packageVersion: trimmed, version: trimmed };
}

function isUnsafePackageVersion(value: string): boolean {
  return value.includes("://")
    || value.includes("\\")
    || value.startsWith("/")
    || value.startsWith("./")
    || value.startsWith("../")
    || value.toLowerCase().startsWith("file:")
    || value.toLowerCase().startsWith("git+")
    || value.includes("${")
    || value.includes("$(")
    || value.includes("%");
}

function lineOf(text: string, needle: string): number {
  const index = text.indexOf(`"${needle}"`);
  if (index < 0) {
    return 1;
  }
  return text.slice(0, index).split(/\r?\n/).length;
}
