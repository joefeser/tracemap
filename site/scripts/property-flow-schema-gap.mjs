import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import {
  decodeHtmlEntities,
  escapeRegExp,
  fileExists,
  normalizeRenderedText,
  readSitemapLocSet
} from "./validate-utils.mjs";

export const propertyFlowSchemaGapRoute = "/proof-paths/property-flow-schema/";
export const propertyFlowSchemaGapRequiredLinks = [
  "/proof-paths/",
  "/proof-paths/route-flow/",
  "/evidence/gaps/",
  "/evidence/",
  "/limitations/",
  "/static-vs-runtime/",
  "/review-claim-checklist/"
];
export const propertyFlowSchemaGapInboundRoutes = ["/proof-paths/"];

const pageArtifact = "proof-paths/property-flow-schema/index.html";
const sitemapArtifact = "sitemap.xml";
const routesIndexArtifact = "routes-index.json";
const stateArtifact = ".kiro/specs/site-tracemap-tools-property-flow-schema-gap/implementation-state.md";

const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "UnsupportedRouteFlowSchema",
  "RouteFlowUnavailable",
  "empty",
  "available",
  "property-flow.schema.v1",
  "Tier4Unknown",
  "UnknownAnalysisGap",
  "supporting IDs",
  "commit evidence",
  "observed schema context",
  "extractor versions",
  "owner follow-up",
  "does not prove route-flow evidence is absent",
  "existing combined path evidence may still be shown"
];

const requiredAnchors = [
  "property-flow-schema-purpose",
  "property-flow-schema-statuses",
  "property-flow-schema-gap-fields",
  "property-flow-schema-review-language",
  "property-flow-schema-boundaries",
  "property-flow-schema-source-evidence",
  "property-flow-schema-next"
];

const metadataNonClaimTerms = [
  "runtime behavior",
  "production traffic",
  "endpoint performance",
  "impact proof",
  "UI behavior proof",
  "release approval",
  "complete coverage",
  "AI impact analysis",
  "LLM analysis",
  "replacement"
];

const forbiddenClaimPatterns = [
  /\b(?:TraceMap\s+|property-flow\s+|route-flow\s+)?(?:proves?|proven|proof of)\s+(?:runtime behavior|runtime request execution|runtime binding|production traffic|endpoint performance|UI behavior|outage cause|business impact|release safety|operational safety|release approval|complete coverage|impact|route-flow evidence is absent)\b/i,
  /\b(?:certifies?|guarantees?|verifies?|approves?)\s+(?:runtime behavior|production traffic|endpoint performance|UI behavior|release safety|operational safety|release approval|complete coverage|impact|release)\b/i,
  /\b(?:safe to release|production-proven)\b/i,
  /\bperforms?\s+(?:AI impact analysis|LLM impact analysis|LLM analysis)\b/i,
  /\buses?\s+(?:embeddings|vector databases|prompt classification)\b/i,
  /\breplaces?\s+(?:tests|code review|source review|runtime observability|service-owner judgment|human review|human judgment)\b/i
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

const boundarySectionPattern =
  /<section\b(?=[^>]*\bid\s*=\s*["']property-flow-schema-boundaries["'])(?=[^>]*\bdata-tm-boundary\s*=\s*["']property-flow-schema-boundaries["'])[^>]*>[\s\S]*?<\/section>/gi;

export async function validatePropertyFlowSchemaGapDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors,
  root
}) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl, localErrors);
  const pagePath = resolve(dist, "proof-paths", "property-flow-schema", "index.html");

  if (!(await fileExists(pagePath))) {
    errors.push(withEvidence(`Property-flow schema gap page is missing required public route: ${propertyFlowSchemaGapRoute}`, pageArtifact));
    return;
  }

  if (cleanBaseUrl) {
    await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  }

  const routeEntry = await readRouteEntry({ dist, errors: localErrors });
  const html = await readFile(pagePath, "utf8");
  validatePage(html, localErrors);
  validateRouteEntry(routeEntry, localErrors);
  await validateInboundLinks({ dist, errors: localErrors });

  if (root) {
    await validateImplementationState({ root, errors: localErrors });
  }

  errors.push(...localErrors);
}

function normalizeBaseUrl(value, errors) {
  let url;
  try {
    url = new URL(value);
  } catch {
    errors.push(withEvidence(`Property-flow schema gap baseUrl must be a valid absolute URL: ${String(value)}`, "baseUrl input"));
    return null;
  }

  if (url.protocol !== "https:" && url.protocol !== "http:") {
    errors.push(withEvidence(`Property-flow schema gap baseUrl must use http or https: ${String(value)}`, "baseUrl input"));
    return null;
  }

  return url.origin;
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${propertyFlowSchemaGapRoute}`)) {
    errors.push(withEvidence(`Property-flow schema gap sitemap is missing required route: ${baseUrl}${propertyFlowSchemaGapRoute}`, sitemapArtifact));
  }
}

async function readRouteEntry({ dist, errors }) {
  const indexPath = resolve(dist, "routes-index.json");
  if (!(await fileExists(indexPath))) {
    errors.push(withEvidence("Property-flow schema gap routes-index.json is missing.", routesIndexArtifact));
    return null;
  }

  let parsed;
  try {
    parsed = JSON.parse(await readFile(indexPath, "utf8"));
  } catch (error) {
    errors.push(withEvidence(`Property-flow schema gap could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return null;
  }

  if (!Array.isArray(parsed?.entries)) {
    errors.push(withEvidence("Property-flow schema gap routes-index.json is invalid: expected entries array.", routesIndexArtifact));
    return null;
  }

  const normalize = (value) => {
    const path = String(value ?? "").split(/[?#]/, 1)[0];
    return path.endsWith("/") ? path : `${path}/`;
  };

  return parsed.entries.find((entry) => normalize(entry?.path) === propertyFlowSchemaGapRoute) ?? null;
}

function validateRouteEntry(routeEntry, errors) {
  if (!routeEntry) {
    errors.push(withEvidence(`Property-flow schema gap routes-index.json is missing required route: ${propertyFlowSchemaGapRoute}`, routesIndexArtifact));
    return;
  }

  const expectedFields = {
    publicClaimLevel: "concept",
    hintCategory: "evidence",
    sourceType: "site-page",
    preferredProofPath: "/proof-paths/"
  };

  for (const [field, expected] of Object.entries(expectedFields)) {
    if (routeEntry[field] !== expected) {
      errors.push(withEvidence(`Property-flow schema gap routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
    }
  }

  if (!Array.isArray(routeEntry.limitations) || routeEntry.limitations.length === 0) {
    errors.push(withEvidence("Property-flow schema gap routes-index.json must include limitations metadata.", routesIndexArtifact));
  }

  if (!Array.isArray(routeEntry.nonClaims) || routeEntry.nonClaims.length === 0) {
    errors.push(withEvidence("Property-flow schema gap routes-index.json must include nonClaims metadata.", routesIndexArtifact));
    return;
  }

  const normalizedNonClaimsText = normalizeScanText(routeEntry.nonClaims.join(" ")).toLowerCase();
  for (const term of metadataNonClaimTerms) {
    if (!normalizedNonClaimsText.includes(term.toLowerCase())) {
      errors.push(withEvidence(`Property-flow schema gap routes-index.json nonClaims are missing required term: ${term}`, routesIndexArtifact));
    }
  }
}

function validatePage(html, errors) {
  const strippedHtml = html.replace(boundarySectionPattern, " ");
  const mainHtml = extractMainHtml(html);
  const strippedMainHtml = extractMainHtml(strippedHtml);
  const pageText = normalizeRenderedText(mainHtml);
  const strippedText = normalizeRenderedText(strippedMainHtml);
  const metadataText = collectMetadataText(html);
  const attributeText = collectDecodedAttributeText(strippedHtml);
  const allText = `${html} ${decodeHtmlEntities(html)} ${pageText} ${metadataText} ${collectDecodedAttributeText(html)}`;

  for (const phrase of requiredText) {
    if (!pageText.includes(phrase)) {
      errors.push(withEvidence(`Property-flow schema gap page is missing required text: ${phrase}`, pageArtifact));
    }
  }

  for (const anchor of requiredAnchors) {
    if (!hasId(html, anchor)) {
      errors.push(withEvidence(`Property-flow schema gap page is missing required anchor: #${anchor}`, pageArtifact));
    }
  }

  for (const status of ["unavailable", "empty", "unsupported", "available"]) {
    if (!hasSchemaStatus(html, status)) {
      errors.push(withEvidence(`Property-flow schema gap page is missing schema-status marker: ${status}`, pageArtifact));
    }
  }

  for (const link of propertyFlowSchemaGapRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Property-flow schema gap page is missing required link: ${link}`, pageArtifact));
    }
  }

  validatePageMetadata(html, errors);
  validateForbiddenClaims(`${strippedText} ${metadataText} ${attributeText}`, errors);
  validateHardPrivateMaterial(allText, errors);
}

function validatePageMetadata(html, errors) {
  const checks = [
    [/<title>Property-Flow Schema Gap \| TraceMap<\/title>/i.test(html), "title"],
    [/<meta\s+name=["']description["']\s+content=["'][^"']+["']/i.test(html), "description"],
    [/<link\s+rel=["']canonical["']\s+href=["']https:\/\/tracemap\.tools\/proof-paths\/property-flow-schema\/["']/i.test(html), "canonical URL"],
    [/<meta\s+property=["']og:type["']\s+content=["']article["']/i.test(html), "Open Graph type"],
    [/<meta\s+property=["']og:title["']\s+content=["'][^"']+["']/i.test(html), "Open Graph title"],
    [/<meta\s+property=["']og:description["']\s+content=["'][^"']+["']/i.test(html), "Open Graph description"],
    [/<meta\s+property=["']og:url["']\s+content=["']https:\/\/tracemap\.tools\/proof-paths\/property-flow-schema\/["']/i.test(html), "Open Graph URL"]
  ];

  for (const [passed, label] of checks) {
    if (!passed) {
      errors.push(withEvidence(`Property-flow schema gap page is missing required metadata: ${label}`, pageArtifact));
    }
  }
}

async function validateInboundLinks({ dist, errors }) {
  const missing = [];

  for (const route of propertyFlowSchemaGapInboundRoutes) {
    const pagePath = resolve(dist, route.replace(/^\/|\/$/g, ""), "index.html");
    if (!(await fileExists(pagePath))) {
      continue;
    }

    const html = await readFile(pagePath, "utf8");
    if (!hasHref(html, propertyFlowSchemaGapRoute)) {
      missing.push(route);
    }
  }

  if (missing.length > 0) {
    errors.push(withEvidence(`Property-flow schema gap is missing inbound links from live adjacent routes: ${missing.join(", ")}`, "adjacent route HTML"));
  }
}

async function validateImplementationState({ root, errors }) {
  const statePath = await firstExistingPath([resolve(root, "..", stateArtifact), resolve(root, stateArtifact)]);
  if (!(await fileExists(statePath))) {
    errors.push(withEvidence("Property-flow schema gap implementation-state file is missing.", stateArtifact));
    return;
  }

  const state = await readFile(statePath, "utf8");
  for (const phrase of [
    "Selected placement: `/proof-paths/property-flow-schema/`",
    "Rejected placement alternatives",
    "Verified Current-Branch Evidence",
    "desktop browser sanity",
    "mobile browser sanity"
  ]) {
    if (!state.includes(phrase)) {
      errors.push(withEvidence(`Property-flow schema gap implementation-state is missing required record: ${phrase}`, stateArtifact));
    }
  }
}

async function firstExistingPath(paths) {
  for (const path of paths) {
    if (await fileExists(path)) {
      return path;
    }
  }

  return paths[0];
}

function validateForbiddenClaims(text, errors) {
  for (const pattern of forbiddenClaimPatterns) {
    const flags = pattern.flags.includes("g") ? pattern.flags : `${pattern.flags}g`;
    const globalPattern = new RegExp(pattern.source, flags);
    for (const match of text.matchAll(globalPattern)) {
      if (!hasNegatedContext(text, match.index)) {
        errors.push(withEvidence(`Property-flow schema gap contains forbidden public claim: ${match[0]}`, pageArtifact));
        break;
      }
    }
  }
}

function validateHardPrivateMaterial(text, errors) {
  for (const pattern of hardPrivatePatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Property-flow schema gap contains hard private material: ${pattern.source}`, pageArtifact));
    }
  }
}

function hasNegatedContext(value, index) {
  const prefix = value.slice(Math.max(0, index - 96), index).toLowerCase();
  return /(?:not|no|without|cannot|can't|does not|do not|never|outside|instead of|must not|nor|nonclaim|non-claim)\s+(?:a\s+)?(?:real\s+)?(?:public\s+)?(?:runtime\s+)?(?:route-flow\s+evidence\s+is\s+)?$/.test(prefix);
}

function hasId(html, id) {
  return new RegExp(`\\bid\\s*=\\s*["']${escapeRegExp(id)}["']`, "i").test(html);
}

function hasSchemaStatus(html, status) {
  return new RegExp(`\\bdata-property-schema-status\\s*=\\s*["']${escapeRegExp(status)}["']`, "i").test(html);
}

function hasHref(html, href) {
  const normalizedHref = normalizeRouteHref(href);
  return extractHrefs(html).some((candidate) => normalizeRouteHref(candidate) === normalizedHref);
}

function extractHrefs(html) {
  return [...html.matchAll(/\bhref\s*=\s*("([^"]*)"|'([^']*)')/gi)]
    .map((match) => decodeHtmlEntities(match[2] ?? match[3] ?? ""))
    .filter(Boolean);
}

function normalizeRouteHref(href) {
  if (!href.startsWith("/") || href.startsWith("//")) {
    return href;
  }

  const path = href.split(/[?#]/, 1)[0];
  return path.endsWith("/") ? path : `${path}/`;
}

function collectMetadataText(html) {
  const values = [...html.matchAll(/<meta\b[^>]*\bcontent\s*=\s*("[^"]*"|'[^']*')[^>]*>/gi)].map((match) =>
    unquoteAttributeValue(match[1])
  );
  const title = html.match(/<title>([\s\S]*?)<\/title>/i);
  if (title) {
    values.push(title[1]);
  }

  return decodeHtmlEntities(values.join(" "));
}

function collectDecodedAttributeText(html) {
  return decodeHtmlEntities(
    [...html.matchAll(/\s[a-z:-]+\s*=\s*("[^"]*"|'[^']*')/gi)]
      .map((match) => unquoteAttributeValue(match[1]))
      .join(" ")
  );
}

function extractMainHtml(html) {
  const match = html.match(/<main\b[^>]*>([\s\S]*?)<\/main>/i);
  return match ? match[1] : html;
}

function unquoteAttributeValue(value) {
  return String(value).slice(1, -1);
}

function normalizeScanText(value) {
  return String(value)
    .normalize("NFKC")
    .replace(/\p{Cf}/gu, "")
    .replace(/\s+/g, " ")
    .trim();
}

function withEvidence(message, artifact) {
  return `${message} [${artifact}]`;
}
