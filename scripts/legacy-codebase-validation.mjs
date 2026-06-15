#!/usr/bin/env node
import { createHash } from "node:crypto";
import { createReadStream } from "node:fs";
import { createWriteStream } from "node:fs";
import {
  access,
  mkdir,
  readdir,
  readFile,
  stat,
  writeFile
} from "node:fs/promises";
import {
  basename,
  dirname,
  isAbsolute,
  join,
  relative,
  resolve,
} from "node:path";
import { fileURLToPath } from "node:url";
import { spawn, spawnSync } from "node:child_process";
import { createInterface } from "node:readline";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const defaultRoot = resolve(scriptDir, "..");
const localRootRelative = ".tmp/legacy-codebase-validation";
const manifestRelative = `${localRootRelative}/repos.local.json`;
const defaultOutRelative = `${localRootRelative}/out`;
const summaryRelative = `${localRootRelative}/summary`;
const defaultTimeoutSeconds = 1200;
const defaultMaxArtifactBytes = 500 * 1024 * 1024;
const allowedKinds = new Set(["legacy-ui", "large-public", "unknown-legacy"]);
const requiredArtifacts = [
  "scan-manifest.json",
  "facts.ndjson",
  "index.sqlite",
  "report.md",
  "logs/analyzer.log"
];
const rawArtifactFamilies = new Set(requiredArtifacts.map((artifact) => basename(artifact)));
const publicRuleId = "legacy.validation.summary.v1";

export async function main(argv = process.argv.slice(2), { root = defaultRoot } = {}) {
  if (argv.includes("--help") || argv.includes("-h")) {
    process.stdout.write(helpText());
    return 0;
  }

  const manifestArg = argv[0] ?? manifestRelative;
  const outArg = argv[1] ?? defaultOutRelative;
  const manifestPath = validateManifestPath(manifestArg, root);
  const outRoot = validateOutputRoot(outArg, root);
  const summaryRoot = resolve(root, summaryRelative);
  await assertNoTrackedLocalValidationFiles(root);

  const manifest = await readLocalManifest(manifestPath, root);
  await mkdir(outRoot, { recursive: true });
  await mkdir(summaryRoot, { recursive: true });

  const results = [];
  for (const sample of manifest.samples) {
    const sampleOut = resolve(outRoot, sample.label);
    const result = await runSample({ sample, sampleOut, root });
    results.push(result);
    process.stdout.write(`${sample.label}: ${result.status} (${result.durationSeconds}s, facts ${result.factCount})\n`);
  }

  const summary = createLegacyValidationSummary({
    samples: results,
    generatedAt: new Date().toISOString()
  });
  const rawSummary = JSON.stringify(summary, null, 2);
  const privateFragments = privateFragmentsFromSamples(manifest.samples);
  assertPublicSafeText(rawSummary, { privateFragments });

  const jsonPath = resolve(summaryRoot, "legacy-validation-summary.json");
  const markdownPath = resolve(summaryRoot, "legacy-validation-summary.md");
  await writeFile(jsonPath, `${rawSummary}\n`, "utf8");
  const markdown = renderLegacyValidationMarkdown(summary);
  assertPublicSafeText(markdown, { privateFragments });
  await writeFile(markdownPath, markdown, "utf8");

  process.stdout.write(`Legacy validation summary: ${relative(root, markdownPath)}\n`);
  return results.some((result) => result.status === "failed" || result.status === "truncated") ? 1 : 0;
}

export function validateManifestPath(inputPath, root = defaultRoot) {
  const fullPath = resolve(root, inputPath);
  const expected = resolve(root, manifestRelative);
  if (fullPath !== expected) {
    throw new Error(`Manifest path must be ${manifestRelative}.`);
  }

  return fullPath;
}

export function validateOutputRoot(inputPath, root = defaultRoot) {
  const fullPath = resolve(root, inputPath);
  const localRoot = resolve(root, localRootRelative);
  const rel = relative(localRoot, fullPath);
  if (rel === "" || rel.startsWith("..") || isAbsolute(rel)) {
    throw new Error(`Output root must be under ${localRootRelative}.`);
  }

  return fullPath;
}

export async function assertNoTrackedLocalValidationFiles(root = defaultRoot) {
  const result = spawnSync("git", ["ls-files", localRootRelative], {
    cwd: root,
    encoding: "utf8"
  });
  if (result.status !== 0) {
    throw new Error("Unable to inspect tracked local legacy validation files.");
  }

  const tracked = result.stdout.trim().split(/\r?\n/u).filter(Boolean);
  if (tracked.length > 0) {
    throw new Error(`Local legacy validation files must remain untracked (${localRootRelative}).`);
  }
}

export async function readLocalManifest(manifestPath, root = defaultRoot) {
  const parsed = JSON.parse(await readFile(manifestPath, "utf8"));
  if (!parsed || !Array.isArray(parsed.samples) || parsed.samples.length === 0) {
    throw new Error("Local legacy validation manifest must include a non-empty samples array.");
  }

  const labels = new Set();
  const samples = parsed.samples.map((sample, index) => validateSample(sample, index, labels, root));
  return { samples };
}

function validateSample(sample, index, labels, root) {
  if (!sample || typeof sample !== "object") {
    throw new Error(`samples[${index}] must be an object.`);
  }

  const label = stringField(sample, "label", index);
  if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/u.test(label)) {
    throw new Error(`samples[${index}].label must be a neutral kebab-case label.`);
  }

  if (labels.has(label)) {
    throw new Error(`Duplicate sample label: ${label}.`);
  }
  labels.add(label);

  const kind = stringField(sample, "kind", index);
  if (!allowedKinds.has(kind)) {
    throw new Error(`samples[${index}].kind must be one of ${[...allowedKinds].join(", ")}.`);
  }

  const samplePath = stringField(sample, "path", index);
  if (!isAbsolute(samplePath)) {
    throw new Error(`samples[${index}].path must be an absolute operator-local path.`);
  }

  return {
    label,
    path: resolve(root, samplePath),
    kind,
    timeoutSeconds: positiveInteger(sample.timeoutSeconds, defaultTimeoutSeconds, `samples[${index}].timeoutSeconds`),
    maxArtifactBytes: positiveInteger(sample.maxArtifactBytes, defaultMaxArtifactBytes, `samples[${index}].maxArtifactBytes`)
  };
}

function stringField(sample, field, index) {
  const value = sample[field];
  if (typeof value !== "string" || value.trim() === "") {
    throw new Error(`samples[${index}].${field} must be a non-empty string.`);
  }
  return value;
}

function positiveInteger(value, defaultValue, field) {
  if (value === undefined || value === null) {
    return defaultValue;
  }
  if (!Number.isInteger(value) || value <= 0) {
    throw new Error(`${field} must be a positive integer.`);
  }
  return value;
}

async function runSample({ sample, sampleOut, root }) {
  await mkdir(sampleOut, { recursive: true });
  const started = Date.now();
  const command = process.env.TRACEMAP_SCAN_COMMAND
    ? splitCommand(process.env.TRACEMAP_SCAN_COMMAND)
    : ["dotnet", "run", "--project", resolve(root, "src/dotnet/TraceMap.Cli"), "--"];
  const args = [...command.slice(1), "scan", "--repo", sample.path, "--out", sampleOut];
  const run = await runProcess(command[0], args, {
    cwd: root,
    timeoutSeconds: sample.timeoutSeconds,
    logPath: join(sampleOut, "legacy-validation-run.log")
  });
  const durationSeconds = Math.round((Date.now() - started) / 1000);
  const artifactBytes = await directorySize(sampleOut);
  const artifactStatus = await inspectArtifacts(sampleOut);
  const scanSummary = await summarizeScanOutput(sampleOut, sample.kind);
  const boundLimitations = [];

  if (run.timedOut) {
    boundLimitations.push("Scan exceeded the configured timeout and was marked truncated.");
  }
  if (artifactBytes > sample.maxArtifactBytes) {
    boundLimitations.push("Output exceeded the configured artifact-size bound and was marked truncated.");
  }
  if (run.exitCode !== 0) {
    boundLimitations.push("TraceMap scan exited non-zero; any extracted evidence is partial.");
  }
  if (run.logWriteFailed) {
    boundLimitations.push("Process log writing failed; inspect local filesystem permissions or free space.");
  }
  if (scanSummary.buildStatus !== "Succeeded") {
    boundLimitations.push("Build or project load did not succeed; validation status is partial.");
  }

  const status = sampleStatus({ run, artifactBytes, maxArtifactBytes: sample.maxArtifactBytes, scanSummary, artifactStatus });

  return normalizeObject({
    label: sample.label,
    kind: sample.kind,
    status,
    exitCode: run.exitCode,
    durationSeconds,
    artifactBytes,
    artifactStatus,
    factCount: scanSummary.factCount,
    commitSha: scanSummary.commitSha,
    repositoryIdentityHash: scanSummary.repositoryIdentityHash,
    coverage: scanSummary.coverage,
    analysisLevel: scanSummary.analysisLevel,
    buildStatus: scanSummary.buildStatus,
    analyzerGapCount: scanSummary.analyzerGapCount,
    targetFrameworks: scanSummary.targetFrameworks,
    legacyIndicators: scanSummary.legacyIndicators,
    environmentGuidance: scanSummary.environmentGuidance,
    uiEventProbe: scanSummary.uiEventProbe,
    limitations: [...scanSummary.limitations, ...boundLimitations],
    outputArtifacts: requiredArtifacts.map((artifact) => ({
      name: artifact,
      present: artifactStatus[artifact] === true,
      publicSafe: !rawArtifactFamilies.has(basename(artifact))
    }))
  });
}

function splitCommand(value) {
  const parts = value.match(/(?:[^\s"]+|"[^"]*")+/gu)?.map((part) => part.replace(/^"|"$/gu, "")) ?? [];
  if (parts.length === 0) {
    throw new Error("TRACEMAP_SCAN_COMMAND must not be empty.");
  }
  return parts;
}

async function runProcess(command, args, { cwd, timeoutSeconds, logPath }) {
  try {
    await mkdir(dirname(logPath), { recursive: true });
  } catch {
    return { exitCode: 1, timedOut: false, logWriteFailed: true };
  }

  return new Promise((resolvePromise) => {
    let settled = false;
    const child = spawn(command, args, { cwd, stdio: ["ignore", "pipe", "pipe"] });
    let timedOut = false;
    let logWriteFailed = false;
    const logStream = createWriteStream(logPath, { flags: "w" });
    logStream.on("error", () => {
      logWriteFailed = true;
    });
    const writeLog = (chunk) => {
      if (!logStream.destroyed) {
        logStream.write(chunk);
      }
    };
    const finish = (result) => {
      if (settled) {
        return;
      }
      settled = true;
      clearTimeout(timer);
      const finalResult = { ...result, logWriteFailed };
      const fallback = setTimeout(() => resolvePromise(finalResult), 1000);
      fallback.unref();
      if (logStream.destroyed || logStream.writableEnded) {
        clearTimeout(fallback);
        resolvePromise(finalResult);
        return;
      }
      logStream.end(() => {
        clearTimeout(fallback);
        resolvePromise(finalResult);
      });
    };
    const timer = setTimeout(() => {
      timedOut = true;
      child.kill("SIGTERM");
      setTimeout(() => {
        if (child.exitCode === null && child.signalCode === null) {
          child.kill("SIGKILL");
        }
      }, 5000).unref();
    }, timeoutSeconds * 1000);

    child.stdout.on("data", (chunk) => {
      writeLog(chunk);
    });
    child.stderr.on("data", (chunk) => {
      writeLog(chunk);
    });
    child.on("error", (error) => {
      writeLog(`\nprocess-error: ${error.message}\n`);
      finish({ exitCode: 1, timedOut });
    });
    child.on("close", (code) => {
      finish({
        exitCode: timedOut ? 124 : code ?? 1,
        timedOut
      });
    });
  });
}

async function inspectArtifacts(sampleOut) {
  const result = {};
  for (const artifact of requiredArtifacts) {
    result[artifact] = await exists(join(sampleOut, artifact));
  }
  return result;
}

async function summarizeScanOutput(sampleOut, kind) {
  const manifest = await readJsonIfExists(join(sampleOut, "scan-manifest.json"));
  const factSummary = await summarizeFacts(join(sampleOut, "facts.ndjson"));
  const coverage = coverageFromManifest(manifest, factSummary.analyzerGapCount);
  const legacyIndicators = legacyIndicatorsFrom(manifest, factSummary);
  const environmentGuidance = environmentGuidanceFrom(manifest, factSummary);
  const limitations = [];

  if (!manifest) {
    limitations.push("scan-manifest.json was not produced; coverage is unknown.");
  } else if (coverage !== "Full") {
    limitations.push("Scan coverage is reduced; absence-of-evidence findings are coverage-relative.");
  }

  return {
    factCount: factSummary.factCount,
    commitSha: manifest?.commitSha ?? "unknown",
    repositoryIdentityHash: repositoryIdentityHashFrom(manifest),
    coverage,
    analysisLevel: manifest?.analysisLevel ?? "Unknown",
    buildStatus: manifest?.buildStatus ?? "Unknown",
    analyzerGapCount: factSummary.analyzerGapCount,
    targetFrameworks: uniqueSorted([
      ...(manifest?.targetFrameworks ?? []),
      ...factSummary.targetFrameworks
    ]),
    legacyIndicators,
    environmentGuidance,
    uiEventProbe: uiEventProbeFrom(factSummary, kind),
    limitations
  };
}

async function summarizeFacts(factsPath) {
  const summary = {
    factCount: 0,
    analyzerGapCount: 0,
    targetFrameworks: [],
    ruleIds: new Set(),
    evidenceTiers: new Set(),
    factTypes: new Map(),
    packagesConfigCount: 0,
    bindingRedirectCount: 0,
    oldStyleProjectCount: 0,
    toolsVersions: new Set(),
    sdkHints: new Set(),
    handlerMethodFacts: [],
    handlerCallFacts: [],
    eventWiringFacts: [],
    dependencyFacts: []
  };

  if (!(await exists(factsPath))) {
    return finalizeFactSummary(summary);
  }

  const rl = createInterface({
    input: createReadStream(factsPath, { encoding: "utf8" }),
    crlfDelay: Infinity
  });

  for await (const line of rl) {
    if (line.trim() === "") {
      continue;
    }
    let fact;
    try {
      fact = JSON.parse(line);
    } catch {
      continue;
    }
    summary.factCount += 1;
    addMapCount(summary.factTypes, fact.factType ?? "Unknown");
    if (fact.ruleId) {
      summary.ruleIds.add(fact.ruleId);
    }
    if (fact.evidenceTier) {
      summary.evidenceTiers.add(fact.evidenceTier);
    }
    if (fact.factType === "AnalysisGap") {
      summary.analyzerGapCount += 1;
    }
    if (fact.factType === "TargetFrameworkDeclared") {
      addIfPresent(summary.targetFrameworks, fact.contractElement);
      addIfPresent(summary.targetFrameworks, fact.properties?.targetFramework);
    }
    if (fact.factType === "PackageReferenced" && fact.properties?.manifestKind === "packages.config") {
      summary.packagesConfigCount += 1;
    }
    if (fact.factType === "ConfigFileDeclared" && safeTextIncludes(fact, "bindingRedirect")) {
      summary.bindingRedirectCount += 1;
    }
    if (safeTextIncludes(fact, "ToolsVersion")) {
      addIfPresent(summary.toolsVersions, fact.properties?.toolsVersion ?? "present");
    }
    if (safeTextIncludes(fact, "old-style") || safeTextIncludes(fact, "NonSdkStyleProject")) {
      summary.oldStyleProjectCount += 1;
    }
    if (safeTextIncludes(fact, "missing SDK") || safeTextIncludes(fact, "missing runtime") || safeTextIncludes(fact, "MSBuild")) {
      summary.sdkHints.add("missing-sdk-runtime-or-msbuild");
    }
    collectUiEventFacts(summary, fact);
  }

  return finalizeFactSummary(summary);
}

function finalizeFactSummary(summary) {
  return {
    factCount: summary.factCount,
    analyzerGapCount: summary.analyzerGapCount,
    targetFrameworks: uniqueSorted(summary.targetFrameworks),
    ruleIds: [...summary.ruleIds].sort(),
    evidenceTiers: [...summary.evidenceTiers].sort(),
    factTypes: Object.fromEntries([...summary.factTypes].sort(([a], [b]) => a.localeCompare(b))),
    packagesConfigCount: summary.packagesConfigCount,
    bindingRedirectCount: summary.bindingRedirectCount,
    oldStyleProjectCount: summary.oldStyleProjectCount,
    toolsVersions: [...summary.toolsVersions].sort(),
    sdkHints: [...summary.sdkHints].sort(),
    handlerMethodFacts: summarizedFacts(summary.handlerMethodFacts),
    handlerCallFacts: summarizedFacts(summary.handlerCallFacts),
    eventWiringFacts: summarizedFacts(summary.eventWiringFacts),
    dependencyFacts: summarizedFacts(summary.dependencyFacts)
  };
}

function collectUiEventFacts(summary, fact) {
  const evidence = summarizeFactForPublicOutput(fact);
  if (isHandlerMethodFact(fact)) {
    summary.handlerMethodFacts.push(evidence);
  }
  if (fact.factType === "CallEdge" && isHandlerLike(fact.sourceSymbol)) {
    summary.handlerCallFacts.push(evidence);
  }
  if (safeTextIncludes(fact, "Click") || safeTextIncludes(fact, "OnClick") || safeTextIncludes(fact, "InitializeComponent") || safeTextIncludes(fact, "+=")) {
    summary.eventWiringFacts.push(evidence);
  }
  if (isDependencySurfaceFact(fact) && isHandlerLike(fact.sourceSymbol)) {
    summary.dependencyFacts.push(evidence);
  }
}

function isHandlerMethodFact(fact) {
  return fact.factType === "MethodDeclared"
    && (isHandlerLike(fact.sourceSymbol) || isHandlerLike(fact.targetSymbol) || isHandlerLike(fact.contractElement) || safeTextIncludes(fact, "Click"));
}

function isHandlerLike(value) {
  return typeof value === "string" && /(?:^|[._])(?:on)?[a-z0-9_]*click(?:$|[._])|handler|initializecomponent/iu.test(value);
}

function isDependencySurfaceFact(fact) {
  return [
    "DependencyResolved",
    "HttpCallDetected",
    "DapperCallDetected",
    "SqlCommandDetected",
    "SqlTextUsed",
    "ConfigBinding",
    "ConfigKeyDeclared",
    "ConnectionStringDeclared",
    "DatabaseColumnMapping"
  ].includes(fact.factType);
}

function summarizeFactForPublicOutput(fact) {
  const filePath = safeRelativeFilePath(fact.evidence?.filePath);
  const originalPath = typeof fact.evidence?.filePath === "string" ? fact.evidence.filePath : "unknown";
  return normalizeObject({
    factType: fact.factType ?? "Unknown",
    ruleId: fact.ruleId ?? "unknown",
    evidenceTier: fact.evidenceTier ?? "Tier4Unknown",
    span: {
      filePath,
      filePathHash: `path-hash:${hashValue(originalPath, 24)}`,
      snippetHash: fact.evidence?.snippetHash ?? "unknown",
      startLine: Number.isInteger(fact.evidence?.startLine) ? fact.evidence.startLine : 0,
      endLine: Number.isInteger(fact.evidence?.endLine) ? fact.evidence.endLine : 0
    }
  });
}

function summarizedFacts(items) {
  const ruleIds = new Set();
  const evidenceTiers = new Set();
  const factTypes = new Set();
  for (const item of items) {
    ruleIds.add(item.ruleId);
    evidenceTiers.add(item.evidenceTier);
    factTypes.add(item.factType);
  }

  return {
    count: items.length,
    factTypes: [...factTypes].sort(),
    ruleIds: [...ruleIds].sort(),
    evidenceTiers: [...evidenceTiers].sort(),
    examples: items.slice(0, 5)
  };
}

function safeRelativeFilePath(filePath) {
  if (typeof filePath !== "string" || filePath.trim() === "") {
    return undefined;
  }
  const normalized = filePath.replaceAll("\\", "/");
  if (normalized.startsWith("/") || /^[A-Za-z]:\//u.test(normalized)) {
    return undefined;
  }
  const parts = normalized.split("/");
  if (parts.some((part) => part === "" || part === "." || part === "..")) {
    return undefined;
  }
  return normalized;
}

function safeTextIncludes(fact, token) {
  const haystack = [
    fact.factType,
    fact.ruleId,
    fact.evidenceTier,
    fact.sourceSymbol,
    fact.targetSymbol,
    fact.contractElement,
    ...Object.keys(fact.properties ?? {}),
    ...Object.values(fact.properties ?? {})
  ].filter((value) => typeof value === "string").join("\n");
  return haystack.toLowerCase().includes(token.toLowerCase());
}

function coverageFromManifest(manifest, analyzerGapCount) {
  if (!manifest) {
    return "Unknown";
  }
  if (manifest.analysisLevel === "Level1SemanticAnalysis" && manifest.buildStatus === "Succeeded" && analyzerGapCount === 0) {
    return "Full";
  }
  return "Reduced";
}

export function sampleStatus({ run, artifactBytes, maxArtifactBytes, scanSummary, artifactStatus }) {
  if (run.timedOut || artifactBytes > maxArtifactBytes) {
    return "truncated";
  }
  if (run.exitCode !== 0 || Object.values(artifactStatus).some((present) => present !== true)) {
    return "failed";
  }
  if (scanSummary.coverage !== "Full" || scanSummary.buildStatus !== "Succeeded") {
    return "partial";
  }
  return "completed";
}

function repositoryIdentityHashFrom(manifest) {
  if (!manifest) {
    return "unknown";
  }
  const stableParts = [
    manifest.gitRootHash,
    manifest.scanRootPathHash,
    manifest.remoteUrl,
    manifest.repoName
  ].filter((value) => typeof value === "string" && value.length > 0);
  if (stableParts.length === 0) {
    return "unknown";
  }
  return `repo-hash:${hashValue(stableParts.join("|"), 24)}`;
}

function legacyIndicatorsFrom(manifest, factSummary) {
  const targetFrameworks = uniqueSorted([
    ...(manifest?.targetFrameworks ?? []),
    ...factSummary.targetFrameworks
  ]);
  return normalizeObject({
    oldTargetFrameworks: targetFrameworks.filter(isLegacyTargetFramework),
    packagesConfigCount: factSummary.packagesConfigCount,
    bindingRedirectCount: factSummary.bindingRedirectCount,
    oldStyleProjectCount: factSummary.oldStyleProjectCount,
    toolsVersions: factSummary.toolsVersions
  });
}

function environmentGuidanceFrom(manifest, factSummary) {
  const guidance = [];
  const analysisLevel = manifest?.analysisLevel ?? "Unknown";
  const buildStatus = manifest?.buildStatus ?? "Unknown";
  if (analysisLevel !== "Level1SemanticAnalysis" || buildStatus !== "Succeeded") {
    guidance.push("Project load or semantic analysis did not complete; inspect local analyzer logs for SDK, runtime, MSBuild, restore, or project-type requirements.");
  }
  if (factSummary.sdkHints.length > 0) {
    guidance.push("Scanner facts include environment/tooling hints; treat them as evidence-backed guidance, not guaranteed remediation.");
  }
  return guidance.sort();
}

function uiEventProbeFrom(factSummary, kind) {
  const facts = [
    factSummary.handlerMethodFacts,
    factSummary.handlerCallFacts,
    factSummary.eventWiringFacts,
    factSummary.dependencyFacts
  ];
  const total = facts.reduce((sum, item) => sum + item.count, 0);
  const gaps = [];
  if (total === 0 && (kind === "legacy-ui" || kind === "unknown-legacy")) {
    gaps.push({
      gapKind: "LegacyUiEventEvidenceUnavailable",
      ruleId: publicRuleId,
      evidenceTier: "Tier4Unknown",
      followUpSpec: "legacy-ui-event-surfaces",
      message: "Current facts did not expose legacy UI event wiring evidence for this sample."
    });
  }

  return normalizeObject({
    classification: total > 0 ? "StaticEvidenceObserved" : "MissingEvidence",
    runtimeCaveat: "Static handler wiring does not prove the handler executes at runtime.",
    handlerMethods: factSummary.handlerMethodFacts,
    handlerCalls: factSummary.handlerCallFacts,
    eventWiring: factSummary.eventWiringFacts,
    dependencySurfaces: factSummary.dependencyFacts,
    gaps
  });
}

export function createLegacyValidationSummary({ samples, generatedAt = "1970-01-01T00:00:00.000Z" }) {
  return normalizeObject({
    version: "1.0",
    ruleId: publicRuleId,
    publicClaimLevel: "hidden",
    generatedAt,
    defaults: {
      timeoutSeconds: defaultTimeoutSeconds,
      maxArtifactBytes: defaultMaxArtifactBytes
    },
    safety: {
      localInputs: manifestRelative,
      rawOutputs: defaultOutRelative,
      summaryOutputs: summaryRelative,
      prePublishChecklist: [
        "neutral sample labels only",
        "no local absolute paths",
        "no raw repository remotes",
        "no private repository names",
        "no raw SQL",
        "no config values",
        "no connection strings or secrets",
        "no source snippets",
        "counts, tiers, coverage labels, limitations, and rule IDs visible"
      ]
    },
    samples: samples.map((sample) => normalizeObject(sample)),
    followUps: uniqueSorted(samples.flatMap((sample) => sample.uiEventProbe?.gaps?.map((gap) => gap.followUpSpec).filter(Boolean) ?? []))
  });
}

export function renderLegacyValidationMarkdown(summary) {
  const lines = [
    "# Legacy Codebase Validation Summary",
    "",
    `- Rule ID: \`${summary.ruleId}\``,
    `- Public claim level: \`${summary.publicClaimLevel}\``,
    `- Local input manifest: \`${summary.safety.localInputs}\``,
    `- Raw outputs: \`${summary.safety.rawOutputs}\``,
    `- Redacted outputs: \`${summary.safety.summaryOutputs}\``,
    "",
    "## Samples",
    ""
  ];

  for (const sample of summary.samples) {
    lines.push(`### ${sample.label}`, "");
    lines.push(`- Kind: \`${sample.kind}\``);
    lines.push(`- Status: \`${sample.status}\``);
    lines.push(`- Coverage: \`${sample.coverage}\``);
    lines.push(`- Repository identity: \`${sample.repositoryIdentityHash}\``);
    lines.push(`- Commit SHA: \`${sample.commitSha}\``);
    lines.push(`- Analysis level: \`${sample.analysisLevel}\``);
    lines.push(`- Build status: \`${sample.buildStatus}\``);
    lines.push(`- Duration seconds: \`${sample.durationSeconds}\``);
    lines.push(`- Fact count: \`${sample.factCount}\``);
    lines.push(`- Analyzer gaps: \`${sample.analyzerGapCount}\``);
    lines.push(`- Artifact bytes: \`${sample.artifactBytes}\``);
    lines.push(`- Target frameworks: ${inlineList(sample.targetFrameworks)}`);
    lines.push(`- Legacy indicators: ${legacyIndicatorText(sample.legacyIndicators)}`);
    lines.push(`- UI event probe: \`${sample.uiEventProbe.classification}\``);
    lines.push(`- UI event rule IDs: ${inlineList(ruleIdsFromUiProbe(sample.uiEventProbe))}`);
    lines.push(`- UI event caveat: ${sample.uiEventProbe.runtimeCaveat}`);
    if (sample.limitations.length > 0) {
      lines.push(`- Limitations: ${inlineList(sample.limitations)}`);
    }
    if (sample.environmentGuidance.length > 0) {
      lines.push(`- Environment guidance: ${inlineList(sample.environmentGuidance)}`);
    }
    lines.push("");
  }

  lines.push("## Follow-Ups", "");
  if (summary.followUps.length === 0) {
    lines.push("- None recorded.");
  } else {
    for (const followUp of summary.followUps) {
      lines.push(`- \`${followUp}\``);
    }
  }

  lines.push("", "## Pre-Publish Checklist", "");
  for (const item of summary.safety.prePublishChecklist) {
    lines.push(`- ${item}`);
  }

  return `${lines.join("\n")}\n`;
}

function legacyIndicatorText(indicators) {
  const parts = [
    `old target frameworks ${indicators.oldTargetFrameworks.length}`,
    `packages.config ${indicators.packagesConfigCount}`,
    `binding redirects ${indicators.bindingRedirectCount}`,
    `old-style projects ${indicators.oldStyleProjectCount}`,
    `ToolsVersion values ${indicators.toolsVersions.length}`
  ];
  return parts.map((part) => `\`${part}\``).join(", ");
}

function ruleIdsFromUiProbe(probe) {
  const values = [
    probe.handlerMethods,
    probe.handlerCalls,
    probe.eventWiring,
    probe.dependencySurfaces
  ].flatMap((section) => section.ruleIds ?? []);
  values.push(...(probe.gaps ?? []).map((gap) => gap.ruleId));
  return uniqueSorted(values);
}

function inlineList(values) {
  if (!values || values.length === 0) {
    return "`none`";
  }
  return values.map((value) => `\`${value}\``).join(", ");
}

export function detectUnsafePublicText(text, { privateFragments = [] } = {}) {
  const checks = [
    ["local-absolute-path", /(?:^|[\s"'(])(?:\/Users\/|\/home\/|\/private\/tmp\/|\/tmp\/|[A-Za-z]:\\Users\\)/u],
    ["file-url", /file:\/\/(?:\/|[A-Za-z]:)/iu],
    ["raw-remote", /(?:git@|ssh:\/\/git@|https?:\/\/[^"'\s]+\.git\b|github\.com[:/][^"'\s]+\/[^"'\s]+)/iu],
    ["raw-sql", /\b(?:select|insert|update|delete|merge|create|alter|drop)\b[\s\S]{0,120}\b(?:from|into|table|where|set|values)\b/iu],
    ["connection-string", /\b(?:server|data source|initial catalog|user id|uid|password|pwd)\s*=\s*[^;\n]+(?:;|$)/iu],
    ["secret", /\b(?:api[_-]?key|access[_-]?token|secret|password|bearer)\b\s*[:=]\s*["']?[A-Za-z0-9_./+=-]{8,}/iu],
    ["config-value", /\b(?:appSettings|connectionStrings|endpoint|baseUrl|value)\s*[:=]\s*["'][^"']{4,}["']/iu],
    ["snippet", /\b(?:namespace|public|private|protected|internal)\s+(?:class|record|struct|interface|void|string|int|async)\b|=>|;\s*$/imu]
  ];

  for (const fragment of privateFragments) {
    if (fragment.length >= 4 && text.toLowerCase().includes(fragment.toLowerCase())) {
      return "private-name";
    }
  }

  for (const [category, pattern] of checks) {
    if (pattern.test(text)) {
      return category;
    }
  }

  return null;
}

export function assertPublicSafeText(text, options = {}) {
  const category = detectUnsafePublicText(text, options);
  if (category) {
    throw new Error(`Public-safe legacy validation summary rejected unsafe content category: ${category}.`);
  }
}

function privateFragmentsFromSamples(samples) {
  const fragments = new Set();
  for (const sample of samples) {
    for (const part of sample.path.split(/[\\/]+/u)) {
      if (!part || part === "." || part === "..") {
        continue;
      }
      if (/^(?:users|home|tmp|private|src|repos|code|git|github|work|workspace|documents|desktop|sample|samples)$/iu.test(part)) {
        continue;
      }
      if (part.toLowerCase() === sample.label.toLowerCase()) {
        continue;
      }
      if (part.length >= 4) {
        fragments.add(part);
      }
    }
  }
  return [...fragments].sort();
}

function isLegacyTargetFramework(value) {
  return /^net(?:1|2|3|4)\d*$/iu.test(value)
    || /^netcoreapp(?:1|2)\./iu.test(value)
    || /^netstandard1\./iu.test(value);
}

async function readJsonIfExists(path) {
  if (!(await exists(path))) {
    return null;
  }
  return JSON.parse(await readFile(path, "utf8"));
}

async function exists(path) {
  try {
    await access(path);
    return true;
  } catch {
    return false;
  }
}

async function directorySize(path) {
  if (!(await exists(path))) {
    return 0;
  }
  const info = await stat(path);
  if (info.isFile()) {
    return info.size;
  }
  if (!info.isDirectory()) {
    return 0;
  }

  let total = 0;
  for (const entry of await readdir(path, { withFileTypes: true })) {
    total += await directorySize(join(path, entry.name));
  }
  return total;
}

function addIfPresent(target, value) {
  if (!value || typeof value !== "string") {
    return;
  }
  if (target instanceof Set) {
    target.add(value);
  } else {
    target.push(value);
  }
}

function addMapCount(map, key) {
  map.set(key, (map.get(key) ?? 0) + 1);
}

function uniqueSorted(values) {
  return [...new Set(values.filter((value) => typeof value === "string" && value.length > 0))].sort();
}

function hashValue(value, length) {
  return createHash("sha256").update(value).digest("hex").slice(0, length);
}

function normalizeObject(value) {
  if (Array.isArray(value)) {
    return value.map((item) => normalizeObject(item));
  }
  if (!value || typeof value !== "object") {
    return value;
  }
  return Object.fromEntries(
    Object.entries(value)
      .filter(([, entryValue]) => entryValue !== undefined)
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([key, entryValue]) => [key, normalizeObject(entryValue)])
  );
}

function helpText() {
  return `Usage: ./scripts/validate-legacy-codebases.sh ${manifestRelative} ${defaultOutRelative}

Runs TraceMap scans for operator-local legacy samples, writes raw outputs under
${defaultOutRelative}, and writes redacted summary candidates under
${summaryRelative}. The only accepted manifest path is ${manifestRelative}.
`;
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  main().then((code) => {
    process.exitCode = code;
  }).catch((error) => {
    process.stderr.write(`error: ${error.message}\n`);
    process.exitCode = 1;
  });
}
