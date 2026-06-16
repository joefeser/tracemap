#!/usr/bin/env node
import { createHash } from "node:crypto";
import { copyFile, mkdir, readFile, stat, writeFile } from "node:fs/promises";
import { dirname, isAbsolute, relative, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { spawnSync } from "node:child_process";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const defaultRoot = resolve(scriptDir, "..");
const trackedRootRelative = "docs/validation/legacy-sample-smoke-catalog";
const localRootRelative = ".tmp/legacy-sample-smoke-catalog";
const schemaVersion = "legacy-sample-smoke-catalog.v1";
const validatorVersion = "legacy-sample-smoke-catalog-validator.v1";
const markdownSentinelPattern = /^<!-- catalog-json-sha256: ([a-f0-9]{64}) -->$/u;

const claimLevels = ["hidden", "demo-safe", "public-safe"];
const claimRank = new Map(claimLevels.map((value, index) => [value, index]));
const sourceClassifications = new Set([
  "synthetic-fixture",
  "public-repo",
  "public-archive",
  "public-doc-sample",
  "private-local",
  "operator-local",
  "unknown"
]);
const sourceIdentityKinds = new Set(["neutral-label", "safe-public-alias", "category-only"]);
const commitIdentityKinds = new Set(["public-sha", "fixture-version", "category-only"]);
const familyIds = new Set([
  "wcf-service-reference",
  "wcf-config-endpoint-shape",
  "remoting-registration",
  "remoting-channel-config",
  "webforms-event-binding",
  "webforms-markup-codebehind",
  "dbml-linq-to-sql",
  "edmx-entity-framework",
  "typed-dataset",
  "legacy-sql-or-query-surface",
  "build-environment-diagnostics",
  "msbuild-project-load-failure",
  "packages-config",
  "binding-redirects",
  "large-repo-stress",
  "fallback-syntax-scan",
  "analysis-gap-reporting"
]);
const expectationLevels = new Set(["required", "optional", "exploratory"]);
const expectationStates = new Set([
  "observed",
  "expected-not-yet-run",
  "reduced",
  "analysis-gap",
  "unsupported",
  "deferred",
  "truncated",
  "rejected"
]);
const evidenceTiers = new Set(["Tier1Semantic", "Tier2Structural", "Tier3SyntaxOrTextual", "Tier4Unknown"]);
const coverageLabels = new Set([
  "Full",
  "Reduced",
  "Level1SemanticAnalysisReduced",
  "SyntaxFallback",
  "AnalysisGap",
  "Unsupported",
  "Deferred",
  "Truncated",
  "Rejected"
]);
const commandModes = new Set(["checked-in-sample", "operator-local", "redacted-artifact-only"]);
const timeoutBuckets = new Set(["small", "medium", "large", "extra-large", "operator-defined"]);
const artifactSizeBuckets = new Set(["small", "medium", "large", "extra-large", "operator-defined"]);
const relationshipArtifactKinds = new Set([
  "redacted-validation-summary",
  "redacted-baseline-snapshot",
  "redacted-comparison-report",
  "evidence-pack-summary"
]);
const commandInputKinds = new Set([
  "legacy-validation-summary",
  "legacy-baseline",
  "legacy-baseline-comparison",
  "legacy-evidence-pack",
  "catalog-json"
]);
const extractorGapCodes = new Set([
  "extractor-not-available",
  "extractor-deferred",
  "extractor-unsupported",
  "extractor-reduced-coverage"
]);
const redactionProfiles = new Set(["catalog-metadata-only"]);
const artifactClasses = new Set([
  "scan-manifest",
  "facts-ndjson-present",
  "index-sqlite-present",
  "report-md-present",
  "analyzer-log-present",
  "redacted-summary",
  "redacted-baseline",
  "redacted-comparison",
  "evidence-pack-reference",
  "catalog-json",
  "catalog-md"
]);
const allowedPlaceholders = new Set([
  "<sample-root>",
  "<scan-output>",
  "<redacted-summary>",
  "<catalog-output>",
  "<pack-output>",
  "<catalog-json>",
  "<sample-label>",
  "<claim-level>",
  "<YYYY-MM>"
]);
const allowedCommandFlags = new Set([
  "--repo",
  "--out",
  "--input",
  "--input-kind",
  "--label",
  "--claim-level",
  "--date",
  "--catalog",
  "--expected-claim-level",
  "--dry-run"
]);
const identityOptionFlags = new Set([
  "--repo",
  "--out",
  "--input",
  "--label",
  "--claim-level",
  "--date",
  "--catalog",
  "--expected-claim-level"
]);
const rawArtifactNames = new Set([
  "scan-manifest.json",
  "facts.ndjson",
  "index.sqlite",
  "report.md",
  "analyzer.log",
  "baseline-manifest.json",
  "evidence-pack.json"
]);

export class CatalogValidationError extends Error {
  constructor(diagnostics) {
    super(formatDiagnostics(diagnostics));
    this.name = "CatalogValidationError";
    this.diagnostics = diagnostics;
  }
}

export async function main(argv = process.argv.slice(2), { root = defaultRoot } = {}) {
  const [command, ...args] = argv;
  try {
    if (!command || command === "--help" || command === "-h") {
      process.stdout.write(helpText());
      return 0;
    }

    if (command === "validate") {
      const options = parseOptions(args);
      const catalogPath = requiredOption(options, "--catalog");
      const markdownPath = options.get("--markdown") ?? defaultMarkdownFor(catalogPath);
      const result = await validateCatalogFiles({ catalogPath, markdownPath, root });
      process.stdout.write(`Catalog validation passed: ${relative(root, result.catalogPath)}\n`);
      return 0;
    }

    if (command === "render") {
      const options = parseOptions(args);
      const result = await renderCatalogCommand({ options, root });
      process.stdout.write(`${result.dryRun ? "Catalog render dry-run" : "Catalog rendered"}: ${result.files.map((file) => relative(root, file)).join(", ")}\n`);
      return 0;
    }

    if (command === "promote") {
      const options = parseOptions(args);
      const result = await promoteCatalogCommand({ options, root });
      process.stdout.write(`${result.dryRun ? "Catalog promote dry-run" : "Catalog promoted"}: ${result.files.map((file) => relative(root, file)).join(", ")}\n`);
      return 0;
    }

    throw new Error(`Unknown command: ${command}`);
  } catch (error) {
    process.stderr.write(`${error.message}\n`);
    return 1;
  }
}

export async function validateCatalogFiles({ catalogPath, markdownPath = defaultMarkdownFor(catalogPath), root = defaultRoot }) {
  const fullCatalogPath = resolve(root, catalogPath);
  const catalog = await readCatalog(fullCatalogPath);
  const ruleIds = await readRuleCatalogIds(root);
  const diagnostics = validateCatalogObject(catalog, { catalogPath: fullCatalogPath, ruleIds });

  const canonical = canonicalJson(catalog);
  const original = await readFile(fullCatalogPath, "utf8");
  if (normalizeNewlines(original) !== canonical) {
    diagnostics.push(diagnostic("catalog-json-not-canonical", fullCatalogPath, "", "Catalog JSON must use canonical ordering, indentation, LF line endings, and final newline."));
  }

  if (markdownPath) {
    const fullMarkdownPath = resolve(root, markdownPath);
    await validateMarkdown({ catalog, markdownPath: fullMarkdownPath, diagnostics });
  }

  if (diagnostics.length > 0) {
    throw new CatalogValidationError(diagnostics);
  }

  return { catalogPath: fullCatalogPath, markdownPath: markdownPath ? resolve(root, markdownPath) : undefined };
}

export function validateCatalogObject(catalog, { catalogPath = "catalog.json", ruleIds = new Set() } = {}) {
  const diagnostics = [];

  if (!catalog || typeof catalog !== "object" || Array.isArray(catalog)) {
    return [diagnostic("schema", catalogPath, "", "Catalog must be a JSON object.")];
  }

  requireString(catalog, "schemaVersion", "", schemaVersion, diagnostics, catalogPath);
  requireString(catalog, "catalogId", "", "legacy-sample-smoke-catalog", diagnostics, catalogPath);
  validateGeneratedFrom(catalog.generatedFrom, diagnostics, catalogPath);
  validateSafety(catalog.safety, diagnostics, catalogPath);
  scanJsonStrings(catalog, diagnostics, catalogPath);

  if (!Array.isArray(catalog.entries) || catalog.entries.length === 0) {
    diagnostics.push(diagnostic("entries", catalogPath, "/entries", "Catalog entries must be a non-empty array."));
    return diagnostics;
  }

  const labels = new Set();
  for (let index = 0; index < catalog.entries.length; index += 1) {
    validateEntry(catalog.entries[index], index, labels, diagnostics, catalogPath, ruleIds);
  }

  if (catalog.safety && typeof catalog.safety === "object" && claimLevels.includes(catalog.safety.classification)) {
    const entryRanks = catalog.entries
      .map((entry) => claimRank.get(entry?.claimLevel))
      .filter((rank) => rank !== undefined);
    if (entryRanks.length > 0) {
      const floor = Math.min(...entryRanks);
      const topRank = claimRank.get(catalog.safety.classification);
      if (topRank > floor) {
        diagnostics.push(diagnostic("claim-level-floor", catalogPath, "/safety/classification", "Catalog classification is higher than the least-safe included entry."));
      }
      if (topRank >= claimRank.get("demo-safe") && catalog.entries.some((entry) => entry?.claimLevel === "hidden")) {
        diagnostics.push(diagnostic("hidden-entry-in-tracked-output", catalogPath, "/entries", "Demo-safe or public-safe tracked output cannot include hidden entries."));
      }
    }
  }

  return diagnostics;
}

export async function renderCatalogCommand({ options, root = defaultRoot }) {
  const sourcePath = resolve(root, requiredOption(options, "--catalog"));
  const outDir = resolve(root, requiredOption(options, "--out"));
  const date = requiredOption(options, "--date");
  const minimumEntryClaimLevel = options.get("--minimum-entry-claim-level");
  const force = options.has("--force");
  const dryRun = options.has("--dry-run");

  validateYearMonth(date);
  if (minimumEntryClaimLevel && !["demo-safe", "public-safe"].includes(minimumEntryClaimLevel)) {
    throw new Error("--minimum-entry-claim-level must be demo-safe or public-safe.");
  }

  const source = await readCatalog(sourcePath);
  const rendered = renderCatalogObject(source, { date, minimumEntryClaimLevel });
  const catalogPath = resolve(outDir, "catalog.json");
  const markdownPath = resolve(outDir, "catalog.md");
  const diagnostics = validateCatalogObject(rendered, {
    catalogPath,
    ruleIds: await readRuleCatalogIds(root)
  });
  const markdown = renderCatalogMarkdown(rendered);
  validateMarkdownText({ catalog: rendered, markdown, markdownPath, diagnostics });
  assertTrackedDestination(outDir, root, diagnostics);

  if (diagnostics.length > 0) {
    throw new CatalogValidationError(diagnostics);
  }

  const files = [catalogPath, markdownPath];
  if (dryRun) {
    return { dryRun, files, catalog: rendered };
  }

  await mkdir(outDir, { recursive: true });
  await writeIfAllowed(catalogPath, canonicalJson(rendered), { force });
  await writeIfAllowed(markdownPath, markdown, { force });
  await validateCatalogFiles({ catalogPath, markdownPath, root });
  return { dryRun, files, catalog: rendered };
}

export async function promoteCatalogCommand({ options, root = defaultRoot }) {
  const sourceCatalogPath = resolve(root, requiredOption(options, "--catalog"));
  const sourceMarkdownPath = resolve(root, options.get("--markdown") ?? defaultMarkdownFor(sourceCatalogPath));
  const outDir = resolve(root, requiredOption(options, "--out"));
  const force = options.has("--force");
  const dryRun = options.has("--dry-run");
  const diagnostics = [];

  assertTrackedDestination(outDir, root, diagnostics);
  assertNotIgnoredDestination(outDir, root, diagnostics);
  if (diagnostics.length > 0) {
    throw new CatalogValidationError(diagnostics);
  }

  await validateCatalogFiles({ catalogPath: sourceCatalogPath, markdownPath: sourceMarkdownPath, root });

  const destCatalogPath = resolve(outDir, "catalog.json");
  const destMarkdownPath = resolve(outDir, "catalog.md");
  const files = [destCatalogPath, destMarkdownPath];
  if (dryRun) {
    return { dryRun, files };
  }

  await mkdir(outDir, { recursive: true });
  await copyIfAllowed(sourceCatalogPath, destCatalogPath, { force });
  await copyIfAllowed(sourceMarkdownPath, destMarkdownPath, { force });
  await validateCatalogFiles({ catalogPath: destCatalogPath, markdownPath: destMarkdownPath, root });
  return { dryRun, files };
}

export function renderCatalogObject(source, { date, minimumEntryClaimLevel } = {}) {
  validateYearMonth(date);
  let entries = Array.isArray(source.entries) ? [...source.entries] : [];
  if (entries.length === 0) {
    throw new Error("Catalog render requires at least one entry.");
  }
  if (entries.some((entry) => !entry || !claimRank.has(entry.claimLevel))) {
    throw new Error("Catalog render requires entries with valid claimLevel values.");
  }
  if (minimumEntryClaimLevel) {
    const minimumRank = claimRank.get(minimumEntryClaimLevel);
    entries = entries.filter((entry) => claimRank.get(entry.claimLevel) >= minimumRank);
    if (entries.length === 0) {
      throw new Error("--minimum-entry-claim-level removed every catalog entry.");
    }
  }

  const floorRank = Math.min(...entries.map((entry) => claimRank.get(entry.claimLevel)));
  const classification = claimLevels[floorRank];
  const rendered = normalizeObject({
    ...source,
    generatedFrom: {
      ...source.generatedFrom,
      generatedAt: date,
      toolVersion: source.generatedFrom?.toolVersion ?? schemaVersion
    },
    safety: {
      ...source.safety,
      classification,
      validatorVersion,
      redactionProfile: source.safety?.redactionProfile ?? "catalog-metadata-only"
    },
    entries
  });
  return rendered;
}

export function renderCatalogMarkdown(catalog) {
  const hash = catalogJsonHash(catalog);
  const lines = [
    `<!-- catalog-json-sha256: ${hash} -->`,
    "# Legacy Sample Smoke Catalog",
    "",
    `Schema: \`${catalog.schemaVersion}\``,
    `Generated: \`${catalog.generatedFrom.generatedAt}\``,
    `Classification: \`${catalog.safety.classification}\``,
    "",
    "This catalog is deterministic validation metadata for legacy sample smoke coverage. It is not raw scan output, an evidence pack, a baseline, a site page, or an impact-analysis result.",
    "",
    "## Entries",
    "",
    "| Sample | Claim | Source | Commit identity | Families | Commands | Relationships |",
    "| --- | --- | --- | --- | --- | --- | --- |"
  ];

  for (const entry of catalog.entries) {
    lines.push([
      markdownCell(`${entry.displayName} (${entry.sampleLabel})`),
      markdownCell(entry.claimLevel),
      markdownCell(`${entry.source.classification}; ${entry.source.identityKind}${entry.source.safeSourceAlias ? `; ${entry.source.safeSourceAlias}` : ""}`),
      markdownCell(`${entry.source.commitIdentity.kind}${entry.source.commitIdentity.value ? `; ${entry.source.commitIdentity.value}` : ""}${entry.source.commitIdentity.shaPresent ? "; sha-present" : ""}`),
      markdownCell(entry.families.map((family) => `${family.familyId} [${family.expectation}; ${family.statesAllowed.join(", ")}]`).join("; ")),
      markdownCell(entry.validation.commandTemplates.map((command) => command.name).join(", ")),
      markdownCell(entry.relationships.map((relationship) => `${relationship.artifactKind}:${relationship.safeArtifactId}:${relationship.claimLevel}`).join("; "))
    ].join(" | ").replace(/^/u, "| ").replace(/$/u, " |"));
  }

  lines.push(
    "",
    "## Family Expectations",
    "",
    "| Sample | Family | Rules | Tiers | Coverage | Extractors | States | Limitations |",
    "| --- | --- | --- | --- | --- | --- | --- | --- |"
  );

  for (const entry of catalog.entries) {
    for (const family of entry.families) {
      lines.push([
        markdownCell(entry.sampleLabel),
        markdownCell(family.familyId),
        markdownCell(family.expectedRuleIds.join(", ")),
        markdownCell(family.expectedEvidenceTiers.join(", ")),
        markdownCell(family.expectedCoverageLabels.join(", ")),
        markdownCell([...(family.expectedExtractors ?? []), ...(family.extractorGapCodes ?? [])].join(", ")),
        markdownCell(family.statesAllowed.join(", ")),
        markdownCell(family.limitations.map((limitation) => limitation.code).join(", "))
      ].join(" | ").replace(/^/u, "| ").replace(/$/u, " |"));
    }
  }

  lines.push(
    "",
    "## Validation Commands",
    "",
    "| Sample | Command | Mode | Template | Artifacts | Gates |",
    "| --- | --- | --- | --- | --- | --- |"
  );

  for (const entry of catalog.entries) {
    for (const command of entry.validation.commandTemplates) {
      lines.push([
        markdownCell(entry.sampleLabel),
        markdownCell(command.name),
        markdownCell(command.mode),
        markdownCell(command.template),
        markdownCell(command.expectedArtifacts.join(", ")),
        markdownCell(entry.validation.gates.join(", "))
      ].join(" | ").replace(/^/u, "| ").replace(/$/u, " |"));
    }
  }

  lines.push(
    "",
    "## Safety Limitations",
    "",
    ...catalog.safety.limitations.map((limitation) => `- \`${escapeMarkdown(limitation.code)}\`: ${escapeMarkdown(limitation.message)}`),
    "",
    "## Entry Limitations",
    ""
  );

  for (const entry of catalog.entries) {
    lines.push(`### ${escapeMarkdown(entry.sampleLabel)}`, "");
    for (const limitation of entry.limitations) {
      lines.push(`- \`${escapeMarkdown(limitation.code)}\`: ${escapeMarkdown(limitation.message)}`);
    }
    lines.push("");
  }

  return `${lines.join("\n")}`;
}

export function canonicalJson(value) {
  return `${JSON.stringify(normalizeObject(value), null, 2)}\n`;
}

export function catalogJsonHash(catalog) {
  return createHash("sha256")
    .update(`legacy-sample-smoke-catalog.v1\n${canonicalJson(catalog)}`, "utf8")
    .digest("hex");
}

export function detectUnsafeText(value) {
  if (typeof value !== "string" || value.length === 0) {
    return null;
  }
  const text = value.normalize("NFKC");

  const patterns = [
    ["unsafe-markdown", /<script\b|javascript:|onerror\s*=|onload\s*=/iu],
    ["local-absolute-path", /(^|[\s"'`(])(?:\/Users\/|\/home\/|\/var\/folders\/|[A-Za-z]:[\\/])/u],
    ["home-fragment", /(^|[\s"'`(])~[\\/]/u],
    ["file-url", /file:\/\//iu],
    ["raw-remote", /(?:git@|ssh:\/\/|https?:\/\/(?:www\.)?(?:github|gitlab|bitbucket)\.com|\.git\b)/iu],
    ["connection-string", /\b(?:Server|Data Source|User ID|Password|Pwd|Initial Catalog)\s*=/iu],
    ["secret", /\b(?:api[_-]?key|secret|token|password|passwd|bearer|private[_-]?key)\b\s*[:=]/iu],
    ["raw-sql", /\b(?:select\s+.+\s+from|insert\s+into|update\s+\w+\s+set|delete\s+from|exec(?:ute)?\s+\w+)/isu],
    ["endpoint-value", /\bhttps?:\/\/(?!example\.invalid\b)[^\s)`"']+/iu],
    ["analyzer-diagnostic", /\b(?:error|warning)\s+(?:CS|MSB|NU)\d{3,5}\b|\bat\s+\w[\w.<>]+\(.*:\d+\)/u],
    ["source-snippet", /\b(?:public|private|protected|internal)\s+(?:class|record|interface|void|string|int|async)\b/u]
  ];

  for (const [category, pattern] of patterns) {
    if (pattern.test(text)) {
      return category;
    }
  }

  const prohibitedClaim = detectProhibitedClaim(text);
  return prohibitedClaim;
}

export function detectProhibitedClaim(value) {
  const text = value.toLowerCase();
  const disclaimer = /\b(?:does not|do not|not|no|without|cannot|never)\s+(?:prove|claim|show|establish|validate|certify|approve|confirm|imply|mean)\b/u;
  if (disclaimer.test(text)) {
    return null;
  }
  const patterns = [
    ["runtime-claim", /\b(?:proves?|confirms?|guarantees?|validates?|shows?)\s+(?:runtime|execution|handler executes|service reachability|reachable service)\b/u],
    ["production-claim", /\b(?:production usage|production traffic|used in production|customer impact|business impact)\b/u],
    ["sql-execution-claim", /\b(?:sql execution|query executes|database executes|runtime sql)\b/u],
    ["security-claim", /\b(?:vulnerab(?:le|ility)|exploit(?:able|ability)|security posture|safe from)\b/u],
    ["release-claim", /\b(?:release approved|safe to release|merge safe|ready for release)\b/u],
    ["impact-claim", /\b(?:definite impact|impacted|impact result|reducer conclusion)\b/u]
  ];
  for (const [category, pattern] of patterns) {
    if (pattern.test(text)) {
      return category;
    }
  }
  return null;
}

export function validateSafeIdentity(value, { field = "identity" } = {}) {
  if (typeof value !== "string" || value.trim() === "") {
    return `${field}-empty`;
  }
  const checks = [
    ["windows-drive", /^[A-Za-z]:/u],
    ["uri-scheme", /^[a-z][a-z0-9+.-]*:/iu],
    ["home-fragment", /(^|-)~(?:$|-|[\\/])/u],
    ["path-separator", /[\\/]/u],
    ["git-suffix", /\.git\b/iu],
    ["at-identity", /@/u],
    ["hostname", /\b[a-z0-9-]+\.(?:com|net|org|io|local|internal)\b/iu],
    ["private-token", /\b(?:private|internal|secret|token|client-name|customer|corp)\b/iu],
    ["branch-name", /\b(?:main|master|dev|feature|release|hotfix)-/iu]
  ];
  for (const [category, pattern] of checks) {
    if (pattern.test(value)) {
      return `${field}-${category}`;
    }
  }
  if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/u.test(value)) {
    return `${field}-syntax`;
  }
  return null;
}

function validateGeneratedFrom(generatedFrom, diagnostics, catalogPath) {
  if (!generatedFrom || typeof generatedFrom !== "object" || Array.isArray(generatedFrom)) {
    diagnostics.push(diagnostic("schema", catalogPath, "/generatedFrom", "generatedFrom must be an object."));
    return;
  }
  requireString(generatedFrom, "kind", "/generatedFrom", "manual-reviewed-metadata", diagnostics, catalogPath);
  requireString(generatedFrom, "toolVersion", "/generatedFrom", schemaVersion, diagnostics, catalogPath);
  if (!/^\d{4}-\d{2}$/u.test(generatedFrom.generatedAt ?? "")) {
    diagnostics.push(diagnostic("date", catalogPath, "/generatedFrom/generatedAt", "generatedAt must be YYYY-MM."));
  }
}

function validateSafety(safety, diagnostics, catalogPath) {
  if (!safety || typeof safety !== "object" || Array.isArray(safety)) {
    diagnostics.push(diagnostic("schema", catalogPath, "/safety", "safety must be an object."));
    return;
  }
  validateEnum(safety.classification, claimLevels, catalogPath, "/safety/classification", diagnostics, "claim-level");
  requireString(safety, "validatorVersion", "/safety", validatorVersion, diagnostics, catalogPath);
  validateEnum(safety.redactionProfile, [...redactionProfiles], catalogPath, "/safety/redactionProfile", diagnostics, "redaction-profile");
  validateLimitations(safety.limitations, "/safety/limitations", diagnostics, catalogPath);
}

function validateEntry(entry, index, labels, diagnostics, catalogPath, ruleIds) {
  const pointer = `/entries/${index}`;
  if (!entry || typeof entry !== "object" || Array.isArray(entry)) {
    diagnostics.push(diagnostic("schema", catalogPath, pointer, "Entry must be an object."));
    return;
  }

  const labelCategory = validateSafeIdentity(entry.sampleLabel, { field: "sampleLabel" });
  if (labelCategory) {
    diagnostics.push(diagnostic(labelCategory, catalogPath, `${pointer}/sampleLabel`, "sampleLabel must be a safe neutral lowercase kebab label."));
  } else if (labels.has(entry.sampleLabel)) {
    diagnostics.push(diagnostic("duplicate-sample-label", catalogPath, `${pointer}/sampleLabel`, "Duplicate sampleLabel values are not allowed."));
  } else {
    labels.add(entry.sampleLabel);
  }

  if (typeof entry.displayName !== "string" || entry.displayName.trim() === "") {
    diagnostics.push(diagnostic("display-name", catalogPath, `${pointer}/displayName`, "displayName must be a non-empty string."));
  }
  validateEnum(entry.claimLevel, claimLevels, catalogPath, `${pointer}/claimLevel`, diagnostics, "claim-level");
  validateSource(entry, pointer, diagnostics, catalogPath);
  validateFamilies(entry, pointer, diagnostics, catalogPath, ruleIds);
  validateValidation(entry.validation, pointer, diagnostics, catalogPath);
  validateRelationships(entry.relationships, pointer, diagnostics, catalogPath);
  validateLimitations(entry.limitations, `${pointer}/limitations`, diagnostics, catalogPath);
}

function validateSource(entry, pointer, diagnostics, catalogPath) {
  const source = entry.source;
  if (!source || typeof source !== "object" || Array.isArray(source)) {
    diagnostics.push(diagnostic("schema", catalogPath, `${pointer}/source`, "source must be an object."));
    return;
  }

  validateEnum(source.classification, [...sourceClassifications], catalogPath, `${pointer}/source/classification`, diagnostics, "source-classification");
  validateEnum(source.identityKind, [...sourceIdentityKinds], catalogPath, `${pointer}/source/identityKind`, diagnostics, "source-identity-kind");
  if (source.safeSourceAlias !== undefined) {
    const aliasCategory = validateSafeIdentity(source.safeSourceAlias, { field: "safeSourceAlias" });
    if (aliasCategory) {
      diagnostics.push(diagnostic(aliasCategory, catalogPath, `${pointer}/source/safeSourceAlias`, "safeSourceAlias must be a safe neutral lowercase kebab label."));
    }
  }

  const commit = source.commitIdentity;
  if (!commit || typeof commit !== "object" || Array.isArray(commit)) {
    diagnostics.push(diagnostic("schema", catalogPath, `${pointer}/source/commitIdentity`, "commitIdentity must be an object."));
    return;
  }

  validateEnum(commit.kind, [...commitIdentityKinds], catalogPath, `${pointer}/source/commitIdentity/kind`, diagnostics, "commit-identity-kind");
  if (commit.kind === "public-sha") {
    if (!["public-repo", "public-archive"].includes(source.classification) || source.reviewed !== true) {
      diagnostics.push(diagnostic("public-sha-source", catalogPath, `${pointer}/source/commitIdentity/kind`, "public-sha is allowed only for reviewed public repo or archive sources."));
    }
    if (!/^[a-f0-9]{40}$/u.test(commit.value ?? "")) {
      diagnostics.push(diagnostic("public-sha-format", catalogPath, `${pointer}/source/commitIdentity/value`, "public-sha must be a lowercase 40 character SHA."));
    }
  }
  if (commit.kind === "fixture-version" && !/^[a-z0-9]+(?:-[a-z0-9]+)*@\d{4}-\d{2}$/u.test(commit.value ?? "")) {
    diagnostics.push(diagnostic("fixture-version", catalogPath, `${pointer}/source/commitIdentity/value`, "fixture-version must use a safe fixture label and YYYY-MM pin."));
  }
  if (["redacted-sha256", "local-only"].includes(commit.kind)) {
    diagnostics.push(diagnostic("tracked-commit-kind", catalogPath, `${pointer}/source/commitIdentity/kind`, "Tracked commit identity kind must not be local-only or redacted-sha256."));
  }
  if (commit.kind === "category-only" && commit.value !== undefined) {
    diagnostics.push(diagnostic("category-only-commit-value", catalogPath, `${pointer}/source/commitIdentity/value`, "category-only commit identity must not expose a value."));
  }
  validateLimitations(commit.limitations ?? [], `${pointer}/source/commitIdentity/limitations`, diagnostics, catalogPath, { allowEmpty: true });

  if (["private-local", "operator-local", "unknown"].includes(source.classification) && entry.claimLevel !== "hidden") {
    diagnostics.push(diagnostic("source-claim-cap", catalogPath, `${pointer}/claimLevel`, "Private, operator-local, unknown, or unreviewed sources must remain hidden."));
  }
  if (commit.kind === "category-only" && commit.shaPresent === true && claimRank.get(entry.claimLevel) > claimRank.get("demo-safe")) {
    diagnostics.push(diagnostic("category-only-claim-cap", catalogPath, `${pointer}/claimLevel`, "category-only SHA proof cannot be classified above demo-safe."));
  }
  if (entry.claimLevel === "public-safe" && !["public-sha", "fixture-version"].includes(commit.kind)) {
    diagnostics.push(diagnostic("public-safe-pinned-proof", catalogPath, `${pointer}/source/commitIdentity/kind`, "public-safe entries require public-sha or fixture-version proof."));
  }
}

function validateFamilies(entry, pointer, diagnostics, catalogPath, ruleIds) {
  if (!Array.isArray(entry.families) || entry.families.length === 0) {
    if (entry.claimLevel !== "hidden") {
      diagnostics.push(diagnostic("families-empty", catalogPath, `${pointer}/families`, "Non-hidden entries require one or more family expectations."));
    }
    return;
  }

  for (let index = 0; index < entry.families.length; index += 1) {
    const family = entry.families[index];
    const familyPointer = `${pointer}/families/${index}`;
    validateEnum(family?.familyId, [...familyIds], catalogPath, `${familyPointer}/familyId`, diagnostics, "evidence-family");
    validateEnum(family?.expectation, [...expectationLevels], catalogPath, `${familyPointer}/expectation`, diagnostics, "expectation-level");
    validateStringArray(family?.expectedRuleIds, `${familyPointer}/expectedRuleIds`, diagnostics, catalogPath, { nonEmpty: true, allowedSet: ruleIds, category: "rule-id" });
    validateStringArray(family?.expectedEvidenceTiers, `${familyPointer}/expectedEvidenceTiers`, diagnostics, catalogPath, { nonEmpty: true, allowedSet: evidenceTiers, category: "evidence-tier" });
    validateStringArray(family?.expectedCoverageLabels, `${familyPointer}/expectedCoverageLabels`, diagnostics, catalogPath, { nonEmpty: true, allowedSet: coverageLabels, category: "coverage-label" });
    if ((!Array.isArray(family?.expectedExtractors) || family.expectedExtractors.length === 0) && (!Array.isArray(family?.extractorGapCodes) || family.extractorGapCodes.length === 0)) {
      diagnostics.push(diagnostic("extractor-or-gap-required", catalogPath, familyPointer, "Family expectation requires expectedExtractors or extractorGapCodes."));
    }
    validateStringArray(family?.expectedExtractors ?? [], `${familyPointer}/expectedExtractors`, diagnostics, catalogPath, { allowedPattern: /^[a-z][a-z0-9.-]*$/u, category: "extractor-id" });
    validateStringArray(family?.extractorGapCodes ?? [], `${familyPointer}/extractorGapCodes`, diagnostics, catalogPath, { allowedSet: extractorGapCodes, category: "extractor-gap-code" });
    validateStringArray(family?.statesAllowed, `${familyPointer}/statesAllowed`, diagnostics, catalogPath, { nonEmpty: true, allowedSet: expectationStates, category: "expectation-state" });
    validateLimitations(family?.limitations, `${familyPointer}/limitations`, diagnostics, catalogPath);
    if (family?.familyId === "fallback-syntax-scan" && family.expectedEvidenceTiers?.some((tier) => tier === "Tier1Semantic" || tier === "Tier2Structural")) {
      diagnostics.push(diagnostic("syntax-fallback-tier", catalogPath, `${familyPointer}/expectedEvidenceTiers`, "Syntax fallback expectations must remain Tier3 or Tier4."));
    }
    if (family?.familyId === "large-repo-stress") {
      validateEnum(family.timeoutBucket, [...timeoutBuckets], catalogPath, `${familyPointer}/timeoutBucket`, diagnostics, "timeout-bucket");
      validateEnum(family.artifactSizeBucket, [...artifactSizeBuckets], catalogPath, `${familyPointer}/artifactSizeBucket`, diagnostics, "artifact-size-bucket");
    }
  }
}

function validateValidation(validation, pointer, diagnostics, catalogPath) {
  if (!validation || typeof validation !== "object" || Array.isArray(validation)) {
    diagnostics.push(diagnostic("schema", catalogPath, `${pointer}/validation`, "validation must be an object."));
    return;
  }
  if (!Array.isArray(validation.commandTemplates) || validation.commandTemplates.length === 0) {
    diagnostics.push(diagnostic("command-template", catalogPath, `${pointer}/validation/commandTemplates`, "commandTemplates must be a non-empty array."));
  } else {
    for (let index = 0; index < validation.commandTemplates.length; index += 1) {
      validateCommandTemplate(validation.commandTemplates[index], `${pointer}/validation/commandTemplates/${index}`, diagnostics, catalogPath);
    }
  }
  validateStringArray(validation.gates, `${pointer}/validation/gates`, diagnostics, catalogPath, { nonEmpty: true, allowedPattern: /^[a-z0-9./ -]+$/u, category: "validation-gate" });
}

function validateCommandTemplate(command, pointer, diagnostics, catalogPath) {
  if (!command || typeof command !== "object" || Array.isArray(command)) {
    diagnostics.push(diagnostic("schema", catalogPath, pointer, "Command template must be an object."));
    return;
  }
  const nameCategory = validateSafeIdentity(command.name, { field: "command-name" });
  if (nameCategory) {
    diagnostics.push(diagnostic(nameCategory, catalogPath, `${pointer}/name`, "Command name must be a safe neutral lowercase kebab label."));
  }
  validateEnum(command.mode, [...commandModes], catalogPath, `${pointer}/mode`, diagnostics, "command-mode");
  validateEnum(command.timeoutBucket, [...timeoutBuckets], catalogPath, `${pointer}/timeoutBucket`, diagnostics, "timeout-bucket");
  validateEnum(command.artifactSizeBucket, [...artifactSizeBuckets], catalogPath, `${pointer}/artifactSizeBucket`, diagnostics, "artifact-size-bucket");
  validateStringArray(command.expectedArtifacts, `${pointer}/expectedArtifacts`, diagnostics, catalogPath, { nonEmpty: true, allowedSet: artifactClasses, category: "artifact-class" });

  if (typeof command.template !== "string" || command.template.trim() === "") {
    diagnostics.push(diagnostic("command-template", catalogPath, `${pointer}/template`, "Command template must be a non-empty string."));
    return;
  }
  validateTemplate(command.template, pointer, diagnostics, catalogPath);
}

function validateTemplate(template, pointer, diagnostics, catalogPath) {
  const unsafe = detectUnsafeText(template);
  if (unsafe) {
    diagnostics.push(diagnostic(unsafe, catalogPath, `${pointer}/template`, "Command template contains unsafe text."));
  }

  const placeholders = template.match(/<[^>]+>/gu) ?? [];
  for (const placeholder of placeholders) {
    if (!allowedPlaceholders.has(placeholder)) {
      diagnostics.push(diagnostic("unknown-placeholder", catalogPath, `${pointer}/template`, "Command template contains an unknown placeholder."));
    }
  }

  const tokens = template.match(/(?:[^\s"]+|"[^"]*")+/gu) ?? [];
  for (let index = 0; index < tokens.length; index += 1) {
    const token = tokens[index].replace(/^"|"$/gu, "");
    if (token.startsWith("--")) {
      if (!allowedCommandFlags.has(token)) {
        diagnostics.push(diagnostic("unsupported-command-flag", catalogPath, `${pointer}/template`, "Command template contains an unsupported flag."));
      }
      const value = tokens[index + 1]?.replace(/^"|"$/gu, "");
      if (identityOptionFlags.has(token) && value && !value.startsWith("--") && !allowedPlaceholders.has(value)) {
        diagnostics.push(diagnostic("identity-option-placeholder", catalogPath, `${pointer}/template`, "Identity-bearing command option values must use placeholders."));
      }
      if (token === "--input-kind" && value && !commandInputKinds.has(value)) {
        diagnostics.push(diagnostic("command-input-kind", catalogPath, `${pointer}/template`, "Command input kind must use a closed vocabulary value."));
      }
    }
  }
}

function validateRelationships(relationships, pointer, diagnostics, catalogPath) {
  if (!Array.isArray(relationships)) {
    diagnostics.push(diagnostic("relationships", catalogPath, `${pointer}/relationships`, "relationships must be an array."));
    return;
  }
  for (let index = 0; index < relationships.length; index += 1) {
    const relationship = relationships[index];
    const relationshipPointer = `${pointer}/relationships/${index}`;
    if (!relationship || typeof relationship !== "object" || Array.isArray(relationship)) {
      diagnostics.push(diagnostic("schema", catalogPath, relationshipPointer, "Relationship must be an object."));
      continue;
    }
    validateEnum(relationship?.artifactKind, [...relationshipArtifactKinds], catalogPath, `${relationshipPointer}/artifactKind`, diagnostics, "relationship-artifact-kind");
    validateEnum(relationship?.claimLevel, claimLevels, catalogPath, `${relationshipPointer}/claimLevel`, diagnostics, "claim-level");
    validateEnum(relationship?.validationStatus, [...expectationStates], catalogPath, `${relationshipPointer}/validationStatus`, diagnostics, "relationship-state");
    const idCategory = validateSafeIdentity(relationship?.safeArtifactId, { field: "safeArtifactId" });
    if (idCategory) {
      diagnostics.push(diagnostic(idCategory, catalogPath, `${relationshipPointer}/safeArtifactId`, "safeArtifactId must be a safe neutral lowercase kebab label."));
    }
    if (!/^[a-z0-9]+(?:-[a-z0-9]+)*\.v\d+$/u.test(relationship?.schemaVersion ?? "")) {
      diagnostics.push(diagnostic("schema-version", catalogPath, `${relationshipPointer}/schemaVersion`, "Relationship schemaVersion must be a safe schema name."));
    }
    const text = JSON.stringify(relationship);
    for (const raw of rawArtifactNames) {
      if (text.includes(raw) || text.includes(localRootRelative)) {
        diagnostics.push(diagnostic("raw-artifact-reference", catalogPath, relationshipPointer, "Relationship references must not point to raw artifacts or ignored local paths."));
        break;
      }
    }
  }
}

function validateLimitations(limitations, pointer, diagnostics, catalogPath, { allowEmpty = false } = {}) {
  if (!Array.isArray(limitations) || (!allowEmpty && limitations.length === 0)) {
    diagnostics.push(diagnostic("limitations", catalogPath, pointer, "limitations must be a non-empty array."));
    return;
  }
  for (let index = 0; index < limitations.length; index += 1) {
    const limitation = limitations[index];
    const limitationPointer = `${pointer}/${index}`;
    const codeCategory = validateSafeIdentity(limitation?.code, { field: "limitation-code" });
    if (codeCategory) {
      diagnostics.push(diagnostic(codeCategory, catalogPath, `${limitationPointer}/code`, "Limitation code must be a safe neutral lowercase kebab label."));
    }
    if (typeof limitation?.message !== "string" || limitation.message.trim() === "") {
      diagnostics.push(diagnostic("limitation-message", catalogPath, `${limitationPointer}/message`, "Limitation message must be a non-empty string."));
    }
  }
}

function scanJsonStrings(value, diagnostics, catalogPath, pointer = "") {
  if (typeof value === "string") {
    const unsafe = detectUnsafeText(value);
    if (unsafe) {
      diagnostics.push(diagnostic(unsafe, catalogPath, pointer, "String value failed catalog safety scanning."));
    }
    return;
  }
  if (Array.isArray(value)) {
    value.forEach((item, index) => scanJsonStrings(item, diagnostics, catalogPath, `${pointer}/${index}`));
    return;
  }
  if (value && typeof value === "object") {
    for (const key of Object.keys(value)) {
      scanJsonStrings(value[key], diagnostics, catalogPath, `${pointer}/${escapePointer(key)}`);
    }
  }
}

async function validateMarkdown({ catalog, markdownPath, diagnostics }) {
  let markdown;
  try {
    markdown = await readFile(markdownPath, "utf8");
  } catch {
    diagnostics.push(diagnostic("catalog-markdown-missing", markdownPath, "", "Generated catalog Markdown is missing."));
    return;
  }
  validateMarkdownText({ catalog, markdown, markdownPath, diagnostics });
}

function validateMarkdownText({ catalog, markdown, markdownPath, diagnostics }) {
  const normalized = normalizeNewlines(markdown);
  const firstLine = normalized.split("\n", 1)[0];
  const match = firstLine.match(markdownSentinelPattern);
  const expectedHash = catalogJsonHash(catalog);
  if (!match) {
    diagnostics.push(diagnostic("markdown-sentinel-missing", markdownPath, "line 1", "Generated Markdown must start with the catalog hash sentinel."));
  } else if (match[1] !== expectedHash) {
    diagnostics.push(diagnostic("markdown-sentinel-stale", markdownPath, "line 1", "Generated Markdown hash sentinel is stale."));
  }

  const expected = renderCatalogMarkdown(catalog);
  if (normalized !== expected) {
    diagnostics.push(diagnostic("markdown-stale", markdownPath, "generated", "Generated Markdown must match catalog.json."));
  }

  const lines = normalized.split("\n");
  for (let index = 0; index < lines.length; index += 1) {
    const unsafe = detectUnsafeText(lines[index]);
    if (unsafe) {
      diagnostics.push(diagnostic(unsafe, markdownPath, `line ${index + 1}`, "Markdown text failed catalog safety scanning."));
    }
  }
}

function requireString(object, field, pointer, expected, diagnostics, catalogPath) {
  if (object[field] !== expected) {
    diagnostics.push(diagnostic("schema", catalogPath, `${pointer}/${field}`, `${field} must be ${expected}.`));
  }
}

function validateEnum(value, allowed, catalogPath, pointer, diagnostics, category) {
  if (!allowed.includes(value)) {
    diagnostics.push(diagnostic(category, catalogPath, pointer, `Value must use the closed ${category} vocabulary.`));
  }
}

function validateStringArray(value, pointer, diagnostics, catalogPath, { nonEmpty = false, allowedSet, allowedPattern, category }) {
  if (!Array.isArray(value) || (nonEmpty && value.length === 0)) {
    diagnostics.push(diagnostic(category, catalogPath, pointer, "Expected a non-empty string array."));
    return;
  }
  for (let index = 0; index < value.length; index += 1) {
    const item = value[index];
    if (typeof item !== "string" || item.trim() === "") {
      diagnostics.push(diagnostic(category, catalogPath, `${pointer}/${index}`, "Array item must be a non-empty string."));
      continue;
    }
    if (allowedSet && !allowedSet.has(item)) {
      diagnostics.push(diagnostic(category, catalogPath, `${pointer}/${index}`, "Array item is not in the closed vocabulary or rule catalog."));
    }
    if (allowedPattern && !allowedPattern.test(item)) {
      diagnostics.push(diagnostic(category, catalogPath, `${pointer}/${index}`, "Array item does not match the required safe pattern."));
    }
  }
}

function normalizeObject(value, key = "") {
  if (Array.isArray(value)) {
    return [...value].map((item) => normalizeObject(item)).sort(arrayComparatorFor(key));
  }
  if (value && typeof value === "object") {
    const result = {};
    for (const objectKey of Object.keys(value).sort(compareOrdinal)) {
      result[objectKey] = normalizeObject(value[objectKey], objectKey);
    }
    return result;
  }
  return value;
}

function arrayComparatorFor(key) {
  if (key === "entries") {
    return (left, right) => compareOrdinal(left?.sampleLabel ?? "", right?.sampleLabel ?? "");
  }
  if (key === "families") {
    return (left, right) => compareOrdinal(left?.familyId ?? "", right?.familyId ?? "");
  }
  if (key === "commandTemplates") {
    return (left, right) => compareOrdinal(left?.name ?? "", right?.name ?? "");
  }
  if (key === "relationships") {
    return (left, right) => compareOrdinal(`${left?.spec ?? ""}:${left?.artifactKind ?? ""}:${left?.safeArtifactId ?? ""}`, `${right?.spec ?? ""}:${right?.artifactKind ?? ""}:${right?.safeArtifactId ?? ""}`);
  }
  if (key === "limitations") {
    return (left, right) => compareOrdinal(`${left?.code ?? ""}:${left?.message ?? ""}`, `${right?.code ?? ""}:${right?.message ?? ""}`);
  }
  return (left, right) => compareOrdinal(JSON.stringify(left), JSON.stringify(right));
}

function compareOrdinal(left, right) {
  return left < right ? -1 : left > right ? 1 : 0;
}

async function readCatalog(path) {
  return JSON.parse(await readFile(path, "utf8"));
}

async function readRuleCatalogIds(root) {
  const text = await readFile(resolve(root, "rules/rule-catalog.yml"), "utf8");
  return new Set([...text.matchAll(/^\s*-\s+id:\s+([a-z0-9._-]+)\s*$/gmu)].map((match) => match[1]));
}

async function writeIfAllowed(path, content, { force }) {
  if (await exists(path)) {
    const current = await readFile(path, "utf8");
    if (normalizeNewlines(current) === content) {
      return;
    }
    if (!force) {
      throw new Error(`Refusing to overwrite ${path} without --force.`);
    }
  }
  await writeFile(path, content, "utf8");
}

async function copyIfAllowed(source, destination, { force }) {
  if (await exists(destination) && !force) {
    throw new Error(`Refusing to overwrite ${destination} without --force.`);
  }
  await copyFile(source, destination);
}

async function exists(path) {
  try {
    await stat(path);
    return true;
  } catch {
    return false;
  }
}

function assertTrackedDestination(outDir, root, diagnostics) {
  const fullTrackedRoot = resolve(root, trackedRootRelative);
  const rel = relative(fullTrackedRoot, outDir);
  if (rel.startsWith("..") || isAbsolute(rel)) {
    diagnostics.push(diagnostic("tracked-root", outDir, "", "Catalog output destination must stay under docs/validation/legacy-sample-smoke-catalog/."));
  }
}

function assertNotIgnoredDestination(outDir, root, diagnostics) {
  const result = spawnSync("git", ["check-ignore", "-q", relative(root, outDir)], { cwd: root });
  if (result.status === 0) {
    diagnostics.push(diagnostic("ignored-destination", outDir, "", "Catalog promotion destination must not be ignored."));
  }
}

function parseOptions(args) {
  const options = new Map();
  for (let index = 0; index < args.length; index += 1) {
    const arg = args[index];
    if (!arg.startsWith("--")) {
      throw new Error(`Unexpected argument: ${arg}`);
    }
    if (["--force", "--dry-run"].includes(arg)) {
      options.set(arg, true);
      continue;
    }
    const value = args[index + 1];
    if (!value || value.startsWith("--")) {
      throw new Error(`Missing value for ${arg}`);
    }
    options.set(arg, value);
    index += 1;
  }
  return options;
}

function requiredOption(options, name) {
  const value = options.get(name);
  if (!value) {
    throw new Error(`${name} is required.`);
  }
  return value;
}

function validateYearMonth(value) {
  if (!/^\d{4}-\d{2}$/u.test(value ?? "")) {
    throw new Error("--date must use YYYY-MM.");
  }
}

function defaultMarkdownFor(catalogPath) {
  return resolve(dirname(catalogPath), "catalog.md");
}

function diagnostic(category, file, pointer, message) {
  return { category, file: String(file), pointer, message };
}

function formatDiagnostics(diagnostics) {
  return diagnostics
    .map((item) => `${item.file} ${item.pointer || "/"} [${item.category}]: ${item.message}`)
    .join("\n");
}

function escapePointer(value) {
  return value.replace(/~/gu, "~0").replace(/\//gu, "~1");
}

function normalizeNewlines(value) {
  return value.replace(/\r\n?/gu, "\n");
}

function markdownCell(value) {
  return escapeMarkdown(String(value)).replace(/\n/gu, " ");
}

function escapeMarkdown(value) {
  return value
    .replace(/\\/gu, "\\\\")
    .replace(/\|/gu, "\\|")
    .replace(/`/gu, "\\`")
    .replace(/</gu, "&lt;")
    .replace(/>/gu, "&gt;");
}

function helpText() {
  return `Usage:
  node scripts/legacy-sample-smoke-catalog.mjs validate --catalog <catalog.json> [--markdown <catalog.md>]
  node scripts/legacy-sample-smoke-catalog.mjs render --catalog <candidate.json> --out docs/validation/legacy-sample-smoke-catalog --date YYYY-MM [--minimum-entry-claim-level demo-safe|public-safe] [--force] [--dry-run]
  node scripts/legacy-sample-smoke-catalog.mjs promote --catalog <catalog.json> --out docs/validation/legacy-sample-smoke-catalog [--markdown <catalog.md>] [--force] [--dry-run]
`;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  process.exitCode = await main();
}
