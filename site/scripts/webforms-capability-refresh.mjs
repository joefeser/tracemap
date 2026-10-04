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
  /\bruntime (?:behavior|execution|reachability) (?:is|was) (?:proven|verified|guaranteed|confirmed)\b/i,
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
    const surfaces = [normalizeRenderedText(html), normalizeTagCollapsedText(html), normalizeAttributeValues(html)];
    for (const pattern of [...forbiddenMaterial, ...forbiddenClaims]) {
      if (surfaces.some((surface) => surfaceChunks(surface).some((chunk) => pattern.test(chunk)))) errors.push(`Web Forms capability refresh route ${route} contains forbidden public material or claim: ${pattern}`);
    }
  }

  validateCapabilityLadder(pages.get("/capabilities/"), errors);
  validateRoadmapLadder(pages.get("/roadmap/"), errors);
  validateLegacyLane(pages.get("/legacy-dotnet/evidence/"), errors);
  validateModernizationMap(pages.get("/legacy-modernization/evidence-map/"), errors);
  validateSupportingLadders(pages, errors);
  validateFutureBoundary(pages, errors);
  await validateDiscovery({ dist, errors });
}

function validateCapabilityLadder(html, errors) {
  if (!html) return;
  if (!/data-webforms-capability-audit=["']v1["']/i.test(html)) errors.push("Web Forms capability refresh is missing its capability audit marker.");
  const expected = new Map([
    ["guided-setup", { level: "shipped", label: "Shipped · guided terminal setup" }],
    ["source-compiled", { level: "concept", label: "Concept · source + compiled projection" }],
    ["local-demo", { level: "demo", label: "Demo · reproducible local corpus" }],
    ["review-workbench", { level: "demo", label: "Demo · review-workbench walkthrough" }]
  ]);
  for (const [name, { level, label }] of expected) {
    const pattern = new RegExp(`<article\\b(?=[^>]*data-webforms-capability=["']${escapeRegex(name)}["'])(?=[^>]*data-public-claim-level=["']${level}["'])[^>]*>`, "i");
    if (!pattern.test(html)) errors.push(`Web Forms capability refresh expected ${name} claim level ${level}.`);
    const article = extractMarkedElement(html, "article", "data-webforms-capability", name);
    if (!article || !normalizeRenderedText(article).includes(label)) errors.push(`Web Forms capability refresh expected ${name} visible claim label: ${label}`);
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
  const futureStatusPattern = /<tr\b(?=[^>]*id=["']claim-webforms-future-automation["'])(?=[^>]*data-evidence-status=["']future-only["'])(?=[^>]*data-wording-status=["']future-facing["'])[^>]*>/i;
  if (!futureStatusPattern.test(html)) errors.push("Web Forms capability refresh roadmap future automation row must retain future-only evidence and future-facing wording statuses.");
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

function validateSupportingLadders(pages, errors) {
  const expected = [
    ["/manager-packet/", "section", "data-webforms-manager-ladder", "v1", ["Shipped setup", "Concept projection", "Demo replay", "Demo review"]],
    ["/manager-faq/", "article", "data-webforms-faq", "claim-ladder", ["guided terminal setup is shipped", "local synthetic replay and review walkthrough are demo-level", "source-plus-compiled projection remains concept-level"]],
    ["/proof-paths/for-managers/", "tr", "id", "question-webforms-claim-level", ["shipped setup", "concept projection", "demo replay", "demo walkthrough"]],
    ["/legacy-modernization/review-handoff/", "tr", "data-handoff-row", "webforms-evidence-ladder", ["Shipped guided setup", "concept source-plus-compiled projection", "demo local corpus", "demo review walkthrough"]]
  ];
  for (const [route, tag, attribute, value, phrases] of expected) {
    const html = pages.get(route);
    if (!html) continue;
    const block = extractMarkedElement(html, tag, attribute, value);
    if (!block) {
      errors.push(`Web Forms capability refresh route ${route} is missing its claim-ladder block.`);
      continue;
    }
    const text = normalizeRenderedText(block);
    for (const phrase of phrases) {
      if (!text.includes(phrase)) errors.push(`Web Forms capability refresh route ${route} is missing ladder claim: ${phrase}`);
    }
  }
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
  const capabilitiesBlock = capabilities ? extractSectionFromMarker(capabilities, "data-webforms-future-boundary") : "";
  const roadmapBlock = roadmap ? extractMarkedElement(roadmap, "tr", "id", "claim-webforms-future-automation") : "";
  const boundaries = [
    ["/capabilities/", capabilitiesBlock, ["Still future"]],
    ["/roadmap/", roadmapBlock, ["future-only", "future-facing", "are not established"]]
  ];
  for (const [route, block, boundaryPhrases] of boundaries) {
    if (!block) {
      errors.push(`Web Forms capability refresh route ${route} is missing its future-only boundary block.`);
      continue;
    }
    const text = normalizeRenderedText(block);
    for (const phrase of boundaryPhrases) {
      if (!text.includes(phrase)) errors.push(`Web Forms capability refresh route ${route} is missing future-only boundary wording: ${phrase}`);
    }
    for (const term of futureTerms) {
      if (!text.includes(term)) errors.push(`Web Forms capability refresh route ${route} is missing future-work boundary: ${term}`);
    }
  }
  const capabilitiesText = capabilitiesBlock ? normalizeRenderedText(capabilitiesBlock) : "";
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
    const discoveryFields = [
      ["summary", [entry.summary]],
      ["limitations", entry.limitations],
      ["nonClaims", entry.nonClaims]
    ];
    for (const [field, values] of discoveryFields) {
      if (!Array.isArray(values)) continue;
      const surface = decodeHtmlEntities(values.map((value) => String(value ?? "")).join(" "));
      for (const pattern of [...forbiddenMaterial, ...forbiddenClaims]) {
        if (pattern.test(surface)) errors.push(`Web Forms capability refresh discovery ${field} for ${route} contains forbidden public material or claim: ${pattern}`);
      }
    }
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
function extractMarkedElement(html, tag, attribute, value) {
  const pattern = new RegExp(`<${tag}\\b(?=[^>]*\\b${escapeRegex(attribute)}=["']${escapeRegex(value)}["'])[^>]*>[\\s\\S]*?<\\/${tag}>`, "i");
  return html.match(pattern)?.[0] ?? "";
}
function extractSectionFromMarker(html, attribute) {
  const start = html.search(new RegExp(`<[^>]+\\b${escapeRegex(attribute)}\\b[^>]*>`, "i"));
  if (start < 0) return "";
  const end = html.indexOf("</section>", start);
  return end < 0 ? "" : html.slice(start, end);
}
function surfaceChunks(value) {
  const text = String(value);
  const chunks = [];
  for (let start = 0; start < text.length; start += 320) chunks.push(text.slice(Math.max(0, start - 192), start + 512));
  return chunks.length > 0 ? chunks : [""];
}
function normalizeTagCollapsedText(html) {
  let text = "";
  let tag = "";
  let insideTag = false;
  let quote = "";
  for (const char of String(html)) {
    if (!insideTag) {
      if (char === "<") {
        insideTag = true;
        tag = char;
      } else {
        text += char;
      }
      continue;
    }
    tag += char;
    if (quote) {
      if (char === quote) quote = "";
      continue;
    }
    if (char === '"' || char === "'") quote = char;
    else if (char === ">") {
      insideTag = false;
      if (/^<\s*\/?\s*(?:address|article|aside|blockquote|br|dd|div|dl|dt|fieldset|figcaption|figure|footer|form|h[1-6]|header|hr|li|main|nav|ol|p|pre|section|table|tbody|td|tfoot|th|thead|tr|ul)\b/i.test(tag)) text += " ";
    }
  }
  return decodeHtmlEntities(text);
}
function escapeRegex(value) { return String(value).replace(/[.*+?^${}()|[\]\\]/g, "\\$&"); }
