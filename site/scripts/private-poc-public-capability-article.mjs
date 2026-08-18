import { execFileSync } from "node:child_process";
import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import {
  decodeHtmlEntities,
  EvidenceTiers,
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
const evidenceTierByRuleId = Object.freeze({
  [findingRuleId]: EvidenceTiers.Tier4Unknown,
  "docs-export.validation.unsafe-value-rejected.v1": EvidenceTiers.Tier4Unknown,
  "docs-export.validation.prohibited-claim-wording.v1": EvidenceTiers.Tier4Unknown
});
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
const tightForbiddenClaimPatterns = [
  /(?:TraceMap|thisarticle|thesynthetic(?:fixture|contract)|thepublic-safe(?:summary|candidate))(?:proves|confirms|establishes|guarantees|validates)/i,
  /(?:safetorelease|safetodeploy|safetomigrate|production-ready)/i,
  /(?:TraceMap|thisarticle|thepubliccapability)completecoverage/i,
  /(?:customer|privateapplication|runtime|production|deployment|migration|compliance|vulnerability|release)(?:is|was|hasbeen)(?:proven|confirmed|safe|validated|established)/i
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
  /SELECT.+?FROM/i,
  /INSERTINTO/i,
  /DELETEFROM/i,
  /UPDATE\S+SET/i,
  /(?:CREATE|ALTER|DROP|GRANT|REVOKE)(?:TABLE|VIEW|USER|ROLE|DATABASE|PROCEDURE|FUNCTION)/i,
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
const privateEndpointPatterns = [
  /\bhttps?:\/\/[a-z0-9.-]*(?:\.internal|\.intranet|\.local|\.private|[-.]private)(?::\d+)?(?:[/?#\s"'<>]|$)/i,
  /\bhttps?:\/\/(?:private|localhost|127(?:\.\d{1,3}){3}|10(?:\.\d{1,3}){3}|192\.168(?:\.\d{1,3}){2}|172\.(?:1[6-9]|2\d|3[0-1])(?:\.\d{1,3}){2})(?::\d+)?(?:[/?#\s"'<>]|$)/i
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
  const source = await readFile(sitemapPath, "utf8");
  const urls = await readSitemapLocSet(sitemapPath);
  if (!urls.has(`${baseUrl}${privatePocPublicCapabilityArticleRoute}`)) {
    errors.push(withEvidence(
      `Private POC sitemap is missing required route: ${baseUrl}${privatePocPublicCapabilityArticleRoute}`,
      "sitemap.xml",
      findingRuleId,
      artifactLineSpan(source)
    ));
  }
}

async function validateBlogIndex({ dist, errors }) {
  const path = resolve(dist, "blog", "index.html");
  if (!(await fileExists(path))) {
    errors.push(withEvidence("Private POC blog index is missing.", "blog/index.html"));
    return;
  }

  const html = await readFile(path, "utf8");
  const artifactSpan = artifactLineSpan(html);
  const finding = (message, ruleId = findingRuleId) => withEvidence(message, "blog/index.html", ruleId, artifactSpan);
  if (!hasHref(html, privatePocPublicCapabilityArticleRoute)) {
    errors.push(finding(`Private POC blog index is missing article registry link: ${privatePocPublicCapabilityArticleRoute}`));
  }
  const card = findAnchorBlockByHref(html, privatePocPublicCapabilityArticleRoute);
  if (!normalizeRenderedText(card).includes(articleTitle)) {
    errors.push(finding("Private POC blog index card is missing the article title from the registry."));
  }
  scanSafety(
    [normalizeRenderedText(card), joinedText(card), decodeHtmlEntities(card)],
    errors,
    "blog/index.html",
    [decodeHtmlEntities(card), tightText(card)],
    tightText(card),
    html
  );
}

async function validateDiscovery({ dist, errors }) {
  const path = resolve(dist, "routes-index.json");
  if (!(await fileExists(path))) {
    errors.push(withEvidence("Private POC routes discovery output is missing.", "routes-index.json"));
    return;
  }

  const source = await readFile(path, "utf8");
  const artifactSpan = artifactLineSpan(source);
  const finding = (message, ruleId = findingRuleId) => withEvidence(message, "routes-index.json", ruleId, artifactSpan);
  let parsed;
  try {
    parsed = JSON.parse(source);
  } catch (error) {
    errors.push(finding(`Private POC routes discovery output is invalid JSON: ${error.message}`));
    return;
  }
  if (!parsed || !Array.isArray(parsed.entries)) {
    errors.push(finding("Private POC routes discovery output must contain an entries array."));
    return;
  }

  const entry = parsed.entries.find((candidate) => candidate?.path === privatePocPublicCapabilityArticleRoute);
  if (!entry) {
    errors.push(finding("Private POC discovery entry is missing."));
    return;
  }
  if (entry.title !== articleTitle) errors.push(finding("Private POC discovery entry title is incorrect."));
  if (entry.publicClaimLevel !== "concept") errors.push(finding("Private POC discovery claim level must be concept."));
  if (entry.preferredProofPath !== "/proof-paths/") errors.push(finding("Private POC preferred proof path must remain /proof-paths/."));
  if (!Array.isArray(entry.limitations) || entry.limitations.length < 2) errors.push(finding("Private POC discovery must include at least two limitations."));
  if (!Array.isArray(entry.nonClaims) || entry.nonClaims.length < 2) errors.push(finding("Private POC discovery must include at least two non-claims."));

  const fields = [
    entry.title,
    entry.summary,
    entry.preferredProofPath,
    ...(Array.isArray(entry.limitations) ? entry.limitations : []),
    ...(Array.isArray(entry.nonClaims) ? entry.nonClaims : [])
  ].filter((value) => typeof value === "string");
  const surfaces = fields.map((value) => decodeHtmlEntities(decodeBrowserEntities(value)));
  const tight = fields.map((value) => tightText(decodeBrowserEntities(value))).join(" ");
  scanSafety(surfaces, errors, "routes-index.json", [...surfaces, tight], tight, source);
}

async function validateArticle({ baseUrl, pagePath, errors }) {
  const rawHtml = await readFile(pagePath, "utf8");
  const html = decodeBrowserEntities(rawHtml);
  const artifactSpan = artifactLineSpan(rawHtml);
  const finding = (message, ruleId = findingRuleId, lineSpan = artifactSpan) => withEvidence(message, pageArtifact, ruleId, lineSpan);
  const decoded = decodeHtmlEntities(html);
  const rendered = normalizeRenderedText(html);
  const metadata = decodeHtmlEntities(
    [...(html.match(/<head\b[^>]*>[\s\S]*?<\/head>/i)?.[0] ?? "").matchAll(/\bcontent\s*=\s*(["'])(.*?)\1/gi)]
      .map((match) => match[2])
      .join(" ")
  );

  if (!html.includes(`<title>${articleTitle} | TraceMap</title>`)) errors.push(finding("Private POC article title is missing or incorrect."));
  if (!hasTagWithAttributes(html, "link", { rel: "canonical", href: `${baseUrl}${privatePocPublicCapabilityArticleRoute}` })) {
    errors.push(finding("Private POC article canonical URL is missing or incorrect."));
  }
  if (!hasTagWithAttributes(html, "meta", { property: "og:title", content: articleTitle })) {
    errors.push(finding("Private POC article Open Graph title is missing or incorrect."));
  }
  if (!hasTagWithAttributes(html, "meta", { property: "og:url", content: `${baseUrl}${privatePocPublicCapabilityArticleRoute}` })) {
    errors.push(finding("Private POC article Open Graph URL is missing or incorrect."));
  }
  if (!hasTagWithAttributePatterns(html, "meta", { property: /article:published_time/, content: /\d{4}-\d{2}-\d{2}/ })) {
    errors.push(finding("Private POC article published-time metadata is missing."));
  }

  for (const block of requiredBlocks) {
    if (!new RegExp(`<section\\b[^>]*data-private-poc-block\\s*=\\s*["']${escapeRegExp(block)}["']`, "i").test(html)) {
      errors.push(finding(`Private POC article is missing required section: ${block}`));
    }
  }

  const boundaryStartTag = html.match(/<section\b[^>]*data-private-poc-block\s*=\s*["']claim-boundary["'][^>]*>/i)?.[0] ?? "";
  for (const [attribute, value] of [
    ["data-private-poc-boundary", "claim-boundary"],
    ["data-tm-boundary", "claim-boundary"]
  ]) {
    if (!new RegExp(`${attribute}\\s*=\\s*["']${escapeRegExp(value)}["']`, "i").test(boundaryStartTag)) {
      errors.push(finding(`Private POC claim-boundary section must carry ${attribute}="${value}"`));
    }
  }

  const chainHtml = html.match(/<ol\b[^>]*data-private-poc-chain\s*=\s*["']promotion["'][\s\S]*?<\/ol>/i)?.[0] ?? "";
  if (!chainHtml) {
    errors.push(finding("Private POC article is missing the promotion chain."));
  } else {
    for (const label of requiredChainLabels) {
      if (!chainHtml.includes(`<strong>${label}:</strong>`)) {
        errors.push(finding(`Private POC promotion chain is missing labeled step: ${label}`));
      }
    }
  }

  for (const phrase of requiredText) {
    if (!rendered.toLowerCase().includes(phrase.toLowerCase())) {
      errors.push(finding(`Private POC article is missing required text: ${phrase}`));
    }
  }
  for (const ruleId of privatePocPublicCapabilityArticleRuleIds) {
    if (!rendered.includes(ruleId)) errors.push(finding(`Private POC article is missing required rule ID: ${ruleId}`));
  }
  for (const token of new Set(rendered.match(/\b[a-z][a-z0-9]*(?:-[a-z0-9]+)*(?:\.[a-z0-9-]+)+\.v\d+\b/g) ?? [])) {
    if (!privatePocPublicCapabilityArticleRuleIds.includes(token)) {
      errors.push(finding(`Private POC article cites a rule ID outside the verified catalog list: ${token}`));
    }
  }
  for (const link of privatePocPublicCapabilityArticleRequiredLinks) {
    if (!hasHref(html, link)) errors.push(finding(`Private POC article is missing required link: ${link}`));
  }

  const words = rendered.split(/\s+/).filter(Boolean).length;
  if (words < 1300 || words > 2200) errors.push(finding(`Private POC article word count must be between 1300 and 2200 words, got ${words}`));
  const tight = tightText(html);
  scanSafety([rendered, joinedText(html), decoded, metadata], errors, pageArtifact, [decoded, metadata, tight], tight, rawHtml);
}

function scanSafety(surfaces, errors, artifact, privateSurfaces = surfaces, tightSurface = "", source = "") {
  // Include a markup- and whitespace-collapsed claim surface so inline/block tags cannot split a forbidden phrase past detection.
  const claimSurfaces = tightSurface ? [...surfaces, tightSurface] : surfaces;
  for (const pattern of forbiddenClaims) {
    if (claimSurfaces.some((surface) => testPattern(pattern, surface))) errors.push(withEvidence(`Private POC article contains unsupported positive claim: ${pattern}`, artifact, "docs-export.validation.prohibited-claim-wording.v1", findLineSpan(source, pattern, ["raw", "joined", "tight"])));
  }
  for (const pattern of tightForbiddenClaimPatterns) {
    if (claimSurfaces.some((surface) => testPattern(pattern, surface))) errors.push(withEvidence(`Private POC article contains unsupported positive claim: ${pattern}`, artifact, "docs-export.validation.prohibited-claim-wording.v1", findLineSpan(source, pattern, ["tight"])));
  }
  for (const pattern of rawMaterialPatterns) {
    if (surfaces.some((surface) => testPattern(pattern, surface))) errors.push(withEvidence(`Private POC article contains raw or executable material: ${pattern}`, artifact, "docs-export.validation.unsafe-value-rejected.v1", findLineSpan(source, pattern, ["raw", "joined", "tight"])));
  }
  for (const pattern of tightRawMaterialPatterns) {
    if (tightSurface && testPattern(pattern, tightSurface)) errors.push(withEvidence(`Private POC article contains raw or executable material: ${pattern}`, artifact, "docs-export.validation.unsafe-value-rejected.v1", findLineSpan(source, pattern, ["tight"])));
  }
  for (const pattern of hardPrivatePatterns) {
    if (privateSurfaces.some((surface) => testPattern(pattern, surface))) errors.push(withEvidence(`Private POC article contains hard private material: ${pattern}`, artifact, "docs-export.validation.unsafe-value-rejected.v1", findLineSpan(source, pattern, ["raw", "joined", "tight"])));
  }
  for (const pattern of privateEndpointPatterns) {
    if (privateSurfaces.some((surface) => testPattern(pattern, surface))) errors.push(withEvidence(`Private POC article contains private endpoint URL: ${pattern}`, artifact, "docs-export.validation.unsafe-value-rejected.v1", findLineSpan(source, pattern, ["raw", "joined", "tight"])));
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

function testPattern(pattern, value) {
  const flags = pattern.flags.replaceAll("g", "").replaceAll("y", "");
  return new RegExp(pattern.source, flags).test(String(value));
}

function artifactLineSpan(source) {
  const lineCount = String(source ?? "").split(/\r?\n/).length;
  return { start_line: 1, end_line: Math.max(1, lineCount) };
}

function findLineSpan(source, pattern, modes) {
  if (!source) return { start_line: 1, end_line: 1 };
  const lines = String(source).split(/\r?\n/);
  for (let start = 0; start < lines.length; start += 1) {
    let window = "";
    for (let end = start; end < lines.length; end += 1) {
      window += end === start ? lines[end] : `\n${lines[end]}`;
      if (modes.some((mode) => testPattern(pattern, surfaceForMode(window, mode)))) {
        return { start_line: start + 1, end_line: end + 1 };
      }
    }
  }
  return artifactLineSpan(source);
}

function surfaceForMode(value, mode) {
  const decoded = decodeBrowserEntities(value);
  if (mode === "raw") return decoded;
  if (mode === "tight") return tightText(decoded);
  return joinedText(decoded);
}

function withEvidence(message, artifact, ruleId = findingRuleId, lineSpan = { start_line: 1, end_line: 1 }) {
  const evidenceTier = evidenceTierByRuleId[ruleId] ?? EvidenceTiers.Tier4Unknown;
  const evidence = {
    rule_id: ruleId,
    evidence_tier: evidenceTier,
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
