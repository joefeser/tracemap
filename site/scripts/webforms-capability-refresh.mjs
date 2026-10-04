import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import { fileExists, normalizeRenderedText } from "./validate-utils.mjs";

export const webFormsCapabilityRefreshRoutes = [
  "/capabilities/",
  "/roadmap/",
  "/legacy-dotnet/evidence/",
  "/legacy-modernization/evidence-map/",
  "/legacy-modernization/review-handoff/",
  "/manager-packet/",
  "/manager-faq/",
  "/proof-paths/for-managers/"
];

export const webFormsProofRoutes = [
  "/webforms/",
  "/webforms/source-plus-compiled-proof/",
  "/webforms/local-demo/",
  "/webforms/review-workbench/"
];

const futureTerms = [
  "Automatic solution-wide discovery",
  "backend microservice onboarding",
  "cross-service click-to-database tracing",
  "source-server/PDB acquisition"
];

const forbiddenMaterial = [
  /(?:~\/|\/(?:Users|home|private|tmp)\/|\/var\/folders\/|[A-Z]:\\(?:Users|Temp)\\|file:\/\/)/i,
  /\b(?:Server|Password|User Id)\s*=/i,
  /\b(?:ConnectionString|api[_-]?key|secret\s*=|sk-[A-Za-z0-9_-]{12,})\b/i,
  /\b(?:SELECT\s+.+?\s+FROM|INSERT\s+INTO|UPDATE\s+\w+\s+SET|DELETE\s+FROM|CREATE\s+TABLE|ALTER\s+TABLE|DROP\s+TABLE)\b/i
];

const forbiddenClaims = [
  /TraceMap (?:ran|executed|observed|launched) (?:the )?(?:Web Forms|page|website|database)/i,
  /(?:customer compatibility|migration parity|complete cross-service tracing) (?:is|was) (?:proven|verified|guaranteed)/i,
  /(?:Web Forms|VB\.NET) (?:support|coverage) (?:is|was) complete/i,
  /(?:safe to run|safe to release|release approved)/i
];

export async function validateWebFormsCapabilityRefreshDist({ dist, errors }) {
  const pages = new Map();
  for (const route of webFormsCapabilityRefreshRoutes) {
    const pagePath = resolve(dist, route.slice(1), "index.html");
    if (!(await fileExists(pagePath))) {
      errors.push(`Web Forms capability refresh is missing audited route: ${route}`);
      continue;
    }
    const html = stripHtmlComments(await readFile(pagePath, "utf8"));
    pages.set(route, html);
    const hrefs = activeAnchorHrefs(html);
    for (const proofRoute of webFormsProofRoutes) {
      if (!hrefs.has(proofRoute)) errors.push(`Web Forms capability refresh route ${route} is missing proof link: ${proofRoute}`);
    }
    const surface = `${normalizeRenderedText(html)} ${normalizeAttributeValues(html)}`;
    for (const pattern of [...forbiddenMaterial, ...forbiddenClaims]) {
      if (pattern.test(surface)) errors.push(`Web Forms capability refresh route ${route} contains forbidden public material or claim: ${pattern}`);
    }
  }

  validateCapabilityLadder(pages.get("/capabilities/"), errors);
  validateRoadmapLadder(pages.get("/roadmap/"), errors);
  validateLegacyLane(pages.get("/legacy-dotnet/evidence/"), errors);
  validateModernizationMap(pages.get("/legacy-modernization/evidence-map/"), errors);
  validateFutureBoundary(pages, errors);
  await validateDiscovery({ dist, errors });
}

function validateCapabilityLadder(html, errors) {
  if (!html) return;
  if (!/data-webforms-capability-audit=["']v1["']/i.test(html)) errors.push("Web Forms capability refresh is missing its capability audit marker.");
  const expected = new Map([
    ["guided-setup", "shipped"],
    ["source-compiled", "concept"],
    ["local-demo", "demo"],
    ["review-workbench", "demo"]
  ]);
  for (const [name, level] of expected) {
    const pattern = new RegExp(`<article\\b(?=[^>]*data-webforms-capability=["']${escapeRegex(name)}["'])(?=[^>]*data-public-claim-level=["']${level}["'])[^>]*>`, "i");
    if (!pattern.test(html)) errors.push(`Web Forms capability refresh expected ${name} claim level ${level}.`);
  }
}

function validateRoadmapLadder(html, errors) {
  if (!html) return;
  const expected = new Map([
    ["claim-webforms-terminal-workflow", "shipped"],
    ["claim-webforms-source-compiled-proof", "concept"],
    ["claim-webforms-local-demo", "demo"],
    ["claim-webforms-review-workbench", "demo"],
    ["claim-webforms-future-automation", "concept"]
  ]);
  for (const [id, level] of expected) {
    const pattern = new RegExp(`<tr\\b(?=[^>]*id=["']${escapeRegex(id)}["'])(?=[^>]*data-claim-level=["']${level}["'])[^>]*>`, "i");
    if (!pattern.test(html)) errors.push(`Web Forms capability refresh roadmap expected ${id} claim level ${level}.`);
  }
}

function validateLegacyLane(html, errors) {
  if (!html) return;
  validateStatusRows(html, "data-lane-row", new Map([
    ["webforms-guided-setup", "shipped"],
    ["webforms", "hidden"],
    ["webforms-source-compiled-proof", "concept"],
    ["webforms-local-demo", "demo"],
    ["webforms-review-workbench", "demo"]
  ]), errors, "legacy lane");
}

function validateModernizationMap(html, errors) {
  if (!html) return;
  validateStatusRows(html, "data-map-row", new Map([
    ["webforms-guided-setup", "shipped"],
    ["webforms-event-route-navigation", "hidden"],
    ["webforms-source-compiled-proof", "concept"],
    ["webforms-local-demo", "demo"],
    ["webforms-review-workbench", "demo"]
  ]), errors, "modernization map");
}

function validateStatusRows(html, key, expected, errors, label) {
  for (const [id, status] of expected) {
    const pattern = new RegExp(`<tr\\b(?=[^>]*${key}=["']${escapeRegex(id)}["'])(?=[^>]*data-public-status=["']${status}["'])[^>]*>`, "i");
    if (!pattern.test(html)) errors.push(`Web Forms capability refresh ${label} expected ${id} status ${status}.`);
  }
}

function validateFutureBoundary(pages, errors) {
  const combined = [pages.get("/capabilities/"), pages.get("/roadmap/")].filter(Boolean).map(normalizeRenderedText).join(" ");
  for (const term of futureTerms) if (!combined.includes(term)) errors.push(`Web Forms capability refresh is missing future-work boundary: ${term}`);
  for (const phrase of ["candidate bridges", "unresolved values", "partial coverage", "Windows-only"]) {
    if (!combined.toLowerCase().includes(phrase.toLowerCase())) errors.push(`Web Forms capability refresh is missing bounded evidence term: ${phrase}`);
  }
}

async function validateDiscovery({ dist, errors }) {
  const indexPath = resolve(dist, "routes-index.json");
  if (!(await fileExists(indexPath))) return;
  let parsed;
  try { parsed = JSON.parse(await readFile(indexPath, "utf8")); }
  catch (error) { errors.push(`Web Forms capability refresh could not parse routes-index.json: ${error.message}`); return; }
  for (const route of webFormsCapabilityRefreshRoutes) {
    const entry = parsed?.entries?.find((item) => item?.path === route);
    if (!entry) { errors.push(`Web Forms capability refresh discovery is missing route: ${route}`); continue; }
    if (!String(entry.summary ?? "").includes("Web Forms")) errors.push(`Web Forms capability refresh discovery summary is stale for: ${route}`);
    if (!Array.isArray(entry.limitations) || entry.limitations.length < 2 || !Array.isArray(entry.nonClaims) || entry.nonClaims.length < 2) errors.push(`Web Forms capability refresh discovery boundaries are incomplete for: ${route}`);
  }
}

function stripHtmlComments(value) { return String(value).replace(/<!--[\s\S]*?-->/g, ""); }
function activeAnchorHrefs(html) {
  const hrefs = new Set();
  for (const match of html.matchAll(/<a\b[^>]*\bhref\s*=\s*["']([^"']+)["'][^>]*>/gi)) hrefs.add(match[1]);
  return hrefs;
}
function normalizeAttributeValues(html) {
  return [...html.matchAll(/\b(?:content|data-[\w-]+|title|aria-label)\s*=\s*["']([^"']*)["']/gi)].map((match) => match[1]).join(" ");
}
function escapeRegex(value) { return String(value).replace(/[.*+?^${}()|[\]\\]/g, "\\$&"); }
