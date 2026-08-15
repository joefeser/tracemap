import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import {
  decodeHtmlEntities,
  escapeRegExp,
  fileExists,
  normalizeBaseUrl,
  normalizeRenderedText,
  readSitemapLocSet,
  stripTagsQuoteAware
} from "./validate-utils.mjs";

export const reducedCoverageArticleSlug = "successful-build-can-still-have-reduced-coverage";
export const reducedCoverageArticleRoute = `/blog/${reducedCoverageArticleSlug}/`;
export const reducedCoverageArticleRequiredLinks = [
  "/blog/modernizing-web-forms-without-running-it/",
  "/legacy-dotnet/evidence/",
  "/legacy-modernization/evidence-map/",
  "/legacy-modernization/review-handoff/",
  "/test-planning/",
  "/static-vs-runtime/",
  "/limitations/"
];

const pageArtifact = `blog/${reducedCoverageArticleSlug}/index.html`;
const requiredBlocks = [
  "four-layers",
  "green-build-answer",
  "mixed-strength-evidence",
  "capability-facts",
  "bounded-example",
  "partial-evidence-useful",
  "reduced-coverage-response",
  "non-claims"
];
// The four layers must stay separate in the published copy.
const requiredLayerTerms = [
  "TraceMap's own build and tests",
  "Target-repository build state",
  "Analyzer capability",
  "Feature and downstream coverage"
];
const requiredText = [
  "Public claim level: concept",
  "Level1SemanticAnalysis",
  "Level1SemanticAnalysisReduced",
  "Level3SyntaxAnalysis",
  "Succeeded",
  "FailedOrPartial",
  "CSharpSemanticCompilation",
  "SyntaxFallbackAvailable",
  "LegacyProjectConfigInspection",
  "LegacyNuGetRestoreAwareness",
  "GeneratedDesignerLinkage",
  "LegacyWebStackShape",
  "DownstreamNoEvidenceCoverage",
  "analyzer.capability.semantic.v1",
  "analyzer.capability.syntax-fallback.v1",
  "analyzer.capability.project-config.v1",
  "analyzer.capability.package-restore.v1",
  "analyzer.capability.generated-design-time.v1",
  "analyzer.capability.legacy-toolchain.v1",
  "analyzer.capability.downstream-coverage.v1",
  "build.environment.workspace-diagnostic.v1",
  "Tier2Structural",
  "Tier3SyntaxOrTextual",
  "Tier4Unknown",
  "not-requested",
  "not-applicable",
  "NoEvidenceFullCoverage",
  "NoEvidenceReducedCoverage",
  "coverage-relative",
  "synthetic",
  "commit"
];
const forbiddenClaims = [
  /\b(?:ran|executed|hosted)\b[^.]{0,80}\b(?:application|page|site|Web Forms app)\b/i,
  /\bproves\b/i,
  /\b(?:is|are|was|were)\s+(?:proved|proven|verified|validated|confirmed|guaranteed)\b/i,
  /\b(?:migration succeeded|parity confirmed|safe to release|safe to migrate|fully covered|complete coverage of the application)\b/i,
  /\b(?:build|scan|toolchain)\b[^.]{0,60}\b(?:guarantees|establishes)\s+(?:complete|full)\b/i,
  /\b(?:TraceMap|this evidence|static evidence|this article)\b[^.]{0,120}\b(?:establishes|confirms|guarantees|validates)\b/i
];
const rawMaterialPatterns = [
  /\bSELECT\s+.+\bFROM\b/i,
  /\b(?:CREATE|ALTER|DROP|GRANT|REVOKE)\s+(?:TABLE|VIEW|USER|ROLE|DATABASE|PROCEDURE|FUNCTION)\b/i,
  /\b(?:Server|Data Source|Initial Catalog|User Id|Password|ConnectionString)\s*=/i
];
// Whitespace-free variants for the tag-stripped tight surface. SQL keyword
// patterns stay case-sensitive because lowercase prose fuses into matching
// shapes once whitespace is removed.
const tightRawMaterialPatterns = [
  /SELECT.+?FROM/,
  /(?:CREATE|ALTER|DROP|GRANT|REVOKE)(?:TABLE|VIEW|USER|ROLE|DATABASE|PROCEDURE|FUNCTION)/,
  /(?:Server|Data ?Source|Initial ?Catalog|User ?Id|Password|ConnectionString)=/i
];
const hardPrivatePatterns = [
  /\/Users\//i,
  /\/private\//i,
  /\/home\//i,
  /\/tmp\//i,
  /\/var\/folders\//i,
  /~\//,
  /\bC:\\/i,
  /\bfile:\/\//i,
  /\bgit@/i,
  /\bsk-[A-Za-z0-9_-]{12,}\b/i
];

// Browsers decode semicolonless numeric entities ("/Us&#101rs/"), so the
// safety scans must decode them too or the rendered violation stays hidden.
function decodeBrowserEntities(value) {
  return String(value)
    .replace(/&#x([0-9a-f]+);?/gi, (match, hex) => codePointText(Number.parseInt(hex, 16), match))
    .replace(/&#([0-9]+);?/gi, (match, digits) => codePointText(Number.parseInt(digits, 10), match));
}

function codePointText(codePoint, fallback) {
  if (!Number.isFinite(codePoint) || codePoint < 1 || codePoint > 0x10ffff) return fallback;
  try {
    return String.fromCodePoint(codePoint);
  } catch {
    return fallback;
  }
}

export async function validateReducedCoverageArticleDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors
}) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "blog", reducedCoverageArticleSlug, "index.html");

  if (!(await fileExists(pagePath))) {
    errors.push(withEvidence(`Reduced coverage article is missing required route: ${reducedCoverageArticleRoute}`, pageArtifact));
    return;
  }

  await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  await validateBlogIndex({ dist, errors: localErrors });
  await validateDiscovery({ dist, errors: localErrors });
  await validateArticle({ baseUrl: cleanBaseUrl, pagePath, errors: localErrors });
  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) return;
  const urls = await readSitemapLocSet(sitemapPath);
  if (!urls.has(`${baseUrl}${reducedCoverageArticleRoute}`)) {
    errors.push(withEvidence(`Reduced coverage sitemap is missing required route: ${baseUrl}${reducedCoverageArticleRoute}`, "sitemap.xml"));
  }
}

async function validateBlogIndex({ dist, errors }) {
  const path = resolve(dist, "blog", "index.html");
  if (!(await fileExists(path))) {
    errors.push(withEvidence("Reduced coverage blog index is missing.", "blog/index.html"));
    return;
  }
  const html = await readFile(path, "utf8");
  if (!hasHref(html, reducedCoverageArticleRoute)) {
    errors.push(withEvidence(`Reduced coverage blog index is missing article link: ${reducedCoverageArticleRoute}`, "blog/index.html"));
    return;
  }
  const card = html.match(new RegExp(`<a\\b[^>]*href\\s*=\\s*["']${escapeRegExp(reducedCoverageArticleRoute)}["'][^>]*>[\\s\\S]*?<\\/a>`, "i"))?.[0] ?? "";
  const cardNormalized = decodeBrowserEntities(card);
  scanSafety(
    [normalizeRenderedText(cardNormalized), decodeHtmlEntities(cardNormalized)],
    errors,
    "blog/index.html",
    [decodeHtmlEntities(cardNormalized), tightText(cardNormalized)]
  );
}

async function validateDiscovery({ dist, errors }) {
  const path = resolve(dist, "routes-index.json");
  if (!(await fileExists(path))) {
    errors.push(withEvidence("Reduced coverage routes discovery output is missing.", "routes-index.json"));
    return;
  }
  let parsed;
  try {
    parsed = JSON.parse(await readFile(path, "utf8"));
  } catch (error) {
    errors.push(withEvidence(`Reduced coverage routes discovery output is invalid JSON: ${error.message}`, "routes-index.json"));
    return;
  }
  if (!parsed || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Reduced coverage routes discovery output must contain an entries array.", "routes-index.json"));
    return;
  }
  const entries = parsed.entries;
  const entry = entries.find((candidate) => candidate?.path === reducedCoverageArticleRoute);
  if (!entry) {
    errors.push(withEvidence("Reduced coverage discovery entry is missing.", "routes-index.json"));
    return;
  }
  if (entry.publicClaimLevel !== "concept") errors.push(withEvidence("Reduced coverage discovery claim level must be concept.", "routes-index.json"));
  if (entry.preferredProofPath !== "/legacy-dotnet/evidence/") errors.push(withEvidence("Reduced coverage preferred proof path must remain /legacy-dotnet/evidence/.", "routes-index.json"));
  if (!Array.isArray(entry.limitations) || entry.limitations.length < 2) errors.push(withEvidence("Reduced coverage discovery must include at least two limitations.", "routes-index.json"));
  if (!Array.isArray(entry.nonClaims) || entry.nonClaims.length < 2) errors.push(withEvidence("Reduced coverage discovery must include at least two non-claims.", "routes-index.json"));
  scanDiscoverySafety(entry, errors);
}

// The discovery entry is published copy: every string field gets the same
// claim, raw-material, and private-material scans as the article body.
function scanDiscoverySafety(entry, errors) {
  const fields = [entry.title, entry.summary, entry.preferredProofPath, ...(entry.limitations ?? []), ...(entry.nonClaims ?? [])]
    .filter((value) => typeof value === "string");
  if (fields.length === 0) return;
  const surfaces = fields.map((value) => decodeHtmlEntities(decodeBrowserEntities(value)));
  const tight = fields.map((value) => tightText(decodeBrowserEntities(value)));
  scanSafety(surfaces, errors, "routes-index.json", [...surfaces, ...tight]);
}

async function validateArticle({ baseUrl, pagePath, errors }) {
  const rawHtml = await readFile(pagePath, "utf8");
  const html = decodeBrowserEntities(rawHtml);
  const decoded = decodeHtmlEntities(html);
  const rendered = normalizeRenderedText(html);
  const metadata = decodeHtmlEntities(
    [...(html.match(/<head\b[^>]*>[\s\S]*?<\/head>/i)?.[0] ?? "").matchAll(/\bcontent\s*=\s*(["'])(.*?)\1/gi)]
      .map((match) => match[2])
      .join(" ")
  );

  if (!html.includes("<title>A Successful Build Can Still Have Reduced Feature Coverage | TraceMap</title>")) {
    errors.push(withEvidence("Reduced coverage article is missing expected title.", pageArtifact));
  }
  if (!new RegExp(`<link\\b[^>]*rel=["']canonical["'][^>]*href=["']${escapeRegExp(baseUrl)}${escapeRegExp(reducedCoverageArticleRoute)}["']`, "i").test(html)) {
    errors.push(withEvidence("Reduced coverage article canonical URL is missing or incorrect.", pageArtifact));
  }
  for (const block of requiredBlocks) {
    if (!new RegExp(`<section\\b[^>]*data-reduced-coverage-block\\s*=\\s*["']${escapeRegExp(block)}["']`, "i").test(html)) {
      errors.push(withEvidence(`Reduced coverage article is missing required block: ${block}`, pageArtifact));
    }
  }
  for (const phrase of requiredText) {
    if (!rendered.toLowerCase().includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Reduced coverage article is missing required text: ${phrase}`, pageArtifact));
    }
  }
  for (const phrase of requiredLayerTerms) {
    if (!rendered.toLowerCase().includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Reduced coverage article is missing required layer term: ${phrase}`, pageArtifact));
    }
  }
  for (const link of reducedCoverageArticleRequiredLinks) {
    if (!hasHref(html, link)) errors.push(withEvidence(`Reduced coverage article is missing required link: ${link}`, pageArtifact));
  }
  const words = rendered.split(/\s+/).filter(Boolean).length;
  if (words < 900 || words > 1800) errors.push(withEvidence(`Reduced coverage article word count must be between 900 and 1800 words, got ${words}`, pageArtifact));
  const tight = tightText(html);
  scanSafety([rendered, metadata], errors, pageArtifact, [decoded, metadata, tight], tight);
}

function scanSafety(surfaces, errors, artifact, privateSurfaces = surfaces, tightSurface = "") {
  for (const pattern of forbiddenClaims) {
    if (surfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`Reduced coverage article contains unsupported positive claim: ${pattern}`, artifact));
  }
  for (const pattern of rawMaterialPatterns) {
    if (surfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`Reduced coverage article contains raw or executable material: ${pattern}`, artifact));
  }
  for (const pattern of [...rawMaterialPatterns, ...tightRawMaterialPatterns]) {
    if (tightSurface && pattern.test(tightSurface)) errors.push(withEvidence(`Reduced coverage article contains raw or executable material: ${pattern}`, artifact));
  }
  for (const pattern of hardPrivatePatterns) {
    if (privateSurfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`Reduced coverage article contains hard private material: ${pattern}`, artifact));
  }
}

// Tag-stripped, whitespace-collapsed surface so tokens split across markup
// (for example "/Use<span>rs/") cannot evade the private/raw scans.
function tightText(html) {
  return decodeHtmlEntities(stripTagsQuoteAware(String(html))).replace(/\s+/g, "");
}

function hasHref(html, href) {
  return new RegExp(`<a\\b[^>]*href\\s*=\\s*["']${escapeRegExp(href)}["'][^>]*>`, "i").test(html);
}

function withEvidence(message, artifact) {
  return `${message} Evidence: ${artifact}.`;
}
