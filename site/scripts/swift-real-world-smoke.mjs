import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import {
  decodeHtmlEntities,
  escapeRegExp,
  fileExists,
  normalizeBaseUrl,
  normalizeRenderedText,
  readSitemapLocSet
} from "./validate-utils.mjs";

export const swiftRealWorldSmokeRoute = "/swift/real-world-smoke/";
export const swiftRealWorldSmokeRequiredLinks = [
  "/swift/",
  "/swift/story/",
  "/swift/surface-discovery/",
  "/swift/evidence-safety/",
  "/validation/",
  "/limitations/",
  "/limitations/reduced-coverage/",
  "/site-claim-guardrails/"
];

const pageArtifact = "swift/real-world-smoke/index.html";
const routesIndexArtifact = "routes-index.json";
const sitemapArtifact = "sitemap.xml";

const sampleRows = new Map([
  ["icecubesapp", "9c05a720597b3ff13de2e241bf58d3fba0863c09"],
  ["mastodon-ios", "95ac4a6d726ebf9fa867036dbf9d72f0a4b5f534"],
  ["kickstarter-ios", "203971bdf40f3a3a5071ce0c1fbc4eb3cad5b094"]
]);

const requiredText = [
  "Public claim level: shipped",
  "Swift real-world API-client smoke",
  "scripts/smoke-swift-real-world.sh",
  "pinned public Swift app samples",
  "sanitized evidence summaries",
  "artifact generation",
  "static evidence extraction",
  "Dimillian/IceCubesApp",
  "mastodon/mastodon-ios",
  "kickstarter/ios-oss",
  "does not run Swift apps",
  "runtime API correctness",
  "complete Swift semantic analysis",
  "TraceMap core scanning and reduction do not use AI impact analysis, LLM analysis, prompt-based classification, embeddings, or vector database analysis"
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
  /\bsk-[A-Za-z0-9_-]{12,}\b/i
];

const forbiddenPositiveClaims = [
  /\bTraceMap\b[^.]{0,140}\b(?:proves|guarantees|certifies|approves|validates)\b[^.]{0,140}\b(?:runtime|API correctness|build|navigation|production|release|stored values?|query execution|live schema|network reachability|backend compatibility|package compatibility|complete Swift semantic analysis)\b/i,
  /\bSwift\b[^.]{0,140}\b(?:proves|guarantees|certifies|approves|validates)\b[^.]{0,140}\b(?:runtime|API correctness|build|navigation|production|release|stored values?|query execution|live schema|network reachability|backend compatibility|package compatibility|complete Swift semantic analysis)\b/i,
  /\b(?:safe to release|ready to release|approved to merge|complete Swift analysis)\b/i,
  /\bTraceMap\b[^.]{0,140}\b(?:uses|performs|provides|runs|adds)\b[^.]{0,140}\b(?:AI impact analysis|LLM analysis|prompt-based classification|embedding search|vector database analysis)\b/i
];

export async function validateSwiftRealWorldSmokeDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl, localErrors);
  const pagePath = resolve(dist, "swift", "real-world-smoke", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence(`Swift real-world smoke page is missing required public route: ${swiftRealWorldSmokeRoute}`, pageArtifact));
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
  if (!sitemapUrls.has(`${baseUrl}${swiftRealWorldSmokeRoute}`)) {
    errors.push(withEvidence(`Swift real-world smoke sitemap is missing required route: ${baseUrl}${swiftRealWorldSmokeRoute}`, sitemapArtifact));
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
    errors.push(withEvidence(`Swift real-world smoke could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return;
  }

  if (!Array.isArray(parsed?.entries)) {
    errors.push(withEvidence("Swift real-world smoke routes-index.json is invalid: expected entries array.", routesIndexArtifact));
    return;
  }

  const routeEntry = parsed.entries.find((entry) => entry?.path === swiftRealWorldSmokeRoute);
  if (!routeEntry) {
    errors.push(withEvidence(`Swift real-world smoke routes-index.json is missing required route: ${swiftRealWorldSmokeRoute}`, routesIndexArtifact));
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
      errors.push(withEvidence(`Swift real-world smoke routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
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
  const routePolicyText = decodeHtmlEntities(metadataText);

  for (const phrase of ["runtime behavior", "API correctness", "complete Swift semantic analysis", "AI impact analysis", "raw generated artifacts"]) {
    if (!metadataText.toLowerCase().includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Swift real-world smoke route metadata is missing boundary phrase: ${phrase}`, routesIndexArtifact));
    }
  }

  scanPolicyText({ errors, label: "route metadata", text: routePolicyText, artifact: routesIndexArtifact });
}

async function validatePage({ pagePath, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const pageText = normalizeRenderedText(html);
  const policyText = `${decodedHtml} ${pageText}`;

  if (!/<title>Swift Real-World Smoke Proof \| TraceMap<\/title>/i.test(html)) {
    errors.push(withEvidence("Swift real-world smoke page is missing expected title.", pageArtifact));
  }

  if (!/\bog:type["']\s+content=["']article["']/i.test(html) && !/\bog:type["']\s+content\s*=\s*["']article["']/i.test(html)) {
    errors.push(withEvidence("Swift real-world smoke page must use article metadata.", pageArtifact));
  }

  if (!/\bdata-swift-real-world-smoke\b/i.test(html)) {
    errors.push(withEvidence("Swift real-world smoke page is missing the page marker.", pageArtifact));
  }

  for (const phrase of requiredText) {
    if (!pageText.toLowerCase().includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Swift real-world smoke page is missing required text: ${phrase}`, pageArtifact));
    }
  }

  for (const link of swiftRealWorldSmokeRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Swift real-world smoke page is missing required link: ${link}`, pageArtifact));
    }
  }

  if (!hasHref(html, "https://github.com/joefeser/tracemap/blob/main/docs/VALIDATION.md#swift-real-world-api-client-smoke")) {
    errors.push(withEvidence("Swift real-world smoke page is missing validation guide proof link.", pageArtifact));
  }

  if (!/\bdata-swift-real-world-samples\b/i.test(html)) {
    errors.push(withEvidence("Swift real-world smoke page is missing the sample table marker.", pageArtifact));
  }

  for (const [sample, sha] of sampleRows) {
    const rowMatch = html.match(new RegExp(`<tr\\b[^>]*data-swift-smoke-sample=["']${escapeRegExp(sample)}["'][^>]*>`, "i"));
    if (!rowMatch) {
      errors.push(withEvidence(`Swift real-world smoke page is missing sample row: ${sample}`, pageArtifact));
      continue;
    }

    const rowHtml = sliceRowHtml(html, rowMatch.index ?? 0);
    const rowText = normalizeRenderedText(rowHtml);
    const cellCount = (rowHtml.match(/<td\b/gi) ?? []).length;

    if (cellCount !== 4) {
      errors.push(withEvidence(`Swift real-world smoke sample row ${sample} has ${cellCount} data cells; expected 4.`, pageArtifact));
    }

    if (!rowText.includes(sha)) {
      errors.push(withEvidence(`Swift real-world smoke sample row ${sample} is missing pinned SHA ${sha}.`, pageArtifact));
    }
  }

  scanPolicyText({ errors, label: "page", text: policyText, artifact: pageArtifact });
}

function scanPolicyText({ errors, label, text, artifact }) {
  for (const pattern of hardPrivatePatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Swift real-world smoke ${label} contains forbidden private material: ${redactPattern(pattern)}`, artifact));
    }
  }

  for (const pattern of forbiddenPositiveClaims) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Swift real-world smoke ${label} contains unsupported Swift claim wording: ${pattern}`, artifact));
    }
  }
}

function sliceRowHtml(html, start) {
  const end = html.indexOf("</tr>", start);
  return end === -1 ? html.slice(start) : html.slice(start, end + "</tr>".length);
}

function hasHref(html, href) {
  return new RegExp(`<a\\b(?=[^>]*\\bhref\\s*=\\s*["']${escapeRegExp(href)}["'])[^>]*>`, "i").test(html);
}

function redactPattern(pattern) {
  return `redacted ${pattern.source.slice(0, 24)}...`;
}

function withEvidence(message, artifact) {
  return `${message} Evidence: ${artifact}.`;
}

