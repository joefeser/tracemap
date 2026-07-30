import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import {
  decodeHtmlEntities,
  fileExists,
  normalizeBaseUrl,
  normalizeRenderedText,
  readSitemapLocSet,
  stripTagsQuoteAware
} from "./validate-utils.mjs";

export const msbuildBinlogArticleSlug = "what-an-msbuild-binlog-knows-that-a-source-diff-does-not";
export const msbuildBinlogArticleRoute = `/blog/${msbuildBinlogArticleSlug}/`;
export const msbuildBinlogProofRoute = "/build/msbuild-binlog/proof-packet/";
export const msbuildBinlogProofAsset = "/assets/msbuild-binlog-proof-packet.json";
export const msbuildBinlogSocialAsset = "/assets/msbuild-binlog-evidence-social-card.png";
const evidencedSmokeCommit = "e8021921d18be6fa0c53959fa8860455c1af1d49";

const articleBlocks = [
  "different-evidence",
  "sensitive-artifact",
  "allowlist",
  "dogfood",
  "review-composition",
  "non-claims",
  "where-next"
];
const proofSections = ["hero", "identity", "result", "safety", "limitations", "links"];
const expectedLimitations = [
  "This is one synthetic checked-in sample and is not a completeness, compatibility, or performance benchmark.",
  "The projection does not authenticate or attest the binary log or prove that it was produced from the declared commit.",
  "A recorded successful result does not prove tests passed, the repository was clean, deployment occurred, runtime behavior was correct, or release approval exists.",
  "Project observations and graph edges do not prove runtime reachability.",
  "Diagnostic evidence may be partial, and no diagnostic observation is not proof that no defect exists.",
  "The public packet omits the raw binary log, artifact digest, raw messages, properties, items, tasks, commands, environment values, URLs, credentials, connection material, private hosts, usernames, and machine-local paths."
];
const expectedNonClaims = [
  "No artifact authenticity or commit provenance attestation.",
  "No test-pass, clean-repository, package-use, deployment, runtime-correctness, release-approval, or safe-to-release conclusion.",
  "No complete diagnostic, project graph, target, task, property, item, package, or performance analysis.",
  "No LLM, MCP, embedding, vector-database, or prompt-classification analysis in the TraceMap scanner or reducer."
];
const forbiddenMaterial = [
  /\/Users\//i,
  /\/home\/[^/]+/i,
  /\/tmp\//i,
  /\/var\/folders\//i,
  /\b[A-Z]:\\/i,
  /\bfile:\/\//i,
  /\b(?:Server|Host|Data Source|User Id|Username|Password)\s*=/i,
  /\b(?:api[_-]?key|access[_-]?token|client[_-]?secret)\s*[:=]/i,
  /\bsk-[A-Za-z0-9_-]{12,}\b/i,
  /\bdotnet\s+(?:build|msbuild)\b/i,
  /(?:^|\s)-bl:/i,
  /\b(?:SELECT|INSERT|UPDATE|DELETE|MERGE)\s+(?:INTO|FROM|SET|USING)\b/i
];
const unsupportedClaims = [
  /\bTraceMap\b[^.]{0,120}\b(?:proves|guarantees|certifies|approves)\b[^.]{0,120}\b(?:tests passed|clean repository|runtime|deployment|release|safe)\b/i,
  /\b(?:binlog|binary log)\b[^.]{0,120}\b(?:proves|guarantees|certifies)\b[^.]{0,120}\b(?:authentic|commit|tests passed|runtime|deployment|release)\b/i,
  /\b(?:safe to release|approved to release|ready to deploy)\b/i
];

export async function validateMsbuildBinlogEvidenceDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors
}) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const articlePath = resolve(dist, "blog", msbuildBinlogArticleSlug, "index.html");
  const proofPath = resolve(dist, "build", "msbuild-binlog", "proof-packet", "index.html");
  const assetPath = resolve(dist, "assets", "msbuild-binlog-proof-packet.json");
  const socialAssetPath = resolve(dist, "assets", "msbuild-binlog-evidence-social-card.png");
  const artifacts = [
    [articlePath, msbuildBinlogArticleRoute],
    [proofPath, msbuildBinlogProofRoute],
    [assetPath, msbuildBinlogProofAsset],
    [socialAssetPath, msbuildBinlogSocialAsset]
  ];
  const presence = await Promise.all(artifacts.map(([path]) => fileExists(path)));

  for (const [[, label], present] of artifacts.map((artifact, index) => [artifact, presence[index]])) {
    if (!present) localErrors.push(`MSBuild binlog evidence is missing required artifact: ${label}`);
  }

  if (localErrors.length === 0) {
    await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
    await validateArticle({ articlePath, baseUrl: cleanBaseUrl, errors: localErrors });
    await validateProofPage({ proofPath, baseUrl: cleanBaseUrl, errors: localErrors });
    await validatePacket({ assetPath, errors: localErrors });
    await validateDiscovery({ dist, errors: localErrors });
  }

  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const urls = await readSitemapLocSet(resolve(dist, "sitemap.xml"));
  for (const route of [msbuildBinlogArticleRoute, msbuildBinlogProofRoute]) {
    if (!urls.has(`${baseUrl}${route}`)) errors.push(`MSBuild binlog sitemap is missing required route: ${route}`);
  }
}

async function validateArticle({ articlePath, baseUrl, errors }) {
  const html = await readFile(articlePath, "utf8");
  const text = normalizeRenderedText(html);
  const decoded = decodeHtmlEntities(html);
  const collapsed = decodeHtmlEntities(stripTagsQuoteAware(html));
  requireIncludes(html, `<link rel="canonical" href="${baseUrl}${msbuildBinlogArticleRoute}">`, "article canonical", errors);
  requireIncludes(html, `<meta property="og:type" content="article">`, "article Open Graph type", errors);
  requireIncludes(html, `<meta property="og:image" content="${baseUrl}${msbuildBinlogSocialAsset}">`, "article social image", errors);
  requireIncludes(html, `<meta name="twitter:card" content="summary_large_image">`, "article large social card", errors);
  requireIncludes(html, `href="${msbuildBinlogProofRoute}"`, "article proof link", errors);
  requireIncludes(html, "Public claim level: demo", "article claim level", errors);
  requireIncludes(html, "build.msbuild-binlog.observation.v1", "article observation rule", errors);
  requireIncludes(html, "build.msbuild-binlog.gap.v1", "article gap rule", errors);
  requireIncludes(html, "Tier2Structural", "article evidence tier", errors);
  requireIncludes(html, "observed-bounded", "article coverage", errors);
  requireIncludes(text, "MSBuild Binlog Analyzer for VS Code", "Microsoft announcement attribution", errors);

  for (const block of articleBlocks) {
    requireIncludes(html, `data-msbuild-blog-block="${block}"`, `article block ${block}`, errors);
  }

  const words = text.split(/\s+/).filter(Boolean).length;
  if (words < 700 || words > 1600) errors.push(`MSBuild binlog article word count must be between 700 and 1600, got ${words}`);
  scanUnsupportedClaims(text, "article", errors);
  scanPrivateMaterial(`${decoded}\n${text}\n${collapsed}`, "article", errors);
}

async function validateProofPage({ proofPath, baseUrl, errors }) {
  const html = await readFile(proofPath, "utf8");
  const text = normalizeRenderedText(html);
  const decoded = decodeHtmlEntities(html);
  const collapsed = decodeHtmlEntities(stripTagsQuoteAware(html));
  requireIncludes(html, `<link rel="canonical" href="${baseUrl}${msbuildBinlogProofRoute}">`, "proof canonical", errors);
  requireIncludes(html, `href="${msbuildBinlogProofAsset}"`, "proof asset link", errors);
  requireIncludes(html, `href="${msbuildBinlogArticleRoute}"`, "proof article link", errors);
  for (const section of proofSections) {
    requireIncludes(html, `data-msbuild-proof-section="${section}"`, `proof section ${section}`, errors);
  }
  for (const phrase of [
    "Public claim level: demo",
    "build.msbuild-binlog.observation.v1",
    "build.msbuild-binlog.gap.v1",
    "Tier2Structural",
    "Tier4Unknown",
    "observed-bounded",
    "observed-partial",
    "MsBuildBinlogExtractor",
    "msbuild-binlog/0.1.0",
    "recorded succeeded result"
  ]) {
    requireIncludes(text, phrase, `proof text ${phrase}`, errors);
  }
  scanUnsupportedClaims(text, "proof page", errors);
  scanPrivateMaterial(`${decoded}\n${text}\n${collapsed}`, "proof page", errors);
}

async function validatePacket({ assetPath, errors }) {
  let packet;
  try {
    packet = JSON.parse(await readFile(assetPath, "utf8"));
  } catch (error) {
    errors.push(`MSBuild binlog proof packet is invalid JSON: ${error.message}`);
    return;
  }

  requireExactKeys(packet, ["schemaVersion", "publicClaimLevel", "purpose", "source", "artifactBoundary", "observation", "validation", "limitations", "nonClaims"], "packet", errors);
  if (!packet || typeof packet !== "object" || Array.isArray(packet)) return;
  requireExactKeys(packet.source, ["repositoryId", "commitSha", "sourceKind", "repeatedScanCount"], "source", errors);
  requireExactKeys(packet.artifactBoundary, ["suppliedExplicitly", "declaredCommitMatched", "rawArtifactRetained", "artifactAuthentication", "artifactLocation"], "artifact boundary", errors);
  requireExactKeys(packet.observation, ["ruleId", "evidenceTier", "coverageLabel", "extractorId", "extractorVersion", "recordedBuildResult", "artifactObservationCount", "projectObservationCount", "projectReferenceObservationCount", "diagnosticObservationCount", "gapCount"], "observation", errors);
  requireExactKeys(packet.validation, ["deterministicAcrossRepeatedScans", "commitBoundOnEveryProjectedFact", "syntheticArtifactSpanOnEveryProjectedFact", "protectedOutputMatchCount", "standardOutputCount", "standardOutputs"], "validation", errors);

  if (packet.schemaVersion !== "tracemap-public-msbuild-binlog-proof-packet/v1") errors.push("MSBuild binlog packet schemaVersion is unsupported");
  if (packet.publicClaimLevel !== "demo") errors.push("MSBuild binlog packet publicClaimLevel must remain demo");
  if (packet.source?.repositoryId !== "joefeser/tracemap") errors.push("MSBuild binlog packet repository identity is unexpected");
  if (packet.source?.commitSha !== evidencedSmokeCommit) errors.push("MSBuild binlog packet commit SHA does not match the evidenced smoke commit");
  if (packet.source?.sourceKind !== "checked-in-sample-local-product-smoke" || packet.source?.repeatedScanCount !== 2) errors.push("MSBuild binlog packet source contract is unexpected");

  const boundary = packet.artifactBoundary ?? {};
  if (boundary.suppliedExplicitly !== true || boundary.declaredCommitMatched !== true || boundary.rawArtifactRetained !== false || boundary.artifactAuthentication !== "not-established" || boundary.artifactLocation !== "synthetic-identity-only") {
    errors.push("MSBuild binlog packet artifact boundary is inconsistent");
  }

  const observation = packet.observation ?? {};
  const expectedObservation = {
    ruleId: "build.msbuild-binlog.observation.v1",
    evidenceTier: "Tier2Structural",
    coverageLabel: "observed-bounded",
    extractorId: "MsBuildBinlogExtractor",
    extractorVersion: "msbuild-binlog/0.1.0",
    recordedBuildResult: "succeeded",
    artifactObservationCount: 1,
    projectObservationCount: 1,
    projectReferenceObservationCount: 0,
    diagnosticObservationCount: 0,
    gapCount: 0
  };
  for (const [key, value] of Object.entries(expectedObservation)) {
    if (observation[key] !== value) errors.push(`MSBuild binlog packet observation field is unexpected: ${key}`);
  }

  const validation = packet.validation ?? {};
  if (
    validation.deterministicAcrossRepeatedScans !== true ||
    validation.commitBoundOnEveryProjectedFact !== true ||
    validation.syntheticArtifactSpanOnEveryProjectedFact !== true ||
    validation.protectedOutputMatchCount !== 0 ||
    validation.standardOutputCount !== 5 ||
    !sameArray(validation.standardOutputs, ["scan-manifest.json", "facts.ndjson", "index.sqlite", "report.md", "logs/analyzer.log"])
  ) {
    errors.push("MSBuild binlog packet validation contract is inconsistent");
  }
  if (!sameArray(packet.limitations, expectedLimitations)) errors.push("MSBuild binlog packet limitations must match the reviewed set");
  if (!sameArray(packet.nonClaims, expectedNonClaims)) errors.push("MSBuild binlog packet nonClaims must match the reviewed set");

  const serializedPacket = JSON.stringify(packet);
  scanUnsupportedClaims(serializedPacket, "proof packet", errors);
  scanPrivateMaterial(serializedPacket, "proof packet", errors);
}

async function validateDiscovery({ dist, errors }) {
  let routes;
  try {
    routes = JSON.parse(await readFile(resolve(dist, "routes-index.json"), "utf8"));
  } catch (error) {
    errors.push(`MSBuild binlog discovery metadata is unavailable or invalid: ${error.message}`);
    return;
  }
  const entries = Array.isArray(routes) ? routes : routes?.entries;
  if (!Array.isArray(entries)) {
    errors.push("MSBuild binlog discovery metadata entries must be an array");
    return;
  }
  const entry = entries.find((candidate) => candidate.path === msbuildBinlogProofRoute);
  if (!entry) {
    errors.push("MSBuild binlog proof route is missing from discovery metadata");
    return;
  }
  if (entry.publicClaimLevel !== "demo" || entry.preferredProofPath !== msbuildBinlogArticleRoute) {
    errors.push("MSBuild binlog discovery entry has an inconsistent claim level or proof path");
  }
}

function requireExactKeys(value, expected, label, errors) {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    errors.push(`MSBuild binlog ${label} must be an object`);
    return;
  }
  const actual = Object.keys(value).sort();
  const wanted = [...expected].sort();
  if (!sameArray(actual, wanted)) errors.push(`MSBuild binlog ${label} fields must match the reviewed contract`);
}

function sameArray(actual, expected) {
  return Array.isArray(actual) && actual.length === expected.length && actual.every((value, index) => value === expected[index]);
}

function requireIncludes(text, expected, label, errors) {
  if (!text.includes(expected)) errors.push(`MSBuild binlog evidence is missing ${label}`);
}

function scanUnsupportedClaims(text, label, errors) {
  for (const pattern of unsupportedClaims) {
    if (pattern.test(text)) errors.push(`MSBuild binlog ${label} contains an unsupported public claim: ${pattern}`);
  }
}

function scanPrivateMaterial(text, label, errors) {
  for (const pattern of forbiddenMaterial) {
    if (pattern.test(text)) errors.push(`MSBuild binlog ${label} contains forbidden private or executable material: ${pattern}`);
  }
}
