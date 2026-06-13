import fs from "node:fs/promises";
import path from "node:path";
import { CodeFact, EvidenceTiers, FactTypes, ScanManifest, ScanOptions, ScanResult } from "../facts/Models";
import { createEvidence, createFact } from "../facts/FactFactory";
import { RuleIds, ScannerVersions } from "../facts/RuleIds";
import { collectFileInventory } from "./FileInventory";
import { getGitMetadata } from "./GitMetadataProvider";
import { AnalysisGapCollector } from "./AnalysisGapCollector";
import { aggregateDiagnostics } from "./DiagnosticAggregator";
import { extractPackageFacts } from "../extractors/PackageJsonExtractor";
import { extractConfigFacts } from "../extractors/ConfigExtractor";
import { loadTypeScriptProjects } from "../extractors/TypeScriptProjectLoader";
import { extractSyntaxFacts } from "../extractors/TypeScriptSyntaxExtractor";
import { extractSemanticFacts } from "../extractors/TypeScriptSemanticExtractor";
import { extractIntegrationFacts } from "../extractors/IntegrationExtractor";
import { writeFactsJsonl } from "../storage/JsonlFactWriter";
import { writeManifest } from "../storage/ManifestWriter";
import { writeSqliteIndex } from "../storage/SqliteIndexWriter";
import { writeMarkdownReport } from "../reporting/MarkdownReportWriter";
import { hash } from "../util/Hash";
import { isUnderPath } from "../util/Paths";

export async function scan(options: ScanOptions): Promise<ScanResult> {
  const repoPath = path.resolve(options.repoPath);
  const outputPath = path.resolve(options.outputPath);
  await ensureRepo(repoPath);
  ensureSafeOutputPath(repoPath, outputPath);
  await fs.rm(outputPath, { recursive: true, force: true });
  await fs.mkdir(path.join(outputPath, "logs"), { recursive: true });

  const git = await getGitMetadata(repoPath);
  if (git.commitSha === "unknown") {
    throw new Error("TraceMap TypeScript scan requires git commit SHA. Run inside a git checkout with at least one commit.");
  }
  const inventory = await collectFileInventory(options);
  const manifest: ScanManifest = {
    scanId: createScanId(git, inventory),
    repoName: git.repoName,
    remoteUrl: git.remoteUrl,
    branch: git.branch,
    commitSha: git.commitSha,
    scannerVersion: ScannerVersions.TraceMapTypeScript,
    scannedAt: new Date().toISOString(),
    analysisLevel: "Level3SyntaxAnalysis",
    buildStatus: "NotRun",
    solutions: [],
    projects: [],
    targetFrameworks: [],
    knownGaps: [...git.knownGaps],
    scanRootRelativePath: git.gitRootPath ? normalizeManifestPath(path.relative(git.gitRootPath, repoPath)) : ".",
    scanRootPathHash: hash(repoPath),
    gitRootHash: git.gitRootPath ? hash(path.resolve(git.gitRootPath)) : null
  };
  const gapCollector = new AnalysisGapCollector();
  for (const gitGap of git.knownGaps) {
    gapCollector.add(manifest, "git-metadata", gitGap);
  }

  const facts: CodeFact[] = [];
  facts.push(...inventoryFacts(manifest, inventory));
  for (const skipped of inventory.filter((item) => item.skipped)) {
    gapCollector.add(manifest, "file-size-limit", "File exceeded max byte-size threshold.", skipped.relativePath, 1, { sizeBytes: skipped.sizeBytes });
  }
  facts.push(...await extractPackageFacts(manifest, repoPath, inventory));
  facts.push(...await extractConfigFacts(manifest, inventory));

  const projects = options.semantic ? await loadTypeScriptProjects(repoPath, options, inventory) : [];
  manifest.projects = projects.map((project) => project.projectPath).sort();
  manifest.targetFrameworks = [...new Set(projects.map((project) => String(project.parsed.options.target ? project.parsed.options.target : "default")))].sort();
  if (options.semantic && projects.length === 0) {
    gapCollector.add(manifest, "project-load", "No tsconfig.json was found; syntax fallback only.");
  }
  for (const project of projects) {
    facts.push(projectFact(manifest, project.projectPath));
    for (const diagnostic of aggregateDiagnostics(project.diagnostics, repoPath)) {
      gapCollector.add(manifest, diagnostic.category, `TypeScript diagnostic ${diagnostic.code}`, diagnostic.filePath, diagnostic.startLine, {
        diagnosticCode: diagnostic.code,
        count: diagnostic.count,
        messageHash: diagnostic.messageHash
      });
    }
    for (const skipped of [...project.skippedFiles].filter((file) => path.resolve(file).startsWith(repoPath))) {
      gapCollector.add(manifest, "file-size-limit", "Compiler host skipped oversized file.", path.relative(repoPath, skipped).replaceAll("\\", "/"));
    }
  }

  facts.push(...await extractSyntaxFacts(manifest, inventory));
  if (options.semantic && projects.length > 0) {
    facts.push(...await extractSemanticFacts(repoPath, manifest, projects));
    facts.push(...extractIntegrationFacts(repoPath, manifest, projects));
  }

  facts.push(...gapCollector.facts());
  manifest.knownGaps = [...new Set([...manifest.knownGaps, ...gapCollector.messages()])].sort();
  const fullSemantic = options.semantic && projects.length > 0 && manifest.commitSha !== "unknown" && manifest.knownGaps.length === 0;
  manifest.analysisLevel = fullSemantic ? "Level1SemanticAnalysis" : options.semantic && projects.length > 0 ? "Level1SemanticAnalysisReduced" : "Level3SyntaxAnalysis";
  manifest.buildStatus = fullSemantic ? "Succeeded" : options.semantic && projects.length > 0 ? "FailedOrPartial" : "NotRun";

  const result: ScanResult = { manifest, facts: dedupeFacts(facts), inventory };
  await writeManifest(path.join(outputPath, "scan-manifest.json"), result.manifest);
  await writeFactsJsonl(path.join(outputPath, "facts.ndjson"), result.facts);
  await writeSqliteIndex(path.join(outputPath, "index.sqlite"), result.manifest, result.facts);
  await writeMarkdownReport(path.join(outputPath, "report.md"), result);
  await fs.writeFile(path.join(outputPath, "logs", "analyzer.log"), analyzerLog(result), "utf8");
  return result;
}

function normalizeManifestPath(value: string): string {
  return !value || value === "." ? "." : value.replaceAll("\\", "/");
}

function createScanId(git: { repoName: string; remoteUrl: string | null; commitSha: string }, inventory: readonly { relativePath: string; kind: string; sizeBytes: number }[]): string {
  const repoIdentity = git.remoteUrl && git.remoteUrl.trim().length > 0 ? git.remoteUrl : git.repoName;
  const signature = inventory
    .map((item) => `${item.relativePath}|${item.kind}|${item.sizeBytes}`)
    .sort()
    .join("\n");
  return `scan-${hash(`${repoIdentity}|${git.commitSha}|${signature}`, 20)}`;
}

function ensureSafeOutputPath(repoPath: string, outputPath: string): void {
  const parsed = path.parse(outputPath);
  if (outputPath === parsed.root) {
    throw new Error(`Unsafe output path: ${outputPath}. Choose a dedicated TraceMap output directory.`);
  }
  if (outputPath === repoPath || isUnderPath(repoPath, outputPath)) {
    throw new Error(`Unsafe output path: ${outputPath}. Output cannot be the repository root or an ancestor of it.`);
  }
}

function inventoryFacts(manifest: ScanManifest, inventory: readonly { relativePath: string; kind: string; sizeBytes: number; skipped: boolean }[]): CodeFact[] {
  return inventory.map((item) =>
    createFact(
      manifest,
      FactTypes.FileInventoried,
      RuleIds.FileInventory,
      EvidenceTiers.Tier2Structural,
      createEvidence(item.relativePath, 1, 1, "file-inventory", ScannerVersions.FileInventoryExtractor),
      {
        targetSymbol: item.relativePath,
        properties: {
          name: path.basename(item.relativePath),
          fileKind: item.kind,
          sizeBytes: item.sizeBytes,
          skipped: item.skipped
        }
      }
    )
  );
}

function projectFact(manifest: ScanManifest, projectPath: string): CodeFact {
  return createFact(
    manifest,
    FactTypes.ProjectDeclared,
    RuleIds.TypeScriptProject,
    EvidenceTiers.Tier2Structural,
    createEvidence(projectPath, 1, 1, "typescript-project", ScannerVersions.TypeScriptProjectExtractor),
    { targetSymbol: projectPath, properties: { name: projectPath, projectPath, projectKind: "tsconfig" } }
  );
}

function dedupeFacts(facts: CodeFact[]): CodeFact[] {
  const byId = new Map<string, CodeFact>();
  for (const fact of facts) {
    byId.set(fact.factId, fact);
  }
  return [...byId.values()].sort((left, right) =>
    left.evidence.filePath.localeCompare(right.evidence.filePath)
    || left.evidence.startLine - right.evidence.startLine
    || left.factType.localeCompare(right.factType)
    || left.factId.localeCompare(right.factId)
  );
}

function analyzerLog(result: ScanResult): string {
  return [
    `TraceMap TypeScript scanner ${result.manifest.scannerVersion}`,
    `repo=${result.manifest.repoName}`,
    `commitSha=${result.manifest.commitSha}`,
    `analysisLevel=${result.manifest.analysisLevel}`,
    `buildStatus=${result.manifest.buildStatus}`,
    `files=${result.inventory.length}`,
    `facts=${result.facts.length}`,
    `knownGaps=${result.manifest.knownGaps.length}`,
    ""
  ].join("\n");
}

async function ensureRepo(repoPath: string): Promise<void> {
  const stat = await fs.stat(repoPath).catch(() => null);
  if (!stat?.isDirectory()) {
    throw new Error(`Repository path does not exist: ${repoPath}`);
  }
}
