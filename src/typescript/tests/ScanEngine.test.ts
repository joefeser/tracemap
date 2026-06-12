import fs from "node:fs";
import fsp from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { describe, expect, it } from "vitest";
import { scan } from "../src/scan/ScanEngine";
import { FactTypes } from "../src/facts/Models";
import { exportIndex } from "../src/export/IndexExporter";

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
    expect(result.facts).toContainEqual(expect.objectContaining({ factType: FactTypes.HttpRouteBinding }));
    expect(result.facts).toContainEqual(expect.objectContaining({ factType: FactTypes.ConfigKeyDeclared, targetSymbol: "CUSTOMER_ENDPOINT" }));
    expect(result.facts).toContainEqual(expect.objectContaining({ factType: FactTypes.QueryPatternDetected }));
    expect(result.facts).toContainEqual(expect.objectContaining({ factType: FactTypes.ObjectShapeInferred }));
    const prismaPattern = result.facts.find((fact) => fact.factType === FactTypes.QueryPatternDetected && fact.properties.orm === "prisma");
    expect(prismaPattern?.properties.filterFields).toContain("status");
    const entityPattern = result.facts.find((fact) => fact.factType === FactTypes.QueryPatternDetected && fact.properties.integration === "base44-entity");
    expect(entityPattern?.properties.entityName).toBe("Customer");
    expect(entityPattern?.properties.filterFields).toContain("organization_id");
    expect(entityPattern?.properties.sortFields).toContain("updated_at");
    expect(JSON.stringify(result.facts)).not.toContain("organization_id: \"org_1\"");
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

  it("can be reduced by the existing .NET reducer as DefiniteImpact", async () => {
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
    expect(markdown).toContain("DefiniteImpact");
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
});

async function tempDir(): Promise<string> {
  return fsp.mkdtemp(path.join(os.tmpdir(), "tracemap-ts-"));
}
