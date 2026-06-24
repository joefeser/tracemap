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

export const reviewMeetingAgendaRoute = "/review-room/agenda/";
export const reviewMeetingAgendaRequiredLinks = [
  "/proof-paths/",
  "/evidence/",
  "/validation/",
  "/limitations/",
  "/review-room/",
  "/reviewer-quickstart/",
  "/packets/assembly/",
  "/handoff/template/",
  "/owners/follow-up/",
  "/decisions/evidence-record/",
  "/demo/manager-script/"
];

const pageArtifact = "review-room/agenda/index.html";
const sitemapArtifact = "sitemap.xml";
const routesIndexArtifact = "routes-index.json";
const llmsArtifact = "llms.txt";
const implementationStateArtifact = ".kiro/specs/site-tracemap-tools-review-meeting-agenda/implementation-state.md";

const requiredSections = new Map([
  ["before-the-meeting", "Before the meeting"],
  ["agenda", "Agenda"],
  ["evidence-checks", "Evidence checks"],
  ["gap-capture", "Gap capture"],
  ["owner-assignment", "Owner assignment"],
  ["decision-record-handoff", "Decision record handoff"],
  ["stop-conditions", "Stop conditions"],
  ["non-claims", "Non-claims"]
]);

const requiredAgendaFields = ["agenda row", "meeting purpose", "evidence input", "stop or handoff output"];
const requiredAgendaRows = new Map([
  ["question framing", ["State the review question before any claim is repeated", "wording to remove, downgrade, or route"]],
  ["proof path check", ["Confirm where public-safe evidence can be inspected", "missing-proof gap"]],
  ["evidence tier/coverage check", ["Tier1Semantic", "Tier2Structural", "Tier3SyntaxOrTextual", "Tier4Unknown"]],
  ["limitation check", ["Rule ID or rule family", "Limitation kept"]],
  ["gap register check", ["unknown, reduced, unavailable, or private-only evidence", "Gap entry"]],
  ["owner follow-up", ["Assign the next owner", "Role-based owner"]],
  ["decision record", ["validation evidence category, limitations, gaps, owners, and non-claims", "limitations and without stronger claims"]],
  ["closeout", ["Draft notes", "Unsupported claims removed, downgraded, or assigned"]]
]);

const requiredEvidenceCheckTerms = [
  "proof path",
  "rule ID or rule family",
  "evidence tier",
  "coverage label",
  "limitation",
  "Tier1Semantic",
  "Tier2Structural",
  "Tier3SyntaxOrTextual",
  "Tier4Unknown"
];

const requiredStopConditionTerms = [
  "proof path is missing",
  "private-only",
  "raw",
  "unknown or reduced",
  "runtime",
  "release",
  "safety",
  "production",
  "endpoint performance",
  "absence-of-impact",
  "complete-coverage",
  "AI analysis",
  "LLM analysis",
  "governance-replacement",
  "no next owner",
  "blames"
];

const requiredNeighborDistinctions = new Map([
  ["/review-room/", "broader orientation"],
  ["/reviewer-quickstart/", "onboarding and first-review orientation"],
  ["/packets/assembly/", "gathers public-safe ingredients"],
  ["/handoff/template/", "portable field structure"],
  ["/owners/follow-up/", "tracks post-meeting responsibility"],
  ["/decisions/evidence-record/", "preserves the bounded result"],
  ["/demo/manager-script/", "bounded presentation aid"]
]);

const requiredDiscoveryTerms = [
  "publicClaimLevel",
  "concept",
  "proof paths",
  "evidence tiers",
  "coverage labels",
  "limitations",
  "gaps",
  "owners",
  "decision-record handoff",
  "meeting automation",
  "runtime proof",
  "AI analysis",
  "human judgment"
];

const forbiddenPositiveClaimPatterns = [
  /\bautomates?\s+(?:the\s+)?meeting\b/i,
  /\bmeeting automation\b(?![^.]{0,80}\b(?:not|non-claim|No)\b)/i,
  /\b(?:approves?|certifies?|clears?|guarantees?)\s+(?:the\s+)?(?:release|runtime|production|endpoint|change|claim)\b/i,
  /\b(?:proves?|validates?|guarantees?)\s+(?:runtime behavior|production traffic|endpoint performance|release safety|operational safety|absence of impact|no impact|complete coverage)\b/i,
  /\b(?:AI|LLM|embedding|vector database|prompt)\b[^.]{0,80}\b(?:impact analysis|classification|reasoning|proof)\b/i,
  /\breplaces?\s+(?:human judgment|human governance|governance|tests|code review|source review|runtime observability|release review|owner confirmation)\b/i
];

const rawMaterialPatterns = [
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
  /\bcredential-like values?\b/i,
  /\bconnection strings?\b/i,
  /\btokens?\b/i,
  /\bkeys?\b/i
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

const unsupportedCertaintyPatterns = [
  /\bimpacted\b/i,
  /(?<!public-)\bsafe\b/i,
  /\bunsafe\b/i,
  /\bapproved\b/i,
  /\bblocked\b/i,
  /\bproduction proven\b/i,
  /\bperformance proven\b/i,
  /\broot cause\b/i,
  /\bhigh confidence\b/i,
  /\bmedium confidence\b/i,
  /\blow confidence\b/i,
  /\bverified\b/i,
  /\bguaranteed\b/i
];

const blamePatterns = [
  /\bfault of\b/i,
  /\bat fault\b/i,
  /\bblame\b/i,
  /\bbad (?:team|vendor|consultant|code|reviewer|owner)\b/i,
  /\b(?:team|vendor|consultant|reviewer|owner)\s+(?:failed|broke|hid|ignored)\b/i
];

export async function validateReviewMeetingAgendaDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors
}) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "review-room", "agenda", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence(`Review meeting agenda is missing required public route: ${reviewMeetingAgendaRoute}`, pageArtifact));
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
  if (!sitemapUrls.has(`${baseUrl}${reviewMeetingAgendaRoute}`)) {
    errors.push(withEvidence(`Review meeting agenda sitemap is missing required route: ${baseUrl}${reviewMeetingAgendaRoute}`, sitemapArtifact));
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
    errors.push(withEvidence(`Review meeting agenda could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return { routeEntry, routes, sitemapRoutes };
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Review meeting agenda routes-index.json is invalid: expected entries array", routesIndexArtifact));
    return { routeEntry, routes, sitemapRoutes };
  }

  for (const entry of parsed.entries) {
    if (typeof entry?.path === "string") {
      routes.add(normalizeRouteHref(entry.path));
    }

    if (entry?.path === reviewMeetingAgendaRoute) {
      routeEntry = entry;
    }
  }

  validateRouteEntry(routeEntry, errors);
  return { routeEntry, routes, sitemapRoutes };
}

function validateRouteEntry(routeEntry, errors) {
  if (!routeEntry) {
    errors.push(withEvidence(`Review meeting agenda routes-index.json is missing route: ${reviewMeetingAgendaRoute}`, routesIndexArtifact));
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
      errors.push(withEvidence(`Review meeting agenda routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
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
      errors.push(withEvidence(`Review meeting agenda discovery metadata is missing required term: ${term}`, routesIndexArtifact));
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
  validateSections(html, errors);
  validateAgendaRows(html, errors);
  validateEvidenceChecks(html, errors);
  validateStopConditions(html, errors);
  validateAdjacentLinks(html, routeContext, errors);
  validateBoundaryRegions(html, errors);
  validateForbiddenMaterial({ decodedHtml, scopedHtml, scopedText, errors });
  validateUnsupportedCertainty(scopedText, errors);
  validateBlame(scopedText, errors);
  validateWordCount(wordCount, errors);
}

function validateVisibleText(pageText, errors) {
  for (const phrase of [
    "Public claim level: concept",
    "No public conclusion without evidence",
    "review question",
    "proof path",
    "rule ID or rule family",
    "evidence tier",
    "coverage label",
    "limitation",
    "gap",
    "next owner",
    "decision record",
    "non-claims"
  ]) {
    if (!pageText.includes(phrase)) {
      errors.push(withEvidence(`Review meeting agenda is missing required visible text: ${phrase}`, pageArtifact));
    }
  }
}

function validateMetadata(html, errors) {
  const checks = [
    /<title>Evidence Review Meeting Agenda \| TraceMap<\/title>/i,
    /<link\b[^>]*rel=["']canonical["'][^>]*href=["']https:\/\/tracemap\.tools\/review-room\/agenda\/["']/i,
    /<meta\b[^>]*property=["']og:type["'][^>]*content=["']article["']/i,
    /<meta\b[^>]*property=["']og:title["'][^>]*content=["']TraceMap Evidence Review Meeting Agenda["']/i,
    /<meta\b[^>]*property=["']og:url["'][^>]*content=["']https:\/\/tracemap\.tools\/review-room\/agenda\/["']/i
  ];

  for (const pattern of checks) {
    if (!pattern.test(html)) {
      errors.push(withEvidence("Review meeting agenda is missing required standalone route metadata.", pageArtifact));
    }
  }
}

function validateSections(html, errors) {
  const ids = extractIds(html);
  const text = normalizeRenderedText(html);

  for (const [id, label] of requiredSections) {
    if (!ids.has(id)) {
      errors.push(withEvidence(`Review meeting agenda is missing required section id: ${id}`, pageArtifact));
    }

    if (!text.includes(label)) {
      errors.push(withEvidence(`Review meeting agenda is missing required section label: ${label}`, pageArtifact));
    }
  }

  for (const id of findDuplicateIds(html)) {
    errors.push(withEvidence(`Review meeting agenda contains duplicate id: ${id}`, pageArtifact));
  }
}

function validateAgendaRows(html, errors) {
  const table = html.match(/<table\b(?=[^>]*\bdata-review-agenda-table\b)[^>]*>[\s\S]*?<\/table>/i)?.[0];
  if (!table) {
    errors.push(withEvidence("Review meeting agenda is missing data-review-agenda-table.", pageArtifact));
    return;
  }

  if (!/<caption\b[\s\S]*?purpose[\s\S]*?evidence[\s\S]*?stop or handoff output[\s\S]*?<\/caption>/i.test(table)) {
    errors.push(withEvidence("Review meeting agenda table caption must describe purpose, evidence input, and stop or handoff output.", pageArtifact));
  }

  for (const header of ["Agenda row", "Meeting purpose", "Evidence input", "Stop or handoff output"]) {
    const pattern = new RegExp(`<th\\b[^>]*\\bscope=["']col["'][^>]*>\\s*${escapeForPattern(header)}\\s*</th>`, "i");
    if (!pattern.test(table)) {
      errors.push(withEvidence(`Review meeting agenda table is missing column header: ${header}`, pageArtifact));
    }
  }

  const rows = extractTaggedElements(table, "tr", "data-review-agenda-row");
  const seen = new Set();

  for (const row of rows) {
    const rowName = getAttribute(row.attributes, "data-review-agenda-row");
    if (!requiredAgendaRows.has(rowName)) {
      errors.push(withEvidence(`Review meeting agenda has unexpected agenda row: ${String(rowName)}`, pageArtifact));
      continue;
    }

    seen.add(rowName);
    validateAgendaRowFields(row, rowName, errors);
  }

  for (const rowName of requiredAgendaRows.keys()) {
    if (!seen.has(rowName)) {
      errors.push(withEvidence(`Review meeting agenda is missing required agenda row: ${rowName}`, pageArtifact));
    }
  }
}

function validateAgendaRowFields(row, rowName, errors) {
  const rowText = normalizeRenderedText(row.full);

  for (const field of requiredAgendaFields) {
    if (!new RegExp(`data-field=["']${escapeForPattern(field)}["']`, "i").test(row.full)) {
      errors.push(withEvidence(`Review meeting agenda row "${rowName}" is missing required field: ${field}`, pageArtifact));
    }
  }

  for (const phrase of requiredAgendaRows.get(rowName) ?? []) {
    if (!rowText.includes(phrase)) {
      errors.push(withEvidence(`Review meeting agenda row "${rowName}" is missing required wording: ${phrase}`, pageArtifact));
    }
  }
}

function validateEvidenceChecks(html, errors) {
  const section = extractSectionById(html, "evidence-checks");
  const text = normalizeRenderedText(section);

  for (const term of requiredEvidenceCheckTerms) {
    if (!text.includes(term)) {
      errors.push(withEvidence(`Review meeting agenda evidence checks missing term: ${term}`, pageArtifact));
    }
  }
}

function validateStopConditions(html, errors) {
  const section = extractSectionById(html, "stop-conditions");
  const text = normalizeRenderedText(section);

  for (const term of requiredStopConditionTerms) {
    if (!text.includes(term)) {
      errors.push(withEvidence(`Review meeting agenda stop conditions missing trigger: ${term}`, pageArtifact));
    }
  }
}

function validateAdjacentLinks(html, routeContext, errors) {
  for (const href of reviewMeetingAgendaRequiredLinks) {
    if (!hasHref(html, href)) {
      errors.push(withEvidence(`Review meeting agenda is missing required link: ${href}`, pageArtifact));
    }

    if (!routeContext.routes.has(href) || !routeContext.sitemapRoutes.has(href)) {
      errors.push(withEvidence(`Review meeting agenda required link does not resolve in generated output: ${href}`, pageArtifact));
    }
  }

  const distinctionSection = extractSectionById(html, "adjacent-surfaces");
  const distinctionText = normalizeRenderedText(distinctionSection);
  for (const [href, phrase] of requiredNeighborDistinctions) {
    if (!hasHref(distinctionSection, href)) {
      errors.push(withEvidence(`Review meeting agenda neighboring distinction is missing link: ${href}`, pageArtifact));
    }

    if (!distinctionText.includes(phrase)) {
      errors.push(withEvidence(`Review meeting agenda neighboring distinction is missing phrase for ${href}: ${phrase}`, pageArtifact));
    }
  }
}

function validateBoundaryRegions(html, errors) {
  for (const [id, boundary] of [
    ["stop-conditions", "stop-conditions"],
    ["non-claims", "non-claims"]
  ]) {
    const pattern = new RegExp(`<section\\b(?=[^>]*\\bid=["']${escapeForPattern(id)}["'])(?=[^>]*\\bdata-review-agenda-boundary=["']${escapeForPattern(boundary)}["'])[^>]*>`, "i");
    if (!pattern.test(html)) {
      errors.push(withEvidence(`Review meeting agenda section ${id} must use data-review-agenda-boundary="${boundary}".`, pageArtifact));
    }
  }
}

function validateForbiddenMaterial({ decodedHtml, scopedHtml, scopedText, errors }) {
  const hardPrivateValues = privateMaterialScanValues(decodedHtml);
  const rawMaterialValues = privateMaterialScanValues(scopedHtml);

  for (const pattern of hardPrivatePatterns) {
    if (hardPrivateValues.some((value) => pattern.test(value))) {
      errors.push(withEvidence(`Review meeting agenda contains hard private material: ${pattern}`, pageArtifact));
    }
  }

  for (const pattern of forbiddenPositiveClaimPatterns) {
    if (pattern.test(scopedText)) {
      errors.push(withEvidence(`Review meeting agenda contains forbidden positive claim outside bounded contexts: ${pattern}`, pageArtifact));
    }
  }

  for (const pattern of rawMaterialPatterns) {
    if (rawMaterialValues.some((value) => pattern.test(value))) {
      errors.push(withEvidence(`Review meeting agenda contains forbidden raw/private material outside bounded contexts: ${pattern}`, pageArtifact));
    }
  }
}

function validateUnsupportedCertainty(scopedText, errors) {
  for (const pattern of unsupportedCertaintyPatterns) {
    if (pattern.test(scopedText)) {
      errors.push(withEvidence(`Review meeting agenda contains unsupported certainty language outside bounded contexts: ${pattern}`, pageArtifact));
    }
  }
}

function validateBlame(scopedText, errors) {
  for (const pattern of blamePatterns) {
    if (pattern.test(scopedText)) {
      errors.push(withEvidence(`Review meeting agenda contains blame language: ${pattern}`, pageArtifact));
    }
  }
}

function validateWordCount(wordCount, errors) {
  if (wordCount < 700 || wordCount > 1500) {
    errors.push(withEvidence(`Review meeting agenda visible prose word count must be between 700 and 1500 words, got ${wordCount}.`, pageArtifact));
  }
}

async function validateInboundLinks({ dist, errors }) {
  const reviewRoomPath = resolve(dist, "review-room", "index.html");
  if (!(await fileExists(reviewRoomPath))) {
    errors.push(withEvidence("Review meeting agenda cannot validate inbound link because /review-room/ is missing.", pageArtifact));
    return;
  }

  const html = await readFile(reviewRoomPath, "utf8");
  if (!hasHref(html, reviewMeetingAgendaRoute)) {
    errors.push(withEvidence("Review meeting agenda is missing inbound link from /review-room/.", pageArtifact));
  }
}

async function validateDiscoveryOutputs({ dist, errors }) {
  for (const artifact of [routesIndexArtifact, llmsArtifact]) {
    const path = resolve(dist, artifact);
    if (!(await fileExists(path))) {
      errors.push(withEvidence(`Review meeting agenda discovery output is missing: ${artifact}`, artifact));
      continue;
    }

    const text = await readFile(path, "utf8");
    if (!text.includes(reviewMeetingAgendaRoute) || !text.includes("Evidence Review Meeting Agenda")) {
      errors.push(withEvidence(`Review meeting agenda discovery output does not include the route and title: ${artifact}`, artifact));
    }
  }
}

async function validateImplementationState({ dist, errors }) {
  const candidatePaths = implementationStateCandidatePaths(dist);
  const statePath = await firstExistingFile(candidatePaths);
  if (!statePath) {
    errors.push(withEvidence("Review meeting agenda implementation-state.md is missing.", implementationStateArtifact));
    return;
  }

  const state = await readFile(statePath, "utf8");
  for (const phrase of [
    "Selected placement: `/review-room/agenda/`",
    "Rejected alternative: `/meetings/evidence-review/`",
    "Rejected alternative: section on `/review-room/`",
    "Rejected alternative: section on `/reviewer-quickstart/`",
    "Primary navigation remains unchanged.",
    "Word-count bounds: 700 to 1500 rendered main-content words",
    "Manual public-safety reviewer signoff: completed by implementation owner"
  ]) {
    if (!state.includes(phrase)) {
      errors.push(withEvidence(`Review meeting agenda implementation-state is missing phrase: ${phrase}`, implementationStateArtifact));
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
  const withoutRows = mainHtml.replace(/<table\b(?=[^>]*\bdata-review-agenda-table\b)[^>]*>[\s\S]*?<\/table>/gi, " ");
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
  for (const boundary of ["non-claims", "stop-conditions"]) {
    output = stripElementsWithAttribute(output, "data-review-agenda-boundary", boundary);
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
    if (isInsideHtmlComment(html, match.index ?? 0)) {
      continue;
    }

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

function isInsideHtmlComment(html, index) {
  const lastCommentOpen = html.lastIndexOf("<!--", index);
  if (lastCommentOpen === -1) {
    return false;
  }

  const lastCommentClose = html.lastIndexOf("-->", index);
  return lastCommentClose < lastCommentOpen;
}

function extractTaggedElements(html, tagName, attributeName) {
  const pattern = new RegExp(`<${tagName}\\b(?=[^>]*\\b${attributeName}=)([^>]*)>[\\s\\S]*?<\\/${tagName}>`, "gi");
  return [...html.matchAll(pattern)].map((match) => ({
    attributes: match[1],
    full: match[0]
  }));
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

  return withoutQueryAndHash.endsWith("/") ? withoutQueryAndHash : `${withoutQueryAndHash}/`;
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
