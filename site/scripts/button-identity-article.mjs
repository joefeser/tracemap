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

export const buttonIdentityArticleSlug = "a-button-named-save-is-not-an-identity";
export const buttonIdentityArticleRoute = `/blog/${buttonIdentityArticleSlug}/`;
export const buttonIdentityArticleRequiredLinks = [
  "/legacy-modernization/evidence-map/",
  "/blog/modernizing-web-forms-without-running-it/",
  "/blog/when-a-gap-has-no-honest-line-number/",
  "/blog/what-depends-on-this-symbol/",
  "/legacy-dotnet/evidence/",
  "/evidence/gaps/",
  "/limitations/",
  "/static-vs-runtime/"
];

const pageArtifact = `blog/${buttonIdentityArticleSlug}/index.html`;
const requiredBlocks = [
  "label-not-key",
  "identity-layers",
  "resolution-ladder",
  "duplicates-preserved",
  "fail-closed",
  "synthetic-comparison",
  "downstream-cost",
  "non-claims"
];
// Rule IDs verified against rules/rule-catalog.yml at the article's base SHA.
// The closed list below also anchors the unknown-rule-token scan.
export const buttonIdentityArticleRuleIds = [
  "legacy.webforms.inventory.v1",
  "legacy.webforms.event-binding.v1",
  "legacy.webforms.handler-resolution.v1",
  "legacy.webforms.designer-control.v1",
  "legacy.webforms.event-flow.v1",
  "csharp.semantic.symbolidentity.v1"
];
const requiredLadderTiers = [
  "Tier1Semantic",
  "Tier2Structural",
  "Tier3SyntaxOrTextual",
  "Tier4Unknown"
];
const requiredIdentityLayers = [
  "Visible caption or display label",
  "Control declaration and control ID",
  "Markup file and page or control type",
  "Event name and binding fact identity",
  "Linked code-behind scope",
  "Canonical handler method symbol",
  "Supporting fact IDs, rule IDs, evidence tier, line span, commit, and extractor provenance"
];
const chainBlocks = {
  "semantic-page": ["structural", "semantic", "gap"],
  "textual-page": ["structural", "candidate", "syntax/textual", "gap"]
};
const requiredText = [
  "Public claim level: concept",
  "A repeated name is not itself a defect",
  "collapsing identities is the defect",
  "convincing but false cross-page flow",
  "labels alone are not canonical identity",
  "never matched globally by method name",
  "The first same-named method is never chosen",
  "under reduced coverage does not mean",
  "ambiguity gap",
  "supporting evidence only",
  "stable fact IDs",
  "deterministic ordering",
  "canonical handler symbol",
  "different markup files",
  "different code-behind scopes",
  "does not prove that the event fires",
  "does not prove persistence",
  "business intent",
  "migration success",
  "synthetic",
  "no LLM calls, embeddings, vector databases, or prompt-based classification"
];
const forbiddenClaims = [
  /\bproves\b/i,
  /\b(?:is|are|was|were)\s+(?:proved|proven|verified|validated|confirmed|guaranteed)\b/i,
  /\b(?:migration succeeded|parity confirmed|safe to release|safe to migrate|fully covered|complete coverage of the application)\b/i,
  /\b(?:deployed|deployment)\s+(?:successfully|succeeded|confirmed)\b/i,
  /\buser\s+clicked\b/i,
  /\b(?:button|control|handler|event)\s+caused\b/i,
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

export async function validateButtonIdentityArticleDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors
}) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "blog", buttonIdentityArticleSlug, "index.html");

  if (!(await fileExists(pagePath))) {
    errors.push(withEvidence(`Button identity article is missing required route: ${buttonIdentityArticleRoute}`, pageArtifact));
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
  if (!urls.has(`${baseUrl}${buttonIdentityArticleRoute}`)) {
    errors.push(withEvidence(`Button identity sitemap is missing required route: ${baseUrl}${buttonIdentityArticleRoute}`, "sitemap.xml"));
  }
}

async function validateBlogIndex({ dist, errors }) {
  const path = resolve(dist, "blog", "index.html");
  if (!(await fileExists(path))) {
    errors.push(withEvidence("Button identity blog index is missing.", "blog/index.html"));
    return;
  }
  const html = await readFile(path, "utf8");
  if (!hasHref(html, buttonIdentityArticleRoute)) {
    errors.push(withEvidence(`Button identity blog index is missing article link: ${buttonIdentityArticleRoute}`, "blog/index.html"));
    return;
  }
  const card = html.match(new RegExp(`<a\\b[^>]*href\\s*=\\s*["']${escapeRegExp(buttonIdentityArticleRoute)}["'][^>]*>[\\s\\S]*?<\\/a>`, "i"))?.[0] ?? "";
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
    errors.push(withEvidence("Button identity routes discovery output is missing.", "routes-index.json"));
    return;
  }
  let parsed;
  try {
    parsed = JSON.parse(await readFile(path, "utf8"));
  } catch (error) {
    errors.push(withEvidence(`Button identity routes discovery output is invalid JSON: ${error.message}`, "routes-index.json"));
    return;
  }
  if (!parsed || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Button identity routes discovery output must contain an entries array.", "routes-index.json"));
    return;
  }
  const entries = parsed.entries;
  const entry = entries.find((candidate) => candidate?.path === buttonIdentityArticleRoute);
  if (!entry) {
    errors.push(withEvidence("Button identity discovery entry is missing.", "routes-index.json"));
    return;
  }
  if (entry.publicClaimLevel !== "concept") errors.push(withEvidence("Button identity discovery claim level must be concept.", "routes-index.json"));
  if (entry.preferredProofPath !== "/legacy-modernization/evidence-map/") errors.push(withEvidence("Button identity preferred proof path must remain /legacy-modernization/evidence-map/.", "routes-index.json"));
  if (!Array.isArray(entry.limitations) || entry.limitations.length < 2) errors.push(withEvidence("Button identity discovery must include at least two limitations.", "routes-index.json"));
  if (!Array.isArray(entry.nonClaims) || entry.nonClaims.length < 2) errors.push(withEvidence("Button identity discovery must include at least two non-claims.", "routes-index.json"));
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

  if (!html.includes("<title>A Button Named Save Is Not an Identity | TraceMap</title>")) {
    errors.push(withEvidence("Button identity article is missing expected title.", pageArtifact));
  }
  if (!new RegExp(`<link\\b[^>]*rel=["']canonical["'][^>]*href=["']${escapeRegExp(baseUrl)}${escapeRegExp(buttonIdentityArticleRoute)}["']`, "i").test(html)) {
    errors.push(withEvidence("Button identity article canonical URL is missing or incorrect.", pageArtifact));
  }
  if (!new RegExp(`<meta\\b[^>]*property=["']og:title["'][^>]*content=["']A Button Named Save Is Not an Identity["']`, "i").test(html)) {
    errors.push(withEvidence("Button identity article Open Graph title is missing or incorrect.", pageArtifact));
  }
  if (!new RegExp(`<meta\\b[^>]*property=["']og:url["'][^>]*content=["']${escapeRegExp(baseUrl)}${escapeRegExp(buttonIdentityArticleRoute)}["']`, "i").test(html)) {
    errors.push(withEvidence("Button identity article Open Graph URL is missing or incorrect.", pageArtifact));
  }
  if (!/property=["']article:published_time["']\s+content=["']\d{4}-\d{2}-\d{2}["']/.test(html)) {
    errors.push(withEvidence("Button identity article published-time metadata is missing.", pageArtifact));
  }
  for (const block of requiredBlocks) {
    if (!new RegExp(`<section\\b[^>]*data-save-identity-block\\s*=\\s*["']${escapeRegExp(block)}["']`, "i").test(html)) {
      errors.push(withEvidence(`Button identity article is missing required block: ${block}`, pageArtifact));
    }
  }
  if (!new RegExp(`<section\\b[^>]*data-save-identity-block\\s*=\\s*["']non-claims["'][^>]*data-save-identity-boundary\\s*=\\s*["']non-claims["'][^>]*data-tm-boundary\\s*=\\s*["']claim-boundary["']`, "i").test(html)) {
    errors.push(withEvidence("Button identity article non-claims block must carry the claim-boundary attributes.", pageArtifact));
  }
  for (const [chain, labels] of Object.entries(chainBlocks)) {
    const chainHtml = html.match(new RegExp(`<ol\\b[^>]*data-save-identity-chain\\s*=\\s*["']${escapeRegExp(chain)}["'][\\s\\S]*?<\\/ol>`, "i"))?.[0] ?? "";
    if (!chainHtml) {
      errors.push(withEvidence(`Button identity synthetic comparison is missing chain: ${chain}`, pageArtifact));
      continue;
    }
    for (const label of labels) {
      if (!chainHtml.includes(`<strong>${label}:</strong>`)) {
        errors.push(withEvidence(`Button identity chain ${chain} is missing labeled step: ${label}`, pageArtifact));
      }
    }
  }
  for (const phrase of requiredText) {
    if (!rendered.toLowerCase().includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Button identity article is missing required text: ${phrase}`, pageArtifact));
    }
  }
  for (const tier of requiredLadderTiers) {
    if (!rendered.includes(tier)) {
      errors.push(withEvidence(`Button identity article is missing resolution ladder tier: ${tier}`, pageArtifact));
    }
  }
  for (const layer of requiredIdentityLayers) {
    if (!rendered.includes(layer)) {
      errors.push(withEvidence(`Button identity article is missing identity layer: ${layer}`, pageArtifact));
    }
  }
  for (const ruleId of buttonIdentityArticleRuleIds) {
    if (!rendered.includes(ruleId)) {
      errors.push(withEvidence(`Button identity article is missing required rule ID: ${ruleId}`, pageArtifact));
    }
  }
  for (const token of new Set(rendered.match(/\b[a-z][a-z0-9]*(?:-[a-z0-9]+)*(?:\.[a-z0-9-]+)+\.v\d+\b/g) ?? [])) {
    if (!buttonIdentityArticleRuleIds.includes(token)) {
      errors.push(withEvidence(`Button identity article cites a rule ID outside the verified catalog list: ${token}`, pageArtifact));
    }
  }
  for (const link of buttonIdentityArticleRequiredLinks) {
    if (!hasHref(html, link)) errors.push(withEvidence(`Button identity article is missing required link: ${link}`, pageArtifact));
  }
  const words = rendered.split(/\s+/).filter(Boolean).length;
  if (words < 900 || words > 1800) errors.push(withEvidence(`Button identity article word count must be between 900 and 1800 words, got ${words}`, pageArtifact));
  const tight = tightText(html);
  scanSafety([rendered, joinedText(html), metadata], errors, pageArtifact, [decoded, metadata, tight], tight);
}

function scanSafety(surfaces, errors, artifact, privateSurfaces = surfaces, tightSurface = "") {
  for (const pattern of forbiddenClaims) {
    if (surfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`Button identity article contains unsupported positive claim: ${pattern}`, artifact));
  }
  for (const pattern of rawMaterialPatterns) {
    if (surfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`Button identity article contains raw or executable material: ${pattern}`, artifact));
  }
  for (const pattern of [...rawMaterialPatterns, ...tightRawMaterialPatterns]) {
    if (tightSurface && pattern.test(tightSurface)) errors.push(withEvidence(`Button identity article contains raw or executable material: ${pattern}`, artifact));
  }
  for (const pattern of hardPrivatePatterns) {
    if (privateSurfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`Button identity article contains hard private material: ${pattern}`, artifact));
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
