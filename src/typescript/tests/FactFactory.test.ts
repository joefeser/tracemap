import { describe, expect, it } from "vitest";
import { createEvidence, createFact } from "../src/facts/FactFactory";
import { EvidenceTiers, FactTypes, ScanManifest } from "../src/facts/Models";
import { RuleIds } from "../src/facts/RuleIds";

describe("FactFactory", () => {
  it("excludes extractor version from deterministic fact id input", () => {
    const manifest = testManifest();
    const first = createFact(
      manifest,
      FactTypes.PropertyAccessed,
      RuleIds.TypeScriptSemanticPropertyAccess,
      EvidenceTiers.Tier1Semantic,
      createEvidence("src/customer.ts", 5, 5, "typescript-semantic", "v1"),
      { targetSymbol: "status", properties: { propertyName: "status", containingType: "CustomerContract" } }
    );
    const second = createFact(
      manifest,
      FactTypes.PropertyAccessed,
      RuleIds.TypeScriptSemanticPropertyAccess,
      EvidenceTiers.Tier1Semantic,
      createEvidence("src/customer.ts", 5, 5, "typescript-semantic", "v2"),
      { targetSymbol: "status", properties: { containingType: "CustomerContract", propertyName: "status" } }
    );

    expect(second.factId).toBe(first.factId);
    expect(first.properties).toEqual({ containingType: "CustomerContract", propertyName: "status" });
  });
});

function testManifest(): ScanManifest {
  return {
    scanId: "scan-test",
    repoName: "repo",
    remoteUrl: null,
    branch: null,
    commitSha: "abc",
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
