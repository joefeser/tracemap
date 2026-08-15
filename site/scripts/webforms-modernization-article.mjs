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

export const webformsModernizationArticleSlug = "modernizing-web-forms-without-running-it";
export const webformsModernizationArticleRoute = `/blog/${webformsModernizationArticleSlug}/`;
export const webformsModernizationArticleRequiredLinks = [
  "/legacy-modernization/evidence-map/",
  "/legacy-modernization/review-handoff/",
  "/legacy-dotnet/evidence/",
  "/test-planning/",
  "/static-vs-runtime/",
  "/limitations/"
];

const pageArtifact = `blog/${webformsModernizationArticleSlug}/index.html`;
const requiredBlocks = [
  "scattered-evidence",
  "identity-problem",
  "observable-evidence",
  "handler-ladder",
  "example-chain",
  "coverage-gap",
  "why-useful",
  "non-claims"
];
const requiredText = [
  "Public claim level: concept",
  "legacy.webforms.inventory.v1",
  "legacy.webforms.event-binding.v1",
  "legacy.webforms.handler-resolution.v1",
  "legacy.webforms.event-flow.v1",
  "legacy.aspnet.route.v1",
  "analyzer.capability.syntax-fallback.v1",
  "Tier1Semantic",
  "Tier2Structural",
  "Tier3SyntaxOrTextual",
  "Tier4Unknown",
  "candidate handler",
  "declared",
  "gap",
  "reduced",
  "synthetic fixture",
  "AutoEventWireup",
  "supporting fact IDs",
  "extractor version",
  "commit"
];
const forbiddenClaims = [
  /\b(?:ran|hosted|executed|rendered)\b[^.]{0,80}\b(?:application|page|site|Web Forms app)\b/i,
  /\bproves\b/i,
  /\b(?:is|are|was|were)\s+(?:proved|proven|verified|validated|confirmed|guaranteed)\b/i,
  /\b(?:migration succeeded|parity confirmed|safe to release|safe to migrate|fully covered|complete coverage of the application)\b/i,
  /\b(?:TraceMap|this evidence|static evidence|this article)\b[^.]{0,120}\b(?:establishes|confirms|guarantees|validates)\b/i
];
const rawMaterialPatterns = [
  /\bSELECT\s+.+\bFROM\b/i,
  /\b(?:CREATE|ALTER|DROP|GRANT|REVOKE)\s+(?:TABLE|VIEW|USER|ROLE|DATABASE|PROCEDURE|FUNCTION)\b/i,
  /\b(?:Server|Data Source|Initial Catalog|User Id|Password|ConnectionString)\s*=/i
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
// Whitespace-free variants for the tag-stripped tight surface, where markup
// like "SEL<span>ECT ... FROM" collapses to contiguous text. SQL keyword
// patterns stay case-sensitive because lowercase prose ("was selected ...
// from") fuses into matching shapes once whitespace is removed.
const tightRawMaterialPatterns = [
  /SELECT.+?FROM/,
  /(?:CREATE|ALTER|DROP|GRANT|REVOKE)(?:TABLE|VIEW|USER|ROLE|DATABASE|PROCEDURE|FUNCTION)/,
  /(?:Server|Data ?Source|Initial ?Catalog|User ?Id|Password|ConnectionString)=/i
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

export async function validateWebformsModernizationArticleDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors
}) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "blog", webformsModernizationArticleSlug, "index.html");

  if (!(await fileExists(pagePath))) {
    errors.push(withEvidence(`Web Forms modernization article is missing required route: ${webformsModernizationArticleRoute}`, pageArtifact));
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
  if (!urls.has(`${baseUrl}${webformsModernizationArticleRoute}`)) {
    errors.push(withEvidence(`Web Forms modernization sitemap is missing required route: ${baseUrl}${webformsModernizationArticleRoute}`, "sitemap.xml"));
  }
}

async function validateBlogIndex({ dist, errors }) {
  const path = resolve(dist, "blog", "index.html");
  if (!(await fileExists(path))) {
    errors.push(withEvidence("Web Forms modernization blog index is missing.", "blog/index.html"));
    return;
  }
  const html = await readFile(path, "utf8");
  if (!hasHref(html, webformsModernizationArticleRoute)) {
    errors.push(withEvidence(`Web Forms modernization blog index is missing article link: ${webformsModernizationArticleRoute}`, "blog/index.html"));
    return;
  }
  const card = html.match(new RegExp(`<a\\b[^>]*href\\s*=\\s*["']${escapeRegExp(webformsModernizationArticleRoute)}["'][^>]*>[\\s\\S]*?<\\/a>`, "i"))?.[0] ?? "";
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
    errors.push(withEvidence("Web Forms modernization routes discovery output is missing.", "routes-index.json"));
    return;
  }
  let parsed;
  try {
    parsed = JSON.parse(await readFile(path, "utf8"));
  } catch (error) {
    errors.push(withEvidence(`Web Forms modernization routes discovery output is invalid JSON: ${error.message}`, "routes-index.json"));
    return;
  }
  if (!parsed || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Web Forms modernization routes discovery output must contain an entries array.", "routes-index.json"));
    return;
  }
  const entries = parsed.entries;
  const entry = entries.find((candidate) => candidate?.path === webformsModernizationArticleRoute);
  if (!entry) {
    errors.push(withEvidence("Web Forms modernization discovery entry is missing.", "routes-index.json"));
    return;
  }
  if (entry.publicClaimLevel !== "concept") errors.push(withEvidence("Web Forms modernization discovery claim level must be concept.", "routes-index.json"));
  if (entry.preferredProofPath !== "/legacy-modernization/evidence-map/") errors.push(withEvidence("Web Forms modernization preferred proof path must remain /legacy-modernization/evidence-map/.", "routes-index.json"));
  if (!Array.isArray(entry.limitations) || entry.limitations.length < 2) errors.push(withEvidence("Web Forms modernization discovery must include at least two limitations.", "routes-index.json"));
  if (!Array.isArray(entry.nonClaims) || entry.nonClaims.length < 2) errors.push(withEvidence("Web Forms modernization discovery must include at least two non-claims.", "routes-index.json"));
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

  if (!html.includes("<title>Modernizing Web Forms Without Running It | TraceMap</title>")) {
    errors.push(withEvidence("Web Forms modernization article is missing expected title.", pageArtifact));
  }
  if (!new RegExp(`<link\\b[^>]*rel=["']canonical["'][^>]*href=["']${escapeRegExp(baseUrl)}${escapeRegExp(webformsModernizationArticleRoute)}["']`, "i").test(html)) {
    errors.push(withEvidence("Web Forms modernization article canonical URL is missing or incorrect.", pageArtifact));
  }
  for (const block of requiredBlocks) {
    if (!new RegExp(`<section\\b[^>]*data-webforms-article-block\\s*=\\s*["']${escapeRegExp(block)}["']`, "i").test(html)) {
      errors.push(withEvidence(`Web Forms modernization article is missing required block: ${block}`, pageArtifact));
    }
  }
  for (const phrase of requiredText) {
    if (!rendered.toLowerCase().includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Web Forms modernization article is missing required text: ${phrase}`, pageArtifact));
    }
  }
  for (const link of webformsModernizationArticleRequiredLinks) {
    if (!hasHref(html, link)) errors.push(withEvidence(`Web Forms modernization article is missing required link: ${link}`, pageArtifact));
  }
  const words = rendered.split(/\s+/).filter(Boolean).length;
  if (words < 900 || words > 1800) errors.push(withEvidence(`Web Forms modernization article word count must be between 900 and 1800 words, got ${words}`, pageArtifact));
  const tight = tightText(html);
  scanSafety([rendered, metadata], errors, pageArtifact, [decoded, metadata, tight], tight);
}

function scanSafety(surfaces, errors, artifact, privateSurfaces = surfaces, tightSurface = "") {
  for (const pattern of forbiddenClaims) {
    if (surfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`Web Forms modernization article contains unsupported positive claim: ${pattern}`, artifact));
  }
  for (const pattern of rawMaterialPatterns) {
    if (surfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`Web Forms modernization article contains raw or executable material: ${pattern}`, artifact));
  }
  for (const pattern of [...rawMaterialPatterns, ...tightRawMaterialPatterns]) {
    if (tightSurface && pattern.test(tightSurface)) errors.push(withEvidence(`Web Forms modernization article contains raw or executable material: ${pattern}`, artifact));
  }
  for (const pattern of hardPrivatePatterns) {
    if (privateSurfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`Web Forms modernization article contains hard private material: ${pattern}`, artifact));
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
