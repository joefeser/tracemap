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

export const gapLineNumberArticleSlug = "when-a-gap-has-no-honest-line-number";
export const gapLineNumberArticleRoute = `/blog/${gapLineNumberArticleSlug}/`;
export const gapLineNumberArticleRequiredLinks = [
  "/blog/how-a-gap-becomes-a-rule/",
  "/blog/successful-build-can-still-have-reduced-coverage/",
  "/blog/reverse-engineering-access-without-running-it/",
  "/evidence/gaps/",
  "/evidence/",
  "/outputs/",
  "/test-planning/",
  "/limitations/"
];

const pageArtifact = `blog/${gapLineNumberArticleSlug}/index.html`;
const requiredBlocks = [
  "why-locations",
  "why-no-line",
  "five-classes",
  "fabricated-precision",
  "example-workspace",
  "example-container",
  "example-unavailable",
  "persistence",
  "reviewer-questions",
  "non-claims"
];
// Rule IDs verified against rules/rule-catalog.yml at the article's base SHA.
// The closed list below also anchors the unknown-rule-token scan.
export const gapLineNumberArticleRuleIds = [
  "csharp.semantic.declarations.v1",
  "csharp.syntax.declarations.v1",
  "build.environment.workspace-diagnostic.v1",
  "csharp.semantic.workspace.v1",
  "legacy.access.database.inventory.v1",
  "legacy.access.schema.v1",
  "legacy.access.vba.v1",
  "analyzer.capability.downstream-coverage.v1",
  "impact.reverse.gap.v1"
];
const requiredLocationClasses = [
  "Exact source span",
  "Supporting declaration span",
  "Owning-container anchor",
  "Workspace/repository anchor",
  "Span unavailable"
];
const requiredExampleLabels = ["exact evidence", "supporting declaration", "container anchor", "workspace anchor", "unavailable", "gap"];
const exampleBlockLabels = {
  "example-workspace": ["gap", "unavailable", "workspace anchor", "supporting declaration"],
  "example-container": ["exact evidence", "container anchor", "gap"],
  "example-unavailable": ["exact evidence", "unavailable", "gap"]
};
const requiredText = [
  "Public claim level: concept",
  "Line 1 can be an anchor without being a source-line claim.",
  "A location is evidence provenance, not proof of causality.",
  "Unavailable location metadata is not evidence absence.",
  "A downstream consumer must not strengthen an anchor into an exact source claim.",
  "not every line-one span means the same thing",
  "repository identity and commit SHA",
  "scan ID where applicable",
  "repository-relative path",
  "one-based span where available",
  "extractor ID and extractor version",
  "supporting fact IDs where available",
  "scope or container metadata",
  "coverage with limitations",
  "sourceScope",
  "Tier1Semantic",
  "Tier2Structural",
  "Tier3SyntaxOrTextual",
  "Tier4Unknown",
  "does not independently prove causality",
  "runtime reachability",
  "business intent",
  "complete coverage",
  "missing behavior",
  "no evidence exists",
  "synthetic",
  "no LLM calls, embeddings, vector databases, or prompt-based classification"
];
const forbiddenClaims = [
  /\bproves\b/i,
  /\b(?:is|are|was|were)\s+(?:proved|proven|verified|validated|confirmed|guaranteed)\b/i,
  /\b(?:migration succeeded|parity confirmed|safe to release|safe to migrate|fully covered|complete coverage of the application)\b/i,
  /\b(?:span|path|anchor|location)\s+caused\b/i,
  /\b(?:TraceMap|this evidence|static evidence|this article)\b[^.]{0,120}\b(?:establishes|confirms|guarantees|validates)\b/i
];
const rawMaterialPatterns = [
  /\bSELECT\s+.+\bFROM\b/i,
  /\b(?:CREATE|ALTER|DROP|GRANT|REVOKE)\s+(?:TABLE|VIEW|USER|ROLE|DATABASE|PROCEDURE|FUNCTION)\b/i,
  /\b(?:Server|Data Source|Initial Catalog|User Id|Password|ConnectionString)\s*=/i
];
// Whitespace-free variants for the tag-stripped tight surface. SQL keyword
// patterns stay case-sensitive because lowercase prose fuses into matching
// shapes once whitespace is removed; the tag-joined surface below catches
// markup-split keywords case-insensitively instead, so fused-scan case
// sensitivity stays a documented limitation rather than a bypass.
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

export async function validateGapLineNumberArticleDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors
}) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "blog", gapLineNumberArticleSlug, "index.html");

  if (!(await fileExists(pagePath))) {
    errors.push(withEvidence(`Gap line-number article is missing required route: ${gapLineNumberArticleRoute}`, pageArtifact));
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
  if (!urls.has(`${baseUrl}${gapLineNumberArticleRoute}`)) {
    errors.push(withEvidence(`Gap line-number sitemap is missing required route: ${baseUrl}${gapLineNumberArticleRoute}`, "sitemap.xml"));
  }
}

async function validateBlogIndex({ dist, errors }) {
  const path = resolve(dist, "blog", "index.html");
  if (!(await fileExists(path))) {
    errors.push(withEvidence("Gap line-number blog index is missing.", "blog/index.html"));
    return;
  }
  const html = await readFile(path, "utf8");
  if (!hasHref(html, gapLineNumberArticleRoute)) {
    errors.push(withEvidence(`Gap line-number blog index is missing article link: ${gapLineNumberArticleRoute}`, "blog/index.html"));
    return;
  }
  const card = html.match(new RegExp(`<a\\b[^>]*href\\s*=\\s*["']${escapeRegExp(gapLineNumberArticleRoute)}["'][^>]*>[\\s\\S]*?<\\/a>`, "i"))?.[0] ?? "";
  const cardNormalized = decodeBrowserEntities(card);
  scanSafety(
    [normalizeRenderedText(cardNormalized), joinedText(cardNormalized), decodeHtmlEntities(cardNormalized)],
    errors,
    "blog/index.html",
    [decodeHtmlEntities(cardNormalized), tightText(cardNormalized)],
    tightText(cardNormalized)
  );
}

async function validateDiscovery({ dist, errors }) {
  const path = resolve(dist, "routes-index.json");
  if (!(await fileExists(path))) {
    errors.push(withEvidence("Gap line-number routes discovery output is missing.", "routes-index.json"));
    return;
  }
  let parsed;
  try {
    parsed = JSON.parse(await readFile(path, "utf8"));
  } catch (error) {
    errors.push(withEvidence(`Gap line-number routes discovery output is invalid JSON: ${error.message}`, "routes-index.json"));
    return;
  }
  if (!parsed || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Gap line-number routes discovery output must contain an entries array.", "routes-index.json"));
    return;
  }
  const entries = parsed.entries;
  const entry = entries.find((candidate) => candidate?.path === gapLineNumberArticleRoute);
  if (!entry) {
    errors.push(withEvidence("Gap line-number discovery entry is missing.", "routes-index.json"));
    return;
  }
  if (entry.publicClaimLevel !== "concept") errors.push(withEvidence("Gap line-number discovery claim level must be concept.", "routes-index.json"));
  if (entry.preferredProofPath !== "/evidence/") errors.push(withEvidence("Gap line-number preferred proof path must remain /evidence/.", "routes-index.json"));
  if (!Array.isArray(entry.limitations) || entry.limitations.length < 2) errors.push(withEvidence("Gap line-number discovery must include at least two limitations.", "routes-index.json"));
  if (!Array.isArray(entry.nonClaims) || entry.nonClaims.length < 2) errors.push(withEvidence("Gap line-number discovery must include at least two non-claims.", "routes-index.json"));
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
  scanSafety(surfaces, errors, "routes-index.json", [...surfaces, ...tight], tight.join(" "));
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

  if (!html.includes("<title>When a Gap Has No Honest Line Number | TraceMap</title>")) {
    errors.push(withEvidence("Gap line-number article is missing expected title.", pageArtifact));
  }
  if (!new RegExp(`<link\\b[^>]*rel=["']canonical["'][^>]*href=["']${escapeRegExp(baseUrl)}${escapeRegExp(gapLineNumberArticleRoute)}["']`, "i").test(html)) {
    errors.push(withEvidence("Gap line-number article canonical URL is missing or incorrect.", pageArtifact));
  }
  if (!new RegExp(`<meta\\b[^>]*property=["']og:title["'][^>]*content=["']When a Gap Has No Honest Line Number["']`, "i").test(html)) {
    errors.push(withEvidence("Gap line-number article Open Graph title is missing or incorrect.", pageArtifact));
  }
  if (!new RegExp(`<meta\\b[^>]*property=["']og:url["'][^>]*content=["']${escapeRegExp(baseUrl)}${escapeRegExp(gapLineNumberArticleRoute)}["']`, "i").test(html)) {
    errors.push(withEvidence("Gap line-number article Open Graph URL is missing or incorrect.", pageArtifact));
  }
  if (!/property=["']article:published_time["']\s+content=["']\d{4}-\d{2}-\d{2}["']/.test(html)) {
    errors.push(withEvidence("Gap line-number article published-time metadata is missing.", pageArtifact));
  }
  for (const block of requiredBlocks) {
    if (!new RegExp(`<section\\b[^>]*data-gap-span-block\\s*=\\s*["']${escapeRegExp(block)}["']`, "i").test(html)) {
      errors.push(withEvidence(`Gap line-number article is missing required block: ${block}`, pageArtifact));
    }
  }
  for (const [block, labels] of Object.entries(exampleBlockLabels)) {
    const blockHtml = html.match(new RegExp(`<section\\b[^>]*data-gap-span-block\\s*=\\s*["']${escapeRegExp(block)}["'][\\s\\S]*?<\\/section>`, "i"))?.[0] ?? "";
    for (const label of labels) {
      if (!blockHtml.includes(`<strong>${label}:</strong>`)) {
        errors.push(withEvidence(`Gap line-number example ${block} is missing labeled step: ${label}`, pageArtifact));
      }
    }
  }
  for (const phrase of requiredText) {
    if (!rendered.toLowerCase().includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Gap line-number article is missing required text: ${phrase}`, pageArtifact));
    }
  }
  for (const locationClass of requiredLocationClasses) {
    if (!rendered.includes(locationClass)) {
      errors.push(withEvidence(`Gap line-number article is missing location class: ${locationClass}`, pageArtifact));
    }
  }
  for (const label of requiredExampleLabels) {
    if (!html.includes(`<strong>${label}:</strong>`)) {
      errors.push(withEvidence(`Gap line-number article is missing example step label: ${label}`, pageArtifact));
    }
  }
  for (const ruleId of gapLineNumberArticleRuleIds) {
    if (!rendered.includes(ruleId)) {
      errors.push(withEvidence(`Gap line-number article is missing required rule ID: ${ruleId}`, pageArtifact));
    }
  }
  for (const token of new Set(rendered.match(/\b[a-z][a-z0-9]*(?:-[a-z0-9]+)*(?:\.[a-z0-9-]+)+\.v\d+\b/g) ?? [])) {
    if (!gapLineNumberArticleRuleIds.includes(token)) {
      errors.push(withEvidence(`Gap line-number article cites a rule ID outside the verified catalog list: ${token}`, pageArtifact));
    }
  }
  for (const link of gapLineNumberArticleRequiredLinks) {
    if (!hasHref(html, link)) errors.push(withEvidence(`Gap line-number article is missing required link: ${link}`, pageArtifact));
  }
  const words = rendered.split(/\s+/).filter(Boolean).length;
  if (words < 900 || words > 1800) errors.push(withEvidence(`Gap line-number article word count must be between 900 and 1800 words, got ${words}`, pageArtifact));
  const tight = tightText(html);
  scanSafety([rendered, joinedText(html), metadata], errors, pageArtifact, [decoded, metadata, tight], tight);
}

function scanSafety(surfaces, errors, artifact, privateSurfaces = surfaces, tightSurface = "") {
  for (const pattern of forbiddenClaims) {
    if (surfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`Gap line-number article contains unsupported positive claim: ${pattern}`, artifact));
  }
  for (const pattern of rawMaterialPatterns) {
    if (surfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`Gap line-number article contains raw or executable material: ${pattern}`, artifact));
  }
  for (const pattern of [...rawMaterialPatterns, ...tightRawMaterialPatterns]) {
    if (tightSurface && pattern.test(tightSurface)) errors.push(withEvidence(`Gap line-number article contains raw or executable material: ${pattern}`, artifact));
  }
  for (const pattern of hardPrivatePatterns) {
    if (privateSurfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`Gap line-number article contains hard private material: ${pattern}`, artifact));
  }
}

// Tag-stripped, whitespace-collapsed surface so tokens split across markup
// (for example "/Use<span>rs/") cannot evade the private/raw scans.
function tightText(html) {
  return decodeHtmlEntities(stripTagsQuoteAware(String(html))).replace(/\s+/g, "");
}

// Tag-stripped surface that preserves whitespace so markup-split keywords
// ("sel<span>ect</span>") rejoin as real words for the case-insensitive
// raw-material patterns without fusing ordinary prose.
function joinedText(html) {
  return decodeHtmlEntities(stripTagsQuoteAware(String(html))).replace(/\s+/g, " ").trim();
}

function hasHref(html, href) {
  return new RegExp(`<a\\b[^>]*href\\s*=\\s*["']${escapeRegExp(href)}["'][^>]*>`, "i").test(html);
}

function withEvidence(message, artifact) {
  return `${message} Evidence: ${artifact}.`;
}
