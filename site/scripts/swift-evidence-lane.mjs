import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import {
  decodeHtmlEntities,
  escapeRegExp,
  fileExists,
  normalizeRenderedText,
  readSitemapLocSet
} from "./validate-utils.mjs";

export const swiftEvidenceLaneRoute = "/swift/";
export const swiftEvidenceLaneRequiredLinks = [
  "/capabilities/",
  "/roadmap/",
  "/site-claim-guardrails/",
  "/limitations/",
  "/limitations/reduced-coverage/",
  "/proof-paths/",
  "/proof-source-catalog/",
  "/validation/"
];

const pageArtifact = "swift/index.html";
const routesIndexArtifact = "routes-index.json";
const sitemapArtifact = "sitemap.xml";

const expectedRows = new Map([
  ["v0-evidence-lane", "shipped"],
  ["static-inventory", "shipped"],
  ["symbol-call-evidence", "shipped"],
  ["surface-discovery", "shipped/demo"],
  ["storage-data-surfaces", "shipped/demo"],
  ["evidence-safety", "shipped"]
]);

const requiredText = [
  "Public claim level: shipped",
  "PR #425",
  "e8813daaf763e277e7c5d88a2c0b2ad0b570f25a",
  "Swift v0 is a shipped static evidence lane",
  "Static inventory",
  "Swift symbols and calls",
  "Swift surface discovery",
  "Swift storage and data surfaces",
  "Swift evidence safety",
  "Rule IDs",
  "evidence tiers",
  "coverage labels",
  "reduced-coverage gaps",
  "Tier2Structural",
  "Tier3SyntaxOrTextual",
  "Tier4Unknown",
  "ReducedCoverage",
  "TraceMap does not run Swift apps, simulators, devices, Xcode builds, SwiftPM restores, production traffic, or runtime persistence",
  "TraceMap does not perform AI impact analysis, LLM analysis, prompt-based classification, embeddings, or vector database analysis"
];

const hardPrivatePatterns = [
  /\/Users\//i,
  /\/private\//i,
  /\/home\//i,
  /~\//,
  /\bC:\\/i,
  /\bfile:\/\//i,
  /\blocalhost\b/i,
  /\b127\.0\.0\.1\b/i,
  /\bgit@/i,
  /\bConnectionString\b/i,
  /\bconnection string\b/i,
  /\bServer\s*=/i,
  /\bUser Id\s*=/i,
  /\bPassword\s*=/i,
  /\bapi[_-]?key\b/i,
  /\bsecret\s*=/i,
  /\bsk-[A-Za-z0-9_-]{12,}\b/i,
  /\bfacts\.ndjson\b/i,
  /\bindex\.sqlite\b/i,
  /\blogs\/analyzer\.log\b/i,
  /\banalyzer\.log\b/i
];

const forbiddenClaims = [
  /\bTraceMap\b[^.]{0,100}\b(?:proves|guarantees|certifies|approves|validates)\b[^.]{0,100}\b(?:runtime|build|navigation|production|release|stored values?|query execution|live schema|network reachability)\b/i,
  /\bSwift v0\b[^.]{0,100}\b(?:proves|guarantees|certifies|approves|validates)\b[^.]{0,100}\b(?:runtime|build|navigation|production|release|stored values?|query execution|live schema|network reachability)\b/i,
  /\b(?:safe to release|ready to release|approved to merge|complete Swift analysis)\b/i,
  /\bTraceMap\b[^.]{0,100}\b(?:uses|performs|provides|runs|adds)\b[^.]{0,100}\b(?:LLM analysis|prompt-based classification|embedding search|vector database analysis)\b/i
];

export async function validateSwiftEvidenceLaneDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl, localErrors);
  const pagePath = resolve(dist, "swift", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence(`Swift evidence lane page is missing required public route: ${swiftEvidenceLaneRoute}`, pageArtifact));
    errors.push(...localErrors);
    return;
  }

  if (cleanBaseUrl) {
    await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  }
  await validateRoutesIndex({ dist, errors: localErrors });
  await validatePage({ pagePath, errors: localErrors });

  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${swiftEvidenceLaneRoute}`)) {
    errors.push(withEvidence(`Swift evidence lane sitemap is missing required route: ${baseUrl}${swiftEvidenceLaneRoute}`, sitemapArtifact));
  }
}

async function validateRoutesIndex({ dist, errors }) {
  const indexPath = resolve(dist, "routes-index.json");
  if (!(await fileExists(indexPath))) {
    return;
  }

  let parsed;
  try {
    parsed = JSON.parse(await readFile(indexPath, "utf8"));
  } catch (error) {
    errors.push(withEvidence(`Swift evidence lane could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return;
  }

  if (!Array.isArray(parsed?.entries)) {
    errors.push(withEvidence("Swift evidence lane routes-index.json is invalid: expected entries array.", routesIndexArtifact));
    return;
  }

  const routeEntry = parsed.entries.find((entry) => entry?.path === swiftEvidenceLaneRoute);
  if (!routeEntry) {
    errors.push(withEvidence(`Swift evidence lane routes-index.json is missing required route: ${swiftEvidenceLaneRoute}`, routesIndexArtifact));
    return;
  }

  const expectedFields = {
    publicClaimLevel: "shipped",
    hintCategory: "evidence",
    sourceType: "site-page",
    preferredProofPath: "/validation/"
  };

  for (const [field, expected] of Object.entries(expectedFields)) {
    if (routeEntry[field] !== expected) {
      errors.push(withEvidence(`Swift evidence lane routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
    }
  }

  const metadataText = normalizeRenderedText(
    [
      routeEntry.title,
      routeEntry.summary,
      ...(Array.isArray(routeEntry.limitations) ? routeEntry.limitations : []),
      ...(Array.isArray(routeEntry.nonClaims) ? routeEntry.nonClaims : [])
    ].join(" ")
  );

  for (const phrase of ["runtime behavior", "build success", "AI impact analysis", "stored-value proof"]) {
    if (!metadataText.toLowerCase().includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Swift evidence lane route metadata is missing boundary phrase: ${phrase}`, routesIndexArtifact));
    }
  }
}

async function validatePage({ pagePath, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const pageText = normalizeRenderedText(html);
  const policyText = `${decodedHtml} ${pageText}`;

  if (!/<title>Swift Evidence Lane \| TraceMap<\/title>/i.test(html)) {
    errors.push(withEvidence("Swift evidence lane page is missing expected title.", pageArtifact));
  }

  for (const phrase of requiredText) {
    if (!pageText.toLowerCase().includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Swift evidence lane page is missing required text: ${phrase}`, pageArtifact));
    }
  }

  for (const link of swiftEvidenceLaneRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Swift evidence lane page is missing required link: ${link}`, pageArtifact));
    }
  }

  if (!/\bdata-swift-evidence-lane\b/i.test(html)) {
    errors.push(withEvidence("Swift evidence lane page is missing the table marker.", pageArtifact));
  }

  for (const [story, expectedLevel] of expectedRows) {
    const rowMatch = html.match(new RegExp(`<tr\\b[^>]*data-swift-story=["']${escapeRegExp(story)}["'][^>]*>`, "i"));
    if (!rowMatch) {
      errors.push(withEvidence(`Swift evidence lane page is missing story row: ${story}`, pageArtifact));
      continue;
    }

    const tag = rowMatch[0];
    const actualLevel = getAttribute(tag, "data-public-claim-level");
    const rowHtml = sliceRowHtml(html, rowMatch.index ?? 0);
    const rowText = normalizeRenderedText(rowHtml);
    const cellCount = (rowHtml.match(/<td\b/gi) ?? []).length;

    if (actualLevel !== expectedLevel) {
      errors.push(withEvidence(`Swift evidence lane row ${story} has public claim level ${String(actualLevel)}; expected ${expectedLevel}.`, pageArtifact));
    }

    if (cellCount !== 5) {
      errors.push(withEvidence(`Swift evidence lane row ${story} has ${cellCount} data cells; expected 5 matrix fields.`, pageArtifact));
    }

    if (!/PR\s*#425/i.test(rowText)) {
      errors.push(withEvidence(`Swift evidence lane row ${story} is missing PR #425 proof anchor.`, pageArtifact));
    }

    if (!/\b(?:does not|not |no |without|limitation|limited)\b/i.test(rowText)) {
      errors.push(withEvidence(`Swift evidence lane row ${story} is missing adjacent limitation text.`, pageArtifact));
    }
  }

  for (const pattern of hardPrivatePatterns) {
    if (pattern.test(policyText)) {
      errors.push(withEvidence(`Swift evidence lane page contains forbidden private or raw artifact text: ${redactPattern(pattern)}`, pageArtifact));
    }
  }

  for (const pattern of forbiddenClaims) {
    if (pattern.test(policyText)) {
      errors.push(withEvidence(`Swift evidence lane page contains unsupported Swift claim wording: ${pattern}`, pageArtifact));
    }
  }
}

function hasHref(html, href) {
  return new RegExp(`<a\\b(?=[^>]*\\bhref\\s*=\\s*["']${escapeRegExp(href)}["'])[^>]*>`, "i").test(html);
}

function normalizeBaseUrl(value, errors) {
  let url;
  try {
    url = new URL(String(value));
  } catch {
    errors.push(withEvidence(`Swift evidence lane baseUrl must be a valid absolute URL: ${String(value)}`, "baseUrl input"));
    return null;
  }

  if (url.protocol !== "https:" && url.protocol !== "http:") {
    errors.push(withEvidence(`Swift evidence lane baseUrl must use http or https: ${String(value)}`, "baseUrl input"));
    return null;
  }

  return url.origin;
}

function getAttribute(tag, name) {
  const match = tag.match(new RegExp(`\\b${name}=["']([^"']*)["']`, "i"));
  return match ? decodeHtmlEntities(match[1]) : null;
}

function sliceRowHtml(html, rowStart) {
  const rest = html.slice(rowStart);
  const end = rest.search(/<\/tr\s*>/i);
  if (end === -1) {
    return rest;
  }

  const close = rest.slice(end).match(/^<\/tr\s*>/i);
  return rest.slice(0, end + close[0].length);
}

function redactPattern(pattern) {
  return `redacted ${pattern.source.slice(0, 24)}...`;
}

function withEvidence(message, artifact) {
  return `${message} Evidence: ${artifact}.`;
}
