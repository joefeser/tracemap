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

export const stakeholderObjectionGuideRoute = "/questions/objections/";
export const stakeholderObjectionGuideRequiredLinks = [
  "/questions/",
  "/manager-faq/",
  "/limitations/",
  "/static-vs-runtime/",
  "/review-claim-checklist/",
  "/proof-paths/tour/",
  "/demo/manager-script/",
  "/capabilities/",
  "/proof-source-catalog/"
];
export const stakeholderObjectionGuideInboundRoutes = ["/questions/"];

const pageArtifact = "questions/objections/index.html";
const sitemapArtifact = "sitemap.xml";
const routesIndexArtifact = "routes-index.json";

const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "objection-to-evidence handoff",
  "Safe short answer",
  "Evidence to check",
  "Stop condition",
  "Next owner",
  "No. TraceMap can orient static repository evidence",
  "No. TraceMap evidence can inform review questions",
  "No. Static evidence can show code references or route surfaces",
  "No. Core scanner and reducer claims are deterministic",
  "No. Missing evidence is a gap or unknown",
  "No. Public sharing should use public-safe summaries",
  "TraceMap can point to the kind of owner needed",
  "Keep reduced coverage visible"
];

const requiredObjections = new Map([
  ["runtime-behavior", "Does this prove runtime behavior?"],
  ["release-approval", "Can I use this for release approval?"],
  ["production-traffic-performance", "Does this show production traffic or endpoint performance?"],
  ["ai-analysis", "Is this AI analysis?"],
  ["missing-evidence", "Does missing evidence mean no impact?"],
  ["raw-artifacts", "Can I share raw artifacts?"],
  ["next-owner", "Who owns the next answer?"],
  ["reduced-coverage", "What do we do under reduced coverage?"]
]);

const requiredAnchors = [
  "objection-runtime-behavior",
  "objection-release-approval",
  "objection-production-traffic",
  "objection-ai-analysis",
  "objection-missing-evidence",
  "objection-raw-artifacts",
  "objection-next-owner",
  "objection-reduced-coverage"
];

const requiredFields = new Set([
  "objection",
  "safeShortAnswer",
  "evidenceToCheck",
  "stopCondition",
  "nextOwner",
  "publicClaimLevel",
  "limitationNonClaim",
  "supportingRoute"
]);

const publicOwnerTerms = [
  "service owner",
  "runtime observability owner",
  "release owner",
  "test owner",
  "reviewer",
  "TraceMap owner",
  "security owner",
  "repository owner",
  "manager",
  "code reviewer",
  "build/tooling owner"
];

const expectedRouteMetadata = {
  publicClaimLevel: "concept",
  hintCategory: "use-case",
  sourceType: "site-page",
  preferredProofPath: "/proof-paths/"
};

const requiredMetadataNonClaims = [
  "runtime behavior",
  "production traffic",
  "endpoint performance",
  "outage cause",
  "release safety",
  "operational safety",
  "complete coverage",
  "release approval",
  "AI impact analysis",
  "LLM analysis",
  "embeddings",
  "vector databases",
  "prompt classification",
  "raw facts",
  "raw SQLite content",
  "analyzer logs",
  "raw command output",
  "credential-like values"
];

const forbiddenPrivatePatterns = [
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

const forbiddenRawMaterialPatterns = [
  /\bfacts\.ndjson\b/i,
  /\bindex\.sqlite\b/i,
  /\blogs\/analyzer\.log\b/i,
  /\banalyzer logs?\b/i,
  /\braw facts?\b/i,
  /\braw SQLite(?: content)?\b/i,
  /\braw source snippets?\b/i,
  /\braw SQL\b/i,
  /\bconfig values?\b/i,
  /\bsecrets?\b/i,
  /\blocal paths?\b/i,
  /\braw remotes?\b/i,
  /\bgenerated scan directories\b/i,
  /\bprivate sample names?\b/i,
  /\braw command output\b/i,
  /\bhidden validation details\b/i,
  /\bcredential-like values\b/i
];

const forbiddenPositioningPatterns = [
  /\bAI[- ]?powered\b/i,
  /\bAI impact analysis\b/i,
  /\bLLM[- ]?powered\b/i,
  /\bLLM analysis\b/i,
  /\bmachine learning impact analysis\b/i,
  /\bartificial intelligence impact analysis\b/i,
  /\bembedding[- ]?based impact\b/i,
  /\bvector database impact\b/i,
  /\bprompt[- ]?classification\b/i,
  /\bprompt[- ]?classified impact\b/i,
  /\bintelligent impact\b/i,
  /\bautomated release approval\b/i,
  /\brelease approval\b/i,
  /\boperational assurance\b/i,
  /\bruntime proof\b/i,
  /\bproduction proven\b/i,
  /\babsence of impact\b/i,
  /\bno impact proven\b/i
];

const forbiddenOverclaimPatterns = [
  /\b(?:proves?|guarantees?|certifies?|approves?|blocks?)\b/i,
  /\bsafe to release\b/i,
  /\bvalidated for release\b/i,
  /\bapproved for release\b/i,
  /\bdeployment[- ]safe\b/i,
  /\bproduction[- ]traffic\b/i,
  /\bendpoint[- ]performance\b/i,
  /\bruntime[- ]behavior\b/i,
  /\boutage[- ]cause\b/i,
  /\broot[- ]cause\b/i,
  /\bcomplete[- ]coverage\b/i,
  /\bno impact\b/i,
  /\bnot impacted\b/i,
  /\bautonomous[- ]approval\b/i
];

export async function validateStakeholderObjectionGuideDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors
}) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "questions", "objections", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence("Stakeholder objection guide page is missing required public route: /questions/objections/", pageArtifact));
    errors.push(...localErrors);
    return;
  }

  await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  await validateRoutesIndex({ dist, errors: localErrors });
  await validateObjectionGuidePage({ dist, pagePath, errors: localErrors });
  await validateInboundLinks({ dist, errors: localErrors });

  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${stakeholderObjectionGuideRoute}`)) {
    errors.push(withEvidence(`Stakeholder objection guide sitemap is missing required route: ${baseUrl}${stakeholderObjectionGuideRoute}`, sitemapArtifact));
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
    errors.push(withEvidence(`Stakeholder objection guide could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return;
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Stakeholder objection guide routes-index.json is invalid: expected entries array", routesIndexArtifact));
    return;
  }

  const routeEntry = parsed.entries.find((entry) => entry?.path === stakeholderObjectionGuideRoute);
  if (!routeEntry) {
    errors.push(withEvidence(`Stakeholder objection guide routes-index.json is missing required route: ${stakeholderObjectionGuideRoute}`, routesIndexArtifact));
    return;
  }

  for (const [field, expected] of Object.entries(expectedRouteMetadata)) {
    if (routeEntry[field] !== expected) {
      errors.push(withEvidence(`Stakeholder objection guide routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
    }
  }

  const nonClaimsText = Array.isArray(routeEntry.nonClaims) ? routeEntry.nonClaims.join(" ") : "";
  for (const phrase of requiredMetadataNonClaims) {
    if (!nonClaimsText.toLowerCase().includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Stakeholder objection guide routes-index.json nonClaims are missing boundary phrase: ${phrase}`, routesIndexArtifact));
    }
  }

  validateMetadataBoundaryText(routeEntry, errors);
}

async function validateObjectionGuidePage({ dist, pagePath, errors }) {
  const html = await readFile(pagePath, "utf8");
  const mainHtml = extractMainHtml(html);
  const decodedHtml = decodeHtmlEntities(html);
  const pageText = normalizeRenderedText(mainHtml);
  const lowerPageText = pageText.toLowerCase();
  const wordCount = countWords(normalizeRenderedText(stripNonBodyWords(mainHtml)));

  validatePageMetadata(html, errors);

  for (const phrase of requiredText) {
    if (!lowerPageText.includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Stakeholder objection guide page is missing required text: ${phrase}`, pageArtifact));
    }
  }

  for (const [family, objection] of requiredObjections) {
    if (!lowerPageText.includes(objection.toLowerCase())) {
      errors.push(withEvidence(`Stakeholder objection guide page is missing required objection: ${objection}`, pageArtifact));
    }

    if (!new RegExp(`data-objection-family\\s*=\\s*["']${escapeRegExp(family)}["']`, "i").test(html)) {
      errors.push(withEvidence(`Stakeholder objection guide page is missing required objection family: ${family}`, pageArtifact));
    }
  }

  for (const anchor of requiredAnchors) {
    if (!hasId(html, anchor)) {
      errors.push(withEvidence(`Stakeholder objection guide page is missing required row anchor: ${anchor}`, pageArtifact));
    }
  }

  for (const link of stakeholderObjectionGuideRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Stakeholder objection guide page is missing required link: ${link}`, pageArtifact));
    }
  }

  if (!/<table\b[\s\S]*?\bdata-stakeholder-objection-guide\b[\s\S]*?<th\b[^>]*scope=["']col["'][\s\S]*?Objection[\s\S]*?<\/table>/i.test(html)) {
    errors.push(withEvidence("Stakeholder objection guide is missing an accessible objection matrix table.", pageArtifact));
  }

  if (wordCount < 900 || wordCount > 2400) {
    errors.push(withEvidence(`Stakeholder objection guide word count must be between 900 and 2400 words, got ${wordCount}`, pageArtifact));
  }

  validateObjectionRows(html, errors);
  validateSupportingRoutesResolve({ dist, html, errors });
  validateForbiddenProofLinks(html, errors);
  validatePrivateAndRawBoundaryText({ decodedHtml, html, pageText, errors });
  validateClaimBoundaryText({ html, errors });
}

function validatePageMetadata(html, errors) {
  const checks = [
    [/Stakeholder Objection Guide \| TraceMap/i, "title"],
    [/name=["']description["'][^>]*content=["'][^"']*objection guide/i, "description"],
    [/rel=["']canonical["'][^>]*href=["']https:\/\/tracemap\.tools\/questions\/objections\//i, "canonical"],
    [/property=["']og:title["'][^>]*content=["']TraceMap Stakeholder Objection Guide["']/i, "Open Graph title"],
    [/property=["']og:url["'][^>]*content=["']https:\/\/tracemap\.tools\/questions\/objections\/["']/i, "Open Graph URL"]
  ];

  for (const [pattern, label] of checks) {
    if (!pattern.test(html)) {
      errors.push(withEvidence(`Stakeholder objection guide page metadata is missing or incorrect: ${label}`, pageArtifact));
    }
  }
}

function validateObjectionRows(html, errors) {
  const rows = extractRows(html, "data-objection-row");
  const seenFamilies = new Set();
  const ids = new Set();

  if (rows.length !== requiredObjections.size) {
    errors.push(withEvidence(`Stakeholder objection guide must have ${requiredObjections.size} objection rows, got ${rows.length}`, pageArtifact));
  }

  for (const row of rows) {
    const id = getAttribute(row.attributes, "id");
    const family = getAttribute(row.attributes, "data-objection-family");

    if (!id) {
      errors.push(withEvidence("Stakeholder objection guide row is missing a stable id anchor.", pageArtifact));
    } else if (ids.has(id)) {
      errors.push(withEvidence(`Stakeholder objection guide row has duplicate id: ${id}`, pageArtifact));
    } else {
      ids.add(id);
    }

    if (!requiredObjections.has(family)) {
      errors.push(withEvidence(`Stakeholder objection guide row has unexpected family: ${String(family)}`, pageArtifact));
    } else {
      seenFamilies.add(family);
    }

    const fields = extractCellsByField(row.body);
    for (const field of requiredFields) {
      if (!fields.has(field)) {
        errors.push(withEvidence(`Stakeholder objection guide ${family ?? "row"} is missing required field: ${field}`, pageArtifact));
        continue;
      }

      const text = normalizeRenderedText(fields.get(field));
      if (text.trim() === "") {
        errors.push(withEvidence(`Stakeholder objection guide ${family ?? "row"} has empty field: ${field}`, pageArtifact));
      }
    }

    const objection = fields.has("objection") ? normalizeRenderedText(fields.get("objection")) : "";
    if (family && requiredObjections.has(family) && objection !== requiredObjections.get(family)) {
      errors.push(withEvidence(`Stakeholder objection guide ${family} has unexpected objection title: ${objection}`, pageArtifact));
    }

    const level = fields.has("publicClaimLevel") ? normalizeRenderedText(fields.get("publicClaimLevel")) : "";
    if (level !== "concept") {
      errors.push(withEvidence(`Stakeholder objection guide ${family ?? "row"} must use row-level concept claim level, got: ${level}`, pageArtifact));
    }

    const supportingRoute = fields.get("supportingRoute") ?? "";
    if (!/<a\b[^>]*\bhref\s*=/i.test(supportingRoute)) {
      errors.push(withEvidence(`Stakeholder objection guide ${family ?? "row"} supportingRoute field must include a link.`, pageArtifact));
    }

    const owner = fields.has("nextOwner") ? normalizeRenderedText(fields.get("nextOwner")) : "";
    if (!publicOwnerTerms.some((term) => owner.toLowerCase().includes(term.toLowerCase()))) {
      errors.push(withEvidence(`Stakeholder objection guide ${family ?? "row"} has no recognized public owner category: ${owner}`, pageArtifact));
    }
  }

  for (const family of requiredObjections.keys()) {
    if (!seenFamilies.has(family)) {
      errors.push(withEvidence(`Stakeholder objection guide is missing required family: ${family}`, pageArtifact));
    }
  }
}

async function validateSupportingRoutesResolve({ dist, html, errors }) {
  for (const href of extractHrefs(html).filter((href) => href.startsWith("/"))) {
    if (!(await publicPathExists(dist, href))) {
      errors.push(withEvidence(`Stakeholder objection guide references missing supporting route: ${href}`, pageArtifact));
    }
  }
}

function validateForbiddenProofLinks(html, errors) {
  for (const href of extractHrefs(html)) {
    if (/\b(?:facts\.ndjson|index\.sqlite|logs\/analyzer\.log|analyzer\.log|scan-manifest\.json|report\.md)\b/i.test(href)) {
      errors.push(withEvidence(`Stakeholder objection guide links directly to forbidden proof target: ${href}`, pageArtifact));
    }
  }
}

function validatePrivateAndRawBoundaryText({ decodedHtml, html, pageText, errors }) {
  const unbounded = stripBoundedClaimContext(html);
  const scanText = `${normalizeRenderedText(unbounded)} ${decodeHtmlEntities(unbounded)} ${stripBoundedClaimContext(decodedHtml)}`;

  for (const pattern of forbiddenPrivatePatterns) {
    const match = scanText.match(pattern);
    if (match) {
      errors.push(withEvidence(`Stakeholder objection guide contains forbidden private or credential-like text outside a bounded non-shareable example: ${match[0]}`, pageArtifact));
    }
  }

  for (const pattern of forbiddenRawMaterialPatterns) {
    const match = scanText.match(pattern);
    if (match) {
      errors.push(withEvidence(`Stakeholder objection guide contains forbidden raw material text outside a bounded non-shareable example: ${match[0]}`, pageArtifact));
    }
  }
}

function validateClaimBoundaryText({ html, errors }) {
  const unbounded = stripBoundedClaimContext(html);
  const scanText = `${normalizeRenderedText(unbounded)} ${decodeHtmlEntities(unbounded)}`;

  for (const pattern of [...forbiddenPositioningPatterns, ...forbiddenOverclaimPatterns]) {
    const matches = [...scanText.matchAll(new RegExp(pattern.source, pattern.flags.includes("g") ? pattern.flags : `${pattern.flags}g`))];
    for (const match of matches) {
      errors.push(withEvidence(`Stakeholder objection guide contains forbidden unbounded claim wording: ${match[0]}`, pageArtifact));
    }
  }
}

function validateMetadataBoundaryText(routeEntry, errors) {
  const scanText = [
    routeEntry.title,
    routeEntry.summary,
    ...(Array.isArray(routeEntry.limitations) ? routeEntry.limitations : [])
  ]
    .filter((value) => typeof value === "string")
    .join(" ");

  for (const pattern of [...forbiddenPositioningPatterns, ...forbiddenOverclaimPatterns]) {
    const match = scanText.match(pattern);
    if (match) {
      errors.push(withEvidence(`Stakeholder objection guide routes-index.json contains forbidden unbounded claim wording: ${match[0]}`, routesIndexArtifact));
    }
  }
}

async function validateInboundLinks({ dist, errors }) {
  for (const route of stakeholderObjectionGuideInboundRoutes) {
    const pagePath = resolve(dist, ...route.replace(/^\/|\/$/g, "").split("/").filter(Boolean), "index.html");
    if (!(await fileExists(pagePath))) {
      errors.push(withEvidence(`Stakeholder objection guide inbound route is missing: ${route}`, pageArtifact));
      continue;
    }

    const html = await readFile(pagePath, "utf8");
    if (!hasHref(html, stakeholderObjectionGuideRoute)) {
      errors.push(withEvidence(`Stakeholder objection guide missing inbound link from ${route}`, route === "/" ? "index.html" : `${route.replace(/^\/|\/$/g, "")}/index.html`));
    }
  }
}

function stripBoundedClaimContext(html) {
  return String(html)
    .replace(/<table\b(?=[^>]*\bdata-stakeholder-objection-guide\b)[^>]*>[\s\S]*?<\/table>/gi, " ")
    .replace(/<section\b(?=[^>]*\bdata-objection-boundary\s*=\s*["'][^"']+["'])[^>]*>[\s\S]*?<\/section>/gi, " ");
}

function stripNonBodyWords(html) {
  return String(html)
    .replace(/<nav\b[\s\S]*?<\/nav>/gi, " ")
    .replace(/<header\b[\s\S]*?<\/header>/gi, " ")
    .replace(/<footer\b[\s\S]*?<\/footer>/gi, " ")
    .replace(/<thead\b[\s\S]*?<\/thead>/gi, " ");
}

function extractMainHtml(html) {
  const match = String(html).match(/<main\b[^>]*>([\s\S]*?)<\/main>/i);
  return match ? match[1] : html;
}

function extractRows(html, markerAttribute) {
  const rows = [];
  const pattern = /<tr\b([^>]*)>([\s\S]*?)<\/tr>/gi;
  const markerPattern = new RegExp(`\\b${escapeRegExp(markerAttribute)}\\b`, "i");

  for (const match of html.matchAll(pattern)) {
    if (markerPattern.test(match[1])) {
      rows.push({
        attributes: match[1],
        body: match[2]
      });
    }
  }

  return rows;
}

function extractCellsByField(rowHtml) {
  const cells = new Map();
  for (const match of rowHtml.matchAll(/<td\b([^>]*)>([\s\S]*?)<\/td>/gi)) {
    const field = getAttribute(match[1], "data-field");
    if (field) {
      cells.set(field, match[2]);
    }
  }
  return cells;
}

function extractHrefs(html) {
  return [...String(html).matchAll(/<a\b[^>]*\bhref\s*=\s*["']([^"']+)["']/gi)].map((match) => decodeHtmlEntities(match[1]));
}

async function publicPathExists(dist, pathname) {
  if (pathname === "/") {
    return fileExists(resolve(dist, "index.html"));
  }

  const parts = pathname.replace(/^\/|\/$/g, "").split("/").filter(Boolean);
  return fileExists(resolve(dist, ...parts, "index.html"));
}

function hasHref(html, href) {
  const escaped = escapeRegExp(href);
  return new RegExp(`<a\\b[^>]*\\bhref\\s*=\\s*["']${escaped}["']`, "i").test(html);
}

function hasId(html, id) {
  const escaped = escapeRegExp(id);
  return new RegExp(`\\bid\\s*=\\s*["']${escaped}["']`, "i").test(html);
}

function getAttribute(attributes, name) {
  const match = attributes.match(new RegExp(`\\b${escapeRegExp(name)}\\s*=\\s*["']([^"']+)["']`, "i"));
  return match ? decodeHtmlEntities(match[1]) : null;
}

function withEvidence(message, artifact) {
  return `${message} Evidence: ${artifact}.`;
}

function countWords(value) {
  return (value.match(/\b[\w'-]+\b/g) ?? []).length;
}
