import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import {
  decodeHtmlEntities,
  escapeRegExp,
  fileExists,
  normalizeRenderedText,
  readSitemapLocSet
} from "./validate-utils.mjs";

export const swiftApiClientWalkthroughRoute = "/swift/api-client-walkthrough/";
export const swiftApiClientWalkthroughRequiredLinks = [
  "/swift/",
  "/swift/story/",
  "/swift/surface-discovery/",
  "/swift/real-world-smoke/",
  "/swift/evidence-safety/",
  "/swift/claim-language/",
  "/validation/",
  "/limitations/",
  "/limitations/reduced-coverage/",
  "/site-claim-guardrails/"
];

const pageArtifact = "swift/api-client-walkthrough/index.html";
const routesIndexArtifact = "routes-index.json";
const sitemapArtifact = "sitemap.xml";

const evidenceShapes = new Map([
  ["urlsession-literal", "swift.http.urlsession.v1"],
  ["urlrequest-method", "swift.http.urlsession.v1"],
  ["alamofire-request", "swift.http.client-library.v1"],
  ["moya-target", "swift.http.client-library.v1"]
]);

const requiredText = [
  "Public claim level: demo",
  "Swift API-client evidence walkthrough",
  "URLSession",
  "URLRequest",
  "Alamofire",
  "Moya",
  "static candidates",
  "rule-backed static candidates",
  "rule ID",
  "evidence tier",
  "coverage label",
  "limitations",
  "non-claims",
  "Tier3SyntaxOrTextual",
  "swift.http.urlsession.v1",
  "swift.http.client-library.v1",
  "endpoint reachability",
  "backend compatibility",
  "request success",
  "auth flow",
  "production traffic",
  "runtime behavior",
  "TraceMap core scanning and reduction do not use AI impact analysis, LLM analysis, prompt-based classification, embeddings, or vector database analysis"
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
  /\bTraceMap\b[^.]{0,140}\b(?:proves|guarantees|certifies|approves|validates)\b[^.]{0,140}\b(?:runtime|API correctness|build|navigation|production|release|stored values?|query execution|live schema|network reachability|endpoint reachability|backend compatibility|package compatibility|complete Swift semantic analysis)\b/i,
  /\bSwift\b[^.]{0,140}\b(?:proves|guarantees|certifies|approves|validates)\b[^.]{0,140}\b(?:runtime|API correctness|build|navigation|production|release|stored values?|query execution|live schema|network reachability|endpoint reachability|backend compatibility|package compatibility|complete Swift semantic analysis)\b/i,
  /\b(?:safe to release|ready to release|approved to merge|complete Swift analysis)\b/i,
  /\bTraceMap\b[^.]{0,140}\b(?:uses|performs|provides|runs|adds)\b[^.]{0,140}\b(?:AI impact analysis|LLM analysis|prompt-based classification|embeddings?|embedding-backed search|embedding search|vector databases?|vector database analysis)\b/i
];

export async function validateSwiftApiClientWalkthroughDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl, localErrors);
  const pagePath = resolve(dist, "swift", "api-client-walkthrough", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence(`Swift API-client walkthrough page is missing required public route: ${swiftApiClientWalkthroughRoute}`, pageArtifact));
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
  if (!sitemapUrls.has(`${baseUrl}${swiftApiClientWalkthroughRoute}`)) {
    errors.push(withEvidence(`Swift API-client walkthrough sitemap is missing required route: ${baseUrl}${swiftApiClientWalkthroughRoute}`, sitemapArtifact));
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
    errors.push(withEvidence(`Swift API-client walkthrough could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return;
  }

  if (!Array.isArray(parsed?.entries)) {
    errors.push(withEvidence("Swift API-client walkthrough routes-index.json is invalid: expected entries array.", routesIndexArtifact));
    return;
  }

  const routeEntry = parsed.entries.find((entry) => entry?.path === swiftApiClientWalkthroughRoute);
  if (!routeEntry) {
    errors.push(withEvidence(`Swift API-client walkthrough routes-index.json is missing required route: ${swiftApiClientWalkthroughRoute}`, routesIndexArtifact));
    return;
  }

  const expectedFields = {
    publicClaimLevel: "demo",
    hintCategory: "evidence",
    sourceType: "site-page",
    preferredProofPath: "/swift/real-world-smoke/"
  };

  for (const [field, expected] of Object.entries(expectedFields)) {
    if (routeEntry[field] !== expected) {
      errors.push(withEvidence(`Swift API-client walkthrough routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
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

  for (const phrase of ["runtime behavior", "endpoint reachability", "backend compatibility", "request success", "auth flow", "production traffic", "AI impact analysis", "raw generated artifacts"]) {
    if (!metadataText.toLowerCase().includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Swift API-client walkthrough route metadata is missing boundary phrase: ${phrase}`, routesIndexArtifact));
    }
  }

  scanPolicyText({ errors, label: "route metadata", text: routePolicyText, artifact: routesIndexArtifact });
}

async function validatePage({ pagePath, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const pageText = normalizeRenderedText(html);
  const tightPageText = normalizeTightHtmlText(html);
  const policyText = `${decodedHtml} ${pageText} ${tightPageText}`;

  if (!/<title>Swift API-Client Evidence Walkthrough \| TraceMap<\/title>/i.test(html)) {
    errors.push(withEvidence("Swift API-client walkthrough page is missing expected title.", pageArtifact));
  }

  if (!hasTagWithAttributes(html, "meta", { property: "og:type", content: "article" })) {
    errors.push(withEvidence("Swift API-client walkthrough page must use article metadata.", pageArtifact));
  }

  if (!/\bdata-swift-api-client-walkthrough\b/i.test(html)) {
    errors.push(withEvidence("Swift API-client walkthrough page is missing the page marker.", pageArtifact));
  }

  for (const phrase of requiredText) {
    if (!pageText.toLowerCase().includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Swift API-client walkthrough page is missing required text: ${phrase}`, pageArtifact));
    }
  }

  for (const link of swiftApiClientWalkthroughRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Swift API-client walkthrough page is missing required link: ${link}`, pageArtifact));
    }
  }

  if (!hasHref(html, "https://github.com/joefeser/tracemap/blob/main/docs/VALIDATION.md#swift-real-world-api-client-smoke")) {
    errors.push(withEvidence("Swift API-client walkthrough page is missing validation guide proof link.", pageArtifact));
  }

  if (!/\bdata-swift-api-client-shapes\b/i.test(html)) {
    errors.push(withEvidence("Swift API-client walkthrough page is missing the API-client evidence shape table marker.", pageArtifact));
  }

  for (const [shape, ruleId] of evidenceShapes) {
    const rowMatch = findTagWithAttribute(html, "tr", "data-swift-api-client-shape", shape);
    if (!rowMatch) {
      errors.push(withEvidence(`Swift API-client walkthrough page is missing evidence shape row: ${shape}`, pageArtifact));
      continue;
    }

    const rowHtml = sliceRowHtml(html, rowMatch.index ?? 0);
    const rowText = normalizeRenderedText(rowHtml);
    const cellCount = (rowHtml.match(/<td\b/gi) ?? []).length;

    if (cellCount !== 6) {
      errors.push(withEvidence(`Swift API-client walkthrough evidence shape row ${shape} has ${cellCount} data cells; expected 6.`, pageArtifact));
    }

    if (!rowText.includes(ruleId)) {
      errors.push(withEvidence(`Swift API-client walkthrough evidence shape row ${shape} is missing rule ID ${ruleId}.`, pageArtifact));
    }

    if (!rowText.includes("Tier3SyntaxOrTextual")) {
      errors.push(withEvidence(`Swift API-client walkthrough evidence shape row ${shape} is missing Tier3SyntaxOrTextual.`, pageArtifact));
    }

    if (!rowText.includes("CoverageRelative") || !rowText.includes("ReducedCoverage")) {
      errors.push(withEvidence(`Swift API-client walkthrough evidence shape row ${shape} is missing coverage labels.`, pageArtifact));
    }
  }

  scanPolicyText({ errors, label: "page", text: policyText, artifact: pageArtifact });
}

function scanPolicyText({ errors, label, text, artifact }) {
  for (const pattern of hardPrivatePatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Swift API-client walkthrough ${label} contains forbidden private material: ${redactPattern(pattern)}`, artifact));
    }
  }

  for (const pattern of rawArtifactPatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Swift API-client walkthrough ${label} contains forbidden raw artifact name: ${redactPattern(pattern)}`, artifact));
    }
  }

  for (const pattern of forbiddenPositiveClaims) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Swift API-client walkthrough ${label} contains unsupported Swift claim wording: ${pattern}`, artifact));
    }
  }
}

function normalizeBaseUrl(value, errors) {
  let url;
  try {
    url = new URL(String(value));
  } catch {
    errors.push(withEvidence(`Swift API-client walkthrough baseUrl must be a valid absolute URL: ${String(value)}`, "baseUrl input"));
    return null;
  }

  if (url.protocol !== "https:" && url.protocol !== "http:") {
    errors.push(withEvidence(`Swift API-client walkthrough baseUrl must use http or https: ${String(value)}`, "baseUrl input"));
    return null;
  }

  return url.origin;
}

function sliceRowHtml(html, start) {
  const end = html.indexOf("</tr>", start);
  return end === -1 ? html.slice(start) : html.slice(start, end + "</tr>".length);
}

function hasHref(html, href) {
  return new RegExp(`<a\\b(?=[^>]*\\bhref\\s*=\\s*["']${escapeRegExp(href)}["'])[^>]*>`, "i").test(html);
}

function hasTagWithAttributes(html, tagName, attributes) {
  return Boolean(findTagWithAttributes(html, tagName, attributes));
}

function findTagWithAttribute(html, tagName, attributeName, attributeValue) {
  return findTagWithAttributes(html, tagName, { [attributeName]: attributeValue });
}

function findTagWithAttributes(html, tagName, attributes) {
  const tagPattern = new RegExp(`<${escapeRegExp(tagName)}\\b[^>]*>`, "gi");
  let match;
  while ((match = tagPattern.exec(html)) !== null) {
    const tagHtml = match[0];
    const hasAllAttributes = Object.entries(attributes).every(([name, value]) => hasAttribute(tagHtml, name, value));
    if (hasAllAttributes) {
      return match;
    }
  }

  return null;
}

function hasAttribute(tagHtml, attributeName, attributeValue) {
  return new RegExp(`\\b${escapeRegExp(attributeName)}\\s*=\\s*["']${escapeRegExp(attributeValue)}["']`, "i").test(tagHtml);
}

function redactPattern(pattern) {
  return `redacted ${pattern.source.slice(0, 24)}...`;
}

function normalizeTightHtmlText(html) {
  return decodeHtmlEntities(String(html).replace(/<[^>]+>/g, ""))
    .replace(/\s+/g, " ")
    .trim();
}

function withEvidence(message, artifact) {
  return `${message} Evidence: ${artifact}.`;
}
