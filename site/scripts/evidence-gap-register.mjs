import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import {
  decodeHtmlEntities,
  fileExists,
  normalizeBaseUrl,
  normalizeRenderedText,
  readSitemapLocSet
} from "./validate-utils.mjs";

export const evidenceGapRegisterRoute = "/evidence/gaps/";
export const evidenceGapRegisterRequiredLinks = [
  "/limitations/reduced-coverage/",
  "/limitations/",
  "/validation/",
  "/questions/objections/",
  "/owners/follow-up/",
  "/decisions/evidence-record/",
  "/review-claim-checklist/"
];
export const evidenceGapRegisterInboundRoutes = ["/evidence/", "/limitations/reduced-coverage/"];

const pageArtifact = "evidence/gaps/index.html";
const sitemapArtifact = "sitemap.xml";
const routesIndexArtifact = "routes-index.json";
const llmsArtifact = "llms.txt";
const implementationStateArtifact = ".kiro/specs/site-tracemap-tools-evidence-gap-register/implementation-state.md";

const requiredSections = new Map([
  ["when-a-gap-is-useful", "when a gap is useful"],
  ["gap-register-fields", "gap register fields"],
  ["example-gap-rows", "example gap rows"],
  ["stop-conditions", "stop conditions"],
  ["next-owner-handoff", "next-owner handoff"],
  ["safe-wording", "safe wording"],
  ["unsafe-wording", "unsafe wording"],
  ["non-claims", "non-claims"],
  ["adjacent-surfaces", "adjacent surfaces"]
]);

const requiredRowFields = [
  "gap label",
  "what evidence exists",
  "what cannot be concluded",
  "public claim level",
  "next owner",
  "proof/validation route",
  "safe wording",
  "stop condition"
];

const requiredRows = new Map([
  ["missing proof path", ["unavailable proof path cannot support a public proof-link claim", "Public proof"]],
  ["reduced coverage", ["reduced coverage cannot support clean, complete, release-ready, or absence-of-impact wording", "Clean repo"]],
  ["Tier4Unknown", ["unknown evidence cannot be upgraded by confidence, repetition, reviewer seniority, or pressure", "confidence, repetition, reviewer seniority, or pressure"]],
  ["private-only support", ["private-only evidence cannot be cited as public proof until summarized through a public-safe route", "Public proof"]],
  ["stale commit", ["stale source context cannot support current-head, current-release, or current-proof wording", "Current-head proof"]],
  ["unsupported framework surface", ["unsupported framework evidence cannot support complete framework or route coverage", "Complete framework coverage"]],
  ["missing validation evidence", ["absent validation cannot support validation-passed, demo-backed, or implementation-ready wording", "Validation passed"]],
  ["unresolved owner question", ["an unanswered owner question cannot be converted into an assumption of safety or no impact", "Owner agreement"]]
]);

const adjacentDistinctions = new Map([
  ["/limitations/reduced-coverage/", "Reduced coverage playbook"],
  ["/limitations/", "Limitations and non-claims"],
  ["/validation/", "Validation evidence"],
  ["/questions/objections/", "Stakeholder objection guide"],
  ["/owners/follow-up/", "Owner follow-up map"],
  ["/decisions/evidence-record/", "Evidence decision record"],
  ["/review-claim-checklist/", "Review claim checklist"]
]);

const allowedProofRoutes = new Set([
  "/limitations/reduced-coverage/",
  "/limitations/",
  "/validation/",
  "/owners/follow-up/",
  "/decisions/evidence-record/",
  "/review-claim-checklist/"
]);

const requiredDiscoveryTerms = [
  "publicClaimLevel",
  "concept",
  "missing",
  "reduced",
  "stale",
  "private-only",
  "unsupported",
  "unknown",
  "validation",
  "owner-question"
];

const forbiddenPositiveClaimPatterns = [
  /\b(?:proves?|guarantees?|certifies?|validates?)\s+(?:there is no impact|no impact|absence of impact|runtime behavior|production traffic|endpoint performance|outage cause|release readiness|release safety|operational certainty|complete coverage|clean repo)\b/i,
  /\b(?:release|operation|runtime|production|endpoint|outage)\b[^.]{0,70}\b(?:approved|ready|safe|proven|certified|guaranteed)\b/i,
  /\b(?:AI|LLM|embedding|vector database|prompt)\b[^.]{0,70}\b(?:impact analysis|analysis|classification|search|reasoning)\b/i,
  /\breplaces?\s+(?:human review|tests|source review|code review|runtime observability|release controls|human judgment)\b/i,
  /\bautonomous approval\b/i
];

const rawMaterialPatterns = [
  /\bfacts\.ndjson\b/i,
  /\bindex\.sqlite\b/i,
  /\blogs\/analyzer\.log\b/i,
  /\braw facts?\b/i,
  /\braw SQLite(?: content)?\b/i,
  /\banalyzer logs?\b/i,
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
  /\bcredential-like values?\b/i
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

const blamePatterns = [
  /\bfault of\b/i,
  /\bat fault\b/i,
  /\bblame\b/i,
  /\bbad (?:team|vendor|consultant|code|reviewer)\b/i,
  /\b(?:team|vendor|consultant|reviewer|owner)\s+(?:failed|broke|hid|ignored)\b/i
];

export async function validateEvidenceGapRegisterDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors
}) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "evidence", "gaps", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence(`Evidence gap register is missing required public route: ${evidenceGapRegisterRoute}`, pageArtifact));
    errors.push(...localErrors);
    return;
  }

  await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  const routeContext = await readRouteContext({ dist, errors: localErrors });
  await validatePage({ pagePath, routeContext, errors: localErrors });
  await validateInboundLinks({ dist, errors: localErrors });
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
  if (!sitemapUrls.has(`${baseUrl}${evidenceGapRegisterRoute}`)) {
    errors.push(withEvidence(`Evidence gap register sitemap is missing required route: ${baseUrl}${evidenceGapRegisterRoute}`, sitemapArtifact));
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
    errors.push(withEvidence(`Evidence gap register could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return { routeEntry, routes, sitemapRoutes };
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Evidence gap register routes-index.json is invalid: expected entries array", routesIndexArtifact));
    return { routeEntry, routes, sitemapRoutes };
  }

  for (const entry of parsed.entries) {
    if (typeof entry?.path === "string") {
      routes.add(normalizeRouteHref(entry.path));
    }

    if (entry?.path === evidenceGapRegisterRoute) {
      routeEntry = entry;
    }
  }

  validateRouteEntry(routeEntry, errors);
  return { routeEntry, routes, sitemapRoutes };
}

function validateRouteEntry(routeEntry, errors) {
  if (!routeEntry) {
    errors.push(withEvidence(`Evidence gap register routes-index.json is missing route: ${evidenceGapRegisterRoute}`, routesIndexArtifact));
    return;
  }

  const expectedFields = {
    publicClaimLevel: "concept",
    hintCategory: "evidence",
    sourceType: "site-page",
    preferredProofPath: "/review-claim-checklist/"
  };

  for (const [field, expected] of Object.entries(expectedFields)) {
    if (routeEntry[field] !== expected) {
      errors.push(withEvidence(`Evidence gap register routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
    }
  }

  const metadataText = normalizeRenderedText(
    JSON.stringify({
      title: routeEntry.title,
      summary: routeEntry.summary,
      publicClaimLevel: routeEntry.publicClaimLevel,
      limitations: routeEntry.limitations,
      nonClaims: routeEntry.nonClaims
    })
  );

  for (const term of requiredDiscoveryTerms) {
    if (!metadataText.includes(term)) {
      errors.push(withEvidence(`Evidence gap register discovery metadata is missing required term: ${term}`, routesIndexArtifact));
    }
  }
}

async function validatePage({ pagePath, routeContext, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const pageText = normalizeRenderedText(html);
  const scopedHtml = stripAllowedBoundaryRegions(stripGapRows(decodedHtml));
  const scopedText = normalizeRenderedText(scopedHtml);
  const wordCount = countWords(extractVisibleBodyProse(html));

  validateVisibleText(pageText, errors);
  validateMetadata(html, errors);
  validateSections(html, errors);
  validateRows(html, routeContext, errors);
  validateAdjacentLinks(html, routeContext, errors);
  validateBoundaryRegions(html, errors);
  validateForbiddenMaterial({ decodedHtml, scopedHtml, scopedText, errors });
  validateBlame(scopedText, errors);
  validateWordCount(wordCount, errors);
}

function validateVisibleText(pageText, errors) {
  for (const phrase of [
    "Public claim level: concept",
    "No public conclusion without evidence",
    "bounded follow-up item"
  ]) {
    if (!pageText.includes(phrase)) {
      errors.push(withEvidence(`Evidence gap register is missing required visible text: ${phrase}`, pageArtifact));
    }
  }
}

function validateMetadata(html, errors) {
  const checks = [
    /<title>Evidence Gap Register \| TraceMap<\/title>/i,
    /<link\b[^>]*rel=["']canonical["'][^>]*href=["']https:\/\/tracemap\.tools\/evidence\/gaps\/["']/i,
    /<meta\b[^>]*property=["']og:type["'][^>]*content=["']article["']/i,
    /<meta\b[^>]*property=["']og:url["'][^>]*content=["']https:\/\/tracemap\.tools\/evidence\/gaps\/["']/i
  ];

  for (const pattern of checks) {
    if (!pattern.test(html)) {
      errors.push(withEvidence("Evidence gap register is missing required standalone route metadata.", pageArtifact));
    }
  }
}

function validateSections(html, errors) {
  const ids = extractIds(html);

  for (const [id, label] of requiredSections) {
    if (!ids.has(id)) {
      errors.push(withEvidence(`Evidence gap register is missing required section: ${label}`, pageArtifact));
    }
  }

  for (const id of findDuplicateIds(html)) {
    errors.push(withEvidence(`Evidence gap register contains duplicate id: ${id}`, pageArtifact));
  }
}

function validateRows(html, routeContext, errors) {
  const table = html.match(/<table\b(?=[^>]*\bdata-evidence-gap-register\b)[^>]*>[\s\S]*?<\/table>/i)?.[0];
  if (!table) {
    errors.push(withEvidence("Evidence gap register is missing data-evidence-gap-register table.", pageArtifact));
    return;
  }

  if (!/<caption\b[\s\S]*?illustrative public-safe[\s\S]*?<\/caption>/i.test(table)) {
    errors.push(withEvidence("Evidence gap register table must label rows as illustrative public-safe examples.", pageArtifact));
  }

  for (const header of requiredRowFields) {
    const pattern = new RegExp(`<th\\b[^>]*\\bscope=["']col["'][^>]*>\\s*${escapeForPattern(header)}\\s*</th>`, "i");
    if (!pattern.test(table)) {
      errors.push(withEvidence(`Evidence gap register table is missing column header: ${header}`, pageArtifact));
    }
  }

  const rows = extractTaggedElements(table, "tr", "data-evidence-gap-row");
  const seen = new Set();

  for (const row of rows) {
    const rowName = getAttribute(row.attributes, "data-evidence-gap-row");
    if (!requiredRows.has(rowName)) {
      errors.push(withEvidence(`Evidence gap register has unexpected row: ${String(rowName)}`, pageArtifact));
      continue;
    }

    seen.add(rowName);
    validateRowFields(row, rowName, routeContext, errors);
  }

  for (const rowName of requiredRows.keys()) {
    if (!seen.has(rowName)) {
      errors.push(withEvidence(`Evidence gap register is missing required row: ${rowName}`, pageArtifact));
    }
  }
}

function validateRowFields(row, rowName, routeContext, errors) {
  const rowText = normalizeRenderedText(row.full);
  const required = requiredRows.get(rowName) ?? [];

  for (const field of requiredRowFields) {
    if (!new RegExp(`data-field=["']${escapeForPattern(field)}["']`, "i").test(row.full)) {
      errors.push(withEvidence(`Evidence gap register row "${rowName}" is missing required field: ${field}`, pageArtifact));
    }
  }

  for (const phrase of required) {
    if (!rowText.includes(phrase)) {
      errors.push(withEvidence(`Evidence gap register row "${rowName}" is missing required boundary wording: ${phrase}`, pageArtifact));
    }
  }

  if (!new RegExp(`data-field=["']public claim level["'][^>]*>\\s*concept\\s*<`, "i").test(row.full)) {
    errors.push(withEvidence(`Evidence gap register row "${rowName}" must use public claim level concept.`, pageArtifact));
  }

  const proofCell = row.full.match(/<td\b(?=[^>]*data-field=["']proof\/validation route["'])[^>]*>([\s\S]*?)<\/td>/i)?.[1] ?? "";
  const routeHrefs = extractHrefs(proofCell).map(normalizeRouteHref).filter(Boolean);
  if (routeHrefs.length === 0) {
    errors.push(withEvidence(`Evidence gap register row "${rowName}" has no proof/validation route.`, pageArtifact));
  }

  for (const href of routeHrefs) {
    if (!allowedProofRoutes.has(href)) {
      errors.push(withEvidence(`Evidence gap register row "${rowName}" uses unsupported proof/validation route: ${href}`, pageArtifact));
    }

    if (!routeContext.routes.has(href) || !routeContext.sitemapRoutes.has(href)) {
      errors.push(withEvidence(`Evidence gap register row "${rowName}" proof/validation route does not resolve in generated output: ${href}`, pageArtifact));
    }
  }
}

function validateAdjacentLinks(html, routeContext, errors) {
  for (const [href, label] of adjacentDistinctions) {
    const linkPattern = new RegExp(`<a\\b[^>]*href=["']${escapeForPattern(href)}["'][^>]*>[\\s\\S]*?${escapeForPattern(label)}[\\s\\S]*?<\\/a>`, "i");
    if (!linkPattern.test(html)) {
      errors.push(withEvidence(`Evidence gap register adjacent surface link is missing: ${href}`, pageArtifact));
    }

    if (!routeContext.routes.has(href) || !routeContext.sitemapRoutes.has(href)) {
      errors.push(withEvidence(`Evidence gap register adjacent surface route does not resolve in generated output: ${href}`, pageArtifact));
    }
  }
}

function validateBoundaryRegions(html, errors) {
  if (!/<section\b[^>]*id=["']unsafe-wording["'][^>]*data-evidence-gap-boundary=["']rejected-patterns["']/i.test(html)) {
    errors.push(withEvidence('Evidence gap register unsafe wording must use data-evidence-gap-boundary="rejected-patterns".', pageArtifact));
  }

  const unsafeSection = html.match(/<section\b[^>]*id=["']unsafe-wording["'][^>]*>[\s\S]*?<\/section>/i)?.[0] ?? "";
  for (const phrase of [
    "proves there is no impact",
    "repository is clean",
    "Tier4Unknown is enough",
    "private evidence proves",
    "stale evidence supports",
    "unsupported framework coverage is complete",
    "missing validation evidence is acceptable",
    "treated as approval",
    "replacement for human review"
  ]) {
    if (!normalizeRenderedText(unsafeSection).includes(phrase)) {
      errors.push(withEvidence(`Evidence gap register unsafe wording is missing rejected pattern: ${phrase}`, pageArtifact));
    }
  }
}

function validateForbiddenMaterial({ decodedHtml, scopedHtml, scopedText, errors }) {
  const hardPrivateValues = privateMaterialScanValues(decodedHtml);
  const rawMaterialValues = privateMaterialScanValues(scopedHtml);

  for (const pattern of hardPrivatePatterns) {
    if (hardPrivateValues.some((value) => pattern.test(value))) {
      errors.push(withEvidence(`Evidence gap register contains hard private material: ${pattern}`, pageArtifact));
    }
  }

  for (const pattern of forbiddenPositiveClaimPatterns) {
    if (pattern.test(scopedText)) {
      errors.push(withEvidence(`Evidence gap register contains forbidden claim wording outside bounded contexts: ${pattern}`, pageArtifact));
    }
  }

  for (const pattern of rawMaterialPatterns) {
    if (rawMaterialValues.some((value) => pattern.test(value))) {
      errors.push(withEvidence(`Evidence gap register contains forbidden raw/private material outside bounded contexts: ${pattern}`, pageArtifact));
    }
  }
}

function validateBlame(scopedText, errors) {
  for (const pattern of blamePatterns) {
    if (pattern.test(scopedText)) {
      errors.push(withEvidence(`Evidence gap register contains blame language: ${pattern}`, pageArtifact));
    }
  }
}

function validateWordCount(wordCount, errors) {
  if (wordCount < 900 || wordCount > 1700) {
    errors.push(withEvidence(`Evidence gap register visible prose word count must be between 900 and 1700 words, got ${wordCount}.`, pageArtifact));
  }
}

async function validateInboundLinks({ dist, errors }) {
  const missing = [];

  for (const route of evidenceGapRegisterInboundRoutes) {
    const filePath = resolve(dist, route.replace(/^\/|\/$/g, ""), "index.html");
    if (!(await fileExists(filePath))) {
      missing.push(route);
      continue;
    }

    const html = await readFile(filePath, "utf8");
    if (!new RegExp(`<a\\b[^>]*href=["']${escapeForPattern(evidenceGapRegisterRoute)}["']`, "i").test(html)) {
      missing.push(route);
    }
  }

  if (missing.length > 0) {
    errors.push(withEvidence(`Evidence gap register is missing inbound links from: ${missing.join(", ")}`, pageArtifact));
  }
}

async function validateDiscoveryOutputs({ dist, errors }) {
  for (const artifact of [routesIndexArtifact, llmsArtifact]) {
    const path = resolve(dist, artifact);
    if (!(await fileExists(path))) {
      errors.push(withEvidence(`Evidence gap register discovery output is missing: ${artifact}`, artifact));
      continue;
    }

    const text = await readFile(path, "utf8");
    if (!text.includes(evidenceGapRegisterRoute) || !text.includes("Evidence Gap Register")) {
      errors.push(withEvidence(`Evidence gap register discovery output does not include the route and title: ${artifact}`, artifact));
    }
  }
}

async function validateImplementationState({ dist, errors }) {
  const candidatePaths = [
    resolve(dist, "..", "..", implementationStateArtifact),
    resolve(dist, "..", implementationStateArtifact)
  ];
  const statePath = await firstExistingFile(candidatePaths);
  if (!statePath) {
    errors.push(withEvidence("Evidence gap register implementation-state.md is missing.", implementationStateArtifact));
    return;
  }

  const state = await readFile(statePath, "utf8");
  for (const phrase of [
    "Selected placement: standalone route `/evidence/gaps/`",
    "Rejected alternatives:",
    "Adjacent route inventory before site edits:",
    "Rejected-pattern marker: use `data-evidence-gap-boundary=\"rejected-patterns\"`",
    "No adjacent route substitutions, omissions, or deferrals are needed.",
    "Discovery artifacts for validation:"
  ]) {
    if (!state.includes(phrase)) {
      errors.push(withEvidence(`Evidence gap register implementation-state is missing phrase: ${phrase}`, implementationStateArtifact));
    }
  }
}

async function firstExistingFile(paths) {
  for (const path of paths) {
    if (await fileExists(path)) {
      return path;
    }
  }

  return null;
}

function stripGapRows(html) {
  return html.replace(/<table\b(?=[^>]*\bdata-evidence-gap-register\b)[^>]*>[\s\S]*?<\/table>/gi, " ");
}

function extractVisibleBodyProse(html) {
  const mainHtml = html.match(/<main\b[^>]*>([\s\S]*?)<\/main>/i)?.[1] ?? "";
  const withoutRows = stripGapRows(mainHtml);
  const withoutCode = withoutRows.replace(/<code\b[^>]*>[\s\S]*?<\/code>/gi, " ");
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

function stripAllowedBoundaryRegions(html) {
  let output = html;
  for (const boundary of ["rejected-patterns", "non-claims", "raw-material-boundary", "stop-conditions"]) {
    output = stripElementsWithAttribute(output, "data-evidence-gap-boundary", boundary);
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
  const tagPattern = new RegExp(`</?${escapeForPattern(tagName)}\\b[^>]*>`, "gi");
  tagPattern.lastIndex = start;
  let depth = 0;
  let match;
  let firstTagLength = 0;

  while ((match = tagPattern.exec(html))) {
    if (firstTagLength === 0) {
      firstTagLength = match[0].length;
    }

    if (match[0].startsWith("</")) {
      depth -= 1;
      if (depth === 0) {
        return tagPattern.lastIndex;
      }
    } else if (!match[0].endsWith("/>")) {
      depth += 1;
    }
  }

  return start + firstTagLength;
}

function extractTaggedElements(html, tagName, attributeName) {
  const pattern = new RegExp(`<${tagName}\\b(?=[^>]*\\b${attributeName}=)([^>]*)>[\\s\\S]*?<\\/${tagName}>`, "gi");
  return [...html.matchAll(pattern)].map((match) => ({
    attributes: match[1],
    full: match[0]
  }));
}

function extractHrefs(html) {
  return [...html.matchAll(/<a\b[^>]*href=["']([^"']+)["'][^>]*>/gi)].map((match) => decodeHtmlEntities(match[1]));
}

function extractIds(html) {
  return new Set([...html.matchAll(/\bid=["']([^"']+)["']/gi)].map((match) => decodeHtmlEntities(match[1])));
}

function findDuplicateIds(html) {
  const ids = [...html.matchAll(/\bid=["']([^"']+)["']/gi)].map((match) => decodeHtmlEntities(match[1]));
  const seen = new Set();
  const duplicates = new Set();

  for (const id of ids) {
    if (seen.has(id)) {
      duplicates.add(id);
    }
    seen.add(id);
  }

  return [...duplicates];
}

function getAttribute(attributes, name) {
  return decodeHtmlEntities(attributes.match(new RegExp(`\\b${escapeForPattern(name)}=["']([^"']+)["']`, "i"))?.[1] ?? "");
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

  return withoutQueryAndHash.endsWith("/") ? withoutQueryAndHash : `${withoutQueryAndHash}/`;
}

function countWords(text) {
  return (text.match(/[A-Za-z0-9]+(?:[-'][A-Za-z0-9]+)*/g) ?? []).length;
}

function escapeForPattern(value) {
  return String(value).replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function withEvidence(message, artifact) {
  return `${message} [${artifact}]`;
}
