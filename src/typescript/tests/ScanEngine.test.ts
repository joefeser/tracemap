import fs from "node:fs";
import fsp from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";
import initSqlJs from "sql.js";
import { describe, expect, it } from "vitest";
import { scan } from "../src/scan/ScanEngine";
import { compilerInputPath } from "../src/extractors/TypeScriptProjectLoader";
import { FactTypes, ScanManifest } from "../src/facts/Models";
import { RuleIds } from "../src/facts/RuleIds";
import { exportIndex } from "../src/export/IndexExporter";
import { extractPackageFacts } from "../src/extractors/PackageJsonExtractor";
import { findSqlJsFile } from "../src/storage/SqliteIndexWriter";
import { collectFileInventory } from "../src/scan/FileInventory";

const packageRoot = process.cwd();
const repoRoot = path.resolve(packageRoot, "../..");

describe("ScanEngine", () => {
  it("scans the modern TypeScript sample and writes reducer-compatible artifacts", async () => {
    const out = await tempDir();
    const result = await scan({
      repoPath: path.join(repoRoot, "samples/typescript-modern-sample"),
      outputPath: out,
      projectPaths: [],
      includeGlobs: [],
      excludeGlobs: [],
      maxFileByteSize: 1024 * 1024,
      semantic: true
    });

    expect(fs.existsSync(path.join(out, "scan-manifest.json"))).toBe(true);
    expect(fs.existsSync(path.join(out, "facts.ndjson"))).toBe(true);
    expect(fs.existsSync(path.join(out, "index.sqlite"))).toBe(true);
    expect(fs.existsSync(path.join(out, "report.md"))).toBe(true);
    expect(fs.existsSync(path.join(out, "logs/analyzer.log"))).toBe(true);
    expect(result.manifest.analysisLevel).toBe("Level1SemanticAnalysis");
    expect(result.manifest.buildStatus).toBe("Succeeded");
    expect(result.facts).toContainEqual(expect.objectContaining({ factType: FactTypes.PropertyAccessed, evidenceTier: "Tier1Semantic" }));
    expect(result.facts).toContainEqual(expect.objectContaining({ factType: FactTypes.MethodInvoked, evidenceTier: "Tier1Semantic" }));
    const roleBackedArgument = result.facts.find((fact) =>
      fact.factType === FactTypes.ArgumentPassed
      && fact.ruleId === RuleIds.TypeScriptSemanticValueFlow
      && Boolean(fact.properties.argumentSymbolId)
      && Boolean(fact.properties.parameterSymbolId)
    );
    expect(roleBackedArgument?.properties).toEqual(expect.objectContaining({
      argumentSymbolLanguage: "typescript",
      argumentSymbolDisplayName: expect.any(String),
      parameterName: expect.any(String),
      parameterSymbolLanguage: "typescript",
      parameterSymbolDisplayName: expect.any(String)
    }));
    expect(result.facts).toContainEqual(expect.objectContaining({ factType: FactTypes.HttpRouteBinding }));
    expect(result.facts).toContainEqual(expect.objectContaining({ factType: FactTypes.ConfigKeyDeclared, targetSymbol: "CUSTOMER_ENDPOINT" }));
    expect(result.facts).toContainEqual(expect.objectContaining({ factType: FactTypes.QueryPatternDetected }));
    expect(result.facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.PackageReferenced,
      properties: expect.objectContaining({
        dependencyGroup: "dependencies",
        dependencyScope: "runtime",
        ecosystem: "npm",
        manifestKind: "package.json",
        packageName: "express",
        surfaceKind: "package-config"
      })
    }));
    expect(result.facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.ConfigKeyDeclared,
      targetSymbol: "scripts:build",
      properties: expect.objectContaining({
        redactionReason: "script-command-redacted",
        valueHash: expect.stringMatching(/^[0-9a-f]+$/),
        valueLength: "20"
      })
    }));
    expect(result.facts).toContainEqual(expect.objectContaining({ factType: FactTypes.ObjectShapeInferred }));
    const prismaPattern = result.facts.find((fact) => fact.factType === FactTypes.QueryPatternDetected && fact.properties.orm === "prisma");
    expect(prismaPattern?.properties.filterFields).toContain("status");
    const entityPattern = result.facts.find((fact) => fact.factType === FactTypes.QueryPatternDetected && fact.properties.integration === "base44-entity");
    expect(entityPattern?.properties.entityName).toBe("Customer");
    expect(entityPattern?.properties.filterFields).toContain("organization_id");
    expect(entityPattern?.properties.sortFields).toContain("updated_at");
    expect(JSON.stringify(result.facts)).not.toContain("organization_id: \"org_1\"");
    expect(JSON.stringify(result.facts)).not.toContain("tsc -p tsconfig.json");

    const sqlJs = await initSqlJs({ locateFile: (file) => findSqlJsFile(file) });
    const db = new sqlJs.Database(fs.readFileSync(path.join(out, "index.sqlite")));
    try {
      const factColumns = db.exec("pragma table_info(facts)")[0]?.values.map((row) => row[1]);
      expect(factColumns).toEqual(expect.arrayContaining(["extractor_id", "extractor_version"]));
      const provenance = db.exec("select extractor_id, extractor_version from facts where extractor_id is not null limit 1");
      expect(provenance[0]?.values[0]).toEqual([expect.any(String), expect.any(String)]);
      const rows = db.exec("select role, count(*) from fact_symbols where role in ('argument', 'parameter') group by role order by role");
      expect(rows[0]?.values).toEqual([
        ["argument", expect.any(Number)],
        ["parameter", expect.any(Number)]
      ]);
      expect(Number(rows[0].values[0][1])).toBeGreaterThan(0);
      expect(Number(rows[0].values[1][1])).toBeGreaterThan(0);
    } finally {
      db.close();
    }
  });

  it("runs syntax fallback for a repo with no tsconfig and broken syntax", async () => {
    const out = await tempDir();
    const result = await scan({
      repoPath: path.join(repoRoot, "samples/typescript-broken-sample"),
      outputPath: out,
      projectPaths: [],
      includeGlobs: [],
      excludeGlobs: [],
      maxFileByteSize: 1024 * 1024,
      semantic: true
    });

    expect(result.manifest.analysisLevel).toBe("Level3SyntaxAnalysis");
    expect(result.manifest.buildStatus).toBe("NotRun");
    expect(result.facts).toContainEqual(expect.objectContaining({ factType: FactTypes.AnalysisGap }));
    expect(result.facts).toContainEqual(expect.objectContaining({ factType: FactTypes.TypeDeclared, targetSymbol: "BrokenContract" }));
  });

  it("redacts non-string package scripts without crashing", async () => {
    const root = await tempDir();
    const repo = path.join(root, "repo");
    const packagePath = path.join(repo, "package.json");
    await fsp.mkdir(repo, { recursive: true });
    await fsp.writeFile(packagePath, JSON.stringify({ name: "demo", scripts: { empty: null, object: { command: "build" } } }, null, 2));

    const facts = await extractPackageFacts(manifest("demo"), repo, [{
      absolutePath: packagePath,
      kind: "package-json",
      relativePath: "package.json",
      sizeBytes: (await fsp.stat(packagePath)).size,
      skipped: false
    }]);

    expect(facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.ConfigKeyDeclared,
      targetSymbol: "scripts:empty",
      properties: expect.objectContaining({
        valueKind: "object",
        valueLength: "4"
      })
    }));
    expect(facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.ConfigKeyDeclared,
      targetSymbol: "scripts:object",
      properties: expect.objectContaining({
        valueKind: "object",
        valueLength: "19"
      })
    }));
  });

  it("extracts npm package-lock v2/v3 identity metadata without fetching packages", async () => {
    const root = await tempDir();
    const repo = path.join(root, "repo");
    await fsp.mkdir(repo, { recursive: true });
    const packagePath = path.join(repo, "package.json");
    const lockPath = path.join(repo, "package-lock.json");
    await fsp.writeFile(packagePath, JSON.stringify({
      name: "fixture",
      dependencies: { express: "^4.0.0" },
      devDependencies: { "dev-tool": "1.0.0" },
      optionalDependencies: { "optional-tool": "1.0.0" },
      peerDependencies: { "peer-tool": "1.0.0" }
    }, null, 2));
    const sha512Digest = Buffer.alloc(64, 0x2a).toString("base64");
    await fsp.writeFile(lockPath, JSON.stringify({
      name: "fixture",
      lockfileVersion: 3,
      packages: {
        "": { dependencies: { express: "^4.0.0" } },
        "node_modules/express": {
          version: "4.18.2",
          resolved: "https://registry.npmjs.org/express/-/express-4.18.2.tgz",
          integrity: "sha512-" + sha512Digest
        },
        "node_modules/dev-tool": { version: "1.0.0" },
        "node_modules/optional-tool": { version: "1.0.0" },
        "node_modules/peer-tool": { version: "1.0.0" },
        "node_modules/versionless-one": {},
        "node_modules/versionless-two": { resolved: "https://registry.npmjs.org/versionless-two" },
        "node_modules/bar": {
          version: "2.0.0"
        },
        "node_modules/bar/node_modules/express": {
          version: "3.0.0",
          integrity: "sha512-AAAA"
        },
        "node_modules/express/node_modules/accepts": {
          version: "1.3.8",
          resolved: "https://registry.npmjs.org/accepts/-/accepts-1.3.8.tgz"
        }
      }
    }, null, 2));
    const inventory = [
      { absolutePath: packagePath, kind: "package-json", relativePath: "package.json", sizeBytes: (await fsp.stat(packagePath)).size, skipped: false },
      { absolutePath: lockPath, kind: "package-lock", relativePath: "package-lock.json", sizeBytes: (await fsp.stat(lockPath)).size, skipped: false }
    ];
    const facts = await extractPackageFacts(manifest("npm-lock"), repo, inventory);
    expect(facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.PackageReferenced,
      properties: expect.objectContaining({
        manifestKind: "package-lock.json",
        packageName: "express",
        resolvedVersion: "4.18.2",
        dependencyRelation: "direct",
        dependencyPathDepth: "1",
        registryOrigin: "registry.npmjs.org",
        artifactDigestAlgorithm: "sha512-base64",
        artifactDigest: sha512Digest,
        lockfileHash: expect.stringMatching(/^[0-9a-f]{32}$/)
      })
    }));
    expect(facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.PackageReferenced,
      properties: expect.objectContaining({ packageName: "dev-tool", dependencyRelation: "direct", dependencyGroup: "devDependencies" })
    }));
    expect(facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.PackageReferenced,
      properties: expect.objectContaining({ packageName: "optional-tool", dependencyRelation: "direct", dependencyGroup: "optionalDependencies" })
    }));
    expect(facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.PackageReferenced,
      properties: expect.objectContaining({ packageName: "peer-tool", dependencyRelation: "direct", dependencyGroup: "peerDependencies" })
    }));
    const missingVersionGaps = facts.filter((fact) => fact.factType === FactTypes.AnalysisGap && fact.properties.category === "package-lock-entry-version-missing");
    expect(missingVersionGaps).toHaveLength(2);
    expect(missingVersionGaps.map((fact) => fact.targetSymbol).sort()).toEqual(["versionless-one", "versionless-two"]);
    expect(new Set(missingVersionGaps.map((fact) => fact.evidence.startLine)).size).toBe(2);
    expect(facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.PackageReferenced,
      properties: expect.objectContaining({ packageName: "accepts", dependencyRelation: "transitive", dependencyPathDepth: "2" })
    }));
    expect(facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.PackageReferenced,
      properties: expect.objectContaining({ packageName: "express", resolvedVersion: "3.0.0", dependencyRelation: "transitive", dependencyPathDepth: "2" })
    }));
    expect(facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.AnalysisGap,
      properties: expect.objectContaining({ category: "LockfileDigestUnavailable" })
    }));
    expect(JSON.stringify(facts)).not.toContain("express-4.18.2.tgz");
  });

  it("admits package-lock through production inventory and preserves size bounds", async () => {
    const root = await tempDir();
    const repo = path.join(root, "repo");
    const output = path.join(root, "out");
    await fsp.mkdir(repo, { recursive: true });
    await fsp.writeFile(path.join(repo, "package.json"), JSON.stringify({ name: "fixture", dependencies: { example: "1.0.0" } }));
    await fsp.writeFile(path.join(repo, "package-lock.json"), JSON.stringify({ lockfileVersion: 3, packages: { "node_modules/example": { version: "1.0.0" } } }));
    const options = { repoPath: repo, outputPath: output, projectPaths: [], includeGlobs: [], excludeGlobs: [], maxFileByteSize: 16, semantic: false };

    const inventory = await collectFileInventory(options);
    const lockfile = inventory.find((item) => item.relativePath === "package-lock.json");
    expect(lockfile).toEqual(expect.objectContaining({ kind: "package-lock", skipped: true }));

    const facts = await extractPackageFacts(manifest("bounded-lock"), repo, inventory);
    expect(facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.AnalysisGap,
      properties: expect.objectContaining({ category: "package-lock-size-limit" })
    }));
    expect(facts).not.toContainEqual(expect.objectContaining({
      factType: FactTypes.PackageReferenced,
      properties: expect.objectContaining({ sourceKind: "lockfile" })
    }));
  });

  it("can be reduced by the existing .NET reducer with review-tier fan-out handling", async () => {
    const out = await tempDir();
    await scan({
      repoPath: path.join(repoRoot, "samples/typescript-modern-sample"),
      outputPath: out,
      projectPaths: [],
      includeGlobs: [],
      excludeGlobs: [],
      maxFileByteSize: 1024 * 1024,
      semantic: true
    });

    const report = path.join(out, "impact-report.md");
    const reduce = spawnSync(
      "dotnet",
      [
        "run",
        "--project",
        "src/dotnet/TraceMap.Cli",
        "--",
        "reduce",
        "--index",
        path.join(out, "index.sqlite"),
        "--contract-delta",
        "samples/contract-deltas/typescript-modern.status.json",
        "--out",
        report
      ],
      { cwd: repoRoot, encoding: "utf8" }
    );
    expect(reduce.status, reduce.stderr + reduce.stdout).toBe(0);
    const markdown = await fsp.readFile(report, "utf8");
    expect(markdown).toContain("NeedsReview");
    expect(markdown).toContain("High fan-out match set");
    expect(markdown).toContain("PropertyAccessed");
  }, 60_000);

  it("exports deterministic JSON and Mermaid from a TypeScript index", async () => {
    const out = await tempDir();
    await scan({
      repoPath: path.join(repoRoot, "samples/typescript-modern-sample"),
      outputPath: out,
      projectPaths: [],
      includeGlobs: [],
      excludeGlobs: [],
      maxFileByteSize: 1024 * 1024,
      semantic: true
    });

    const jsonPath = path.join(out, "index-export.json");
    const mermaidPath = path.join(out, "relationships.mmd");
    const jsonResult = await exportIndex({ indexPath: path.join(out, "index.sqlite"), outputPath: jsonPath, format: "json" });
    const mermaidResult = await exportIndex({ indexPath: path.join(out, "index.sqlite"), outputPath: mermaidPath, format: "mermaid" });

    expect(jsonResult.factCount).toBeGreaterThan(0);
    expect(mermaidResult.callEdgeCount).toBeGreaterThan(0);
    const json = await fsp.readFile(jsonPath, "utf8");
    expect(json).toContain('"factsByType"');
    expect(json).toContain('"relationships"');
    expect(json).not.toContain("export class CustomerHandler");
    const mermaid = await fsp.readFile(mermaidPath, "utf8");
    expect(mermaid.startsWith("flowchart TD")).toBe(true);
  });

  it("keeps scanId stable across identical repos in different parent directories", async () => {
    const root = await tempDir();
    const repoA = path.join(root, "a", "repo");
    const repoB = path.join(root, "b", "repo");
    await writeMiniRepo(repoA);
    await writeMiniRepo(repoB);

    const resultA = await scan(scanOptions(repoA, path.join(root, "out-a")));
    const resultB = await scan(scanOptions(repoB, path.join(root, "out-b")));

    expect(resultA.manifest.scanId).toBe(resultB.manifest.scanId);
    expect(resultA.manifest.sourceSnapshotDigest).toBe(resultB.manifest.sourceSnapshotDigest);
    expect(resultA.manifest.sourceSnapshotDigest).toMatch(/^[0-9a-f]{64}$/);
    expect(resultA.facts).toContainEqual(expect.objectContaining({ factType: FactTypes.MethodDeclared, targetSymbol: expect.stringContaining("run") }));
  });

  it("changes authoritative snapshot identity for same-size dirty source bytes", async () => {
    const root = await tempDir();
    const repo = path.join(root, "repo");
    await writeMiniRepo(repo);
    const source = path.join(repo, "src", "sample.ts");

    const first = await scan(scanOptions(repo, path.join(root, "first")));
    const original = await fsp.readFile(source, "utf8");
    const changed = original.replace("value = 1", "value = 2");
    expect(Buffer.byteLength(changed)).toBe(Buffer.byteLength(original));
    await fsp.writeFile(source, changed);
    const second = await scan(scanOptions(repo, path.join(root, "second")));

    expect(first.manifest.commitSha).toBe(second.manifest.commitSha);
    expect(first.manifest.sourceSnapshotDigest).not.toBe(second.manifest.sourceSnapshotDigest);
    expect(first.manifest.scanId).not.toBe(second.manifest.scanId);
    const persisted = JSON.parse(await fsp.readFile(path.join(root, "second", "scan-manifest.json"), "utf8"));
    expect(persisted.sourceSnapshotDigest).toBe(second.manifest.sourceSnapshotDigest);
  });

  it("fails before publishing when source bytes mutate during a scan", async () => {
    const root = await tempDir();
    const repo = path.join(root, "repo");
    const output = path.join(root, "output");
    await writeMiniRepo(repo);
    const source = path.join(repo, "src", "sample.ts");
    await scan(scanOptions(repo, output));
    const baselineManifest = await fsp.readFile(path.join(output, "scan-manifest.json"));

    await expect(scan(scanOptions(repo, output), {
      beforeSnapshotVerification: async () => {
        const original = await fsp.readFile(source, "utf8");
        await fsp.writeFile(source, original.replace("value = 1", "value = 2"));
      }
    })).rejects.toThrow("SourceSnapshotChangedDuringScan");

    expect(await fsp.readFile(path.join(output, "scan-manifest.json"))).toEqual(baselineManifest);
  });

  it("fails when selected source bytes mutate after semantic loading", async () => {
    const root = await tempDir();
    const repo = path.join(root, "repo");
    const output = path.join(root, "output");
    await writeMiniRepo(repo);
    const source = path.join(repo, "src", "sample.ts");

    await expect(scan(scanOptions(repo, output), {
      afterProjectLoad: async () => {
        const original = await fsp.readFile(source, "utf8");
        await fsp.writeFile(source, original.replace("value = 1", "value = 2"));
      }
    })).rejects.toThrow("SourceSnapshotChangedDuringScan");

    await expect(fsp.stat(output)).rejects.toThrow();
  });

  it("binds semantic loading to the captured source bytes across an ABA mutation", async () => {
    const root = await tempDir();
    const repo = path.join(root, "repo");
    await writeMiniRepo(repo);
    const source = path.join(repo, "src", "sample.ts");
    const original = await fsp.readFile(source, "utf8");
    const transient = original.replace("Contract", "TransientContract");

    const result = await scan(scanOptions(repo, path.join(root, "output")), {
      afterInventoryCapture: async () => {
        await fsp.writeFile(source, transient);
      },
      afterProjectLoad: async () => {
        await fsp.writeFile(source, original);
      }
    });

    expect(result.facts).toContainEqual(expect.objectContaining({ factType: FactTypes.TypeDeclared, targetSymbol: "Contract" }));
    expect(result.facts).not.toContainEqual(expect.objectContaining({ factType: FactTypes.TypeDeclared, targetSymbol: "TransientContract" }));
  });

  it("keeps external compiler input identities distinct when path tails collide", () => {
    const repo = path.resolve("/workspace/repo");
    const first = compilerInputPath(repo, "/workspace/one/lib/index.d.ts");
    const second = compilerInputPath(repo, "/workspace/two/lib/index.d.ts");

    expect(first).not.toBe(second);
    expect(first).toMatch(/^external\/[0-9a-f]{32}\/index\.d\.ts$/);
    expect(second).toMatch(/^external\/[0-9a-f]{32}\/index\.d\.ts$/);
  });

  it("fails before publishing when an eligible source is created during a scan", async () => {
    const root = await tempDir();
    const repo = path.join(root, "repo");
    const output = path.join(root, "output");
    await writeMiniRepo(repo);

    await expect(scan(scanOptions(repo, output), {
      beforeSnapshotVerification: async () => {
        await fsp.writeFile(path.join(repo, "src", "added.ts"), "export const added = true;\n");
      }
    })).rejects.toThrow("SourceSnapshotChangedDuringScan");

    await expect(fsp.stat(output)).rejects.toThrow();
  });

  it("keeps excluded in-repo imports outside semantic evidence and snapshot identity", async () => {
    const root = await tempDir();
    const repo = path.join(root, "repo");
    await fsp.mkdir(path.join(repo, "src"), { recursive: true });
    await fsp.writeFile(path.join(repo, "tsconfig.json"), JSON.stringify({ compilerOptions: { target: "ES2022", module: "CommonJS", strict: true }, include: ["src/**/*.ts"] }));
    await fsp.writeFile(path.join(repo, "src", "sample.ts"), "import { ignored } from './ignored';\nexport const selected = ignored;\n");
    const ignored = path.join(repo, "src", "ignored.ts");
    await fsp.writeFile(ignored, "export const ignored = 'first';\n");
    initGitRepo(repo);
    const options = { ...scanOptions(repo, path.join(root, "first")), excludeGlobs: ["src/ignored.ts"] };

    const first = await scan(options);
    await fsp.writeFile(ignored, "export const ignored = 'other';\n");
    const second = await scan({ ...options, outputPath: path.join(root, "second") });

    expect(first.manifest.sourceSnapshotDigest).toBe(second.manifest.sourceSnapshotDigest);
    expect(first.manifest.scanId).toBe(second.manifest.scanId);
    expect(first.facts.some((fact) => fact.evidence.filePath === "src/ignored.ts")).toBe(false);
    expect(first.facts).toEqual(second.facts);
  });

  it("loads dependency declarations without adding them to inventory evidence", async () => {
    const root = await tempDir();
    const repo = path.join(root, "repo");
    await fsp.mkdir(path.join(repo, "src"), { recursive: true });
    await fsp.mkdir(path.join(repo, "node_modules", "fixture-package"), { recursive: true });
    await fsp.writeFile(path.join(repo, "tsconfig.json"), JSON.stringify({ compilerOptions: { target: "ES2022", module: "CommonJS", strict: true }, include: ["src/**/*.ts"] }));
    await fsp.writeFile(path.join(repo, "src", "sample.ts"), "import { dependencyValue } from 'fixture-package';\nexport const selected: string = dependencyValue;\n");
    await fsp.writeFile(path.join(repo, "node_modules", "fixture-package", "index.d.ts"), "export declare const dependencyValue: string;\n");
    initGitRepo(repo);

    const dependencyPath = path.join(repo, "node_modules", "fixture-package", "index.d.ts");
    const first = await scan(scanOptions(repo, path.join(root, "first")));
    await fsp.writeFile(dependencyPath, "export declare const dependencyValue: number;\n");
    const second = await scan(scanOptions(repo, path.join(root, "second")));

    expect(first.facts).not.toContainEqual(expect.objectContaining({
      factType: FactTypes.AnalysisGap,
      properties: expect.objectContaining({ diagnosticCode: "2307" })
    }));
    expect(first.manifest.commitSha).toBe(second.manifest.commitSha);
    expect(first.manifest.sourceSnapshotDigest).not.toBe(second.manifest.sourceSnapshotDigest);
    expect(first.manifest.scanId).not.toBe(second.manifest.scanId);
    expect(first.facts.some((fact) => fact.evidence.filePath.includes("node_modules"))).toBe(false);
    expect(first.inventory.some((item) => item.relativePath.includes("node_modules"))).toBe(false);
  });

  it("binds extended compiler configuration inputs into snapshot identity", async () => {
    const root = await tempDir();
    const repo = path.join(root, "repo");
    const configDirectory = path.join(repo, "node_modules", "fixture-config");
    await fsp.mkdir(path.join(repo, "src"), { recursive: true });
    await fsp.mkdir(configDirectory, { recursive: true });
    await fsp.writeFile(path.join(repo, "tsconfig.json"), JSON.stringify({ extends: "./node_modules/fixture-config/base.json", include: ["src/**/*.ts"] }));
    await fsp.writeFile(path.join(repo, "src", "sample.ts"), "export function sample(value) { return value; }\n");
    const extendedConfig = path.join(configDirectory, "base.json");
    await fsp.writeFile(extendedConfig, JSON.stringify({ compilerOptions: { noImplicitAny: true } }));
    initGitRepo(repo);

    const first = await scan(scanOptions(repo, path.join(root, "first")));
    await fsp.writeFile(extendedConfig, JSON.stringify({ compilerOptions: { noImplicitAny: false } }));
    const second = await scan(scanOptions(repo, path.join(root, "second")));

    expect(first.manifest.commitSha).toBe(second.manifest.commitSha);
    expect(first.manifest.sourceSnapshotDigest).not.toBe(second.manifest.sourceSnapshotDigest);
    expect(first.manifest.scanId).not.toBe(second.manifest.scanId);
    expect(first.facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.AnalysisGap,
      properties: expect.objectContaining({ diagnosticCode: "7006" })
    }));
    expect(second.facts).not.toContainEqual(expect.objectContaining({
      factType: FactTypes.AnalysisGap,
      properties: expect.objectContaining({ diagnosticCode: "7006" })
    }));
    expect(first.facts.some((fact) => fact.evidence.filePath.includes("node_modules"))).toBe(false);
    expect(first.inventory.some((item) => item.relativePath.includes("node_modules"))).toBe(false);
  });

  it("preserves prior output when a staged artifact write fails", async () => {
    const root = await tempDir();
    const repo = path.join(root, "repo");
    const output = path.join(root, "output");
    await writeMiniRepo(repo);
    await scan(scanOptions(repo, output));
    const baselineManifest = await fsp.readFile(path.join(output, "scan-manifest.json"));

    await expect(scan(scanOptions(repo, output), {
      afterManifestWrite: () => { throw new Error("SyntheticArtifactWriteFailure"); }
    })).rejects.toThrow("SyntheticArtifactWriteFailure");

    expect(await fsp.readFile(path.join(output, "scan-manifest.json"))).toEqual(baselineManifest);
    expect((await fsp.readdir(root)).some((name) => name.startsWith(".tracemap-output-"))).toBe(false);
  });

  it("produces deterministic snapshot identity for non-ASCII paths", async () => {
    const root = await tempDir();
    const repo = path.join(root, "repo");
    await writeMiniRepo(repo);
    await fsp.writeFile(path.join(repo, "src", "z.ts"), "export const z = 1;\n");
    await fsp.writeFile(path.join(repo, "src", "ä.ts"), "export const umlaut = 1;\n");
    expect(spawnSync("git", ["add", "."], { cwd: repo, encoding: "utf8" }).status).toBe(0);
    expect(spawnSync("git", ["-c", "user.email=test@example.com", "-c", "user.name=TraceMap Test", "commit", "-m", "ordinal paths"], { cwd: repo, encoding: "utf8" }).status).toBe(0);

    const first = await scan(scanOptions(repo, path.join(root, "first")));
    const second = await scan(scanOptions(repo, path.join(root, "second")));

    expect(first.manifest.sourceSnapshotDigest).toBe(second.manifest.sourceSnapshotDigest);
  });

  it("refuses unsafe output paths before deleting anything", async () => {
    const root = await tempDir();
    const repo = path.join(root, "repo");
    await writeMiniRepo(repo);

    await expect(scan(scanOptions(repo, repo))).rejects.toThrow(/Unsafe output path/);
    expect(fs.existsSync(path.join(repo, "src", "sample.ts"))).toBe(true);
  });

  it("refuses an arbitrary existing output without moving it", async () => {
    const root = await tempDir();
    const repo = path.join(root, "repo");
    const output = path.join(root, "existing");
    await writeMiniRepo(repo);
    await fsp.mkdir(output);
    await fsp.writeFile(path.join(output, "keep.txt"), "important\n");

    await expect(scan(scanOptions(repo, output))).rejects.toThrow(/not replaceable/);
    expect(await fsp.readFile(path.join(output, "keep.txt"), "utf8")).toBe("important\n");
  });

  it("refuses a complete output containing an unowned file", async () => {
    const root = await tempDir();
    const repo = path.join(root, "repo");
    const output = path.join(root, "output");
    await writeMiniRepo(repo);
    await scan(scanOptions(repo, output));
    const sentinel = path.join(output, "caller-owned.txt");
    await fsp.writeFile(sentinel, "keep\n");

    await expect(scan(scanOptions(repo, output))).rejects.toThrow(/not replaceable/);
    expect(await fsp.readFile(sentinel, "utf8")).toBe("keep\n");
  });

  it("frames option lists without delimiter collisions", async () => {
    const root = await tempDir();
    const repo = path.join(root, "repo");
    await writeMiniRepo(repo);

    const one = await scan({ ...scanOptions(repo, path.join(root, "one")), excludeGlobs: ["foo,bar"] });
    const two = await scan({ ...scanOptions(repo, path.join(root, "two")), excludeGlobs: ["foo", "bar"] });

    expect(one.manifest.scanId).not.toBe(two.manifest.scanId);
  });

  it("refuses scans when git commit SHA is unavailable", async () => {
    const root = await tempDir();
    const repo = path.join(root, "not-git");
    await fsp.mkdir(path.join(repo, "src"), { recursive: true });
    await fsp.writeFile(path.join(repo, "src", "sample.ts"), "export const value = 1;\n");

    await expect(scan(scanOptions(repo, path.join(root, "out")))).rejects.toThrow(/requires git commit SHA/);
  });

  it("marks ordinary TypeScript diagnostics as reduced coverage gaps", async () => {
    const root = await tempDir();
    const repo = path.join(root, "repo");
    await fsp.mkdir(path.join(repo, "src"), { recursive: true });
    await fsp.writeFile(path.join(repo, "tsconfig.json"), JSON.stringify({ compilerOptions: { target: "ES2022", module: "CommonJS", strict: true }, include: ["src/**/*.ts"] }, null, 2));
    await fsp.writeFile(path.join(repo, "src", "sample.ts"), "export const value: string = 1;\n");
    initGitRepo(repo);

    const result = await scan(scanOptions(repo, path.join(root, "out")));

    expect(result.manifest.analysisLevel).toBe("Level1SemanticAnalysisReduced");
    expect(result.manifest.buildStatus).toBe("FailedOrPartial");
    expect(result.facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.AnalysisGap,
      properties: expect.objectContaining({ category: "ordinary-type-error", diagnosticCode: "2322" })
    }));
  });

  it("scopes TypeScript callee parameter symbol IDs by declaration", async () => {
    const root = await tempDir();
    const repo = path.join(root, "repo");
    await fsp.mkdir(path.join(repo, "src"), { recursive: true });
    await fsp.writeFile(path.join(repo, "tsconfig.json"), JSON.stringify({ compilerOptions: { target: "ES2022", module: "CommonJS", strict: true }, include: ["src/**/*.ts"] }, null, 2));
    await fsp.writeFile(path.join(repo, "src", "service.ts"), `
      export function save(status: string): string {
        return status;
      }

      export function audit(status: string): string {
        return status;
      }
    `);
    await fsp.writeFile(path.join(repo, "src", "caller.ts"), `
      import { audit, save } from "./service";

      export function run(status: string): void {
        save(status);
        audit(status);
      }
    `);
    initGitRepo(repo);

    const result = await scan(scanOptions(repo, path.join(root, "out")));
    const parameterIds = result.facts
      .filter((fact) =>
        fact.factType === FactTypes.ArgumentPassed
        && fact.ruleId === RuleIds.TypeScriptSemanticValueFlow
        && fact.properties.parameterName === "status"
        && fact.properties.argumentSymbol === "status")
      .map((fact) => fact.properties.parameterSymbolId)
      .filter(Boolean);

    expect(parameterIds).toHaveLength(2);
    expect(new Set(parameterIds).size).toBe(2);
    expect(parameterIds).toEqual(expect.arrayContaining([
      expect.stringContaining("save parameter 0:status"),
      expect.stringContaining("audit parameter 0:status")
    ]));
  });

  it("emits direct SQL text and shape facts without relabeling Prisma query patterns", async () => {
    const root = await tempDir();
    const repo = path.join(root, "repo");
    await fsp.mkdir(path.join(repo, "src"), { recursive: true });
    await fsp.writeFile(path.join(repo, "tsconfig.json"), JSON.stringify({ compilerOptions: { target: "ES2022", module: "CommonJS", strict: true }, include: ["src/**/*.ts"] }, null, 2));
    await fsp.writeFile(path.join(repo, "src", "sql.ts"), `
      declare const client: any;
      declare const sql: any;
      declare const prisma: any;

      export async function loadOrders(table: string) {
        await client.query("SELECT id, status FROM orders WHERE id = $1");
        await client.execute(\`SELECT id FROM \${table}\`);
        await sql\`SELECT id, status FROM orders\`;
        await prisma.order.findMany({ where: { status: "open" }, select: { id: true } });
      }
    `);
    initGitRepo(repo);

    const result = await scan(scanOptions(repo, path.join(root, "out")));
    const sqlText = result.facts.filter((fact) => fact.factType === FactTypes.SqlTextUsed && fact.ruleId === RuleIds.TypeScriptIntegrationSql);
    const sqlShapes = result.facts.filter((fact) => fact.factType === FactTypes.QueryPatternDetected && fact.ruleId === RuleIds.TypeScriptIntegrationSql);
    const prismaPattern = result.facts.find((fact) => fact.factType === FactTypes.QueryPatternDetected && fact.properties.orm === "prisma");

    expect(sqlText).toContainEqual(expect.objectContaining({
      properties: expect.objectContaining({ sqlSourceKind: "literal-string", textHash: expect.stringMatching(/^[0-9a-f]{32}$/) })
    }));
    expect(sqlShapes).toContainEqual(expect.objectContaining({
      properties: expect.objectContaining({ sqlSourceKind: "literal-string", tableName: "orders", columnNames: "id;status", queryShapeHash: expect.stringMatching(/^[0-9a-f]{32}$/) })
    }));
    expect(result.facts).toContainEqual(expect.objectContaining({
      factType: FactTypes.AnalysisGap,
      ruleId: RuleIds.TypeScriptIntegrationSql,
      properties: expect.objectContaining({ sqlSourceKind: "dynamic-boundary", gapKind: "dynamic-sql-boundary" })
    }));
    expect(prismaPattern?.properties.sqlSourceKind).toBeUndefined();
  });

  it("resolves sql.js wasm assets to an existing file", () => {
    const resolved = findSqlJsFile("sql-wasm.wasm");

    expect(fs.existsSync(resolved)).toBe(true);
  });
});

async function tempDir(): Promise<string> {
  return fsp.mkdtemp(path.join(os.tmpdir(), "tracemap-ts-"));
}

function scanOptions(repoPath: string, outputPath: string) {
  return {
    repoPath,
    outputPath,
    projectPaths: [],
    includeGlobs: [],
    excludeGlobs: [],
    maxFileByteSize: 1024 * 1024,
    semantic: true
  };
}

function manifest(repoName: string): ScanManifest {
  return {
    analysisLevel: "Level1SemanticAnalysis",
    branch: "main",
    buildStatus: "Succeeded",
    commitSha: "0".repeat(40),
    knownGaps: [],
    projects: [],
    remoteUrl: null,
    repoName,
    scanId: `scan-${repoName}`,
    scannedAt: "2026-06-13T00:00:00+00:00",
    scannerVersion: "tracemap-typescript/0.1.0",
    solutions: [],
    targetFrameworks: [],
    sourceSnapshotDigest: "0".repeat(64)
  };
}

async function writeMiniRepo(repo: string): Promise<void> {
  await fsp.mkdir(path.join(repo, "src"), { recursive: true });
  await fsp.writeFile(path.join(repo, "tsconfig.json"), JSON.stringify({ compilerOptions: { target: "ES2022", module: "CommonJS", strict: true }, include: ["src/**/*.ts"] }, null, 2));
  await fsp.writeFile(path.join(repo, "src", "sample.ts"), "export interface Contract { run(value: string): void; }\nexport const value = 1;\n");
  initGitRepo(repo);
}

function initGitRepo(repo: string): void {
  expect(spawnSync("git", ["init"], { cwd: repo, encoding: "utf8" }).status).toBe(0);
  expect(spawnSync("git", ["add", "."], { cwd: repo, encoding: "utf8" }).status).toBe(0);
  const env = {
    ...process.env,
    GIT_AUTHOR_DATE: "2026-01-01T00:00:00Z",
    GIT_COMMITTER_DATE: "2026-01-01T00:00:00Z"
  };
  expect(spawnSync("git", ["-c", "user.email=test@example.com", "-c", "user.name=TraceMap Test", "commit", "-m", "initial"], { cwd: repo, env, encoding: "utf8" }).status).toBe(0);
}
