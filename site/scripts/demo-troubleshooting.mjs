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

export const demoTroubleshootingRoute = "/demo/troubleshooting/";
export const demoTroubleshootingAdjacentRoutes = [
  "/demo/runbook/",
  "/demo/start-here/",
  "/demo/result/",
  "/demo/proof-upgrades/",
  "/validation/",
  "/limitations/",
  "/questions/objections/"
];

const pageArtifact = "demo/troubleshooting/index.html";
const sitemapArtifact = "sitemap.xml";
const routesIndexArtifact = "routes-index.json";
const rejectedMarker = "data-rejected-pattern-region";
const nonClaimMarker = "data-non-claim-region";

const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "When to use it",
  "What it is not",
  "Troubleshooting matrix",
  "Safe wording",
  "Rejected wording",
  "Adjacent routes",
  "Owner handoff",
  "Stop conditions and non-claims"
];

const requiredRows = [
  "missing route",
  "outdated demo summary",
  "broken proof expectation",
  "reduced coverage label",
  "private-only evidence",
  "unsupported claim wording",
  "validation mismatch",
  "where to ask next"
];

const requiredFields = [
  "symptom",
  "likely public-safe cause",
  "what to check",
  "what not to conclude",
  "next owner/route",
  "stop condition",
  "non-claim"
];

const requiredRowAssertions = new Map([
  ["missing route", ["evidence exists", "does not exist", "proves the claim"]],
  ["outdated demo summary", ["current-head", "current-release", "current-proof"]],
  ["broken proof expectation", ["public proof", "private or raw material"]],
  ["reduced coverage label", ["complete coverage", "absence of impact", "clean repo", "release safety"]],
  ["private-only evidence", ["not public proof", "public summary exists"]],
  ["unsupported claim wording", ["downgrade", "remove", "limitations", "validation"]],
  ["validation mismatch", ["validation passed", "public validation surface"]],
  ["where to ask next", ["transfers the question", "does not prove or approve"]]
]);

const adjacentRouteLabels = new Map([
  ["/demo/runbook/", "Demo runbook"],
  ["/demo/start-here/", "Demo start-here"],
  ["/demo/result/", "Demo result"],
  ["/demo/proof-upgrades/", "Proof upgrades"],
  ["/validation/", "Validation expectations"],
  ["/limitations/", "Limitations and non-claims"],
  ["/questions/objections/", "Questions and objections"]
]);

const safeWordingLabels = [
  "Missing route",
  "Stale summary",
  "Incomplete proof",
  "Reduced coverage",
  "Private-only evidence",
  "Unsupported wording",
  "Validation mismatch",
  "Owner handoff"
];

const rejectedExamples = [
  "The demo route is missing, so the claim is proven.",
  "The summary is probably fine, so current release wording is safe.",
  "Private-only evidence is enough public proof.",
  "Reduced coverage proves no impact.",
  "The proof route is confusing, but the release is approved.",
  "The page diagnoses runtime behavior or endpoint performance.",
  "TraceMap provides AI or LLM impact analysis.",
  "The demo uses prompt-based classification, embedding search, or vector database analysis.",
  "Troubleshooting replaces validation, tests, owner review, or human review."
];

const forbiddenAffirmativePatterns = [
  /\b(?:support contract|support SLA|SLA promise|response-time promise|ticketing channel|support channel|on-call path|guaranteed answer path)\b/i,
  /\b(?:diagnoses?|proves?|certif(?:y|ies|ied)|guarantees?|approves?)\s+(?:runtime|production|endpoint|release|operational|complete coverage|absence of impact)\b/i,
  /\b(?:release is approved|release wording is safe|enough public proof|proves no impact|claim is proven)\b/i,
  /\bTraceMap provides AI or LLM impact analysis\b/i,
  /\b(?:prompt-based classification|embedding search|vector database analysis)\b/i,
  /\bTroubleshooting replaces validation\b/i
];

const hardPrivatePatterns = [
  { label: "home directory path", pattern: /(?:^|[\s"'(])(?:\/Users\/|\/home\/|~\/)[^\s<>"']*/i },
  { label: "Windows user directory path", pattern: /[A-Z]:\\Users\\/i },
  { label: "file URL", pattern: /\bfile:\/\//i },
  { label: "localhost", pattern: /\blocalhost\b/i },
  { label: "loopback address", pattern: /\b127\.0\.0\.1\b/ },
  { label: "raw git remote", pattern: /\bgit@[\w.-]+:/i },
  { label: "raw ssh remote", pattern: /\bssh:\/\/[^\s<>"']+/i },
  { label: "credential-like value", pattern: /\b(?:Password|Secret|Token|ApiKey|ConnectionString)\s*=/i }
];

const internalArtifactSegments = new Set([".kiro", "specs"]);
const internalArtifactNames = new Set(["implementation-state.md", "tasks.md"]);
const blameWords = /\b(?:fault|blame|culprit|guilty|broken by|caused by team)\b/i;

export async function validateDemoTroubleshootingDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeDemoTroubleshootingBaseUrl(baseUrl, localErrors);
  const pagePath = resolve(dist, "demo", "troubleshooting", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(
      withEvidence("Demo troubleshooting page is missing required public route: /demo/troubleshooting/", pageArtifact)
    );
    errors.push(...localErrors);
    return;
  }

  if (cleanBaseUrl) {
    await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  }
  await validateRoutesIndex({ dist, errors: localErrors });

  const html = await readFile(pagePath, "utf8");
  validatePage({ html, errors: localErrors });

  errors.push(...localErrors);
}

function validatePage({ html, errors }) {
  const decodedHtml = decodeHtmlEntities(html);
  const pageText = normalizeRenderedText(decodedHtml);
  const metadataText = collectMetadataText(decodedHtml);
  const attributeText = collectAttributeText(decodedHtml);
  const mainHtml = extractMainHtml(decodedHtml);
  const unmarkedHtml = stripMarkedRegions(mainHtml);
  const unmarkedText = normalizeRenderedText(unmarkedHtml);
  const unmarkedCollapsedText = collapseTagSplitText(unmarkedHtml);
  const fullCollapsedText = collapseTagSplitText(decodedHtml);

  for (const phrase of requiredText) {
    if (!pageText.includes(phrase)) {
      errors.push(withEvidence(`Demo troubleshooting page is missing required text: ${phrase}`, pageArtifact));
    }
  }

  if (!hasCanonical(html, demoTroubleshootingRoute)) {
    errors.push(withEvidence("Demo troubleshooting page is missing canonical URL metadata.", pageArtifact));
  }
  if (!hasOgTypeArticle(html)) {
    errors.push(withEvidence('Demo troubleshooting page must include <meta property="og:type" content="article">.', pageArtifact));
  }
  if (!hasMetaDescription(html) || !/\bconcept\b/i.test(metadataText)) {
    errors.push(withEvidence("Demo troubleshooting page metadata must use concept-level wording.", pageArtifact));
  }

  validateMatrix(decodedHtml, errors);
  validateSafeAndRejectedWording({ decodedHtml, errors });
  validateAdjacentRoutes({ decodedHtml, errors });
  validateOwnerHandoff({ decodedHtml, errors });
  validateWordCount({ decodedHtml, errors });
  validateNoInternalArtifactDirections(decodedHtml, errors);
  validateHardPrivateMaterial(`${decodedHtml} ${pageText} ${fullCollapsedText} ${metadataText} ${attributeText}`, errors);
  validateForbiddenClaims(`${unmarkedHtml} ${unmarkedText} ${unmarkedCollapsedText} ${metadataText} ${attributeText}`, errors);
  validateBlameLanguage(`${pageText} ${metadataText} ${attributeText}`, errors);
}

function validateMatrix(html, errors) {
  if (!/<table\b[^>]*\bdata-demo-troubleshooting-matrix\b/i.test(html)) {
    errors.push(withEvidence("Demo troubleshooting page is missing the programmatic matrix marker.", pageArtifact));
  }

  if (!/<th\b[^>]*\bscope\s*=\s*["']col["'][^>]*>Scenario<\/th>/i.test(html)) {
    errors.push(withEvidence("Demo troubleshooting matrix must use column table headers.", pageArtifact));
  }

  for (const row of requiredRows) {
    const rowHtml = getTaggedBlockByAttribute(html, "tr", "data-demo-troubleshooting-row", row);
    if (!rowHtml) {
      errors.push(withEvidence(`Demo troubleshooting matrix is missing required row: ${row}`, pageArtifact));
      continue;
    }

    if (!/<th\b[^>]*\bscope\s*=\s*["']row["']/i.test(rowHtml)) {
      errors.push(withEvidence(`Demo troubleshooting row ${row} is missing row-header scope.`, pageArtifact));
    }

    for (const field of requiredFields) {
      if (!hasDataField(rowHtml, field)) {
        errors.push(withEvidence(`Demo troubleshooting row ${row} is missing required field: ${field}`, pageArtifact));
      }
    }

    for (const field of ["what not to conclude", "non-claim"]) {
      if (!hasMarkedDataField(rowHtml, field, nonClaimMarker)) {
        errors.push(withEvidence(`Demo troubleshooting row ${row} field ${field} is missing ${nonClaimMarker}.`, pageArtifact));
      }
    }

    for (const expected of requiredRowAssertions.get(row) ?? []) {
      if (!normalizeRenderedText(rowHtml).toLowerCase().includes(expected.toLowerCase())) {
        errors.push(withEvidence(`Demo troubleshooting row ${row} is missing required assertion: ${expected}`, pageArtifact));
      }
    }
  }
}

function validateSafeAndRejectedWording({ decodedHtml, errors }) {
  const rejectedRegion = getTaggedBlockByAttribute(
    decodedHtml,
    "section",
    rejectedMarker,
    "demo-troubleshooting-rejected"
  );

  if (!rejectedRegion) {
    errors.push(withEvidence(`Rejected wording must be inside a ${rejectedMarker} region.`, pageArtifact));
    return;
  }

  for (const example of rejectedExamples) {
    if (!normalizeRenderedText(rejectedRegion).includes(example)) {
      errors.push(withEvidence(`Rejected wording region is missing required rejected example: ${example}`, pageArtifact));
    }
  }

  for (const label of safeWordingLabels) {
    if (!decodedHtml.includes(`<strong>${label}</strong>`)) {
      errors.push(withEvidence(`Safe wording section is missing example label: ${label}`, pageArtifact));
    }
  }

  if ((decodedHtml.match(new RegExp(`\\b${rejectedMarker}\\b`, "g")) ?? []).length !== 1) {
    errors.push(withEvidence(`Demo troubleshooting page must have exactly one ${rejectedMarker} region.`, pageArtifact));
  }

  if ((decodedHtml.match(new RegExp(`\\b${nonClaimMarker}\\b`, "g")) ?? []).length < 18) {
    errors.push(withEvidence(`Demo troubleshooting page must mark matrix and boundary non-claims with ${nonClaimMarker}.`, pageArtifact));
  }
}

function validateAdjacentRoutes({ decodedHtml, errors }) {
  const adjacentSection = getTaggedBlockByAttribute(
    decodedHtml,
    "section",
    "data-demo-troubleshooting-section",
    "adjacent-routes"
  );

  if (!adjacentSection) {
    errors.push(withEvidence("Demo troubleshooting page is missing adjacent routes section marker.", pageArtifact));
    return;
  }

  for (const route of demoTroubleshootingAdjacentRoutes) {
    if (!hasHref(decodedHtml, route)) {
      errors.push(withEvidence(`Demo troubleshooting page is missing required adjacent route link: ${route}`, pageArtifact));
    }

    const label = adjacentRouteLabels.get(route);
    if (label && !hasAnchorText(adjacentSection, route, label)) {
      errors.push(withEvidence(`Demo troubleshooting adjacent route ${route} must use bounded anchor text: ${label}`, pageArtifact));
    }
  }

  for (const anchorText of extractAnchorTexts(decodedHtml)) {
    if (/^(?:here|more|learn more|click here)$/i.test(anchorText)) {
      errors.push(withEvidence(`Demo troubleshooting page uses unbounded anchor text: ${anchorText}`, pageArtifact));
    }
  }
}

function validateOwnerHandoff({ decodedHtml, errors }) {
  const section = getTaggedBlockByAttribute(decodedHtml, "section", "data-demo-troubleshooting-section", "owner-handoff");
  if (!section) {
    errors.push(withEvidence("Demo troubleshooting page is missing owner handoff section marker.", pageArtifact));
    return;
  }

  for (const label of ["Demo owner", "Site owner", "Validation owner", "Reviewer"]) {
    if (!section.includes(`<strong>${label}</strong>`)) {
      errors.push(withEvidence(`Demo troubleshooting owner handoff is missing role label: ${label}`, pageArtifact));
    }
  }

  if (/\b(?:SLA|ticket|on-call|response time|guaranteed answer|support channel)\b/i.test(normalizeRenderedText(section))) {
    errors.push(withEvidence("Demo troubleshooting owner handoff implies a support channel or service commitment.", pageArtifact));
  }
}

function validateWordCount({ decodedHtml, errors }) {
  const mainHtml = extractMainHtml(decodedHtml);
  const matrixless = mainHtml.replace(
    /<table\b(?=[^>]*\bdata-demo-troubleshooting-matrix\b)[^>]*>[\s\S]*?<\/table>/i,
    " "
  );
  const text = normalizeRenderedText(matrixless);
  const words = text === "" ? 0 : text.split(/\s+/).filter(Boolean).length;

  if (words < 700 || words > 1500) {
    errors.push(
      withEvidence(
        `Demo troubleshooting standalone visible body word count excluding required matrix must be 700 to 1500; found ${words}.`,
        pageArtifact
      )
    );
  }
}

function validateNoInternalArtifactDirections(html, errors) {
  const rows = [
    ...html.matchAll(/<tr\b(?=[^>]*\bdata-demo-troubleshooting-row\s*=\s*["'][^"']+["'])[^>]*>[\s\S]*?<\/tr>/gi)
  ].map((match) => match[0]);

  for (const row of rows) {
    for (const href of extractHrefs(row)) {
      const segments = href.split(/[/?#]+/).filter(Boolean);
      if (segments.some((segment) => internalArtifactSegments.has(segment)) || segments.some((segment) => internalArtifactNames.has(segment))) {
        errors.push(withEvidence(`Demo troubleshooting row directs visitors to internal artifact: ${href}`, pageArtifact));
      }
    }

    const text = normalizeRenderedText(row);
    for (const name of internalArtifactNames) {
      if (text.includes(name)) {
        errors.push(withEvidence(`Demo troubleshooting row names internal artifact: ${name}`, pageArtifact));
      }
    }
  }
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${demoTroubleshootingRoute}`)) {
    errors.push(
      withEvidence(`Demo troubleshooting sitemap is missing required route: ${baseUrl}${demoTroubleshootingRoute}`, sitemapArtifact)
    );
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
    errors.push(withEvidence(`Demo troubleshooting could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return;
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Demo troubleshooting routes-index.json is invalid: expected entries array", routesIndexArtifact));
    return;
  }

  const routeEntry = parsed.entries.find((entry) => entry?.path === demoTroubleshootingRoute);
  if (!routeEntry) {
    errors.push(withEvidence(`Demo troubleshooting routes-index.json is missing required route: ${demoTroubleshootingRoute}`, routesIndexArtifact));
    return;
  }

  const expectedFields = {
    publicClaimLevel: "concept",
    hintCategory: "demo",
    sourceType: "site-page",
    preferredProofPath: "/demo/runbook/"
  };

  for (const [field, expected] of Object.entries(expectedFields)) {
    if (routeEntry[field] !== expected) {
      errors.push(withEvidence(`Demo troubleshooting routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
    }
  }

  for (const field of ["title", "summary"]) {
    if (typeof routeEntry[field] !== "string" || routeEntry[field].trim() === "") {
      errors.push(withEvidence(`Demo troubleshooting routes-index.json is missing non-empty ${field}.`, routesIndexArtifact));
    }
  }

  for (const field of ["limitations", "nonClaims"]) {
    if (!Array.isArray(routeEntry[field]) || routeEntry[field].length === 0) {
      errors.push(withEvidence(`Demo troubleshooting routes-index.json is missing non-empty ${field}.`, routesIndexArtifact));
      continue;
    }

    validateHardPrivateMaterial(routeEntry[field].join(" "), errors, routesIndexArtifact);
  }

  validateHardPrivateMaterial(JSON.stringify(routeEntry), errors, routesIndexArtifact);
}

function validateForbiddenClaims(text, errors, artifact = pageArtifact) {
  for (const pattern of forbiddenAffirmativePatterns) {
    const match = text.match(pattern);
    if (match) {
      errors.push(withEvidence(`Demo troubleshooting page contains unsupported affirmative claim outside marked regions: ${match[0]}`, artifact));
    }
  }
}

function validateHardPrivateMaterial(text, errors, artifact = pageArtifact) {
  for (const { label, pattern } of hardPrivatePatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Demo troubleshooting page contains forbidden private or credential-like text: ${label}`, artifact));
    }
  }
}

function validateBlameLanguage(text, errors) {
  const match = text.match(blameWords);
  if (match) {
    errors.push(withEvidence(`Demo troubleshooting page contains blame language: ${match[0]}`, pageArtifact));
  }
}

function stripMarkedRegions(html) {
  let stripped = html;
  for (const attribute of [rejectedMarker, nonClaimMarker]) {
    stripped = stripped.replace(
      new RegExp(`<([a-z][a-z0-9:-]*)\\b(?=[^>]*\\b${attribute}\\b)[^>]*>[\\s\\S]*?<\\/\\1>`, "gi"),
      " "
    );
  }
  return stripped;
}

function extractMainHtml(html) {
  const match = html.match(/<main\b[^>]*>([\s\S]*?)<\/main>/i);
  return match ? match[1] : html;
}

function collectMetadataText(html) {
  return [...html.matchAll(/<meta\b[^>]*>/gi)]
    .map((match) => getHtmlAttribute(match[0], "content") ?? "")
    .filter(Boolean)
    .join(" ");
}

function collectAttributeText(html) {
  return [...html.matchAll(/\b(?:aria-label|title|alt)\s*=\s*["']([^"']*)["']/gi)].map((match) => match[1]).join(" ");
}

function getTaggedBlockByAttribute(html, tag, attribute, value) {
  const escapedAttribute = escapeRegExp(attribute);
  const escapedValue = escapeRegExp(value);
  const pattern = new RegExp(
    `<${tag}\\b(?=[^>]*\\b${escapedAttribute}\\s*=\\s*["']${escapedValue}["'])[^>]*>[\\s\\S]*?<\\/${tag}>`,
    "i"
  );
  return html.match(pattern)?.[0] ?? null;
}

function hasDataField(html, field) {
  const escaped = escapeRegExp(field);
  return new RegExp(`\\bdata-field\\s*=\\s*["']${escaped}["']`, "i").test(html);
}

function hasMarkedDataField(html, field, marker) {
  const escapedField = escapeRegExp(field);
  const escapedMarker = escapeRegExp(marker);
  return new RegExp(
    `<(?:td|th)\\b(?=[^>]*\\bdata-field\\s*=\\s*["']${escapedField}["'])(?=[^>]*\\b${escapedMarker}\\b)[^>]*>`,
    "i"
  ).test(html);
}

function hasHref(html, href) {
  const escaped = escapeRegExp(href);
  return new RegExp(`<a\\b[^>]*\\bhref\\s*=\\s*["']${escaped}["']`, "i").test(html);
}

function hasAnchorText(html, href, text) {
  const escapedHref = escapeRegExp(href);
  const escapedText = escapeRegExp(text);
  return new RegExp(`<a\\b[^>]*\\bhref\\s*=\\s*["']${escapedHref}["'][^>]*>\\s*${escapedText}\\s*<\\/a>`, "i").test(html);
}

function extractAnchorTexts(html) {
  return [...html.matchAll(/<a\b[^>]*>([\s\S]*?)<\/a>/gi)].map((match) => normalizeRenderedText(match[1]));
}

function collapseTagSplitText(html) {
  return normalizeRenderedText(String(html).replace(/<[^>]+>/g, ""));
}

function getHtmlAttribute(html, name) {
  const match = html.match(new RegExp(`\\b${escapeRegExp(name)}\\s*=\\s*(["'])(.*?)\\1`, "i"));
  return match ? match[2] : null;
}

function extractHrefs(html) {
  return [...html.matchAll(/\bhref\s*=\s*["']([^"']+)["']/gi)].map((match) => match[1]);
}

function hasCanonical(html, route) {
  return new RegExp(
    `<link\\b(?=[^>]*\\brel\\s*=\\s*["']canonical["'])(?=[^>]*\\bhref\\s*=\\s*["']https://tracemap\\.tools${escapeRegExp(route)}["'])[^>]*>`,
    "i"
  ).test(html);
}

function hasMetaDescription(html) {
  return [...html.matchAll(/<meta\b[^>]*>/gi)].some(
    (match) => getHtmlAttribute(match[0], "name") === "description" && Boolean(getHtmlAttribute(match[0], "content"))
  );
}

function hasOgTypeArticle(html) {
  return /<meta\b(?=[^>]*\bproperty\s*=\s*["']og:type["'])(?=[^>]*\bcontent\s*=\s*["']article["'])[^>]*>/i.test(html);
}

function normalizeDemoTroubleshootingBaseUrl(value, errors) {
  try {
    return normalizeBaseUrl(new URL(value).origin);
  } catch {
    errors.push(withEvidence(`Demo troubleshooting baseUrl must be a valid absolute URL: ${String(value)}`, "baseUrl input"));
    return null;
  }
}

function withEvidence(message, artifact) {
  return `${message} Evidence: ${artifact}.`;
}
