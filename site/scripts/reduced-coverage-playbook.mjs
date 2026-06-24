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

export const reducedCoveragePlaybookRoute = "/limitations/reduced-coverage/";
export const reducedCoveragePlaybookRequiredLinks = [
  "/limitations/",
  "/validation/",
  "/static-vs-runtime/",
  "/questions/objections/",
  "/proof-paths/faq/",
  "/review-claim-checklist/"
];
export const reducedCoveragePlaybookInboundRoutes = ["/limitations/"];

const pageArtifact = "limitations/reduced-coverage/index.html";
const sitemapArtifact = "sitemap.xml";
const routesIndexArtifact = "routes-index.json";
const allowedEvidenceTiers = new Set([
  "Tier1Semantic",
  "Tier2Structural",
  "Tier3SyntaxOrTextual",
  "Tier4Unknown"
]);
const allowedMarkers = new Set(["unavailable", "private-only", "stale"]);
const requiredSections = new Map([
  ["what-reduced-coverage-means", "what reduced coverage means"],
  ["how-to-label-it", "how to label it"],
  ["reduced-coverage-matrix", "reduced coverage matrix"],
  ["safe-conclusions", "safe conclusions"],
  ["unsafe-conclusions", "unsafe conclusions"],
  ["next-evidence-to-collect", "next evidence to collect"],
  ["owner-handoff", "owner handoff"],
  ["stop-conditions", "stop conditions"],
  ["non-claims", "non-claims"],
  ["adjacent-surfaces", "adjacent surfaces"]
]);
const requiredRowFields = [
  "coverage label",
  "evidence tier",
  "evidence available",
  "what cannot be concluded",
  "next owner",
  "safe wording",
  "stop condition",
  "proof/validation link"
];
const requiredRows = new Map([
  [
    "build/load failure",
    {
      cannot: ["Clean-repo claim", "complete analysis", "compiler-resolved conclusion", "absence-of-impact proof"],
      marker: "unavailable"
    }
  ],
  [
    "syntax fallback",
    {
      cannot: ["Compiler-resolved symbol conclusion", "call graph certainty", "complete dependency path"]
    }
  ],
  [
    "missing semantic evidence",
    {
      cannot: ["Tier1Semantic conclusion", "compiler-resolved ownership", "complete path"],
      marker: "unavailable"
    }
  ],
  [
    "unsupported framework surface",
    {
      cannot: ["Complete framework coverage", "route completeness", "runtime behavior"]
    }
  ],
  [
    "missing generated artifact",
    {
      cannot: ["Public proof-link claim", "reproducible public demo evidence", "complete artifact set"],
      marker: "unavailable"
    }
  ],
  [
    "private-only support",
    {
      cannot: ["Public proof", "customer-specific conclusion", "public demo support"],
      marker: "private-only"
    }
  ],
  [
    "stale commit context",
    {
      cannot: ["Current-head behavior", "current release status", "current proof path"],
      marker: "stale"
    }
  ],
  [
    "unknown evidence tier",
    {
      cannot: ["Stronger evidence tier", "semantic certainty", "complete coverage"]
    }
  ]
]);
const requiredSafePhrases = [
  "Coverage is reduced",
  "Syntax fallback found a static reference",
  "Semantic evidence is missing",
  "public-safe artifact output is unavailable",
  "Private-only evidence can support internal follow-up",
  "The source context is stale",
  "The tier is unknown"
];
const adjacentDistinctions = new Map([
  ["/limitations/", "Canonical boundary definitions"],
  ["/validation/", "Check-result orientation"],
  ["/static-vs-runtime/", "Runtime question routing"],
  ["/questions/objections/", "Broad objection answers"],
  ["/proof-paths/faq/", "Proof-path explanation"],
  ["/review-claim-checklist/", "Claim repetition ritual"]
]);
const boundedAnchorText = new Map([
  ["/limitations/", ["limitations and non-claims"]],
  ["/validation/", ["validation checks"]],
  ["/static-vs-runtime/", ["static versus runtime boundaries"]],
  ["/questions/objections/", ["stakeholder objection guide"]],
  ["/proof-paths/faq/", ["proof path FAQ"]],
  ["/review-claim-checklist/", ["review claim checklist"]]
]);
const allowedProofRoutes = new Set([
  "/limitations/",
  "/validation/",
  "/proof-paths/faq/",
  "/review-claim-checklist/"
]);
const boundaryNames = new Set(["rejected-patterns", "non-claims", "stop-conditions"]);
const forbiddenClaimPatterns = [
  /\bproves?\s+there is no impact\b/i,
  /\bclean repo\b/i,
  /\bproves?\s+runtime behavior\b/i,
  /\bproves?\s+compiler-resolved impact\b/i,
  /\b(?:release|operational)\s+(?:is\s+)?(?:approved|safe|certified|guaranteed)\b/i,
  /\bcomplete coverage\b/i,
  /\bAI impact analysis\b/i,
  /\bLLM analysis\b/i,
  /\bprompt-based classification\b/i,
  /\bembedding search\b/i,
  /\bvector database analysis\b/i,
  /\bautonomous approval\b/i,
  /\breplacement of human review\b/i,
  /\breplaces?\s+human review\b/i
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
  /\bcredential-like values\b/i
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
  /\bsk-[A-Za-z0-9_-]{12,}\b/i
];
const blamePatterns = [
  /\b(?:person|team|vendor|consultant|customer|reviewer|owner)\s+(?:caused|broke|failed|missed|hid)\b/i,
  /\b(?:caused by|blame|fault|negligent|careless)\b/i
];

export async function validateReducedCoveragePlaybookDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "limitations", "reduced-coverage", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence("Reduced coverage playbook page is missing required public route: /limitations/reduced-coverage/", pageArtifact));
    errors.push(...localErrors);
    return;
  }

  await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  await validateRoutesIndex({ dist, errors: localErrors });
  await validatePage({ pagePath, errors: localErrors });
  await validateInboundLinks({ dist, errors: localErrors });

  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${reducedCoveragePlaybookRoute}`)) {
    errors.push(withEvidence(`Reduced coverage playbook sitemap is missing required route: ${baseUrl}${reducedCoveragePlaybookRoute}`, sitemapArtifact));
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
    errors.push(withEvidence(`Reduced coverage playbook could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return;
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Reduced coverage playbook routes-index.json is invalid: expected entries array", routesIndexArtifact));
    return;
  }

  const routeEntry = parsed.entries.find((entry) => entry?.path === reducedCoveragePlaybookRoute);
  if (!routeEntry) {
    errors.push(withEvidence(`Reduced coverage playbook routes-index.json is missing route: ${reducedCoveragePlaybookRoute}`, routesIndexArtifact));
    return;
  }

  const expectedFields = {
    publicClaimLevel: "concept",
    hintCategory: "limitations",
    sourceType: "site-page",
    preferredProofPath: "/limitations/"
  };

  for (const [field, expected] of Object.entries(expectedFields)) {
    if (routeEntry[field] !== expected) {
      errors.push(withEvidence(`Reduced coverage playbook routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
    }
  }

  const metadataText = normalizeRenderedText(
    JSON.stringify({
      title: routeEntry.title,
      summary: routeEntry.summary,
      limitations: routeEntry.limitations
    })
  );
  for (const pattern of forbiddenClaimPatterns) {
    if (pattern.test(metadataText)) {
      errors.push(withEvidence("Reduced coverage playbook metadata contains forbidden public claim wording.", routesIndexArtifact));
    }
  }
}

async function validatePage({ pagePath, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const pageText = normalizeRenderedText(html);
  const scopedHtml = stripAllowedBoundaryRegions(stripMatrix(decodedHtml));
  const scopedText = normalizeRenderedText(scopedHtml);
  const wordCount = countWords(normalizeRenderedText(stripMatrix(html)));

  validateVisibleText(pageText, errors);
  validateMetadata(html, errors);
  validateSections(html, errors);
  validateMatrix(html, errors);
  validateAdjacentLinks(html, errors);
  validateBoundaryRegions(html, errors);
  validateForbiddenMaterial({ decodedHtml, html, scopedHtml, scopedText, errors });
  validateSafeExamples(pageText, errors);
  validateWordCount(wordCount, errors);
}

function validateVisibleText(pageText, errors) {
  for (const phrase of ["Public claim level: concept", "No public conclusion without evidence"]) {
    if (!pageText.includes(phrase)) {
      errors.push(withEvidence(`Reduced coverage playbook is missing required visible text: ${phrase}`, pageArtifact));
    }
  }
}

function validateMetadata(html, errors) {
  const checks = [
    /<title>Reduced Coverage Playbook \| TraceMap<\/title>/i,
    /<link\b[^>]*rel=["']canonical["'][^>]*href=["']https:\/\/tracemap\.tools\/limitations\/reduced-coverage\/["']/i,
    /<meta\b[^>]*property=["']og:type["'][^>]*content=["']article["']/i,
    /<meta\b[^>]*property=["']og:url["'][^>]*content=["']https:\/\/tracemap\.tools\/limitations\/reduced-coverage\/["']/i
  ];

  for (const pattern of checks) {
    if (!pattern.test(html)) {
      errors.push(withEvidence("Reduced coverage playbook is missing required standalone route metadata.", pageArtifact));
    }
  }
}

function validateSections(html, errors) {
  const ids = extractIds(html);

  for (const [id, label] of requiredSections) {
    if (!ids.has(id)) {
      errors.push(withEvidence(`Reduced coverage playbook is missing required section: ${label}`, pageArtifact));
    }
  }

  const duplicates = findDuplicateIds(html);
  for (const id of duplicates) {
    errors.push(withEvidence(`Reduced coverage playbook contains duplicate id: ${id}`, pageArtifact));
  }
}

function validateMatrix(html, errors) {
  const table = html.match(/<table\b(?=[^>]*\bdata-reduced-coverage-matrix\b)[^>]*>[\s\S]*?<\/table>/i)?.[0];
  if (!table) {
    errors.push(withEvidence("Reduced coverage playbook is missing data-reduced-coverage-matrix table.", pageArtifact));
    return;
  }

  if (!/<th\b[^>]*\bscope=["']col["'][\s\S]*?Coverage label[\s\S]*?<\/thead>/i.test(table)) {
    errors.push(withEvidence("Reduced coverage matrix must use semantic column headers.", pageArtifact));
  }

  const rows = extractTaggedElements(table, "tr", "data-reduced-coverage-row");
  const seen = new Set();

  for (const row of rows) {
    const rowName = getAttribute(row.attributes, "data-reduced-coverage-row");
    if (!requiredRows.has(rowName)) {
      errors.push(withEvidence(`Reduced coverage matrix has unexpected row: ${String(rowName)}`, pageArtifact));
      continue;
    }

    seen.add(rowName);
    validateMatrixRow(rowName, row.html, errors);
  }

  for (const rowName of requiredRows.keys()) {
    if (!seen.has(rowName)) {
      errors.push(withEvidence(`Reduced coverage matrix is missing required row: ${rowName}`, pageArtifact));
    }
  }
}

function validateMatrixRow(rowName, rowHtml, errors) {
  const rowText = normalizeRenderedText(rowHtml);
  const fields = new Set(
    [...rowHtml.matchAll(/\bdata-field=["']([^"']+)["']/gi)].map((match) => decodeHtmlEntities(match[1].trim()))
  );

  for (const field of requiredRowFields) {
    if (!fields.has(field)) {
      errors.push(withEvidence(`Reduced coverage row "${rowName}" is missing field: ${field}`, pageArtifact));
    }
  }

  for (const phrase of requiredRows.get(rowName).cannot) {
    if (!rowText.includes(phrase)) {
      errors.push(withEvidence(`Reduced coverage row "${rowName}" is missing cannot-conclude text: ${phrase}`, pageArtifact));
    }
  }

  const tierCell = extractFieldCell(rowHtml, "evidence tier");
  const codeValues = [...tierCell.matchAll(/<code\b[^>]*>([\s\S]*?)<\/code>/gi)].map((match) =>
    normalizeRenderedText(match[1])
  );
  const tiers = codeValues.filter((value) => /^Tier/.test(value));
  const markers = codeValues.filter((value) => !/^Tier/.test(value));

  if (tiers.length === 0) {
    errors.push(withEvidence(`Reduced coverage row "${rowName}" is missing evidence tier code tokens.`, pageArtifact));
  }

  for (const tier of tiers) {
    if (!allowedEvidenceTiers.has(tier)) {
      errors.push(withEvidence(`Reduced coverage row "${rowName}" has invalid evidence tier: ${tier}`, pageArtifact));
    }
  }

  for (const marker of markers) {
    if (!allowedMarkers.has(marker)) {
      errors.push(withEvidence(`Reduced coverage row "${rowName}" has invalid supplementary marker: ${marker}`, pageArtifact));
    }
  }

  const expectedMarker = requiredRows.get(rowName).marker;
  if (expectedMarker && !markers.includes(expectedMarker)) {
    errors.push(withEvidence(`Reduced coverage row "${rowName}" is missing marker: ${expectedMarker}`, pageArtifact));
  }

  if (rowName === "private-only support" && !(tiers.length === 1 && tiers[0] === "Tier4Unknown")) {
    errors.push(withEvidence('Reduced coverage row "private-only support" must use Tier4Unknown in public copy.', pageArtifact));
  }

  const proofCell = extractFieldCell(rowHtml, "proof/validation link");
  const proofHref = getAttribute(proofCell.match(/<a\b([^>]*)>/i)?.[1] ?? "", "href");
  if (!proofHref || !allowedProofRoutes.has(proofHref)) {
    errors.push(withEvidence(`Reduced coverage row "${rowName}" has invalid proof/validation link: ${String(proofHref)}`, pageArtifact));
  }
}

function validateAdjacentLinks(html, errors) {
  const adjacentSection = extractElementById(html, "section", "adjacent-surfaces");
  if (!adjacentSection) {
    return;
  }

  const text = normalizeRenderedText(adjacentSection);
  for (const [route, phrase] of adjacentDistinctions) {
    if (!text.includes(phrase)) {
      errors.push(withEvidence(`Reduced coverage adjacent surface distinction is missing: ${route}`, pageArtifact));
    }

    if (!hasHref(adjacentSection, route)) {
      errors.push(withEvidence(`Reduced coverage adjacent surface link is missing: ${route}`, pageArtifact));
    }
  }

  for (const route of reducedCoveragePlaybookRequiredLinks) {
    if (!hasHref(html, route)) {
      errors.push(withEvidence(`Reduced coverage playbook is missing required link: ${route}`, pageArtifact));
    }
  }

  for (const [route, allowedTexts] of boundedAnchorText) {
    const anchors = extractAnchors(html).filter((anchor) => anchor.href === route);
    if (anchors.length === 0) {
      continue;
    }

    const allowedLower = allowedTexts.map((text) => text.toLowerCase());
    const hasBoundedText = anchors.some((anchor) => allowedLower.includes(anchor.text.toLowerCase()));
    if (!hasBoundedText) {
      errors.push(withEvidence(`Reduced coverage link to ${route} lacks bounded anchor text.`, pageArtifact));
    }
  }
}

function validateBoundaryRegions(html, errors) {
  const regions = extractTaggedElements(html, "section", "data-reduced-coverage-boundary");
  const seen = new Set();

  for (const region of regions) {
    const name = getAttribute(region.attributes, "data-reduced-coverage-boundary");
    if (!boundaryNames.has(name)) {
      errors.push(withEvidence(`Reduced coverage playbook has unknown boundary region: ${String(name)}`, pageArtifact));
      continue;
    }

    seen.add(name);
  }

  for (const name of boundaryNames) {
    if (!seen.has(name)) {
      errors.push(withEvidence(`Reduced coverage playbook is missing bounded region: ${name}`, pageArtifact));
    }
  }
}

function validateForbiddenMaterial({ decodedHtml, html, scopedHtml, scopedText, errors }) {
  for (const pattern of hardPrivatePatterns) {
    if (pattern.test(decodedHtml)) {
      errors.push(withEvidence("Reduced coverage playbook contains hard private material in rendered HTML or attributes.", pageArtifact));
    }
  }

  for (const pattern of forbiddenClaimPatterns) {
    if (pattern.test(scopedText) || pattern.test(scopedHtml)) {
      errors.push(withEvidence("Reduced coverage playbook contains forbidden claim wording outside bounded contexts.", pageArtifact));
    }
  }

  for (const pattern of rawMaterialPatterns) {
    if (pattern.test(scopedText) || pattern.test(scopedHtml)) {
      errors.push(withEvidence("Reduced coverage playbook contains forbidden raw/private material outside bounded contexts.", pageArtifact));
    }
  }

  const stripForBlame = normalizeRenderedText(stripAllowedBoundaryRegions(html));
  for (const pattern of blamePatterns) {
    if (pattern.test(stripForBlame)) {
      errors.push(withEvidence("Reduced coverage playbook contains blame language.", pageArtifact));
    }
  }

  const buildRow = extractRow(html, "build/load failure");
  if (!buildRow) {
    return;
  }

  const surrounding = normalizeRenderedText(buildRow).replace(/\bbuild\/load failure\b/gi, "");
  for (const pattern of blamePatterns) {
    if (pattern.test(surrounding)) {
      errors.push(withEvidence("Reduced coverage build/load failure row attributes the state instead of keeping neutral wording.", pageArtifact));
    }
  }
}

function validateSafeExamples(pageText, errors) {
  for (const phrase of requiredSafePhrases) {
    if (!pageText.includes(phrase)) {
      errors.push(withEvidence(`Reduced coverage playbook is missing safe wording example: ${phrase}`, pageArtifact));
    }
  }
}

function validateWordCount(wordCount, errors) {
  if (wordCount < 1000 || wordCount > 1900) {
    errors.push(withEvidence(`Reduced coverage playbook prose word count must be between 1000 and 1900 words excluding matrix text, got ${wordCount}.`, pageArtifact));
  }
}

async function validateInboundLinks({ dist, errors }) {
  const missing = [];

  for (const route of reducedCoveragePlaybookInboundRoutes) {
    const filePath = route === "/" ? resolve(dist, "index.html") : resolve(dist, ...route.split("/").filter(Boolean), "index.html");
    if (!(await fileExists(filePath))) {
      continue;
    }

    const html = await readFile(filePath, "utf8");
    if (!hasHref(html, reducedCoveragePlaybookRoute)) {
      missing.push(route);
    }
  }

  if (missing.length > 0) {
    errors.push(withEvidence(`Reduced coverage playbook is missing inbound links from: ${missing.join(", ")}`, pageArtifact));
  }
}

function stripMatrix(html) {
  return html.replace(/<table\b(?=[^>]*\bdata-reduced-coverage-matrix\b)[^>]*>[\s\S]*?<\/table>/gi, " ");
}

function stripAllowedBoundaryRegions(html) {
  return extractTaggedElements(html, "section", "data-reduced-coverage-boundary")
    .filter((section) => boundaryNames.has(getAttribute(section.attributes, "data-reduced-coverage-boundary")))
    .reduce((result, section) => result.replace(section.html, " "), html);
}

function extractIds(html) {
  return new Set([...html.matchAll(actualAttributePattern("id", "g"))].map((match) => decodeHtmlEntities(match[1].trim())));
}

function findDuplicateIds(html) {
  const seen = new Set();
  const duplicates = new Set();

  for (const match of html.matchAll(actualAttributePattern("id", "g"))) {
    const id = decodeHtmlEntities(match[1].trim());
    if (seen.has(id)) {
      duplicates.add(id);
    }

    seen.add(id);
  }

  return [...duplicates].sort();
}

function extractTaggedElements(html, tagName, attributeName) {
  const pattern = new RegExp(`<\\/?${tagName}\\b[^>]*>`, "gi");
  const elements = [];
  let match;

  while ((match = pattern.exec(html))) {
    if (match[0].startsWith(`</`)) {
      continue;
    }

    const attributes = match[0].replace(new RegExp(`^<${tagName}\\b|>$`, "gi"), "");
    const startIndex = match.index;
    let depth = 1;
    let endIndex = pattern.lastIndex;
    let innerMatch;

    while ((innerMatch = pattern.exec(html))) {
      if (innerMatch[0].startsWith(`</`)) {
        depth -= 1;
      } else {
        depth += 1;
      }

      if (depth === 0) {
        endIndex = pattern.lastIndex;
        break;
      }
    }

    if (hasAttribute(attributes, attributeName)) {
      elements.push({ attributes, html: html.slice(startIndex, endIndex) });
    }

    pattern.lastIndex = endIndex;
  }

  return elements;
}

function extractRow(html, rowName) {
  const escaped = escapeRegExp(rowName);
  return html.match(new RegExp(`<tr\\b(?=[^>]*\\bdata-reduced-coverage-row=["']${escaped}["'])[^>]*>[\\s\\S]*?<\\/tr>`, "i"))?.[0] ?? "";
}

function extractFieldCell(rowHtml, field) {
  const escaped = escapeRegExp(field);
  return rowHtml.match(new RegExp(`<(?:td|th)\\b(?=[^>]*\\bdata-field=["']${escaped}["'])[^>]*>[\\s\\S]*?<\\/(?:td|th)>`, "i"))?.[0] ?? "";
}

function extractElementById(html, tagName, id) {
  const escaped = escapeRegExp(id);
  return html.match(new RegExp(`<${tagName}\\b(?=[^>]*(?:^|\\s)id=["']${escaped}["'])[^>]*>[\\s\\S]*?<\\/${tagName}>`, "i"))?.[0] ?? "";
}

function extractAnchors(html) {
  return [...html.matchAll(/<a\b([^>]*)>([\s\S]*?)<\/a>/gi)].map((match) => ({
    href: getAttribute(match[1], "href"),
    text: normalizeRenderedText(match[2])
  }));
}

function getAttribute(attributes, name) {
  const match = String(attributes).match(actualAttributePattern(name));
  return match ? decodeHtmlEntities(match[1].trim()) : null;
}

function hasHref(html, href) {
  return new RegExp(`<a\\b(?=[^>]*(?:^|\\s)href=["']${escapeRegExp(href)}["'])[^>]*>`, "i").test(html);
}

function hasAttribute(attributes, name) {
  return actualAttributePattern(name).test(String(attributes));
}

function actualAttributePattern(name, flags = "") {
  return new RegExp(`(?:^|\\s)${escapeRegExp(name)}\\s*=\\s*["']([^"']+)["']`, `i${flags}`);
}

function countWords(text) {
  const words = text.match(/\b[\w'-]+\b/g);
  return words ? words.length : 0;
}

function withEvidence(message, artifact) {
  return `${message} Evidence: ${artifact}.`;
}
