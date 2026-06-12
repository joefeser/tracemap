import fs from "node:fs/promises";
import path from "node:path";
import { CodeFact, ScanResult } from "../facts/Models";

export async function writeMarkdownReport(filePath: string, result: ScanResult): Promise<void> {
  await fs.mkdir(path.dirname(filePath), { recursive: true });
  const factsByType = countBy(result.facts, (fact) => fact.factType);
  const factsByTier = countBy(result.facts, (fact) => fact.evidenceTier);
  const gaps = result.facts.filter((fact) => fact.factType === "AnalysisGap");
  const lines = [
    "# TraceMap TypeScript Scan Report",
    "",
    `- Repo: \`${result.manifest.repoName}\``,
    `- Commit: \`${result.manifest.commitSha}\``,
    `- Analysis level: \`${result.manifest.analysisLevel}\``,
    `- Build status: \`${result.manifest.buildStatus}\``,
    `- Projects: ${result.manifest.projects.length}`,
    `- Facts: ${result.facts.length}`,
    "",
    "## Coverage",
    "",
    result.manifest.analysisLevel === "Level1SemanticAnalysis"
      ? "Semantic analysis completed without known gaps."
      : "Analysis is reduced; no-evidence conclusions must be treated as reduced coverage.",
    "",
    "## Fact Counts",
    "",
    ...table(factsByType),
    "",
    "## Evidence Tiers",
    "",
    ...table(factsByTier),
    "",
    "## Known Gaps",
    "",
    ...(gaps.length === 0 ? ["No known gaps were recorded."] : gaps.slice(0, 25).map((fact) => `- \`${fact.properties.category ?? "gap"}\` at \`${fact.evidence.filePath}:${fact.evidence.startLine}\``)),
    "",
    "## Limitations",
    "",
    "- TypeScript `buildStatus = Succeeded` means semantic analysis succeeded; it does not mean target repo tests or bundlers ran.",
    "- Missing dependencies, unresolved path mappings, decorators, dependency injection, and runtime routing can reduce evidence quality.",
    "- Facts store hashes and spans, not raw source snippets."
  ];
  await fs.writeFile(filePath, `${lines.join("\n")}\n`, "utf8");
}

function countBy(facts: readonly CodeFact[], keySelector: (fact: CodeFact) => string): Map<string, number> {
  const counts = new Map<string, number>();
  for (const fact of facts) {
    const key = keySelector(fact);
    counts.set(key, (counts.get(key) ?? 0) + 1);
  }
  return counts;
}

function table(counts: Map<string, number>): string[] {
  return [
    "| Name | Count |",
    "| --- | ---: |",
    ...[...counts.entries()].sort(([left], [right]) => left.localeCompare(right)).map(([key, value]) => `| \`${key}\` | ${value} |`)
  ];
}
