import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import {
  decodeHtmlEntities,
  escapeRegExp,
  fileExists,
  normalizeRenderedText,
  readSitemapLocSet
} from "./validate-utils.mjs";

export const swiftClaimLanguageRoute = "/swift/claim-language/";
export const swiftClaimLanguageRequiredLinks = [
  "/swift/",
  "/swift/story/",
  "/swift/real-world-smoke/",
  "/swift/static-inventory/",
  "/swift/symbols-calls/",
  "/swift/surface-discovery/",
  "/swift/storage-data/",
  "/swift/evidence-safety/",
  "/site-claim-guardrails/",
  "/review-claim-checklist/",
  "/proof-source-catalog/",
  "/limitations/",
  "/limitations/reduced-coverage/",
  "/validation/",
  "https://github.com/joefeser/tracemap/pull/425"
];

const pageArtifact = "swift/claim-language/index.html";
const routesIndexArtifact = "routes-index.json";
const sitemapArtifact = "sitemap.xml";

const requiredText = [
  "Public claim level: shipped",
  "Swift v0 is shipped as static evidence discovery",
  "proof paths",
  "rule-backed evidence",
  "coverage labels",
  "limitations",
  "non-claims",
  "claim level",
  "evidence tier",
  "TraceMap inventories and reports static Swift evidence where rules support it",
  "runtime API correctness",
  "complete Swift semantic analysis",
  "AI impact analysis",
  "raw generated artifacts"
];

const requiredChecks = new Map([
  ["claim-level", "Public claim level"],
  ["proof-path", "Proof path"],
  ["evidence-tier", "Evidence tier"],
  ["coverage-label", "Coverage label"],
  ["non-claim", "Non-claim boundary"]
]);

const requiredExamples = new Set(["safe-shipped", "safe-demo", "safe-gap"]);

const hardPrivatePatterns = [
  /\/Users\//i,
  /\/private\//i,
  /\/home\//i,
  /\/tmp\//i,
  /\/var\/folders\//i,
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

const rawArtifactPatterns = [
  /\bscan-manifest\.json\b/i,
  /\bfacts\.ndjson\b/i,
  /\bindex\.sqlite\b/i,
  /\breport\.md\b/i,
  /\blogs\/analyzer\.log\b/i,
  /\banalyzer\.log\b/i
];

const forbiddenPositiveClaims = [
  /\bTraceMap\b[^.]{0,140}\b(?:proves|guarantees|certifies|approves|validates)\b[^.]{0,140}\b(?:runtime|API correctness|build|navigation|production|release|stored values?|query execution|live schema|network reachability|backend compatibility|package compatibility|complete Swift semantic analysis)\b/i,
  /\bSwift\b[^.]{0,140}\b(?:proves|guarantees|certifies|approves|validates)\b[^.]{0,140}\b(?:runtime|API correctness|build|navigation|production|release|stored values?|query execution|live schema|network reachability|backend compatibility|package compatibility|complete Swift semantic analysis)\b/i,
  /\b(?:safe to release|ready to release|approved to merge|complete Swift analysis)\b/i,
  /\bTraceMap\b[^.]{0,140}\b(?:uses|performs|provides|runs|adds)\b[^.]{0,140}\b(?:AI impact analysis|LLM analysis|prompt-based classification|embedding search|vector database analysis)\b/i
];

export async function validateSwiftClaimLanguageDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl, localErrors);
  const pagePath = resolve(dist, "swift", "claim-language", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence(`Swift claim-language checklist page is missing required public route: ${swiftClaimLanguageRoute}`, pageArtifact));
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
  if (!sitemapUrls.has(`${baseUrl}${swiftClaimLanguageRoute}`)) {
    errors.push(withEvidence(`Swift claim-language checklist sitemap is missing required route: ${baseUrl}${swiftClaimLanguageRoute}`, sitemapArtifact));
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
    errors.push(withEvidence(`Swift claim-language checklist could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return;
  }

  if (!Array.isArray(parsed?.entries)) {
    errors.push(withEvidence("Swift claim-language checklist routes-index.json is invalid: expected entries array.", routesIndexArtifact));
    return;
  }

  const routeEntry = parsed.entries.find((entry) => entry?.path === swiftClaimLanguageRoute);
  if (!routeEntry) {
    errors.push(withEvidence(`Swift claim-language checklist routes-index.json is missing required route: ${swiftClaimLanguageRoute}`, routesIndexArtifact));
    return;
  }

  const expectedFields = {
    publicClaimLevel: "shipped",
    hintCategory: "evidence",
    sourceType: "site-page",
    preferredProofPath: "/swift/"
  };

  for (const [field, expected] of Object.entries(expectedFields)) {
    if (routeEntry[field] !== expected) {
      errors.push(withEvidence(`Swift claim-language checklist routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
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
      errors.push(withEvidence(`Swift claim-language checklist route metadata is missing boundary phrase: ${phrase}`, routesIndexArtifact));
    }
  }

  scanPolicyText({ errors, label: "route metadata", text: routePolicyText, artifact: routesIndexArtifact });
}

async function validatePage({ pagePath, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const pageText = normalizeRenderedText(html);
  const policyText = `${decodedHtml} ${pageText}`;

  if (!/<title>Swift Claim Language Checklist \| TraceMap<\/title>/i.test(html)) {
    errors.push(withEvidence("Swift claim-language checklist page is missing expected title.", pageArtifact));
  }

  if (!/\bog:type["']\s+content=["']article["']/i.test(html) && !/\bog:type["']\s+content\s*=\s*["']article["']/i.test(html)) {
    errors.push(withEvidence("Swift claim-language checklist page must use article metadata.", pageArtifact));
  }

  if (!/\bdata-swift-claim-language\b/i.test(html)) {
    errors.push(withEvidence("Swift claim-language checklist page is missing the page marker.", pageArtifact));
  }

  for (const phrase of requiredText) {
    if (!pageText.toLowerCase().includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Swift claim-language checklist page is missing required text: ${phrase}`, pageArtifact));
    }
  }

  for (const link of swiftClaimLanguageRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Swift claim-language checklist page is missing required link: ${link}`, pageArtifact));
    }
  }

  if (!/\bdata-swift-claim-checklist\b/i.test(html)) {
    errors.push(withEvidence("Swift claim-language checklist page is missing the checklist table marker.", pageArtifact));
  }

  for (const [check, label] of requiredChecks) {
    const rowMatch = html.match(new RegExp(`<tr\\b[^>]*data-swift-claim-check=["']${escapeRegExp(check)}["'][^>]*>`, "i"));
    if (!rowMatch) {
      errors.push(withEvidence(`Swift claim-language checklist page is missing required check row: ${check}`, pageArtifact));
      continue;
    }

    const rowHtml = sliceRowHtml(html, rowMatch.index ?? 0);
    const rowText = normalizeRenderedText(rowHtml);
    const cellCount = (rowHtml.match(/<td\b/gi) ?? []).length;

    if (cellCount !== 4) {
      errors.push(withEvidence(`Swift claim-language checklist row ${check} has ${cellCount} data cells; expected 4 checklist fields.`, pageArtifact));
    }

    if (!rowText.toLowerCase().includes(label.toLowerCase())) {
      errors.push(withEvidence(`Swift claim-language checklist row ${check} is missing label: ${label}`, pageArtifact));
    }
  }

  for (const example of requiredExamples) {
    if (!new RegExp(`\\bdata-swift-claim-example=["']${escapeRegExp(example)}["']`, "i").test(html)) {
      errors.push(withEvidence(`Swift claim-language checklist page is missing safe example: ${example}`, pageArtifact));
    }
  }

  scanPolicyText({ errors, label: "page", text: policyText, artifact: pageArtifact });
}

function scanPolicyText({ errors, label, text, artifact }) {
  for (const pattern of hardPrivatePatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Swift claim-language checklist ${label} contains forbidden private material: ${redactPattern(pattern)}`, artifact));
    }
  }

  for (const pattern of rawArtifactPatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Swift claim-language checklist ${label} contains forbidden raw artifact name: ${redactPattern(pattern)}`, artifact));
    }
  }

  for (const pattern of forbiddenPositiveClaims) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Swift claim-language checklist ${label} contains unsupported Swift claim wording: ${pattern}`, artifact));
    }
  }
}

function hasHref(html, href) {
  return new RegExp(`<a\\b[^>]*href\\s*=\\s*["']${escapeRegExp(href)}["'][^>]*>`, "i").test(html);
}

function normalizeBaseUrl(value, errors) {
  try {
    const url = new URL(String(value));
    if (url.protocol !== "https:" && url.protocol !== "http:") {
      throw new Error("expected http or https URL");
    }
    return url.href.replace(/\/+$/, "");
  } catch {
    errors.push(withEvidence(`Swift claim-language checklist baseUrl must be a valid absolute URL: ${String(value)}`, sitemapArtifact));
    return null;
  }
}

function sliceRowHtml(html, startIndex) {
  const endIndex = html.indexOf("</tr>", startIndex);
  return endIndex === -1 ? html.slice(startIndex) : html.slice(startIndex, endIndex + "</tr>".length);
}

function redactPattern(pattern) {
  return String(pattern).replace(/[A-Za-z0-9_-]{8,}/g, "[redacted]");
}

function withEvidence(message, artifact) {
  return `${message} Evidence: ${artifact}.`;
}
