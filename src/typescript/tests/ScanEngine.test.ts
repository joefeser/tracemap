import fs from "node:fs";
import fsp from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";
import initSqlJs from "sql.js";
import { describe, expect, it } from "vitest";
import { scan } from "../src/scan/ScanEngine";
import { FactTypes, ScanManifest } from "../src/facts/Models";
import { RuleIds } from "../src/facts/RuleIds";
import { exportIndex } from "../src/export/IndexExporter";
import { extractPackageFacts } from "../src/extractors/PackageJsonExtractor";
import { findSqlJsFile } from "../src/storage/SqliteIndexWriter";

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
  });

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
    expect(resultA.facts).toContainEqual(expect.objectContaining({ factType: FactTypes.MethodDeclared, targetSymbol: expect.stringContaining("run") }));
  });

  it("refuses unsafe output paths before deleting anything", async () => {
    const root = await tempDir();
    const repo = path.join(root, "repo");
    await writeMiniRepo(repo);

    await expect(scan(scanOptions(repo, repo))).rejects.toThrow(/Unsafe output path/);
    expect(fs.existsSync(path.join(repo, "src", "sample.ts"))).toBe(true);
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
    targetFrameworks: []
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
