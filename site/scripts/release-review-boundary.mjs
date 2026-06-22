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

export const releaseReviewBoundaryRoute = "/release-review-boundary/";
export const releaseReviewBoundaryRequiredLinks = [
  "/limitations/",
  "/static-vs-runtime/",
  "/review-claim-checklist/",
  "/deploy-audit/",
  "/validation/",
  "/manager-packet/",
  "/questions/objections/",
  "/review-room/"
];
export const releaseReviewBoundaryInboundRoutes = ["/review-room/", "/use-cases/change-review/"];

const pageArtifact = "release-review-boundary/index.html";
const sitemapArtifact = "sitemap.xml";
const routesIndexArtifact = "routes-index.json";

const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "What static evidence can contribute",
  "What release review still owns",
  "Forbidden claims",
  "Safe wording",
  "Stop conditions",
  "Required next owners",
  "Non-claims"
];

const requiredRows = [
  "changed source surface",
  "package/config surface",
  "route/endpoint adjacency",
  "SQL/data surface",
  "coverage gap",
  "validation evidence",
  "runtime telemetry need",
  "release-owner decision"
];

const requiredFields = [
  "release-review question",
  "TraceMap contribution",
  "evidence needed",
  "boundary or non-claim",
  "stop condition",
  "required next owner",
  "public claim level",
  "supporting route"
];

const requiredNonClaimTerms = [
  "release approval",
  "release safety",
  "operational safety",
  "production proof",
  "runtime behavior proof",
  "endpoint performance proof",
  "deployment success proof",
  "absence-of-impact proof",
  "complete coverage",
  "AI impact analysis",
  "LLM analysis",
  "replacement of release controls",
  "human judgment"
];

const metadataNonClaimTerms = [
  "release approval",
  "release safety",
  "operational safety",
  "production proof",
  "runtime behavior proof",
  "endpoint performance proof",
  "deployment success proof",
  "absence-of-impact proof",
  "complete coverage",
  "AI impact analysis",
  "LLM analysis",
  "replacement of release controls"
];

const hardPrivatePatterns = [
  /\/Users\//i,
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

const privateBoundaryPatterns = [
  /\bfacts\.ndjson\b/i,
  /\bindex\.sqlite\b/i,
  /\blogs\/analyzer\.log\b/i,
  /\banalyzer\.log\b/i,
  /\braw source snippets?\b/i,
  /\braw SQL\b/i,
  /\bconfig values?\b/i,
  /\braw remotes?\b/i,
  /\bgenerated scan directories\b/i,
  /\bprivate sample names?\b/i,
  /\braw command output\b/i,
  /\bhidden validation details\b/i,
  /\bcredential-like values?\b/i
];

const forbiddenPositiveClaimPatterns = [
  /\bTraceMap\b[^.]{0,90}\b(?:approves?|blocks?|certifies?|guarantees?)\s+(?:a\s+)?releases?\b/i,
  /\bTraceMap\b[^.]{0,90}\b(?:proves?|provides?)\s+(?:release safety|operational safety|production proof|runtime behavior proof|endpoint performance proof|deployment success proof|absence-of-impact proof|complete coverage)\b/i,
  /\b(?:release-safe|runtime-safe|production-proven|deployment succeeded|validated for release|safe to release|automated release approval)\b/i,
  /\b(?:AI-powered|LLM-powered|AI impact analysis engine|LLM impact analysis engine|embedding-backed|vector database reasoning)\b/i,
  /\breplaces?\s+(?:release controls|human judgment|tests|code review|source review|runtime observability|service-owner review|security review)\b/i
];

const blamePatterns = [
  /\bwho broke it\b/i,
  /\bat fault\b/i,
  /\bculprit\b/i,
  /\bbad code\b/i,
  /\bbad team\b/i,
  /\bbad vendor\b/i,
  /\bguilty\b/i
];

export async function validateReleaseReviewBoundaryDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors
}) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "release-review-boundary", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence(`Release review boundary is missing required public route: ${releaseReviewBoundaryRoute}`, pageArtifact));
    errors.push(...localErrors);
    return;
  }

  await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  const routeContext = await readRouteContext({ dist, errors: localErrors });
  await validatePage({ pagePath, routeContext, errors: localErrors });
  await validateInboundLinks({ dist, errors: localErrors });

  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, sitemapArtifact);
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${releaseReviewBoundaryRoute}`)) {
    errors.push(withEvidence(`Release review boundary sitemap is missing required route: ${baseUrl}${releaseReviewBoundaryRoute}`, sitemapArtifact));
  }
}

async function readRouteContext({ dist, errors }) {
  const routesIndexPath = resolve(dist, routesIndexArtifact);
  const routes = new Set();
  let routeEntry = null;

  if (!(await fileExists(routesIndexPath))) {
    return { routeEntry, routes };
  }

  let parsed;
  try {
    parsed = JSON.parse(await readFile(routesIndexPath, "utf8"));
  } catch (error) {
    errors.push(withEvidence(`Release review boundary could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return { routeEntry, routes };
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Release review boundary routes-index.json is invalid: expected entries array", routesIndexArtifact));
    return { routeEntry, routes };
  }

  for (const entry of parsed.entries) {
    if (typeof entry?.path === "string") {
      routes.add(normalizeRoute(entry.path));
    }
  }

  routeEntry = parsed.entries.find((entry) => normalizeRoute(entry?.path ?? "") === releaseReviewBoundaryRoute) ?? null;
  validateRouteEntry(routeEntry, errors);

  return { routeEntry, routes };
}

function validateRouteEntry(routeEntry, errors) {
  if (!routeEntry) {
    errors.push(withEvidence(`Release review boundary routes-index.json is missing required route: ${releaseReviewBoundaryRoute}`, routesIndexArtifact));
    return;
  }

  const expectedFields = {
    publicClaimLevel: "concept",
    hintCategory: "use-case",
    sourceType: "site-page",
    preferredProofPath: "/proof-paths/"
  };

  for (const [field, expected] of Object.entries(expectedFields)) {
    if (routeEntry[field] !== expected) {
      errors.push(withEvidence(`Release review boundary routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
    }
  }

  if (!Array.isArray(routeEntry.limitations) || routeEntry.limitations.length === 0) {
    errors.push(withEvidence("Release review boundary routes-index.json must include limitations metadata.", routesIndexArtifact));
  }

  if (!Array.isArray(routeEntry.nonClaims) || routeEntry.nonClaims.length === 0) {
    errors.push(withEvidence("Release review boundary routes-index.json must include nonClaims metadata.", routesIndexArtifact));
    return;
  }

  const nonClaimsText = normalizeScanText(routeEntry.nonClaims.join(" "));
  for (const term of metadataNonClaimTerms) {
    if (!nonClaimsText.includes(term.toLowerCase())) {
      errors.push(withEvidence(`Release review boundary routes-index.json nonClaims are missing required term: ${term}`, routesIndexArtifact));
    }
  }

  validateForbiddenPositiveClaims(collectRouteMetadataText(routeEntry), errors, routesIndexArtifact);
}

async function validatePage({ pagePath, routeContext, errors }) {
  const html = await readFile(pagePath, "utf8");
  const mainHtml = extractMainHtml(html);
  const pageText = normalizeRenderedText(mainHtml);
  const metadataText = collectMetadataText(html);
  const attributeText = collectDecodedAttributeText(html);
  const wordCount = countWords(normalizeRenderedText(stripTableHeaderHtml(mainHtml)));

  for (const phrase of requiredText) {
    if (!pageText.includes(phrase)) {
      errors.push(withEvidence(`Release review boundary page is missing required text: ${phrase}`, pageArtifact));
    }
  }

  validateRows(html, errors);
  validateLinks(html, routeContext, errors);
  validateNonClaims(pageText, routeContext.routeEntry, errors);

  if (!hasOgTypeArticle(html)) {
    errors.push(withEvidence('Release review boundary page must include <meta property="og:type" content="article">.', pageArtifact));
  }

  if (wordCount < 900 || wordCount > 2400) {
    errors.push(withEvidence(`Release review boundary page word count must be between 900 and 2400 words, got ${wordCount}`, pageArtifact));
  }

  validateForbiddenPositiveClaims(`${stripSanctionedBoundaryHtml(mainHtml)} ${metadataText} ${attributeText}`, errors, pageArtifact);
  validateHardPrivateMaterial(`${mainHtml} ${metadataText} ${attributeText}`, errors, pageArtifact);
  validateBoundaryPrivateMaterial(`${stripSanctionedBoundaryHtml(mainHtml)} ${metadataText} ${attributeText}`, errors, pageArtifact);
  validateBlameLanguage(`${pageText} ${metadataText} ${attributeText}`, errors, pageArtifact);
}

function validateRows(html, errors) {
  const rows = extractRows(html, "data-release-boundary-row");
  const byName = new Map();

  for (const row of rows) {
    const name = getAttribute(row.attributes, "data-release-boundary-row");
    if (name) {
      byName.set(name, row.html);
    }
  }

  for (const rowName of requiredRows) {
    const rowHtml = byName.get(rowName);
    if (!rowHtml) {
      errors.push(withEvidence(`Release review boundary matrix is missing required row: ${rowName}`, pageArtifact));
      continue;
    }

    for (const field of requiredFields) {
      const fieldPattern = new RegExp(`\\bdata-field\\s*=\\s*["']${escapeRegExp(field)}["']`, "i");
      if (!fieldPattern.test(rowHtml)) {
        errors.push(withEvidence(`Release review boundary row ${rowName} is missing required field: ${field}`, pageArtifact));
      }
    }

    const text = normalizeRenderedText(rowHtml);
    if (!/\bconcept\b/i.test(text)) {
      errors.push(withEvidence(`Release review boundary row ${rowName} is missing concept claim level.`, pageArtifact));
    }
  }
}

function validateLinks(html, routeContext, errors) {
  for (const link of releaseReviewBoundaryRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Release review boundary page is missing required link: ${link}`, pageArtifact));
    }

    if (routeContext.routes.size > 0 && !routeContext.routes.has(normalizeRoute(link))) {
      errors.push(withEvidence(`Release review boundary required link is not present in discovery route index: ${link}`, routesIndexArtifact));
    }
  }
}

function validateNonClaims(pageText, routeEntry, errors) {
  const nonClaimsText = normalizeScanText(`${pageText} ${routeEntry?.nonClaims?.join(" ") ?? ""}`);
  for (const term of requiredNonClaimTerms) {
    if (!nonClaimsText.includes(term.toLowerCase())) {
      errors.push(withEvidence(`Release review boundary is missing required non-claim term: ${term}`, pageArtifact));
    }
  }
}

async function validateInboundLinks({ dist, errors }) {
  const missing = [];

  for (const route of releaseReviewBoundaryInboundRoutes) {
    const pagePath = resolve(dist, route.replace(/^\/|\/$/g, ""), "index.html");
    if (!(await fileExists(pagePath))) {
      continue;
    }

    const html = await readFile(pagePath, "utf8");
    if (!hasHref(html, releaseReviewBoundaryRoute)) {
      missing.push(route);
    }
  }

  if (missing.length > 0) {
    errors.push(withEvidence(`Release review boundary is missing inbound links from live adjacent routes: ${missing.join(", ")}`, "adjacent route HTML"));
  }
}

function validateForbiddenPositiveClaims(value, errors, artifact) {
  const normalized = normalizeScanText(value).replace(/\bpublic-safe\b/gi, "public evidence");
  for (const pattern of forbiddenPositiveClaimPatterns) {
    for (const match of normalized.matchAll(new RegExp(pattern.source, `${pattern.flags.includes("i") ? "i" : ""}g`))) {
      if (!hasNegation(match[0]) && !isNegated(normalized, match.index ?? 0)) {
        errors.push(withEvidence(`Release review boundary contains forbidden positive claim wording: ${match[0]}`, artifact));
      }
    }
  }
}

function validateHardPrivateMaterial(value, errors, artifact) {
  const normalized = decodeHtmlEntities(value);
  for (const pattern of hardPrivatePatterns) {
    if (pattern.test(normalized)) {
      errors.push(withEvidence(`Release review boundary contains forbidden private or raw material: ${pattern.source}`, artifact));
    }
  }
}

function validateBoundaryPrivateMaterial(value, errors, artifact) {
  const normalized = decodeHtmlEntities(value);
  for (const pattern of privateBoundaryPatterns) {
    if (pattern.test(normalized)) {
      errors.push(withEvidence(`Release review boundary contains forbidden private or raw material: ${pattern.source}`, artifact));
    }
  }
}

function validateBlameLanguage(value, errors, artifact) {
  for (const pattern of blamePatterns) {
    if (pattern.test(value)) {
      errors.push(withEvidence(`Release review boundary contains blame-oriented wording: ${pattern.source}`, artifact));
    }
  }
}

function stripSanctionedBoundaryHtml(html) {
  return html.replace(
    /<section\b(?=[^>]*\bid\s*=\s*["'](?:release-boundary-matrix|forbidden-claims|safe-wording|stop-conditions|non-claims|adjacent-surfaces)["'])[^>]*>[\s\S]*?<\/section>/gi,
    " "
  );
}

function stripTableHeaderHtml(html) {
  return html.replace(/<thead\b[^>]*>[\s\S]*?<\/thead>/gi, " ");
}

function extractRows(html, marker) {
  const pattern = new RegExp(`<tr\\b(?=[^>]*\\b${escapeRegExp(marker)}\\b)([^>]*)>[\\s\\S]*?<\\/tr>`, "gi");
  return [...html.matchAll(pattern)].map((match) => ({ attributes: match[1], html: match[0] }));
}

function getAttribute(attributes, name) {
  const match = attributes.match(new RegExp(`\\b${escapeRegExp(name)}\\s*=\\s*["']([^"']*)["']`, "i"));
  return match ? decodeHtmlEntities(match[1]) : null;
}

function hasHref(html, href) {
  const escaped = escapeRegExp(href);
  return new RegExp(`<a\\b[^>]*\\bhref\\s*=\\s*["']${escaped}["']`, "i").test(html);
}

function hasOgTypeArticle(html) {
  return /<meta\b(?=[^>]*\bproperty\s*=\s*["']og:type["'])(?=[^>]*\bcontent\s*=\s*["']article["'])[^>]*>/i.test(html);
}

function extractMainHtml(html) {
  return html.match(/<main\b[^>]*>([\s\S]*?)<\/main>/i)?.[1] ?? html;
}

function collectMetadataText(html) {
  return [...html.matchAll(/<meta\b([^>]*)>/gi)]
    .map((match) => getAttribute(match[1], "content"))
    .filter((value) => typeof value === "string" && value.trim() !== "")
    .join(" ");
}

function collectDecodedAttributeText(html) {
  return [...html.matchAll(/\b(?:alt|aria-label|title|content)\s*=\s*["']([^"']*)["']/gi)]
    .map((match) => decodeHtmlEntities(match[1]))
    .join(" ");
}

function collectRouteMetadataText(routeEntry) {
  if (!routeEntry) {
    return "";
  }

  return [
    routeEntry.title,
    routeEntry.summary,
    ...(Array.isArray(routeEntry.limitations) ? routeEntry.limitations : []),
    ...(Array.isArray(routeEntry.nonClaims) ? routeEntry.nonClaims : [])
  ].join(" ");
}

function normalizeScanText(value) {
  return normalizeRenderedText(String(value)).toLowerCase();
}

function normalizeRoute(route) {
  return `/${String(route).replace(/^\/+|\/+$/g, "")}/`;
}

function isNegated(value, index) {
  const sentencePrefix = value
    .slice(0, index)
    .split(/[.!?]\s+/)
    .pop()
    ?.toLowerCase() ?? "";
  return /(?:cannot|can't|does not|do not|not|no|without|never)\b/.test(sentencePrefix);
}

function hasNegation(value) {
  return /\b(?:cannot|can't|does not|do not|not|no|without|never)\b/i.test(value);
}

function countWords(value) {
  return (value.match(/\b[\w'-]+\b/g) ?? []).length;
}

function withEvidence(message, artifact) {
  return `${message} Evidence: ${artifact}.`;
}
