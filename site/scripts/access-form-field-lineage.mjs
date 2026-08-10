import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import { decodeHtmlEntities, escapeRegExp, fileExists, normalizeBaseUrl, normalizeRenderedText, readSitemapLocSet, stripTagsQuoteAware } from "./validate-utils.mjs";

export const accessFormFieldLineageSlug = "access-form-to-field-lineage";
export const accessFormFieldLineageRoute = `/blog/${accessFormFieldLineageSlug}/`;
export const accessFormFieldLineageCompanionRoute = "/blog/reverse-engineering-access-without-running-it/";
export const accessFormFieldLineageRequiredLinks = [
  accessFormFieldLineageCompanionRoute,
  "/evidence/",
  "/evidence/gaps/",
  "/proof-paths/for-managers/",
  "/static-vs-runtime/",
  "/capabilities/",
  "/use-cases/change-review/",
  "/limitations/"
];

const pageArtifact = `blog/${accessFormFieldLineageSlug}/index.html`;
const blocks = ["starting-point", "lookup", "subforms", "queries", "expressions", "events", "composed-trail", "rules", "handoff", "non-claims", "bottom-line"];
const rules = [
  "legacy.access.ui-surface.v1",
  "legacy.access.binding.v1",
  "legacy.access.vba.v1",
  "legacy.access.event-binding.v1",
  "legacy.access.screen-data-flow.v1"
];
const requiredText = [
  "Public claim level: demo",
  "RecordSource",
  "ControlSource",
  "RowSource",
  "RowSourceType",
  "BoundColumn",
  "projection ordinal",
  "master and child",
  "saved-query output",
  "domain-function",
  "same-module procedure candidate",
  "hash-safe branch context",
  "Tier 2",
  "Tier 3",
  "Tier 4",
  "weakest required hop",
  "fan-out"
];
const forbiddenClaims = [
  /\bTraceMap\b[^.]{0,150}\b(?:proved|proves|observed|verified|validated)\b[^.]{0,150}\b(?:runtime|event firing|query results?|selected branch|production behavior|data correctness|effective permissions)\b/i,
  /\b(?:lineage|trail|path)\b[^.]{0,120}\b(?:proves|guarantees|certifies)\b[^.]{0,120}\b(?:runtime|execution|write|persistence|correctness|safety)\b/i,
  /\b(?:safe to run|safe to release|approved for release|reconstruction succeeded|validation passed)\b/i
];
const rawPatterns = [
  /\bSELECT\s+.+\bFROM\b/i,
  /\b(?:CREATE|ALTER|DROP|GRANT|REVOKE)\s+(?:TABLE|VIEW|USER|ROLE|DATABASE|PROCEDURE|FUNCTION)\b/i,
  /\b(?:Sub|Function)\s+[A-Za-z_]\w*\s*\(/i,
  /\bPassword\s*=/i,
  /\bServer\s*=/i,
  /\bConnectionString\s*=/i
];
const privatePatterns = [/\/Users\//i, /\/private\//i, /\/home\//i, /\/tmp\//i, /\/var\/folders\//i, /~\//, /\bC:\\/i, /\bfile:\/\//i, /\bgit@/i, /\bsk-[A-Za-z0-9_-]{12,}\b/i];

export async function validateAccessFormFieldLineageDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "blog", accessFormFieldLineageSlug, "index.html");
  if (!(await fileExists(pagePath))) {
    errors.push(withEvidence(`Access form-to-field article is missing required route: ${accessFormFieldLineageRoute}`, pageArtifact));
    return;
  }
  await validateSitemap(cleanBaseUrl, dist, localErrors);
  await validateIndexes(dist, localErrors);
  await validatePage(pagePath, localErrors);
  errors.push(...localErrors);
}

async function validateSitemap(baseUrl, dist, errors) {
  const path = resolve(dist, "sitemap.xml");
  if (!(await fileExists(path))) return;
  if (!(await readSitemapLocSet(path)).has(`${baseUrl}${accessFormFieldLineageRoute}`)) errors.push(withEvidence("Access form-to-field sitemap route is missing.", "sitemap.xml"));
}

async function validateIndexes(dist, errors) {
  const blogPath = resolve(dist, "blog", "index.html");
  const companionPath = resolve(dist, "blog", "reverse-engineering-access-without-running-it", "index.html");
  const discoveryPath = resolve(dist, "routes-index.json");
  for (const [path, label, route] of [[blogPath, "blog index", accessFormFieldLineageRoute], [companionPath, "companion article", accessFormFieldLineageRoute]]) {
    if (!(await fileExists(path)) || !hasHref(await readFile(path, "utf8"), route)) errors.push(withEvidence(`Access form-to-field ${label} link is missing.`, label === "blog index" ? "blog/index.html" : "blog/reverse-engineering-access-without-running-it/index.html"));
  }
  if (!(await fileExists(discoveryPath))) {
    errors.push(withEvidence("Access form-to-field discovery output is missing.", "routes-index.json"));
    return;
  }
  let discovery;
  try {
    discovery = JSON.parse(await readFile(discoveryPath, "utf8"));
  } catch {
    errors.push(withEvidence("Access form-to-field discovery output is not valid JSON.", "routes-index.json"));
    return;
  }
  if (!discovery || typeof discovery !== "object" || !Array.isArray(discovery.entries)) {
    errors.push(withEvidence("Access form-to-field discovery output must contain an entries array.", "routes-index.json"));
    return;
  }
  const entries = discovery.entries;
  const entry = entries.find((candidate) => candidate.path === accessFormFieldLineageRoute);
  if (!entry) errors.push(withEvidence("Access form-to-field discovery entry is missing.", "routes-index.json"));
  else {
    if (entry.publicClaimLevel !== "demo") errors.push(withEvidence("Access form-to-field discovery claim level must be demo.", "routes-index.json"));
    if (entry.preferredProofPath !== accessFormFieldLineageCompanionRoute) errors.push(withEvidence("Access form-to-field preferred proof path must remain the acquisition article.", "routes-index.json"));
    if (!Array.isArray(entry.limitations) || entry.limitations.length < 2) errors.push(withEvidence("Access form-to-field discovery must retain at least two limitations.", "routes-index.json"));
    if (!Array.isArray(entry.nonClaims) || entry.nonClaims.length < 2) errors.push(withEvidence("Access form-to-field discovery must retain at least two non-claims.", "routes-index.json"));
  }
}

async function validatePage(pagePath, errors) {
  const html = await readFile(pagePath, "utf8");
  const decoded = decodeHtmlEntities(html);
  const rendered = normalizeRenderedText(html);
  const tagCollapsed = decodeHtmlEntities(stripTagsQuoteAware(decoded)).replace(/\s+/g, " ").trim();
  const safetySurfaces = [decoded, rendered, tagCollapsed];
  if (!html.includes("<title>From an Access Form to a Field | TraceMap</title>")) errors.push(withEvidence("Access form-to-field title is missing.", pageArtifact));
  if (!new RegExp(`<link\\b[^>]*rel=["']canonical["'][^>]*href=["']https://tracemap\\.tools${escapeRegExp(accessFormFieldLineageRoute)}["']`, "i").test(html)) errors.push(withEvidence("Access form-to-field canonical URL is missing or incorrect.", pageArtifact));
  for (const block of blocks) if (!new RegExp(`<section\\b[^>]*data-access-lineage-block\\s*=\\s*["']${block}["']`, "i").test(html)) errors.push(withEvidence(`Access form-to-field article is missing block: ${block}`, pageArtifact));
  for (const phrase of [...requiredText, ...rules]) if (!rendered.toLowerCase().includes(phrase.toLowerCase())) errors.push(withEvidence(`Access form-to-field article is missing required text: ${phrase}`, pageArtifact));
  for (const link of accessFormFieldLineageRequiredLinks) if (!hasHref(html, link)) errors.push(withEvidence(`Access form-to-field article is missing required link: ${link}`, pageArtifact));
  const words = rendered.split(/\s+/).filter(Boolean).length;
  if (words < 1000 || words > 1900) errors.push(withEvidence(`Access form-to-field word count must be between 1000 and 1900 words, got ${words}`, pageArtifact));
  for (const pattern of forbiddenClaims) if (safetySurfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`Access form-to-field article contains unsupported positive claim: ${pattern}`, pageArtifact));
  for (const pattern of rawPatterns) if (safetySurfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`Access form-to-field article contains raw or executable material: ${pattern}`, pageArtifact));
  for (const pattern of privatePatterns) if (safetySurfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`Access form-to-field article contains hard private material: ${pattern}`, pageArtifact));
}

function hasHref(html, href) { return new RegExp(`<a\\b[^>]*href\\s*=\\s*["']${escapeRegExp(href)}["'][^>]*>`, "i").test(html); }
function withEvidence(message, artifact) { return `${message} Evidence: ${artifact}.`; }
