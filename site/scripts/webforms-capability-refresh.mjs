import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import { decodeHtmlEntities, fileExists, normalizeRenderedText } from "./validate-utils.mjs";

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
  /\bTraceMap (?:proves?|verifies?|guarantees?|confirms?)\b[^.!?]{0,160}\b(?:executes?|runs?|is reachable) (?:at runtime|in production)\b/i,
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
  const capabilities = pages.get("/capabilities/");
  const roadmap = pages.get("/roadmap/");
  for (const [route, html] of [["/capabilities/", capabilities], ["/roadmap/", roadmap]]) {
    if (!html) continue;
    const text = normalizeRenderedText(html);
    for (const term of futureTerms) {
      if (!text.includes(term)) errors.push(`Web Forms capability refresh route ${route} is missing future-work boundary: ${term}`);
    }
  }
  const capabilitiesText = capabilities ? normalizeRenderedText(capabilities) : "";
  for (const phrase of ["candidate bridges", "unresolved values", "partial coverage", "Windows-only"]) {
    if (!capabilitiesText.toLowerCase().includes(phrase.toLowerCase())) errors.push(`Web Forms capability refresh route /capabilities/ is missing bounded evidence term: ${phrase}`);
  }
}

async function validateDiscovery({ dist, errors }) {
  const indexPath = resolve(dist, "routes-index.json");
  if (!(await fileExists(indexPath))) return;
  let parsed;
  try { parsed = JSON.parse(await readFile(indexPath, "utf8")); }
  catch (error) { errors.push(`Web Forms capability refresh could not parse routes-index.json: ${error.message}`); return; }
  const expectedMetadata = new Map([
    ["/capabilities/", { publicClaimLevel: "demo", sourceType: "site-page", hintCategory: "start", preferredProofPath: "/evidence/" }],
    ["/roadmap/", { publicClaimLevel: "concept", sourceType: "site-page", hintCategory: "roadmap", preferredProofPath: "/proof-paths/" }],
    ["/legacy-dotnet/evidence/", { publicClaimLevel: "concept", sourceType: "site-page", hintCategory: "evidence", preferredProofPath: "/legacy-evidence/" }],
    ["/legacy-modernization/evidence-map/", { publicClaimLevel: "concept", sourceType: "site-page", hintCategory: "roadmap", preferredProofPath: "/legacy-evidence/" }],
    ["/legacy-modernization/review-handoff/", { publicClaimLevel: "concept", sourceType: "site-page", hintCategory: "use-case", preferredProofPath: "/legacy-modernization/evidence-map/" }],
    ["/manager-packet/", { publicClaimLevel: "demo", sourceType: "site-page", hintCategory: "use-case", preferredProofPath: "/demo/proof-upgrades/" }],
    ["/manager-faq/", { publicClaimLevel: "concept", sourceType: "site-page", hintCategory: "use-case", preferredProofPath: "/proof-paths/" }],
    ["/proof-paths/for-managers/", { publicClaimLevel: "concept", sourceType: "site-page", hintCategory: "evidence", preferredProofPath: "/proof-paths/" }]
  ]);
  for (const [route, expected] of expectedMetadata) {
    const entry = parsed?.entries?.find((item) => item?.path === route);
    if (!entry) { errors.push(`Web Forms capability refresh discovery is missing route: ${route}`); continue; }
    if (!String(entry.summary ?? "").includes("Web Forms")) errors.push(`Web Forms capability refresh discovery summary is stale for: ${route}`);
    for (const [field, value] of Object.entries(expected)) {
      if (entry[field] !== value) errors.push(`Web Forms capability refresh discovery ${field} for ${route} must be ${value}.`);
    }
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
  return [...html.matchAll(/\b(?:content|data-[\w-]+|title|aria-label|href)\s*=\s*["']([^"']*)["']/gi)]
    .map((match) => decodeHtmlEntities(match[1]))
    .join(" ");
}
function escapeRegex(value) { return String(value).replace(/[.*+?^${}()|[\]\\]/g, "\\$&"); }
