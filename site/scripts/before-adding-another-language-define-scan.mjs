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

export const beforeAddingAnotherLanguageDefineScanSlug = "before-adding-another-language-define-scan";
export const beforeAddingAnotherLanguageDefineScanRoute = `/blog/${beforeAddingAnotherLanguageDefineScanSlug}/`;
export const beforeAddingAnotherLanguageDefineScanArticleTitle = "Before Adding Another Language, Define What “Scan” Means";
export const beforeAddingAnotherLanguageDefineScanArticleRequiredLinks = [
  "/evidence/",
  "/evidence/gaps/",
  "/limitations/",
  "/static-vs-runtime/",
  "/capabilities/",
  "/roadmap/",
  "/blog/successful-build-can-still-have-reduced-coverage/",
  "/blog/csharp-extraction-without-plausible-wrong-graphs/",
  "/blog/bugs-hiding-in-graph-history/",
  "/blog/how-tracemap-reads-swift-api-clients/"
];
export const beforeAddingAnotherLanguageDefineScanRuleIds = [
  "adapter.scan-truth.conformance.v1",
  "file.inventory.v1",
  "analyzer.capability.semantic.v1",
  "analyzer.capability.syntax-fallback.v1",
  "analyzer.capability.project-config.v1",
  "analyzer.capability.package-restore.v1",
  "analyzer.capability.generated-design-time.v1",
  "analyzer.capability.legacy-toolchain.v1",
  "analyzer.capability.downstream-coverage.v1"
];
export const beforeAddingAnotherLanguageDefineScanExtractorVersion = "before-adding-another-language-define-scan-validator.v1";

const pageArtifact = `blog/${beforeAddingAnotherLanguageDefineScanSlug}/index.html`;
const requiredBlocks = [
  "extension-vs-contract",
  "authority",
  "artifacts",
  "persistence",
  "failure",
  "shared-truth",
  "readiness-matrix",
  "claim-boundary"
];
const requiredArtifacts = [
  "scan-manifest.json",
  "facts.ndjson",
  "index.sqlite",
  "report.md",
  "logs/analyzer.log"
];
const requiredOutcomes = ["required", "supported", "reduced", "unsupported", "not-applicable", "not-run"];
const requiredText = [
  "Public claim level: concept",
  "repository identity",
  "commit SHA",
  "selected-byte identity",
  "SHA-256",
  "deterministic scan ID",
  "normalized options",
  "extractor versions",
  "transactional",
  "persistence",
  "facts.ndjson",
  "index.sqlite",
  "reduced",
  "rule-backed gaps",
  "Language X",
  "not only a count",
  "semantic parity",
  "runtime",
  "Issue #665",
  "deferred"
];
const forbiddenClaims = [
  /\b(?:TraceMap|this article|the scan|the adapter|the conformance profile|the contract|the result)\b[^.!?]{0,120}\b(?:proves?|confirms?|establishes?|guarantees?|supports?|demonstrates?)\b[^.!?]{0,120}\b(?:semantic parity|complete coverage|complete repository understanding|complete dependency coverage|runtime|production|execution|build success)\b/i,
  /\b(?:semantic parity|complete coverage|complete repository understanding|complete dependency coverage|runtime behavior|production behavior)\b[^.!?]{0,100}\b(?:is|are)\b[^.!?]{0,50}\b(?:proven|guaranteed|established|supported|confirmed)\b/i,
  /\b(?:TraceMap's|the|a|this)\s+Go(?:\s+adapter)?[^.!?]{0,100}\b(?:implemented|supported|ready|shipped|available|complete|finished)\b/i,
  /\bIssue\s*#?665\b[^.!?]{0,100}\b(?:implemented|supported|ready|shipped|closed|complete|finished)\b/i
];
const tightForbiddenClaims = [
  /(?:tracemap|thisarticle|thescan|theadapter|theconformanceprofile|thecontract|theresult)(?:proves?|confirms?|establishes?|guarantees?|supports?|demonstrates?)(?:semanticparity|completecoverage|completerepositoryunderstanding|completedependencycoverage|runtime|production|execution|buildsuccess)/i,
  /(?:semanticparity|completecoverage|completerepositoryunderstanding|completedependencycoverage|runtimebehavior|productionbehavior)(?:is|are)(?:proven|guaranteed|established|supported|confirmed)/i,
  /(?:tracemaps|the|a|this)go(?:adapter)?(?:implemented|supported|ready|shipped|available|complete|finished)/i,
  /issue#?665(?:implemented|supported|ready|shipped|closed|complete|finished)/i
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
  /(?:Server|Data ?Source|Initial ?Catalog|User ?Id|Password|ConnectionString)=/i
];
const hardPrivatePatterns = [
  /\/Users\//i,
  /\/private\//i,
  /\/home\//i,
  /\/tmp\//i,
  /\/var\/folders\//i,
  /~\//,
  /\b[A-Za-z]:[\\/]/i,
  /\bfile:\/\//i,
  /\bgit@/i,
  /\bsk-[A-Za-z0-9_-]{12,}\b/i
];
const privateEndpointPatterns = [
  /\bhttps?:\/\/[a-z0-9.-]*(?:\.internal|\.intranet|\.local|\.private|[-.]private)(?::\d+)?(?:[/?#\s"'<>]|$)/i,
  /\bhttps?:\/\/(?:private|localhost|127(?:\.\d{1,3}){3}|10(?:\.\d{1,3}){3}|192\.168(?:\.\d{1,3}){2}|172\.(?:1[6-9]|2\d|3[0-1])(?:\.\d{1,3}){2})(?::\d+)?(?:[/?#\s"'<>]|$)/i
];
const genericForbiddenProofPattern = /\b(?:proves?|confirms?|establishes?|guarantees?|supports?|demonstrates?)\b[^.!?]{0,120}\b(?:semantic parity|complete coverage|complete repository understanding|complete dependency coverage|runtime(?: behavior| reachability)?|production behavior|execution|build success)\b/i;
const browserNamedEntities = Object.freeze({
  amp: "&",
  apos: "'",
  backslash: "\\",
  bsol: "\\",
  colon: ":",
  gt: ">",
  lt: "<",
  nbsp: " ",
  period: ".",
  quot: '"',
  semi: ";",
  sol: "/"
});

export async function validateBeforeAddingAnotherLanguageDefineScanDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors
}) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "blog", beforeAddingAnotherLanguageDefineScanSlug, "index.html");

  if (!(await fileExists(pagePath))) {
    errors.push(withEvidence(`Language-scan contract article is missing required route: ${beforeAddingAnotherLanguageDefineScanRoute}`, pageArtifact));
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
  if (!urls.has(`${baseUrl}${beforeAddingAnotherLanguageDefineScanRoute}`)) {
    errors.push(withEvidence(
      `Language-scan contract article sitemap is missing required route: ${baseUrl}${beforeAddingAnotherLanguageDefineScanRoute}`,
      "sitemap.xml",
      undefined,
      artifactLineSpan(source)
    ));
  }
}

async function validateBlogIndex({ dist, errors }) {
  const path = resolve(dist, "blog", "index.html");
  if (!(await fileExists(path))) {
    errors.push(withEvidence("Language-scan contract article blog index is missing.", "blog/index.html"));
    return;
  }
  const html = await readFile(path, "utf8");
  const card = findAnchorBlockByHref(html, beforeAddingAnotherLanguageDefineScanRoute);
  if (!hasHref(html, beforeAddingAnotherLanguageDefineScanRoute)) {
    errors.push(withEvidence(`Blog index is missing article registry link: ${beforeAddingAnotherLanguageDefineScanRoute}`, "blog/index.html", undefined, artifactLineSpan(html)));
  }
  if (!normalizeRenderedText(card).includes(beforeAddingAnotherLanguageDefineScanArticleTitle)) {
    errors.push(withEvidence("Blog index card is missing the language-scan contract article title.", "blog/index.html", undefined, artifactLineSpan(html)));
  }
  scanSafety(
    [normalizeRenderedText(decodeBrowserEntities(card)), joinedText(decodeBrowserEntities(card)), decodeHtmlEntities(decodeBrowserEntities(card))],
    errors,
    "blog/index.html",
    [decodeHtmlEntities(decodeBrowserEntities(card)), tightText(decodeBrowserEntities(card))],
    tightText(decodeBrowserEntities(card)),
    html
  );
}

async function validateDiscovery({ dist, errors }) {
  const path = resolve(dist, "routes-index.json");
  if (!(await fileExists(path))) {
    errors.push(withEvidence("Language-scan contract article routes discovery output is missing.", "routes-index.json"));
    return;
  }
  const source = await readFile(path, "utf8");
  let parsed;
  try {
    parsed = JSON.parse(source);
  } catch (error) {
    errors.push(withEvidence(`Language-scan contract article routes discovery output is invalid JSON: ${error.message}`, "routes-index.json", undefined, artifactLineSpan(source)));
    return;
  }
  if (!parsed || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Language-scan contract article routes discovery output must contain an entries array.", "routes-index.json", undefined, artifactLineSpan(source)));
    return;
  }
  const entry = parsed.entries.find((candidate) => candidate?.path === beforeAddingAnotherLanguageDefineScanRoute);
  if (!entry) {
    errors.push(withEvidence("Language-scan contract article discovery entry is missing.", "routes-index.json", undefined, artifactLineSpan(source)));
    return;
  }
  if (entry.title !== beforeAddingAnotherLanguageDefineScanArticleTitle) errors.push(withEvidence("Language-scan contract discovery title is incorrect.", "routes-index.json", undefined, artifactLineSpan(source)));
  if (entry.publicClaimLevel !== "concept") errors.push(withEvidence("Language-scan contract discovery claim level must be concept.", "routes-index.json", undefined, artifactLineSpan(source)));
  if (entry.preferredProofPath !== "/evidence/") errors.push(withEvidence("Language-scan contract preferred proof path must remain /evidence/.", "routes-index.json", undefined, artifactLineSpan(source)));
  if (!Array.isArray(entry.limitations) || entry.limitations.length < 2) errors.push(withEvidence("Language-scan contract discovery must include at least two limitations.", "routes-index.json", undefined, artifactLineSpan(source)));
  if (!Array.isArray(entry.nonClaims) || entry.nonClaims.length < 2) errors.push(withEvidence("Language-scan contract discovery must include at least two non-claims.", "routes-index.json", undefined, artifactLineSpan(source)));

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
  const html = rawHtml;
  const browserDecodedHtml = decodeBrowserEntities(rawHtml);
  const artifactSpan = artifactLineSpan(rawHtml);
  const finding = (message, lineSpan = artifactSpan) => withEvidence(message, pageArtifact, undefined, lineSpan);
  const rendered = normalizeRenderedText(browserDecodedHtml);
  const decoded = decodeHtmlEntities(browserDecodedHtml);
  const metadata = decodeHtmlEntities(
    [...decodeBrowserEntities(html.match(/<head\b[^>]*>[\s\S]*?<\/head>/i)?.[0] ?? "").matchAll(/\bcontent\s*=\s*(["'])(.*?)\1/gi)]
      .map((match) => match[2])
      .join(" ")
  );

  if (!html.includes(`<title>${beforeAddingAnotherLanguageDefineScanArticleTitle} | TraceMap</title>`)) findingPush(errors, finding("Article title is missing or incorrect."));
  if (!hasTagWithAttributes(html, "link", { rel: "canonical", href: `${baseUrl}${beforeAddingAnotherLanguageDefineScanRoute}` })) findingPush(errors, finding("Article canonical URL is missing or incorrect."));
  if (!hasTagWithAttributes(html, "meta", { property: "og:title", content: beforeAddingAnotherLanguageDefineScanArticleTitle })) findingPush(errors, finding("Article Open Graph title is missing or incorrect."));
  if (!hasTagWithAttributes(html, "meta", { property: "og:url", content: `${baseUrl}${beforeAddingAnotherLanguageDefineScanRoute}` })) findingPush(errors, finding("Article Open Graph URL is missing or incorrect."));
  if (!hasTagWithAttributePatterns(html, "meta", { property: /article:published_time/, content: /\d{4}-\d{2}-\d{2}/ })) findingPush(errors, finding("Article published-time metadata is missing."));

  for (const block of requiredBlocks) {
    if (!new RegExp(`<section\\b[^>]*data-language-scan-block\\s*=\\s*["']${escapeRegExp(block)}["']`, "i").test(html)) {
      findingPush(errors, finding(`Article is missing required section: ${block}`));
    }
  }
  const boundaryStartTag = html.match(/<section\b[^>]*data-language-scan-block\s*=\s*["']claim-boundary["'][^>]*>/i)?.[0] ?? "";
  for (const attribute of ["data-language-scan-boundary", "data-tm-boundary"]) {
    if (!new RegExp(`${attribute}\\s*=\\s*["']claim-boundary["']`, "i").test(boundaryStartTag)) {
      findingPush(errors, finding(`Claim-boundary section must carry ${attribute}="claim-boundary"`));
    }
  }
  if (!/<table\b[^>]*data-language-scan-matrix\s*=\s*["']acceptance["']/i.test(html)) findingPush(errors, finding("Article is missing the acceptance readiness matrix."));

  for (const phrase of requiredText) {
    if (!rendered.toLowerCase().includes(phrase.toLowerCase())) findingPush(errors, finding(`Article is missing required text: ${phrase}`));
  }
  for (const artifact of requiredArtifacts) {
    if (!rendered.includes(artifact)) findingPush(errors, finding(`Article is missing required artifact: ${artifact}`));
  }
  for (const outcome of requiredOutcomes) {
    if (!new RegExp(`\\b${escapeRegExp(outcome)}\\b`, "i").test(rendered)) findingPush(errors, finding(`Article is missing required outcome: ${outcome}`));
  }
  for (const ruleId of beforeAddingAnotherLanguageDefineScanRuleIds) {
    if (!rendered.includes(ruleId)) findingPush(errors, finding(`Article is missing required rule ID: ${ruleId}`));
  }
  for (const token of new Set(rendered.match(/\b[a-z][a-z0-9]*(?:-[a-z0-9]+)*(?:\.[a-z0-9-]+)+\.v\d+\b/gi) ?? [])) {
    if (!beforeAddingAnotherLanguageDefineScanRuleIds.includes(token)) findingPush(errors, finding(`Article cites a rule ID outside the verified catalog list: ${token}`));
  }
  for (const link of beforeAddingAnotherLanguageDefineScanArticleRequiredLinks) {
    if (!hasHref(html, link)) findingPush(errors, finding(`Article is missing required link: ${link}`));
  }

  const words = rendered.split(/\s+/).filter(Boolean).length;
  if (words < 1500 || words > 2400) findingPush(errors, finding(`Article word count must be between 1500 and 2400 words, got ${words}`));
  scanSafety([rendered, joinedText(browserDecodedHtml), decoded, metadata], errors, pageArtifact, [decoded, metadata, tightText(browserDecodedHtml)], tightText(browserDecodedHtml), rawHtml);
}

function findingPush(errors, finding) {
  errors.push(finding);
}

function scanSafety(surfaces, errors, artifact, privateSurfaces = surfaces, tightSurface = "", source = "") {
  const claimSurfaces = tightSurface ? [...surfaces, tightSurface] : surfaces;
  for (const pattern of forbiddenClaims) {
    if (claimSurfaces.some((surface) => testPattern(pattern, surface))) errors.push(withEvidence(`Article contains unsupported positive claim: ${pattern}`, artifact, "adapter.scan-truth.conformance.v1", findLineSpan(source, pattern, ["raw", "joined", "tight"])));
  }
  if (claimSurfaces.some((surface) => hasAffirmativeProofClaim(surface))) errors.push(withEvidence("Article contains unsupported positive proof claim", artifact, "adapter.scan-truth.conformance.v1", findLineSpan(source, genericForbiddenProofPattern, ["raw", "joined", "tight"])));
  for (const pattern of tightForbiddenClaims) {
    if (claimSurfaces.some((surface) => testPattern(pattern, surface))) errors.push(withEvidence(`Article contains unsupported positive claim: ${pattern}`, artifact, "adapter.scan-truth.conformance.v1", findLineSpan(source, pattern, ["tight"])));
  }
  for (const pattern of rawMaterialPatterns) {
    if (surfaces.some((surface) => testPattern(pattern, surface))) errors.push(withEvidence(`Article contains raw or executable material: ${pattern}`, artifact, "adapter.scan-truth.conformance.v1", findLineSpan(source, pattern, ["raw", "joined", "tight"])));
  }
  for (const pattern of tightRawMaterialPatterns) {
    if (tightSurface && testPattern(pattern, tightSurface)) errors.push(withEvidence(`Article contains raw or executable material: ${pattern}`, artifact, "adapter.scan-truth.conformance.v1", findLineSpan(source, pattern, ["tight"])));
  }
  for (const pattern of hardPrivatePatterns) {
    if (privateSurfaces.some((surface) => testPattern(pattern, surface))) errors.push(withEvidence(`Article contains hard private material: ${pattern}`, artifact, "adapter.scan-truth.conformance.v1", findLineSpan(source, pattern, ["raw", "joined", "tight"])));
  }
  for (const pattern of privateEndpointPatterns) {
    if (privateSurfaces.some((surface) => testPattern(pattern, surface))) errors.push(withEvidence(`Article contains private endpoint URL: ${pattern}`, artifact, "adapter.scan-truth.conformance.v1", findLineSpan(source, pattern, ["raw", "joined", "tight"])));
  }
}

function decodeBrowserEntities(value) {
  return String(value)
    .replace(/&#x([0-9a-f]+);?/gi, (match, hex) => codePointText(Number.parseInt(hex, 16), match))
    .replace(/&#([0-9]+);?/gi, (match, digits) => codePointText(Number.parseInt(digits, 10), match))
    .replace(/&([a-z][a-z0-9]+);?/gi, (match, name) => browserNamedEntities[name.toLowerCase()] ?? match);
}

function hasAffirmativeProofClaim(value) {
  const text = String(value);
  const pattern = new RegExp(genericForbiddenProofPattern.source, `${genericForbiddenProofPattern.flags.replace("g", "")}g`);
  let match;
  while ((match = pattern.exec(text)) !== null) {
    const sentenceStart = Math.max(text.lastIndexOf(".", match.index - 1), text.lastIndexOf("!", match.index - 1), text.lastIndexOf("?", match.index - 1), text.lastIndexOf("\n", match.index - 1)) + 1;
    const prefix = text.slice(sentenceStart, match.index);
    if (
      !/\b(?:does|do|did)\s+not\s*$/i.test(prefix) &&
      !/\b(?:never|cannot|can't|without)\s*$/i.test(prefix) &&
      !/\bno\s+(?:claim|statement|evidence|proof)\b/i.test(prefix)
    ) return true;
  }
  return false;
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
  const tail = html.slice(anchor.end + 1);
  const close = tail.match(/<\/a\s*>/i);
  return close ? html.slice(anchor.start, anchor.end + 1 + close.index + close[0].length) : html.slice(anchor.start);
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
      if (modes.some((mode) => testPattern(pattern, surfaceForMode(window, mode)))) return { start_line: start + 1, end_line: end + 1 };
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

function withEvidence(message, artifact, ruleId = "adapter.scan-truth.conformance.v1", lineSpan = { start_line: 1, end_line: 1 }) {
  const evidence = {
    rule_id: ruleId,
    evidence_tier: EvidenceTiers.Tier2Structural,
    file_path: artifact,
    line_span: lineSpan,
    commit_sha: validationCommitSha,
    extractor_version: beforeAddingAnotherLanguageDefineScanExtractorVersion
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

const validationCommitSha = resolveValidationCommitSha();

function resolveValidationCommitSha() {
  const environmentSha = process.env.GITHUB_SHA ?? process.env.COMMIT_SHA ?? "";
  if (/^[0-9a-f]{40}$/i.test(environmentSha)) return environmentSha;
  try {
    const repositorySha = execFileSync("git", ["rev-parse", "HEAD"], { cwd: process.cwd(), encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }).trim();
    if (/^[0-9a-f]{40}$/i.test(repositorySha)) return repositorySha;
  } catch {
    // The explicit error below keeps validation provenance fail-closed.
  }
  throw new Error("Language-scan article validation requires a full 40-character commit SHA from GITHUB_SHA, COMMIT_SHA, or git HEAD.");
}
