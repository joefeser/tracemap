import { readFile } from "node:fs/promises";
import { basename, dirname, resolve } from "node:path";

import {
  decodeHtmlEntities,
  escapeRegExp,
  fileExists,
  normalizeBaseUrl,
  normalizeRenderedText,
  readSitemapLocSet
} from "./validate-utils.mjs";

export const reviewRoomDemoPathRoute = "/review-room/demo-path/";
export const reviewRoomDemoPathRequiredLinks = [
  "/review-room/",
  "/review-room/agenda/",
  "/proof-paths/",
  "/proof-paths/tour/",
  "/review-claim-checklist/",
  "/packets/",
  "/packets/assembly/",
  "/packets/examples/",
  "/limitations/",
  "/validation/",
  "/demo/",
  "/demo/start-here/",
  "/demo/evidence-trail/",
  "/demo/proof-assets/",
  "/demo/result/",
  "/demo/runbook/",
  "/demo/troubleshooting/",
  "/owners/follow-up/"
];

const pageArtifact = "review-room/demo-path/index.html";
const sitemapArtifact = "sitemap.xml";
const routesIndexArtifact = "routes-index.json";
const llmsArtifact = "llms.txt";
const implementationStateArtifact = ".kiro/specs/site-tracemap-tools-review-room-demo-path/implementation-state.md";

const requiredSteps = [
  "choose a static question",
  "open the review room",
  "inspect the agenda",
  "inspect proof paths",
  "inspect an evidence packet",
  "run the claim checklist",
  "check limitations and non-claims",
  "route unresolved questions to owners",
  "stop when evidence is insufficient"
];

const requiredStepFields = [
  "step label",
  "visitor action",
  "required evidence field or route",
  "allowed outcome",
  "limitation",
  "next owner or next route",
  "stop condition"
];

const allowedOutcomes = new Set(["continue", "downgrade", "owner follow-up", "internal only", "stop"]);
const placeholderValues = new Set(["", "-", "n/a", "na", "none", "see above"]);

const requiredProofFields = [
  "proof path",
  "rule ID or rule family",
  "evidence tier",
  "coverage label",
  "limitation",
  "non-claim",
  "public claim level",
  "public-safe source context",
  "next owner",
  "validation evidence",
  "Tier1Semantic",
  "Tier2Structural",
  "Tier3SyntaxOrTextual",
  "Tier4Unknown"
];

const requiredStopConditionTerms = [
  "proof path",
  "rule ID or rule family",
  "evidence tier",
  "coverage label",
  "limitation",
  "validation evidence",
  "private-only",
  "public-safe summary",
  "raw artifact",
  "runtime behavior",
  "production traffic",
  "endpoint performance",
  "outage cause",
  "release approval",
  "release safety",
  "operational safety",
  "AI impact analysis",
  "LLM analysis",
  "no next owner",
  "public-safe packet route"
];

const requiredOwnerLabels = [
  "evidence owner",
  "site owner",
  "demo owner",
  "source owner",
  "test owner",
  "runtime owner",
  "service owner",
  "database owner",
  "release reviewer",
  "validation owner",
  "documentation owner",
  "manager/reviewer owner"
];

const requiredDiscoveryTerms = [
  "concept",
  "guided path",
  "static question",
  "review-room",
  "proof-path",
  "packet",
  "checklist",
  "limitation",
  "owner",
  "stop-condition",
  "runtime proof",
  "AI impact analysis"
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

const rawMaterialPatterns = [
  /\braw facts?(?: streams?)?\b/i,
  /\bSQLite databases?\b/i,
  /\braw SQLite\b/i,
  /\banalyzer logs?\b/i,
  /\bsource snippets?\b/i,
  /\braw SQL\b/i,
  /\bconfiguration values?\b/i,
  /\bsecrets?\b/i,
  /\blocal(?: absolute)? paths?\b/i,
  /\braw(?: repository)? remotes?\b/i,
  /\bgenerated (?:scan )?directories\b/i,
  /\bprivate sample names?\b/i,
  /\bprivate owner names?\b/i,
  /\braw command output\b/i,
  /\bhidden validation details?\b/i,
  /\bcredential-like values?\b/i,
  /\bignored output paths?\b/i
];

const forbiddenPositiveClaimPatterns = [
  /\b(?:proves?|validates?|clears?|approves?|guarantees?)\s+(?:runtime|production|release|operational|endpoint|outage|safety|claim|decision)\b/i,
  /\b(?:AI|LLM|embedding|vector database|prompt)\b[^.]{0,80}\b(?:analysis|classification|reasoning|proof)\b/i,
  /\bautonomous\s+(?:review|approval)\b/i,
  /\bautomated management decisions?\b/i,
  /\b(?:complete|full)\s+(?:coverage|product coverage|workflow)\b/i
];

const unsupportedOutcomePatterns = [
  /\bimpacted\b/i,
  /(?<!public-)\bsafe\b/i,
  /\bapproved\b/i,
  /\broot cause\b/i,
  /\bcomplete\b/i,
  /\bproduction proven\b/i
];

export async function validateReviewRoomDemoPathDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors
}) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "review-room", "demo-path", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence(`Review room demo path is missing required public route: ${reviewRoomDemoPathRoute}`, pageArtifact));
    errors.push(...localErrors);
    return;
  }

  await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  const routeContext = await readRouteContext({ dist, errors: localErrors });
  await validatePage({ pagePath, routeContext, errors: localErrors });
  await validateDiscoveryOutputs({ dist, errors: localErrors });
  await validateImplementationState({ dist, errors: localErrors });

  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, sitemapArtifact);
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${reviewRoomDemoPathRoute}`)) {
    errors.push(withEvidence(`Review room demo path sitemap is missing required route: ${baseUrl}${reviewRoomDemoPathRoute}`, sitemapArtifact));
  }
}

async function readRouteContext({ dist, errors }) {
  const routesIndexPath = resolve(dist, routesIndexArtifact);
  const sitemapPath = resolve(dist, sitemapArtifact);
  const routes = new Set();
  const sitemapRoutes = new Set();
  let routeEntry = null;

  if (await fileExists(sitemapPath)) {
    for (const loc of await readSitemapLocSet(sitemapPath)) {
      try {
        sitemapRoutes.add(normalizeRouteHref(new URL(loc).pathname));
      } catch {
        // The aggregate validator reports malformed sitemap URLs separately.
      }
    }
  }

  if (!(await fileExists(routesIndexPath))) {
    return { routeEntry, routes, sitemapRoutes };
  }

  let parsed;
  try {
    parsed = JSON.parse(await readFile(routesIndexPath, "utf8"));
  } catch (error) {
    errors.push(withEvidence(`Review room demo path could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return { routeEntry, routes, sitemapRoutes };
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Review room demo path routes-index.json is invalid: expected entries array", routesIndexArtifact));
    return { routeEntry, routes, sitemapRoutes };
  }

  for (const entry of parsed.entries) {
    if (typeof entry?.path === "string") {
      routes.add(normalizeRouteHref(entry.path));
    }

    if (entry?.path === reviewRoomDemoPathRoute) {
      routeEntry = entry;
    }
  }

  validateRouteEntry(routeEntry, errors);
  return { routeEntry, routes, sitemapRoutes };
}

function validateRouteEntry(routeEntry, errors) {
  if (!routeEntry) {
    errors.push(withEvidence(`Review room demo path routes-index.json is missing route: ${reviewRoomDemoPathRoute}`, routesIndexArtifact));
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
      errors.push(withEvidence(`Review room demo path routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
    }
  }

  const metadataText = normalizeRenderedText(JSON.stringify(routeEntry));
  for (const term of requiredDiscoveryTerms) {
    if (!metadataText.includes(term)) {
      errors.push(withEvidence(`Review room demo path discovery metadata is missing required term: ${term}`, routesIndexArtifact));
    }
  }
}

async function validatePage({ pagePath, routeContext, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const pageText = normalizeRenderedText(html);
  const scopedHtml = stripAllowedBoundaryRegions(decodedHtml);
  const scopedText = normalizeRenderedText(scopedHtml);
  const wordCount = countWords(extractVisibleBodyProse(html));

  validateVisibleText(pageText, errors);
  validateMetadata(html, errors);
  validateGuidedPathTable(html, errors);
  validateProofFields(html, errors);
  validateStopConditions(html, errors);
  validateOwnerRouting(html, errors);
  validateAdjacentLinks(html, routeContext, errors);
  validateBoundaryRegions(html, errors);
  validateForbiddenMaterial({ decodedHtml, scopedHtml, scopedText, errors });
  validateUnsupportedPositiveClaims(publicSafetyScanValues(scopedHtml, scopedText), errors);
  validateWordCount(wordCount, errors);
}

function validateVisibleText(pageText, errors) {
  for (const phrase of [
    "Public claim level: concept",
    "No public conclusion without evidence",
    "choose a static question",
    "inspect an evidence packet",
    "stop when evidence is insufficient",
    "Insufficient evidence prevents a public conclusion"
  ]) {
    if (!pageText.includes(phrase)) {
      errors.push(withEvidence(`Review room demo path is missing required visible text: ${phrase}`, pageArtifact));
    }
  }
}

function validateMetadata(html, errors) {
  const checks = [
    /<title>Review Room Demo Path \| TraceMap<\/title>/i,
    /<link\b[^>]*rel=["']canonical["'][^>]*href=["']https:\/\/tracemap\.tools\/review-room\/demo-path\/["']/i,
    /<meta\b[^>]*property=["']og:type["'][^>]*content=["']article["']/i,
    /<meta\b[^>]*property=["']og:title["'][^>]*content=["']TraceMap Review Room Demo Path["']/i,
    /<meta\b[^>]*property=["']og:url["'][^>]*content=["']https:\/\/tracemap\.tools\/review-room\/demo-path\/["']/i
  ];

  for (const pattern of checks) {
    if (!pattern.test(html)) {
      errors.push(withEvidence("Review room demo path is missing required standalone route metadata.", pageArtifact));
    }
  }
}

function validateGuidedPathTable(html, errors) {
  const table = html.match(/<table\b(?=[^>]*\bdata-review-demo-path-table\b)[^>]*>[\s\S]*?<\/table>/i)?.[0];
  if (!table) {
    errors.push(withEvidence("Review room demo path is missing data-review-demo-path-table.", pageArtifact));
    return;
  }

  for (const header of [
    "Step label",
    "Visitor action",
    "Required evidence field or route",
    "Allowed outcome",
    "Limitation",
    "Next owner or next route",
    "Stop condition"
  ]) {
    const pattern = new RegExp(`<th\\b[^>]*\\bscope=["']col["'][^>]*>\\s*${escapeForPattern(header)}\\s*</th>`, "i");
    if (!pattern.test(table)) {
      errors.push(withEvidence(`Review room demo path table is missing column header: ${header}`, pageArtifact));
    }
  }

  const rows = extractTaggedElements(table, "tr", "data-review-demo-step");
  const actualSteps = rows.map((row) => getAttribute(row.attributes, "data-review-demo-step"));

  if (actualSteps.length !== requiredSteps.length) {
    errors.push(withEvidence(`Review room demo path must render ${requiredSteps.length} contiguous guided steps, got ${actualSteps.length}.`, pageArtifact));
  }

  requiredSteps.forEach((step, index) => {
    if (actualSteps[index] !== step) {
      errors.push(withEvidence(`Review room demo path step ${index + 1} expected "${step}", got "${String(actualSteps[index])}".`, pageArtifact));
    }
  });

  rows.forEach((row) => validateStepRow(row, errors));

  if (actualSteps.at(-1) !== "stop when evidence is insufficient") {
    errors.push(withEvidence("Review room demo path final guided step must be stop when evidence is insufficient.", pageArtifact));
  }
}

function validateStepRow(row, errors) {
  const step = getAttribute(row.attributes, "data-review-demo-step");
  const fields = extractFieldValues(row.full);

  for (const field of requiredStepFields) {
    if (!fields.has(field)) {
      errors.push(withEvidence(`Review room demo path step "${step}" is missing required field: ${field}`, pageArtifact));
      continue;
    }

    const value = fields.get(field);
    if (placeholderValues.has(normalizePlaceholder(value))) {
      errors.push(withEvidence(`Review room demo path step "${step}" has empty or placeholder field: ${field}`, pageArtifact));
    }
  }

  const outcome = fields.get("allowed outcome") ?? "";
  if (!allowedOutcomes.has(outcome)) {
    errors.push(withEvidence(`Review room demo path step "${step}" has unsupported outcome: ${outcome}`, pageArtifact));
  }

  const rowText = normalizeRenderedText(row.full);
  for (const pattern of unsupportedOutcomePatterns) {
    if (pattern.test(rowText) && !/\bpublic-safe\b/i.test(rowText)) {
      errors.push(withEvidence(`Review room demo path step "${step}" contains forbidden unqualified outcome wording: ${pattern}`, pageArtifact));
    }
  }
}

function validateProofFields(html, errors) {
  const section = extractSectionById(html, "proof-packet-fields");
  const text = normalizeRenderedText(section);

  for (const field of requiredProofFields) {
    if (!text.includes(field)) {
      errors.push(withEvidence(`Review room demo path proof and packet fields missing term: ${field}`, pageArtifact));
    }
  }
}

function validateStopConditions(html, errors) {
  const section = extractSectionById(html, "stop-conditions");
  const text = normalizeRenderedText(section);

  for (const term of requiredStopConditionTerms) {
    if (!text.includes(term)) {
      errors.push(withEvidence(`Review room demo path stop conditions missing trigger: ${term}`, pageArtifact));
    }
  }
}

function validateOwnerRouting(html, errors) {
  const rows = extractTaggedElements(html, "tr", "data-review-demo-step");
  const ownerRow = rows.find((row) => getAttribute(row.attributes, "data-review-demo-step") === "route unresolved questions to owners")?.full ?? "";
  const ownerText = normalizeRenderedText(ownerRow);

  for (const label of requiredOwnerLabels) {
    if (!ownerText.includes(label)) {
      errors.push(withEvidence(`Review room demo path owner-routing step is missing role label: ${label}`, pageArtifact));
    }
  }

  for (const phrase of ["does not prove, approve, diagnose, validate, or clear a claim", "role labels only"]) {
    if (!ownerText.includes(phrase)) {
      errors.push(withEvidence(`Review room demo path owner-routing step is missing required phrase: ${phrase}`, pageArtifact));
    }
  }
}

function validateAdjacentLinks(html, routeContext, errors) {
  for (const href of reviewRoomDemoPathRequiredLinks) {
    if (!hasHref(html, href)) {
      errors.push(withEvidence(`Review room demo path is missing required link: ${href}`, pageArtifact));
    }

    if (!routeContext.routes.has(href) || !routeContext.sitemapRoutes.has(href)) {
      errors.push(withEvidence(`Review room demo path required link does not resolve in generated output: ${href}`, pageArtifact));
    }
  }

  const packetStep = extractTaggedElements(html, "tr", "data-review-demo-step")
    .find((row) => getAttribute(row.attributes, "data-review-demo-step") === "inspect an evidence packet")?.full ?? "";
  if (!["/packets/", "/packets/assembly/", "/packets/examples/"].some((href) => hasHref(packetStep, href))) {
    errors.push(withEvidence("Review room demo path evidence-packet step must link at least one packet route.", pageArtifact));
  }
}

function validateBoundaryRegions(html, errors) {
  for (const [id, boundary] of [
    ["stop-conditions", "stop-conditions"],
    ["non-claims", "non-claims"]
  ]) {
    const pattern = new RegExp(`<section\\b(?=[^>]*\\bid=["']${escapeForPattern(id)}["'])(?=[^>]*\\bdata-review-demo-path-boundary=["']${escapeForPattern(boundary)}["'])[^>]*>`, "i");
    if (!pattern.test(html)) {
      errors.push(withEvidence(`Review room demo path section ${id} must use data-review-demo-path-boundary="${boundary}".`, pageArtifact));
    }
  }
}

function validateForbiddenMaterial({ decodedHtml, scopedHtml, scopedText, errors }) {
  const hardPrivateValues = privateMaterialScanValues(decodedHtml);
  const rawMaterialValues = privateMaterialScanValues(scopedHtml);

  for (const pattern of hardPrivatePatterns) {
    if (hardPrivateValues.some((value) => pattern.test(value))) {
      errors.push(withEvidence(`Review room demo path contains hard private material: ${pattern}`, pageArtifact));
    }
  }

  for (const pattern of rawMaterialPatterns) {
    if (rawMaterialValues.some((value) => pattern.test(value))) {
      errors.push(withEvidence(`Review room demo path contains forbidden raw/private material outside bounded contexts: ${pattern}`, pageArtifact));
    }
  }

  if (/\b(?:Codex|Qodo|Gemini|Sourcery)\b/i.test(scopedText)) {
    errors.push(withEvidence("Review room demo path contains private reviewer labels outside bounded contexts.", pageArtifact));
  }
}

function validateUnsupportedPositiveClaims(values, errors) {
  for (const pattern of forbiddenPositiveClaimPatterns) {
    if (values.some((value) => pattern.test(value))) {
      errors.push(withEvidence(`Review room demo path contains forbidden positive claim outside bounded contexts: ${pattern}`, pageArtifact));
    }
  }
}

function validateWordCount(wordCount, errors) {
  if (wordCount < 700 || wordCount > 1800) {
    errors.push(withEvidence(`Review room demo path visible prose word count must be between 700 and 1800 words, got ${wordCount}.`, pageArtifact));
  }
}

async function validateDiscoveryOutputs({ dist, errors }) {
  for (const artifact of [routesIndexArtifact, llmsArtifact]) {
    const path = resolve(dist, artifact);
    if (!(await fileExists(path))) {
      errors.push(withEvidence(`Review room demo path discovery output is missing: ${artifact}`, artifact));
      continue;
    }

    const text = await readFile(path, "utf8");
    if (!text.includes(reviewRoomDemoPathRoute) || !text.includes("Review Room Demo Path")) {
      errors.push(withEvidence(`Review room demo path discovery output does not include the route and title: ${artifact}`, artifact));
    }
  }
}

async function validateImplementationState({ dist, errors }) {
  const statePath = await firstExistingFile(implementationStateCandidatePaths(dist));
  if (!statePath) {
    errors.push(withEvidence("Review room demo path implementation-state.md is missing.", implementationStateArtifact));
    return;
  }

  const state = await readFile(statePath, "utf8");
  for (const phrase of [
    "Implementation branch: `codex/impl-site-review-room-demo-path-20260626095826`",
    "Selected placement: `/review-room/demo-path/`",
    "Rejected alternative: section on `/review-room/`",
    "Rejected alternative: section on `/review-room/agenda/`",
    "Rejected alternative: section on `/demo/start-here/`",
    "Primary navigation remains unchanged.",
    "All preferred adjacent routes exist at implementation time.",
    "Evidence-packet routes present: `/packets/`, `/packets/assembly/`, `/packets/examples/`",
    "Browser sanity:"
  ]) {
    if (!state.includes(phrase)) {
      errors.push(withEvidence(`Review room demo path implementation-state is missing phrase: ${phrase}`, implementationStateArtifact));
    }
  }
}

function implementationStateCandidatePaths(dist) {
  const siteDistCandidate = resolve(dist, "..", "..", implementationStateArtifact);
  if (basename(dirname(dist)) === "site") {
    return [siteDistCandidate];
  }

  return [
    siteDistCandidate,
    resolve(dist, "..", implementationStateArtifact)
  ];
}

async function firstExistingFile(paths) {
  for (const path of paths) {
    if (await fileExists(path)) {
      return path;
    }
  }

  return null;
}

function extractVisibleBodyProse(html) {
  const mainHtml = html.match(/<main\b[^>]*>([\s\S]*?)<\/main>/i)?.[1] ?? "";
  const withoutTable = mainHtml.replace(/<table\b(?=[^>]*\bdata-review-demo-path-table\b)[^>]*>[\s\S]*?<\/table>/gi, " ");
  const withoutCode = withoutTable.replace(/<code\b[^>]*>[\s\S]*?<\/code>/gi, " ");
  return normalizeRenderedText(withoutCode);
}

function privateMaterialScanValues(value) {
  const decoded = decodeHtmlEntities(value);
  return [
    decoded,
    normalizeRenderedText(decoded),
    decoded.replace(/<[^>]+>/g, "")
  ];
}

function publicSafetyScanValues(scopedHtml, scopedText) {
  const values = [scopedText];
  for (const match of scopedHtml.matchAll(/\b(?:content|title|aria-label|alt)=["']([^"']*)["']/gi)) {
    values.push(normalizeRenderedText(decodeHtmlEntities(match[1])));
  }
  for (const match of scopedHtml.matchAll(/<title\b[^>]*>([\s\S]*?)<\/title>/gi)) {
    values.push(normalizeRenderedText(decodeHtmlEntities(match[1])));
  }
  return [...new Set(values.filter((value) => value.length > 0))];
}

function stripAllowedBoundaryRegions(html) {
  let output = html;
  for (const boundary of ["non-claims", "stop-conditions"]) {
    output = stripElementsWithAttribute(output, "data-review-demo-path-boundary", boundary);
  }
  return output;
}

function stripElementsWithAttribute(html, attribute, value) {
  let output = html;
  const openPattern = new RegExp(`<([a-z][a-z0-9-]*)\\b(?=[^>]*\\b${attribute}=["']${escapeForPattern(value)}["'])[^>]*>`, "i");
  let match;

  while ((match = output.match(openPattern))) {
    const start = match.index ?? 0;
    const end = findElementEnd(output, start, match[1]);
    output = `${output.slice(0, start)} ${output.slice(end)}`;
  }

  return output;
}

function findElementEnd(html, start, tagName) {
  const normalizedTagName = tagName.toLowerCase();
  let depth = 0;
  let firstTagLength = 0;
  let cursor = start;

  while (cursor < html.length) {
    const tagStart = html.indexOf("<", cursor);
    if (tagStart === -1) {
      break;
    }

    if (html.startsWith("<!--", tagStart)) {
      const commentEnd = html.indexOf("-->", tagStart + 4);
      cursor = commentEnd === -1 ? html.length : commentEnd + 3;
      continue;
    }

    const tagEnd = html.indexOf(">", tagStart + 1);
    if (tagEnd === -1) {
      break;
    }

    const token = html.slice(tagStart + 1, tagEnd).trim();
    const parsed = parseTagToken(token);
    if (!parsed || parsed.name !== normalizedTagName) {
      cursor = tagEnd + 1;
      continue;
    }

    if (firstTagLength === 0) {
      firstTagLength = tagEnd + 1 - tagStart;
    }

    if (parsed.closing) {
      depth -= 1;
      if (depth === 0) {
        return tagEnd + 1;
      }
    } else if (!parsed.selfClosing) {
      depth += 1;
    }

    cursor = tagEnd + 1;
  }

  return start + firstTagLength;
}

function parseTagToken(token) {
  if (token === "" || token.startsWith("!") || token.startsWith("?")) {
    return null;
  }

  const closing = token.startsWith("/");
  const body = closing ? token.slice(1).trimStart() : token;
  const name = body.match(/^[a-z][a-z0-9-]*/i)?.[0]?.toLowerCase();
  if (!name) {
    return null;
  }

  return {
    closing,
    name,
    selfClosing: /\/\s*$/.test(body)
  };
}

function extractTaggedElements(html, tagName, attributeName) {
  const pattern = new RegExp(`<${tagName}\\b(?=[^>]*\\b${attributeName}=)([^>]*)>[\\s\\S]*?<\\/${tagName}>`, "gi");
  return [...html.matchAll(pattern)].map((match) => ({
    attributes: match[1],
    full: match[0]
  }));
}

function extractFieldValues(html) {
  const values = new Map();
  const fieldPattern = /<(?:td|th)\b(?=[^>]*\bdata-field=["']([^"']+)["'])[^>]*>([\s\S]*?)<\/(?:td|th)>/gi;

  for (const match of html.matchAll(fieldPattern)) {
    values.set(decodeHtmlEntities(match[1]), normalizeRenderedText(match[2]));
  }

  return values;
}

function extractSectionById(html, id) {
  const openPattern = new RegExp(`<section\\b(?=[^>]*\\bid=["']${escapeForPattern(id)}["'])[^>]*>`, "i");
  const match = html.match(openPattern);
  if (!match) {
    return "";
  }

  const start = match.index ?? 0;
  return html.slice(start, findElementEnd(html, start, "section"));
}

function getAttribute(attributes, name) {
  return decodeHtmlEntities(attributes.match(new RegExp(`\\b${escapeForPattern(name)}=["']([^"']+)["']`, "i"))?.[1] ?? "");
}

function hasHref(html, href) {
  return new RegExp(`<a\\b[^>]*\\bhref\\s*=\\s*["']${escapeForPattern(href)}["']`, "i").test(html);
}

function normalizeRouteHref(value) {
  if (typeof value !== "string" || value.trim() === "") {
    return "";
  }

  if (/^https?:\/\//i.test(value)) {
    try {
      return normalizeRouteHref(new URL(value).pathname);
    } catch {
      return "";
    }
  }

  const withoutQueryAndHash = value.split("#")[0].split("?")[0];
  if (!withoutQueryAndHash.startsWith("/")) {
    return "";
  }

  return withoutQueryAndHash.endsWith("/") ? withoutQueryHref(withoutQueryAndHash) : `${withoutQueryAndHash}/`;
}

function withoutQueryHref(value) {
  return value;
}

function normalizePlaceholder(value) {
  return value.trim().toLowerCase();
}

function countWords(text) {
  return (text.match(/[A-Za-z0-9]+(?:[-'][A-Za-z0-9]+)*/g) ?? []).length;
}

function escapeForPattern(value) {
  return escapeRegExp(value);
}

function withEvidence(message, artifact) {
  return `${message} [${artifact}]`;
}
