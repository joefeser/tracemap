import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import { decodeHtmlEntities, escapeRegExp, fileExists, normalizeBaseUrl, normalizeRenderedText, readSitemapLocSet, stripTagsQuoteAware } from "./validate-utils.mjs";

export const accessRebuildReadinessRoute = "/blog/access-rebuild-readiness-gaps/";
export const accessRebuildReadinessRequiredLinks = [
  "/blog/reverse-engineering-access-without-running-it/",
  "/blog/access-form-to-field-lineage/",
  "/evidence/",
  "/evidence/gaps/",
  "/proof-paths/for-managers/",
  "/static-vs-runtime/",
  "/capabilities/",
  "/use-cases/change-review/",
  "/limitations/"
];

const artifact = "blog/access-rebuild-readiness-gaps/index.html";
const companionArtifacts = [
  "blog/reverse-engineering-access-without-running-it/index.html",
  "blog/access-form-to-field-lineage/index.html"
];
const blocks = ["question", "query-shapes", "copy-candidates", "behavior", "flow", "bounds", "bundle", "owner-questions", "companions", "non-claims", "bottom-line"];
const requiredText = [
  "Public claim level: demo",
  "legacy.access.query.v1",
  "legacy.access.binding.v1",
  "legacy.access.event-binding.v1",
  "legacy.access.macro-gap.v1",
  "legacy.access.coverage-gap.v1",
  "legacy.access.screen-data-flow.v1",
  "legacy.access.copy-clone-candidate.v1",
  "TruncatedByLimit",
  "omitted count",
  "supporting fact IDs",
  "extractor versions",
  "Tier 2",
  "Tier 3",
  "Tier 4",
  "owner questions",
  "local Access review bundle"
];
const forbiddenClaims = [
  /\bTraceMap\b[^.]{0,160}\b(?:proves?|guarantees?|certifies?|establishes?|verif(?:y|ies|ied)|asserts?|asserted|confirms?|confirmed)\b[^.]{0,160}\b(?:rebuild|runtime|execution|correctness|complete|safe|production)\b/i,
  /\b(?:application|database|rebuild|migration)\b[^.]{0,140}\b(?:is|was|will be)\s+(?:complete|correct|successful|safe|approved|ready)\b/i,
  /\b(?:validation passed|reconstruction succeeded|permissions are effective|jobs? ran|release approved)\b/i
];
const rawPatterns = [
  /\bSELECT\s+.+\bFROM\b/i,
  /\b(?:CREATE|ALTER|DROP|GRANT|REVOKE)\s+(?:TABLE|VIEW|USER|ROLE|DATABASE|PROCEDURE|FUNCTION)\b/i,
  /\b(?:Sub|Function)\s+[A-Za-z_]\w*\s*\(/i,
  /\b(?:Password|Server|User Id|ConnectionString)\s*=/i
];
const privatePatterns = [/\/Users\//i, /\/private\//i, /\/home\//i, /\/tmp\//i, /\/var\/folders\//i, /~\//, /\bC:\\/i, /\bfile:\/\//i, /\bgit@/i, /\bsk-[A-Za-z0-9_-]{12,}\b/i];

export async function validateAccessRebuildReadinessDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, artifact);
  if (!(await fileExists(pagePath))) {
    errors.push(withEvidence(`Access rebuild-readiness article is missing required route: ${accessRebuildReadinessRoute}`, artifact));
    return;
  }

  const sitemapPath = resolve(dist, "sitemap.xml");
  if (await fileExists(sitemapPath)) {
    if (!(await readSitemapLocSet(sitemapPath)).has(`${cleanBaseUrl}${accessRebuildReadinessRoute}`)) localErrors.push(withEvidence("Access rebuild-readiness sitemap route is missing.", "sitemap.xml"));
  }
  await validateIndexes(dist, localErrors);
  await validatePage(pagePath, cleanBaseUrl, localErrors);
  errors.push(...localErrors);
}

async function validateIndexes(dist, errors) {
  const blogPath = resolve(dist, "blog/index.html");
  if (!(await fileExists(blogPath)) || !hasHref(await readFile(blogPath, "utf8"), accessRebuildReadinessRoute)) errors.push(withEvidence("Access rebuild-readiness blog index link is missing.", "blog/index.html"));
  for (const companion of companionArtifacts) {
    const path = resolve(dist, companion);
    if (!(await fileExists(path)) || !hasHref(await readFile(path, "utf8"), accessRebuildReadinessRoute)) errors.push(withEvidence("Access rebuild-readiness companion link is missing.", companion));
  }

  const discoveryPath = resolve(dist, "routes-index.json");
  if (!(await fileExists(discoveryPath))) {
    errors.push(withEvidence("Access rebuild-readiness discovery output is missing.", "routes-index.json"));
    return;
  }
  let discovery;
  try {
    discovery = JSON.parse(await readFile(discoveryPath, "utf8"));
  } catch {
    errors.push(withEvidence("Access rebuild-readiness discovery output is not valid JSON.", "routes-index.json"));
    return;
  }
  if (!discovery || typeof discovery !== "object" || !Array.isArray(discovery.entries)) {
    errors.push(withEvidence("Access rebuild-readiness discovery output must contain an entries array.", "routes-index.json"));
    return;
  }
  const entry = discovery.entries.find((candidate) => candidate.path === accessRebuildReadinessRoute);
  if (!entry) errors.push(withEvidence("Access rebuild-readiness discovery entry is missing.", "routes-index.json"));
  else {
    if (entry.publicClaimLevel !== "demo") errors.push(withEvidence("Access rebuild-readiness discovery claim level must be demo.", "routes-index.json"));
    if (entry.preferredProofPath !== "/blog/reverse-engineering-access-without-running-it/") errors.push(withEvidence("Access rebuild-readiness preferred proof path is incorrect.", "routes-index.json"));
    if (!Array.isArray(entry.limitations) || entry.limitations.length < 2) errors.push(withEvidence("Access rebuild-readiness discovery must retain at least two limitations.", "routes-index.json"));
    if (!Array.isArray(entry.nonClaims) || entry.nonClaims.length < 2) errors.push(withEvidence("Access rebuild-readiness discovery must retain at least two non-claims.", "routes-index.json"));
  }
}

async function validatePage(pagePath, baseUrl, errors) {
  const html = await readFile(pagePath, "utf8");
  const browserDecoded = decodeBrowserNumericEntities(html);
  const decoded = decodeHtmlEntities(browserDecoded);
  const rendered = normalizeRenderedText(browserDecoded);
  const tagCollapsed = decodeHtmlEntities(stripTagsQuoteAware(decoded)).replace(/\s+/g, " ").trim();
  const surfaces = [decoded, rendered, tagCollapsed];
  surfaces.push(...surfaces.map(decodePercentEscapes));
  if (!html.includes("<title>Rebuild Readiness Is a Gap Register, Not a Promise | TraceMap</title>")) errors.push(withEvidence("Access rebuild-readiness title is missing.", artifact));
  if (!new RegExp(`<link\\b[^>]*rel=["']canonical["'][^>]*href=["']${escapeRegExp(baseUrl)}${escapeRegExp(accessRebuildReadinessRoute)}["']`, "i").test(html)) errors.push(withEvidence("Access rebuild-readiness canonical URL is missing or incorrect.", artifact));
  for (const block of blocks) if (!new RegExp(`<section\\b[^>]*data-access-readiness-block\\s*=\\s*["']${block}["']`, "i").test(html)) errors.push(withEvidence(`Access rebuild-readiness article is missing block: ${block}`, artifact));
  for (const phrase of requiredText) if (!rendered.toLowerCase().includes(phrase.toLowerCase())) errors.push(withEvidence(`Access rebuild-readiness article is missing required text: ${phrase}`, artifact));
  for (const link of accessRebuildReadinessRequiredLinks) if (!hasHref(html, link)) errors.push(withEvidence(`Access rebuild-readiness article is missing required link: ${link}`, artifact));
  const words = rendered.split(/\s+/).filter(Boolean).length;
  if (words < 1100 || words > 2100) errors.push(withEvidence(`Access rebuild-readiness word count must be between 1100 and 2100 words, got ${words}`, artifact));
  for (const pattern of forbiddenClaims) if (surfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`Access rebuild-readiness article contains unsupported positive claim: ${pattern}`, artifact));
  for (const pattern of rawPatterns) if (surfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`Access rebuild-readiness article contains raw or executable material: ${pattern}`, artifact));
  for (const pattern of privatePatterns) if (surfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`Access rebuild-readiness article contains hard private material: ${pattern}`, artifact));
}

function decodeBrowserNumericEntities(value) {
  return String(value).replace(/&#(?:x[0-9a-f]+|[0-9]+);?/gi, (entity) => decodeHtmlEntities(entity.endsWith(";") ? entity : `${entity};`));
}

function decodePercentEscapes(value) {
  try { return decodeURIComponent(String(value)); }
  catch { return String(value); }
}

function hasHref(html, href) { return new RegExp(`<a\\b[^>]*href\\s*=\\s*["']${escapeRegExp(href)}["'][^>]*>`, "i").test(html); }
function withEvidence(message, evidenceArtifact) { return `${message} Evidence: ${evidenceArtifact}.`; }
