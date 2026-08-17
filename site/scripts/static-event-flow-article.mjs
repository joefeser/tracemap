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

export const staticEventFlowArticleSlug = "static-event-flow-what-it-proves";
export const staticEventFlowArticleRoute = `/blog/${staticEventFlowArticleSlug}/`;
export const staticEventFlowArticleRequiredLinks = [
  "/blog/modernizing-web-forms-without-running-it/",
  "/blog/a-button-named-save-is-not-an-identity/",
  "/blog/successful-build-can-still-have-reduced-coverage/",
  "/evidence/",
  "/evidence/gaps/",
  "/static-vs-runtime/",
  "/legacy-modernization/evidence-map/",
  "/legacy-modernization/review-handoff/"
];

const pageArtifact = `blog/${staticEventFlowArticleSlug}/index.html`;
const requiredBlocks = [
  "event-question",
  "evidence-chain",
  "handler-identity",
  "weakest-hop",
  "classifications",
  "synthetic-example",
  "useful-for-review",
  "non-claims"
];
export const staticEventFlowArticleRuleIds = [
  "legacy.webforms.inventory.v1",
  "legacy.webforms.event-binding.v1",
  "legacy.webforms.handler-resolution.v1",
  "legacy.webforms.designer-control.v1",
  "legacy.webforms.event-flow.v1",
  "csharp.semantic.symbolidentity.v1",
  "csharp.semantic.callgraph.v1",
  "csharp.syntax.callgraph.v1"
];
export const staticEventFlowArticleExtractorVersion = "static-event-flow-article-validator.v1";
const staticEventFlowArticleFindingRuleId = "legacy.webforms.event-flow.v1";
const staticEventFlowValidationCommitSha = /^[0-9a-f]{40}$/i.test(process.env.GITHUB_SHA ?? "")
  ? process.env.GITHUB_SHA
  : "unknown";
const requiredClassifications = [
  "StrongStaticEventFlow",
  "ProbableStaticEventFlow",
  "NeedsReviewEventFlow",
  "NoBackendEvidence",
  "UnknownAnalysisGap"
];
const requiredTiers = [
  "Tier1Semantic",
  "Tier2Structural",
  "Tier3SyntaxOrTextual",
  "Tier4Unknown"
];
const requiredText = [
  "Public claim level: concept",
  "declared control event",
  "static call or dependency evidence",
  "bounded terminal surface",
  "scoped handler-resolution",
  "supportingFactIds",
  "supportingEdgeIds",
  "composed classification",
  "weakest required hop",
  "current implementation",
  "BuildStatus == Succeeded",
  "coverage: Full",
  "coverage: Reduced",
  "Coverage gap",
  "Owner question",
  "does not prove",
  "runtime dependency selection",
  "SQL execution",
  "migration correctness or parity",
  "release approval",
  "synthetic",
  "not an LLM"
];

const forbiddenClaims = [
  /\b(?:TraceMap|static event flow|the (?:path|chain|flow))\s+(?:directly\s+)?(?:prove|proves|proved|confirm|confirms|confirmed|guarantee|guarantees|guaranteed|establish|establishes|established|verify|verifies|verified)\b/i,
  /\b(?:screen|event|handler|service|SQL|database|deployment|migration|release)\s+(?:is|was|has been)\s+(?:reachable|executed|available|successful|correct|approved|proven|confirmed)\b/i,
  /\b(?:migration succeeded|parity confirmed|safe to release|safe to migrate|fully covered|complete coverage)\b/i,
  /\buser\s+(?:clicked|reached|saw)\b/i,
  /\b(?:button|event|handler|service|SQL|database)\s+caused\b/i,
  /\b(?:business intent|release approval)\s+(?:is|was)\s+(?:proven|confirmed|established)\b/i
];
const rawMaterialPatterns = [
  /\bSELECT\s+.+\bFROM\b/i,
  /\b(?:INSERT|DELETE)\s+(?:INTO|FROM)\b/i,
  /\bUPDATE\s+\S+\s+SET\b/i,
  /\b(?:CREATE|ALTER|DROP|GRANT|REVOKE)\s+(?:TABLE|VIEW|USER|ROLE|DATABASE|PROCEDURE|FUNCTION)\b/i,
  /\b(?:Server|Data Source|Initial Catalog|User Id|Password|ConnectionString)\s*=/i
];
const tightRawMaterialPatterns = [
  /\bSELECT\s+.+\bFROM\b/i,
  /\b(?:INSERT|DELETE)\s+(?:INTO|FROM)\b/i,
  /\bUPDATE\s+\S+\s+SET\b/i,
  /\b(?:CREATE|ALTER|DROP|GRANT|REVOKE)\s+(?:TABLE|VIEW|USER|ROLE|DATABASE|PROCEDURE|FUNCTION)\b/i,
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

export async function validateStaticEventFlowArticleDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors
}) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "blog", staticEventFlowArticleSlug, "index.html");

  if (!(await fileExists(pagePath))) {
    errors.push(withEvidence(`Static event-flow article is missing required route: ${staticEventFlowArticleRoute}`, pageArtifact));
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
  if (!urls.has(`${baseUrl}${staticEventFlowArticleRoute}`)) {
    errors.push(withEvidence(`Static event-flow sitemap is missing required route: ${baseUrl}${staticEventFlowArticleRoute}`, "sitemap.xml"));
  }
}

async function validateBlogIndex({ dist, errors }) {
  const path = resolve(dist, "blog", "index.html");
  if (!(await fileExists(path))) {
    errors.push(withEvidence("Static event-flow blog index is missing.", "blog/index.html"));
    return;
  }
  const html = await readFile(path, "utf8");
  if (!hasHref(html, staticEventFlowArticleRoute)) {
    errors.push(withEvidence(`Static event-flow blog index is missing article link: ${staticEventFlowArticleRoute}`, "blog/index.html"));
    return;
  }
  const card = findAnchorBlockByHref(html, staticEventFlowArticleRoute);
  const decodedCard = decodeBrowserEntities(card);
  const surfaces = [normalizeRenderedText(decodedCard), joinedText(decodedCard), decodeHtmlEntities(decodedCard)];
  scanSafety(surfaces, errors, "blog/index.html", surfaces, tightText(decodedCard));
}

async function validateDiscovery({ dist, errors }) {
  const path = resolve(dist, "routes-index.json");
  if (!(await fileExists(path))) {
    errors.push(withEvidence("Static event-flow routes discovery output is missing.", "routes-index.json"));
    return;
  }

  let parsed;
  try {
    parsed = JSON.parse(await readFile(path, "utf8"));
  } catch (error) {
    errors.push(withEvidence(`Static event-flow routes discovery output is invalid JSON: ${error.message}`, "routes-index.json"));
    return;
  }
  if (!parsed || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Static event-flow routes discovery output must contain an entries array.", "routes-index.json"));
    return;
  }

  const entry = parsed.entries.find((candidate) => candidate?.path === staticEventFlowArticleRoute);
  if (!entry) {
    errors.push(withEvidence("Static event-flow discovery entry is missing.", "routes-index.json"));
    return;
  }
  if (entry.publicClaimLevel !== "concept") {
    errors.push(withEvidence("Static event-flow discovery claim level must be concept.", "routes-index.json"));
  }
  if (entry.preferredProofPath !== "/legacy-modernization/evidence-map/") {
    errors.push(withEvidence("Static event-flow preferred proof path must remain /legacy-modernization/evidence-map/.", "routes-index.json"));
  }
  if (!Array.isArray(entry.limitations) || entry.limitations.length < 2) {
    errors.push(withEvidence("Static event-flow discovery must include at least two limitations.", "routes-index.json"));
  }
  if (!Array.isArray(entry.nonClaims) || entry.nonClaims.length < 2) {
    errors.push(withEvidence("Static event-flow discovery must include at least two non-claims.", "routes-index.json"));
  }

  const fields = [
    entry.title,
    entry.summary,
    entry.preferredProofPath,
    ...(Array.isArray(entry.limitations) ? entry.limitations : []),
    ...(Array.isArray(entry.nonClaims) ? entry.nonClaims : [])
  ].filter((value) => typeof value === "string");
  const surfaces = fields.map((value) => decodeHtmlEntities(decodeBrowserEntities(value)));
  const tight = fields.map((value) => tightText(decodeBrowserEntities(value))).join(" ");
  scanSafety(surfaces, errors, "routes-index.json", surfaces, tight);
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

  if (!html.includes("<title>Static Event Flow: What It Proves—and What It Does Not | TraceMap</title>")) {
    errors.push(withEvidence("Static event-flow article is missing expected title.", pageArtifact));
  }
  if (!hasTagWithAttributes(html, "link", {
    rel: "canonical",
    href: `${baseUrl}${staticEventFlowArticleRoute}`
  })) {
    errors.push(withEvidence("Static event-flow article canonical URL is missing or incorrect.", pageArtifact));
  }
  if (!hasTagWithAttributes(html, "meta", {
    property: "og:title",
    content: "Static Event Flow: What It Proves—and What It Does Not"
  })) {
    errors.push(withEvidence("Static event-flow article Open Graph title is missing or incorrect.", pageArtifact));
  }
  if (!hasTagWithAttributes(html, "meta", {
    property: "og:url",
    content: `${baseUrl}${staticEventFlowArticleRoute}`
  })) {
    errors.push(withEvidence("Static event-flow article Open Graph URL is missing or incorrect.", pageArtifact));
  }
  if (!hasTagWithAttributePatterns(html, "meta", {
    property: /article:published_time/,
    content: /\d{4}-\d{2}-\d{2}/
  })) {
    errors.push(withEvidence("Static event-flow article published-time metadata is missing.", pageArtifact));
  }

  for (const block of requiredBlocks) {
    if (!new RegExp(`<section\\b[^>]*data-static-event-flow-block\\s*=\\s*["']${escapeRegExp(block)}["']`, "i").test(html)) {
      errors.push(withEvidence(`Static event-flow article is missing required block: ${block}`, pageArtifact));
    }
  }

  const nonClaimsStartTag =
    html.match(/<section\b[^>]*>/gi)?.find((tag) => /data-static-event-flow-block\s*=\s*["']non-claims["']/i.test(tag)) ?? "";
  for (const [attribute, value] of [
    ["data-static-event-flow-boundary", "non-claims"],
    ["data-tm-boundary", "claim-boundary"]
  ]) {
    if (!new RegExp(`${attribute}\\s*=\\s*["']${escapeRegExp(value)}["']`, "i").test(nonClaimsStartTag)) {
      errors.push(withEvidence(`Static event-flow non-claims block must carry ${attribute}="${value}".`, pageArtifact));
    }
  }

  const chainHtml = html.match(/<ol\b[^>]*data-static-event-flow-chain\s*=\s*["']bounded-chain["'][\s\S]*?<\/ol>/i)?.[0] ?? "";
  if (!chainHtml) {
    errors.push(withEvidence("Static event-flow article is missing the bounded evidence chain.", pageArtifact));
  } else {
    for (const label of ["declared", "handler", "relationship", "terminal", "classification", "gap"]) {
      if (!chainHtml.includes(`<strong>${label}:</strong>`)) {
        errors.push(withEvidence(`Static event-flow chain is missing labeled step: ${label}`, pageArtifact));
      }
    }
  }

  for (const phrase of requiredText) {
    if (!rendered.toLowerCase().includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Static event-flow article is missing required text: ${phrase}`, pageArtifact));
    }
  }
  for (const classification of requiredClassifications) {
    if (!rendered.includes(classification)) {
      errors.push(withEvidence(`Static event-flow article is missing classification: ${classification}`, pageArtifact));
    }
  }
  for (const tier of requiredTiers) {
    if (!rendered.includes(tier)) {
      errors.push(withEvidence(`Static event-flow article is missing evidence tier: ${tier}`, pageArtifact));
    }
  }
  for (const ruleId of staticEventFlowArticleRuleIds) {
    if (!rendered.includes(ruleId)) {
      errors.push(withEvidence(`Static event-flow article is missing required rule ID: ${ruleId}`, pageArtifact));
    }
  }
  for (const token of new Set(rendered.match(/\b[a-z][a-z0-9]*(?:-[a-z0-9]+)*(?:\.[a-z0-9-]+)+\.v\d+\b/g) ?? [])) {
    if (!staticEventFlowArticleRuleIds.includes(token)) {
      errors.push(withEvidence(`Static event-flow article cites a rule ID outside the verified catalog list: ${token}`, pageArtifact));
    }
  }
  for (const link of staticEventFlowArticleRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Static event-flow article is missing required link: ${link}`, pageArtifact));
    }
  }

  const words = rendered.split(/\s+/).filter(Boolean).length;
  if (words < 1100 || words > 1900) {
    errors.push(withEvidence(`Static event-flow article word count must be between 1100 and 1900 words, got ${words}`, pageArtifact));
  }

  const tight = tightText(html);
  scanSafety([rendered, joinedText(html), decoded, metadata], errors, pageArtifact, [decoded, metadata, tight], tight);
}

function scanSafety(surfaces, errors, artifact, privateSurfaces = surfaces, tightSurface = "") {
  for (const pattern of forbiddenClaims) {
    if (surfaces.some((surface) => pattern.test(surface))) {
      errors.push(withEvidence(`Static event-flow article contains unsupported positive claim: ${pattern}`, artifact));
    }
  }
  for (const pattern of rawMaterialPatterns) {
    if (surfaces.some((surface) => pattern.test(surface))) {
      errors.push(withEvidence(`Static event-flow article contains raw or executable material: ${pattern}`, artifact));
    }
  }
  for (const pattern of [...rawMaterialPatterns, ...tightRawMaterialPatterns]) {
    if (tightSurface && pattern.test(tightSurface)) {
      errors.push(withEvidence(`Static event-flow article contains raw or executable material: ${pattern}`, artifact));
    }
  }
  for (const pattern of hardPrivatePatterns) {
    if (privateSurfaces.some((surface) => pattern.test(surface))) {
      errors.push(withEvidence(`Static event-flow article contains hard private material: ${pattern}`, artifact));
    }
  }
}

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

function hasHref(html, route) {
  return findAnchorByHref(html, route) !== null;
}

function findAnchorByHref(html, route) {
  const hrefPattern = new RegExp(`\\bhref\\s*=\\s*["']${escapeRegExp(route)}["']`, "i");
  return extractHtmlStartTags(html).find((tag) => tag.name === "a" && hrefPattern.test(tag.raw)) ?? null;
}

function findAnchorBlockByHref(html, route) {
  const anchor = findAnchorByHref(html, route);
  if (!anchor) return "";
  const closingTag = findClosingTag(html, anchor.end + 1, "a");
  return html.slice(anchor.start, closingTag?.end ?? anchor.end + 1);
}

function extractHtmlStartTags(html) {
  const source = String(html);
  const tags = [];
  let index = 0;

  while (index < source.length) {
    const start = source.indexOf("<", index);
    if (start < 0) break;
    if (source.startsWith("<!--", start)) {
      const commentEnd = source.indexOf("-->", start + 4);
      index = commentEnd < 0 ? source.length : commentEnd + 3;
      continue;
    }

    const end = findTagEnd(source, start);
    if (end < 0) break;
    const raw = source.slice(start, end + 1);
    const match = raw.match(/^<\s*([a-z][a-z0-9:-]*)\b/i);
    if (match) {
      const name = match[1].toLowerCase();
      if (name === "script" || name === "style") {
        const closingTag = findClosingTag(source, end + 1, name);
        index = closingTag?.end ?? source.length;
        continue;
      }
      tags.push({ name, raw, start, end });
    }
    index = end + 1;
  }

  return tags;
}

function findTagEnd(source, start) {
  let quote = "";
  for (let index = start + 1; index < source.length; index += 1) {
    const char = source[index];
    if (quote) {
      if (char === quote) quote = "";
    } else if (char === '"' || char === "'") {
      quote = char;
    } else if (char === ">") {
      return index;
    }
  }
  return -1;
}

function findClosingTag(source, start, tagName) {
  let index = start;
  while (index < source.length) {
    const tagStart = source.indexOf("<", index);
    if (tagStart < 0) return null;
    if (source.startsWith("<!--", tagStart)) {
      const commentEnd = source.indexOf("-->", tagStart + 4);
      index = commentEnd < 0 ? source.length : commentEnd + 3;
      continue;
    }
    const end = findTagEnd(source, tagStart);
    if (end < 0) return null;
    const raw = source.slice(tagStart, end + 1);
    if (new RegExp(`^<\\s*\\/\\s*${escapeRegExp(tagName)}\\b`, "i").test(raw)) {
      return { start: tagStart, end };
    }
    index = end + 1;
  }
  return null;
}

function hasTagWithAttributes(html, tagName, attributes) {
  return hasTagWithAttributePatterns(
    html,
    tagName,
    Object.fromEntries(Object.entries(attributes).map(([name, value]) => [name, escapeRegExp(value)]))
  );
}

function hasTagWithAttributePatterns(html, tagName, attributes) {
  const lookaheads = Object.entries(attributes)
    .map(([name, value]) => `(?=[^>]*\\b${escapeRegExp(name)}\\s*=\\s*["']${value.source ?? value}["'])`)
    .join("");
  return new RegExp(`<${escapeRegExp(tagName)}\\b${lookaheads}[^>]*>`, "i").test(html);
}

function joinedText(value) {
  return stripTagsQuoteAware(value).replace(/\s+/g, " ").trim();
}

function tightText(value) {
  return joinedText(value).replace(/(^|\s)([A-Za-z](?:\s+[A-Za-z]){2,})(?=\s|$)/g, (_, prefix, letters) => (
    prefix + letters.replace(/\s+/g, "")
  ));
}

function withEvidence(message, artifact) {
  const lineSpan = { start: null, end: null };
  const evidence = {
    rule_id: staticEventFlowArticleFindingRuleId,
    evidence_tier: "Tier3SyntaxOrTextual",
    file_path: artifact,
    line_span: lineSpan,
    commit_sha: staticEventFlowValidationCommitSha,
    extractor_version: staticEventFlowArticleExtractorVersion
  };
  return {
    message,
    rule_id: evidence.rule_id,
    evidence_tier: evidence.evidence_tier,
    file_path: evidence.file_path,
    line_span: lineSpan,
    commit_sha: evidence.commit_sha,
    extractor_version: evidence.extractor_version,
    evidence: [evidence],
    toString() {
      return this.message + " [evidence: " + this.file_path + "]";
    }
  };
}
