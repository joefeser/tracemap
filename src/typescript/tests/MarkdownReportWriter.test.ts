import fsp from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { writeMarkdownReport } from "../src/reporting/MarkdownReportWriter";
import { CodeFact, EvidenceTiers, FactTypes, ScanManifest, ScanResult } from "../src/facts/Models";
import { RuleIds } from "../src/facts/RuleIds";

describe("MarkdownReportWriter", () => {
  it("renders query-builder patterns with fields", async () => {
    const fact = factWithProperties({
      operationName: "findMany",
      filterFields: "status",
      sortFields: "updated_at",
      patternHash: "0123456789abcdef0123456789abcdef"
    });

    const report = await renderReport([fact]);

    expect(report).toContain("Query builder `findMany` fields `status;updated_at` pattern `0123456789abcdef0123456789abcdef`");
    expect(report).not.toContain("SQL shape `findMany`");
    expect(report).toContain("static shape evidence");
    expect(report).toContain("runtime execution");
  });

  it("renders synthetic SQL-shape patterns without raw SQL", async () => {
    const fact = factWithProperties({
      operationName: "SELECT",
      tableName: "orders",
      columnNames: "id,status,total",
      sqlSourceKind: "orm-text",
      queryShapeHash: "abcdef0123456789abcdef0123456789",
      rawSql: "SELECT id, status, total FROM orders WHERE secret = 'keep-out'"
    });

    const report = await renderReport([fact]);

    expect(report).toContain("SQL shape `SELECT` table `orders` columns `id;status;total` source `orm-text` shape `abcdef0123456789abcdef0123456789`");
    expect(report).toContain("rule `typescript.integration.querypattern.v1`");
    expect(report).not.toContain("fields `none`");
    expect(report).not.toContain("secret");
    expect(report).not.toContain("SELECT id, status, total FROM orders");
    expect(report).toMatch(/shape `[a-f0-9]{32}`/);
  });

  it("hashes unsafe SQL-shape identifiers and absolute paths", async () => {
    const fact = factWithProperties(
      {
        operationName: "SELECT",
        tableName: "orders WHERE tenant_id = 1",
        columnNames: "id,password;status",
        sqlSourceKind: "orm-text",
        queryShapeHash: "abcdef0123456789abcdef0123456789"
      },
      "/tmp/private/orders.ts"
    );

    const report = await renderReport([fact]);

    expect(report).toContain("unsafe-identifier-hash:");
    expect(report).toContain("columns `id;password;status`");
    expect(report).toContain("absolute-path-hash:");
    expect(report).not.toContain("/tmp/private");
    expect(report).not.toContain("WHERE tenant_id");
  });

  it("hashes URL-like evidence paths without flagging snake_case identifiers", async () => {
    const fact = factWithProperties(
      {
        operationName: "SELECT",
        tableName: "orders",
        columnNames: "order_id,created_by",
        sqlSourceKind: "orm-text",
        queryShapeHash: "abcdef0123456789abcdef0123456789"
      },
      "webpack://private/app/orders.ts"
    );

    const report = await renderReport([fact]);

    expect(report).toContain("columns `order_id;created_by`");
    expect(report).toContain("absolute-path-hash:");
    expect(report).not.toContain("webpack://private");
    expect(report).not.toContain("unsafe-identifier-hash:");
  });

  it("hashes Windows-style evidence paths on non-Windows hosts", async () => {
    const fact = factWithProperties(
      {
        operationName: "SELECT",
        tableName: "orders",
        columnNames: "order_id",
        sqlSourceKind: "orm-text",
        queryShapeHash: "abcdef0123456789abcdef0123456789"
      },
      "C:\\private\\orders.ts"
    );

    const report = await renderReport([fact]);

    expect(report).toContain("absolute-path-hash:");
    expect(report).not.toContain("C:\\private");
  });

  it("uses SQL-shape placeholders for missing optional metadata", async () => {
    const fact = factWithProperties({ sqlSourceKind: "orm-text" });

    const report = await renderReport([fact]);

    expect(report).toContain("SQL shape `unknown` table `unknown` columns `none` source `orm-text` shape `n/a`");
  });

  it("treats empty SQL-shape properties as missing", async () => {
    const fact = factWithProperties({
      tableName: "",
      tableNames: "orders;order_items",
      columnNames: "",
      fieldNames: "order_id,created_by",
      sqlSourceKind: "orm-text",
      queryShapeHash: "abcdef0123456789abcdef0123456789"
    });

    const report = await renderReport([fact]);

    expect(report).toContain("table `orders;order_items` columns `order_id;created_by`");
    expect(report).not.toContain("table `unknown`");
  });
});

async function renderReport(facts: CodeFact[]): Promise<string> {
  const root = await fsp.mkdtemp(path.join(os.tmpdir(), "tracemap-ts-report-"));
  const outputPath = path.join(root, "report.md");
  const result: ScanResult = { manifest: testManifest(), facts, inventory: [] };
  await writeMarkdownReport(outputPath, result);
  return fsp.readFile(outputPath, "utf8");
}

function factWithProperties(properties: Record<string, string>, filePath = "src/orders.ts"): CodeFact {
  // Synthetic SQL-shape cases in this file test report rendering only; the TypeScript extractor does not currently emit SQL-shape facts.
  return {
    factId: `fact-${Object.keys(properties).join("-") || "empty"}`,
    scanId: "scan-test",
    repo: "repo",
    commitSha: "abc123",
    projectPath: null,
    factType: FactTypes.QueryPatternDetected,
    ruleId: RuleIds.TypeScriptIntegrationQueryPattern,
    evidenceTier: EvidenceTiers.Tier2Structural,
    sourceSymbol: null,
    targetSymbol: null,
    contractElement: "query",
    evidence: {
      filePath,
      startLine: 10,
      endLine: 10,
      snippetHash: null,
      extractorId: "test",
      extractorVersion: "test/1.0"
    },
    properties
  };
}

function testManifest(): ScanManifest {
  return {
    scanId: "scan-test",
    repoName: "repo",
    remoteUrl: null,
    branch: null,
    commitSha: "abc123",
    scannerVersion: "test",
    scannedAt: "2026-01-01T00:00:00Z",
    analysisLevel: "Level1SemanticAnalysis",
    buildStatus: "Succeeded",
    solutions: [],
    projects: [],
    targetFrameworks: [],
    knownGaps: []
  };
}
