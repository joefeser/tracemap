import { execFileSync } from "node:child_process";
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

export const privatePocPublicCapabilityArticleSlug = "private-poc-pain-to-public-safe-capability";
export const privatePocPublicCapabilityArticleRoute = `/blog/${privatePocPublicCapabilityArticleSlug}/`;
export const privatePocPublicCapabilityArticleRequiredLinks = [
  "/proof-paths/",
  "/proof-source-catalog/",
  "/site-claim-guardrails/",
  "/review-claim-checklist/",
  "/evidence/",
  "/evidence/gaps/",
  "/limitations/",
  "/blog/how-a-gap-becomes-a-rule/",
  "/blog/building-tracemap-under-review-pressure/",
  "/blog/what-a-proof-path-is/",
  "/blog/bugs-hiding-in-graph-history/"
];

const pageArtifact = `blog/${privatePocPublicCapabilityArticleSlug}/index.html`;
const articleTitle = "How Private POC Pain Becomes Public-Safe Product Capability";
const requiredBlocks = [
  "private-signal",
  "abstract-failure-class",
  "independent-reproduction",
  "contract-chain",
  "promotion-decision",
  "public-evidence",
  "private-material",
  "claim-boundary"
];
const requiredChainLabels = [
  "private observation",
  "abstract failure hypothesis",
  "synthetic fixture",
  "rule/invariant",
  "implementation",
  "regression",
  "public-safe candidate",
  "bounded claim"
];
export const privatePocPublicCapabilityArticleRuleIds = [
  "legacy.baseline.safety-validation.v1",
  "legacy.evidence-pack.claim-boundary.v1",
  "legacy.evidence-pack.safety-validation.v1",
  "legacy.sample-smoke-catalog.safety-validation.v1",
  "public.demo.summary.v1",
  "docs-export.validation.unsafe-value-rejected.v1",
  "docs-export.validation.prohibited-claim-wording.v1",
  "docs-export.gap.claim-level-hidden.v1"
];
export const privatePocPublicCapabilityArticleExtractorVersion = "private-poc-public-capability-article-validator.v1";
const findingRuleId = "legacy.evidence-pack.claim-boundary.v1";
const validationCommitSha = resolveValidationCommitSha();
const requiredText = [
  "Public claim level: concept",
  "private observation",
  "abstract failure class",
  "independent synthetic reproduction",
  "deterministic invariant",
  "regression fixture",
  "product implementation",
  "public-safe summary",
  "owner/reviewer decision",
  "Tier1Semantic",
  "Tier2Structural",
  "Tier3SyntaxOrTextual",
  "Tier4Unknown",
  "provenance",
  "spans",
  "coverage",
  "gaps",
  "hidden",
  "local-only",
  "demo-safe",
  "public-safe",
  "concept",
  "demo",
  "shipped",
  "one completely synthetic example",
  "not only a count",
  "no customer success claim",
  "no runtime",
  "no production"
];
const forbiddenClaims = [
  /\b(?:TraceMap|this article|the synthetic (?:fixture|contract)|the public-safe (?:summary|candidate))\s+(?:proves|confirms|establishes|guarantees|validates)\b/i,
  /\b(?:safe to release|safe to deploy|safe to migrate|production-ready)\b/i,
  /\b(?:TraceMap|this article|the public capability)\b[^.]{0,80}\bcomplete coverage\b/i,
  /\b(?:customer|private application|runtime|production|deployment|migration|compliance|vulnerability|release)\s+(?:is|was|has been)\s+(?:proven|confirmed|safe|validated|established)\b/i
];
const rawMaterialPatterns = [
  /\bSELECT\s+.+\bFROM\b/i,
  /\bINSERT\s+INTO\b/i,
  /\bDELETE\s+FROM\b/i,
  /\bUPDATE\s+\S+\s+SET\b/i,
  /\b(?:CREATE|ALTER|DROP|GRANT|REVOKE)\s+(?:TABLE|VIEW|USER|ROLE|DATABASE|PROCEDURE|FUNCTION)\b/i,
  /\b(?:Server|Data Source|Initial Catalog|User Id|Password|ConnectionString)\s*=/i
];
const tightRawMaterialPatterns = [
  /SELECT.+?FROM/,
  /INSERTINTO/,
  /DELETEFROM/,
  /UPDATE\S+SET/,
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

export async function validatePrivatePocPublicCapabilityArticleDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors
}) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "blog", privatePocPublicCapabilityArticleSlug, "index.html");

  if (!(await fileExists(pagePath))) {
    errors.push(withEvidence(`Private POC article is missing required route: ${privatePocPublicCapabilityArticleRoute}`, pageArtifact));
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
  if (!urls.has(`${baseUrl}${privatePocPublicCapabilityArticleRoute}`)) {
    errors.push(withEvidence(`Private POC sitemap is missing required route: ${baseUrl}${privatePocPublicCapabilityArticleRoute}`, "sitemap.xml"));
  }
}

async function validateBlogIndex({ dist, errors }) {
  const path = resolve(dist, "blog", "index.html");
  if (!(await fileExists(path))) {
    errors.push(withEvidence("Private POC blog index is missing.", "blog/index.html"));
    return;
  }

  const html = await readFile(path, "utf8");
  if (!hasHref(html, privatePocPublicCapabilityArticleRoute)) {
    errors.push(withEvidence(`Private POC blog index is missing article registry link: ${privatePocPublicCapabilityArticleRoute}`, "blog/index.html"));
  }
  const card = findAnchorBlockByHref(html, privatePocPublicCapabilityArticleRoute);
  if (!normalizeRenderedText(card).includes(articleTitle)) {
    errors.push(withEvidence("Private POC blog index card is missing the article title from the registry.", "blog/index.html"));
  }
  scanSafety(
    [normalizeRenderedText(card), joinedText(card), decodeHtmlEntities(card)],
    errors,
    "blog/index.html",
    [decodeHtmlEntities(card), tightText(card)],
    tightText(card)
  );
}

async function validateDiscovery({ dist, errors }) {
  const path = resolve(dist, "routes-index.json");
  if (!(await fileExists(path))) {
    errors.push(withEvidence("Private POC routes discovery output is missing.", "routes-index.json"));
    return;
  }

  let parsed;
  try {
    parsed = JSON.parse(await readFile(path, "utf8"));
  } catch (error) {
    errors.push(withEvidence(`Private POC routes discovery output is invalid JSON: ${error.message}`, "routes-index.json"));
    return;
  }
  if (!parsed || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Private POC routes discovery output must contain an entries array.", "routes-index.json"));
    return;
  }

  const entry = parsed.entries.find((candidate) => candidate?.path === privatePocPublicCapabilityArticleRoute);
  if (!entry) {
    errors.push(withEvidence("Private POC discovery entry is missing.", "routes-index.json"));
    return;
  }
  if (entry.title !== articleTitle) errors.push(withEvidence("Private POC discovery entry title is incorrect.", "routes-index.json"));
  if (entry.publicClaimLevel !== "concept") errors.push(withEvidence("Private POC discovery claim level must be concept.", "routes-index.json"));
  if (entry.preferredProofPath !== "/proof-paths/") errors.push(withEvidence("Private POC preferred proof path must remain /proof-paths/.", "routes-index.json"));
  if (!Array.isArray(entry.limitations) || entry.limitations.length < 2) errors.push(withEvidence("Private POC discovery must include at least two limitations.", "routes-index.json"));
  if (!Array.isArray(entry.nonClaims) || entry.nonClaims.length < 2) errors.push(withEvidence("Private POC discovery must include at least two non-claims.", "routes-index.json"));

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

  if (!html.includes(`<title>${articleTitle} | TraceMap</title>`)) errors.push(withEvidence("Private POC article title is missing or incorrect.", pageArtifact));
  if (!hasTagWithAttributes(html, "link", { rel: "canonical", href: `${baseUrl}${privatePocPublicCapabilityArticleRoute}` })) {
    errors.push(withEvidence("Private POC article canonical URL is missing or incorrect.", pageArtifact));
  }
  if (!hasTagWithAttributes(html, "meta", { property: "og:title", content: articleTitle })) {
    errors.push(withEvidence("Private POC article Open Graph title is missing or incorrect.", pageArtifact));
  }
  if (!hasTagWithAttributes(html, "meta", { property: "og:url", content: `${baseUrl}${privatePocPublicCapabilityArticleRoute}` })) {
    errors.push(withEvidence("Private POC article Open Graph URL is missing or incorrect.", pageArtifact));
  }
  if (!hasTagWithAttributePatterns(html, "meta", { property: /article:published_time/, content: /\d{4}-\d{2}-\d{2}/ })) {
    errors.push(withEvidence("Private POC article published-time metadata is missing.", pageArtifact));
  }

  for (const block of requiredBlocks) {
    if (!new RegExp(`<section\\b[^>]*data-private-poc-block\\s*=\\s*["']${escapeRegExp(block)}["']`, "i").test(html)) {
      errors.push(withEvidence(`Private POC article is missing required section: ${block}`, pageArtifact));
    }
  }

  const boundaryStartTag = html.match(/<section\b[^>]*data-private-poc-block\s*=\s*["']claim-boundary["'][^>]*>/i)?.[0] ?? "";
  for (const [attribute, value] of [
    ["data-private-poc-boundary", "claim-boundary"],
    ["data-tm-boundary", "claim-boundary"]
  ]) {
    if (!new RegExp(`${attribute}\\s*=\\s*["']${escapeRegExp(value)}["']`, "i").test(boundaryStartTag)) {
      errors.push(withEvidence(`Private POC claim-boundary section must carry ${attribute}="${value}".`, pageArtifact));
    }
  }

  const chainHtml = html.match(/<ol\b[^>]*data-private-poc-chain\s*=\s*["']promotion["'][\s\S]*?<\/ol>/i)?.[0] ?? "";
  if (!chainHtml) {
    errors.push(withEvidence("Private POC article is missing the promotion chain.", pageArtifact));
  } else {
    for (const label of requiredChainLabels) {
      if (!chainHtml.includes(`<strong>${label}:</strong>`)) {
        errors.push(withEvidence(`Private POC promotion chain is missing labeled step: ${label}`, pageArtifact));
      }
    }
  }

  for (const phrase of requiredText) {
    if (!rendered.toLowerCase().includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Private POC article is missing required text: ${phrase}`, pageArtifact));
    }
  }
  for (const ruleId of privatePocPublicCapabilityArticleRuleIds) {
    if (!rendered.includes(ruleId)) errors.push(withEvidence(`Private POC article is missing required rule ID: ${ruleId}`, pageArtifact));
  }
  for (const token of new Set(rendered.match(/\b[a-z][a-z0-9]*(?:-[a-z0-9]+)*(?:\.[a-z0-9-]+)+\.v\d+\b/g) ?? [])) {
    if (!privatePocPublicCapabilityArticleRuleIds.includes(token)) {
      errors.push(withEvidence(`Private POC article cites a rule ID outside the verified catalog list: ${token}`, pageArtifact));
    }
  }
  for (const link of privatePocPublicCapabilityArticleRequiredLinks) {
    if (!hasHref(html, link)) errors.push(withEvidence(`Private POC article is missing required link: ${link}`, pageArtifact));
  }

  const words = rendered.split(/\s+/).filter(Boolean).length;
  if (words < 1300 || words > 2200) errors.push(withEvidence(`Private POC article word count must be between 1300 and 2200 words, got ${words}`, pageArtifact));
  const tight = tightText(html);
  scanSafety([rendered, joinedText(html), decoded, metadata], errors, pageArtifact, [decoded, metadata, tight], tight);
}

function scanSafety(surfaces, errors, artifact, privateSurfaces = surfaces, tightSurface = "") {
  for (const pattern of forbiddenClaims) {
    if (surfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`Private POC article contains unsupported positive claim: ${pattern}`, artifact, "docs-export.validation.prohibited-claim-wording.v1"));
  }
  for (const pattern of rawMaterialPatterns) {
    if (surfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`Private POC article contains raw or executable material: ${pattern}`, artifact, "docs-export.validation.unsafe-value-rejected.v1"));
  }
  for (const pattern of tightRawMaterialPatterns) {
    if (tightSurface && pattern.test(tightSurface)) errors.push(withEvidence(`Private POC article contains raw or executable material: ${pattern}`, artifact, "docs-export.validation.unsafe-value-rejected.v1"));
  }
  for (const pattern of hardPrivatePatterns) {
    if (privateSurfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`Private POC article contains hard private material: ${pattern}`, artifact, "docs-export.validation.unsafe-value-rejected.v1"));
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
  return extractHtmlStartTags(html).some((tag) => tag.name === "a" && new RegExp(`\\bhref\\s*=\\s*["']${escapeRegExp(route)}["']`, "i").test(tag.raw));
}

function findAnchorBlockByHref(html, route) {
  const anchor = extractHtmlStartTags(html).find((tag) => tag.name === "a" && new RegExp(`\\bhref\\s*=\\s*["']${escapeRegExp(route)}["']`, "i").test(tag.raw));
  if (!anchor) return "";
  const closing = html.slice(anchor.end + 1).search(/<\/a\s*>/i);
  return closing < 0 ? html.slice(anchor.start) : html.slice(anchor.start, anchor.end + 1 + closing + html.slice(anchor.end + 1 + closing).match(/<\/a\s*>/i)[0].length);
}

function hasTagWithAttributes(html, tagName, attributes) {
  return extractHtmlStartTags(html)
    .filter((tag) => tag.name === tagName)
    .some((tag) => Object.entries(attributes).every(([name, value]) => new RegExp(`\\b${escapeRegExp(name)}\\s*=\\s*["']${escapeRegExp(value)}["']`, "i").test(tag.raw)));
}

function hasTagWithAttributePatterns(html, tagName, attributes) {
  return extractHtmlStartTags(html)
    .filter((tag) => tag.name === tagName)
    .some((tag) => Object.entries(attributes).every(([name, value]) => {
      const expected = value instanceof RegExp ? value.source : escapeRegExp(value);
      return new RegExp(`\\b${escapeRegExp(name)}\\s*=\\s*["']${expected}["']`, "i").test(tag.raw);
    }));
}

function extractHtmlStartTags(html) {
  const tags = [];
  let index = 0;
  while (index < html.length) {
    const start = html.indexOf("<", index);
    if (start < 0) break;
    if (html.startsWith("<!--", start)) {
      const commentEnd = html.indexOf("-->", start + 4);
      index = commentEnd < 0 ? html.length : commentEnd + 3;
      continue;
    }
    const end = findTagEnd(html, start);
    if (end < 0) break;
    const raw = html.slice(start, end + 1);
    const match = raw.match(/^<\s*([a-z][a-z0-9:-]*)\b/i);
    if (match) tags.push({ name: match[1].toLowerCase(), raw, start, end });
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

function joinedText(value) {
  return decodeHtmlEntities(stripTagsQuoteAware(value)).replace(/\s+/g, " ").trim();
}

function tightText(value) {
  return decodeHtmlEntities(stripTagsQuoteAware(value)).replace(/\s+/g, "");
}

function withEvidence(message, artifact, ruleId = findingRuleId) {
  const lineSpan = { start: null, end: null };
  const evidence = {
    rule_id: ruleId,
    evidence_tier: "Tier3SyntaxOrTextual",
    file_path: artifact,
    line_span: lineSpan,
    commit_sha: validationCommitSha,
    extractor_version: privatePocPublicCapabilityArticleExtractorVersion
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
      return `${this.message} [evidence: ${this.file_path}]`;
    }
  };
}

function resolveValidationCommitSha() {
  const environmentSha = process.env.GITHUB_SHA ?? process.env.COMMIT_SHA ?? "";
  if (/^[0-9a-f]{40}$/i.test(environmentSha)) return environmentSha;
  try {
    const repositorySha = execFileSync("git", ["rev-parse", "HEAD"], {
      cwd: process.cwd(),
      encoding: "utf8",
      stdio: ["ignore", "pipe", "ignore"]
    }).trim();
    if (/^[0-9a-f]{40}$/i.test(repositorySha)) return repositorySha;
  } catch {
    // The explicit error below keeps missing provenance fail-closed.
  }
  throw new Error("Private POC article validation requires a full 40-character commit SHA from GITHUB_SHA, COMMIT_SHA, or git HEAD.");
}
