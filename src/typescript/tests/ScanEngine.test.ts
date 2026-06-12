import fs from "node:fs";
import fsp from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { describe, expect, it } from "vitest";
import { scan } from "../src/scan/ScanEngine";
import { FactTypes } from "../src/facts/Models";

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
});

async function tempDir(): Promise<string> {
  return fsp.mkdtemp(path.join(os.tmpdir(), "tracemap-ts-"));
}
