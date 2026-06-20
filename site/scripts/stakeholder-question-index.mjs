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

export const stakeholderQuestionIndexRoute = "/questions/";
export const stakeholderQuestionIndexRequiredLinks = [
  "/manager-packet/",
  "/use-cases/endpoint-review/",
  "/incident-evidence-handoff/",
  "/legacy-modernization/evidence-map/",
  "/proof-paths/",
  "/proof-source-catalog/",
  "/review-claim-checklist/",
  "/static-vs-runtime/",
  "/demo/result/",
  "/vault-export/",
  "/limitations/",
  "/validation/"
];

const requiredFamilies = new Set([
  "manager-planning",
  "engineer-endpoint-change-review",
  "incident-adjacent-handoff",
  "modernization-planning",
  "reviewer-claim-checking",
  "demo-evaluation",
  "proof-source-inspection",
  "agent-bot-discovery"
]);

const requiredFields = new Set([
  "audience",
  "question",
  "safeAnswerShape",
  "targetRoute",
  "evidenceSurface",
  "publicClaimLevel",
  "proofPath",
  "ruleIdOrFamily",
  "limitation",
  "nonClaim"
]);

const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "This page is an orientation index",
  "Start with the question",
  "Target-route claim levels remain separate",
  "rule ID or rule family",
  "evidence tier",
  "coverage label",
  "Agents and reviewers must not repeat a row after dropping its proof path"
];

const requiredAnchors = [
  "question-manager-planning",
  "question-engineer-endpoint-change-review",
  "question-incident-adjacent-handoff",
  "question-modernization-planning",
  "question-reviewer-claim-checking",
  "question-demo-evaluation",
  "question-proof-source-inspection",
  "question-agent-bot-discovery"
];

const expectedRouteMetadata = {
  publicClaimLevel: "concept",
  hintCategory: "use-case",
  sourceType: "site-page",
  preferredProofPath: "/proof-paths/"
};

const forbiddenPrivateText = [
  "/Users/",
  "/home/",
  "~/",
  "C:\\",
  "file://",
  "localhost",
  "127.0.0.1",
  "git@",
  ".git",
  "ConnectionString",
  "connection string",
  "Server=",
  "User Id=",
  "Password="
];

const forbiddenDirectProofTargets = [
  "facts.ndjson",
  "index.sqlite",
  "logs/analyzer.log",
  "analyzer.log",
  "scan-manifest.json",
  "report.md",
  "raw-sql",
  "connectionstring"
];

const forbiddenRawArtifactText = [
  "facts.ndjson",
  "index.sqlite",
  "logs/analyzer.log",
  "analyzer.log",
  "scan-manifest.json",
  "report.md",
  "raw SQL",
  "raw source snippets"
];

const forbiddenAffirmativeClaimPatterns = [
  /\bTraceMap\b[^.]{0,90}\b(?:proves?|guarantees?|certifies?|approves?|resolves?|replaces?)\b/gi,
  /\b(?:AI impact analysis|LLM analysis|prompt-based classification|embedding search|vector database analysis)\b/gi,
  /\b(?:runtime behavior|production traffic|endpoint performance|outage cause|release safety|operational safety|complete product coverage)\s+(?:proof|is proven|is guaranteed|is certified)\b/gi,
  /\b(?:safe to release|production-proven|runtime-safe|release-safe|automated approval|autonomous approval)\b/gi,
  /\bimpacted\b/gi
];

export async function validateStakeholderQuestionIndexDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors
}) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "questions", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence("Stakeholder question index page is missing required public route: /questions/", "questions/index.html"));
    errors.push(...localErrors);
    return;
  }

  await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  await validateRoutesIndex({ dist, errors: localErrors });
  await validateQuestionIndexPage({ pagePath, errors: localErrors });

  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${stakeholderQuestionIndexRoute}`)) {
    errors.push(withEvidence(`Stakeholder question index sitemap is missing required route: ${baseUrl}${stakeholderQuestionIndexRoute}`, "sitemap.xml"));
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
    errors.push(withEvidence(`Stakeholder question index could not parse routes-index.json: ${error.message}`, "routes-index.json"));
    return;
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Stakeholder question index routes-index.json is invalid: expected entries array", "routes-index.json"));
    return;
  }

  const routeEntry = parsed.entries.find((entry) => entry?.path === stakeholderQuestionIndexRoute);
  if (!routeEntry) {
    errors.push(withEvidence(`Stakeholder question index routes-index.json is missing required route: ${stakeholderQuestionIndexRoute}`, "routes-index.json"));
    return;
  }

  for (const [field, expected] of Object.entries(expectedRouteMetadata)) {
    if (routeEntry[field] !== expected) {
      errors.push(withEvidence(`Stakeholder question index routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, "routes-index.json"));
    }
  }

  const text = routeTextFields(routeEntry).join(" ");
  if (/\b(?:available|shipped|released|deployed)\b/i.test(text)) {
    errors.push(withEvidence("Stakeholder question index routes-index.json uses shipped wording for concept content.", "routes-index.json"));
  }

  validateRouteMetadataClaimBoundaryText(routeEntry, errors);

  const nonClaimsText = Array.isArray(routeEntry.nonClaims) ? routeEntry.nonClaims.join(" ") : "";
  for (const phrase of [
    "No runtime behavior",
    "production traffic",
    "endpoint performance",
    "outage cause",
    "release safety",
    "operational safety",
    "AI impact analysis",
    "LLM analysis",
    "complete product coverage"
  ]) {
    if (!nonClaimsText.includes(phrase)) {
      errors.push(withEvidence(`Stakeholder question index routes-index.json nonClaims are missing boundary phrase: ${phrase}`, "routes-index.json"));
    }
  }
}

async function validateQuestionIndexPage({ pagePath, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const pageText = normalizeRenderedText(html);
  const wordCount = countWords(pageText);

  for (const phrase of requiredText) {
    if (!pageText.toLowerCase().includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Stakeholder question index page is missing required text: ${phrase}`, "questions/index.html"));
    }
  }

  for (const anchor of requiredAnchors) {
    if (!hasId(html, anchor)) {
      errors.push(withEvidence(`Stakeholder question index page is missing required row anchor: ${anchor}`, "questions/index.html"));
    }
  }

  for (const link of stakeholderQuestionIndexRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Stakeholder question index page is missing required link: ${link}`, "questions/index.html"));
    }
  }

  if (!/<table\b[\s\S]*?\bdata-stakeholder-question-index\b[\s\S]*?<th\b[^>]*scope=["']col["'][\s\S]*?Audience[\s\S]*?<\/table>/i.test(html)) {
    errors.push(withEvidence("Stakeholder question index is missing an accessible question matrix table.", "questions/index.html"));
  }

  if (wordCount < 700 || wordCount > 2600) {
    errors.push(withEvidence(`Stakeholder question index word count must be between 700 and 2600 words, got ${wordCount}`, "questions/index.html"));
  }

  for (const text of forbiddenPrivateText) {
    if (containsForbiddenText(text, html, decodedHtml, pageText)) {
      errors.push(withEvidence(`Stakeholder question index page contains forbidden private text: ${text}`, "questions/index.html"));
    }
  }

  validateQuestionRows(html, errors);
  validateProofLinks(html, errors);
  validateRawArtifactText({ html, errors });
  validateClaimBoundaryText({ decodedHtml, html, pageText, errors });
}

function validateQuestionRows(html, errors) {
  const rows = extractRows(html, "data-question-row");
  const seenFamilies = new Set();
  const ids = new Set();

  if (rows.length === 0) {
    errors.push(withEvidence("Stakeholder question index has no data-question-row entries.", "questions/index.html"));
    return;
  }

  for (const row of rows) {
    const id = getAttribute(row.attributes, "id");
    const family = getAttribute(row.attributes, "data-question-family");

    if (!id) {
      errors.push(withEvidence("Stakeholder question index row is missing a stable id anchor.", "questions/index.html"));
    } else if (ids.has(id)) {
      errors.push(withEvidence(`Stakeholder question index row has duplicate id: ${id}`, "questions/index.html"));
    } else {
      ids.add(id);
    }

    if (!requiredFamilies.has(family)) {
      errors.push(withEvidence(`Stakeholder question index row has unexpected family: ${String(family)}`, "questions/index.html"));
    } else {
      seenFamilies.add(family);
    }

    const fields = extractCellsByField(row.body);
    for (const field of requiredFields) {
      if (!fields.has(field)) {
        errors.push(withEvidence(`Stakeholder question index ${family ?? "row"} is missing required field: ${field}`, "questions/index.html"));
        continue;
      }

      const text = normalizeRenderedText(fields.get(field));
      if (text.trim() === "") {
        errors.push(withEvidence(`Stakeholder question index ${family ?? "row"} has empty field: ${field}`, "questions/index.html"));
      }
    }

    const level = fields.has("publicClaimLevel") ? normalizeRenderedText(fields.get("publicClaimLevel")) : "";
    if (level !== "concept") {
      errors.push(withEvidence(`Stakeholder question index ${family ?? "row"} must use row-level concept claim level, got: ${level}`, "questions/index.html"));
    }

    for (const field of ["targetRoute", "proofPath"]) {
      if (fields.has(field) && !/<a\b[^>]*\bhref\s*=/i.test(fields.get(field))) {
        errors.push(withEvidence(`Stakeholder question index ${family ?? "row"} ${field} field must include at least one link.`, "questions/index.html"));
      }
    }
  }

  for (const family of requiredFamilies) {
    if (!seenFamilies.has(family)) {
      errors.push(withEvidence(`Stakeholder question index is missing required family: ${family}`, "questions/index.html"));
    }
  }
}

function validateProofLinks(html, errors) {
  for (const href of extractHrefs(html)) {
    const lower = href.toLowerCase();
    for (const target of forbiddenDirectProofTargets) {
      if (lower.includes(target)) {
        errors.push(withEvidence(`Stakeholder question index links directly to forbidden proof target: ${href}`, "questions/index.html"));
      }
    }
  }
}

function validateRawArtifactText({ html, errors }) {
  const unboundedHtml = stripBoundedClaimContext(html);
  const scanValues = [
    normalizeRenderedText(unboundedHtml),
    decodeHtmlEntities(unboundedHtml)
  ];

  for (const text of forbiddenRawArtifactText) {
    if (containsForbiddenText(text, ...scanValues)) {
      errors.push(withEvidence(`Stakeholder question index contains forbidden raw artifact text outside a limitation or non-claim: ${text}`, "questions/index.html"));
    }
  }
}

function validateClaimBoundaryText({ decodedHtml, html, pageText, errors }) {
  const boundedHtml = stripBoundedClaimContext(html);
  const boundedText = `${normalizeRenderedText(boundedHtml)} ${decodeHtmlEntities(boundedHtml)}`;
  const scanText = `${boundedText} ${stripBoundedClaimContext(decodedHtml)}`;

  for (const pattern of forbiddenAffirmativeClaimPatterns) {
    const matches = [...scanText.matchAll(pattern)];
    for (const match of matches) {
      errors.push(withEvidence(`Stakeholder question index contains forbidden unbounded claim wording: ${match[0]}`, "questions/index.html"));
    }
  }
}

function validateRouteMetadataClaimBoundaryText(routeEntry, errors) {
  const scanText = routeUnboundedTextFields(routeEntry).join(" ");

  for (const pattern of forbiddenAffirmativeClaimPatterns) {
    const matches = [...scanText.matchAll(pattern)];
    for (const match of matches) {
      errors.push(withEvidence(`Stakeholder question index routes-index.json contains forbidden unbounded claim wording: ${match[0]}`, "routes-index.json"));
    }
  }
}

function stripBoundedClaimContext(html) {
  return html
    .replace(/<td\b(?=[^>]*\bdata-field\s*=\s*["'](?:limitation|nonClaim)["'])[^>]*>[\s\S]*?<\/td>/gi, " ")
    .replace(/<section\b(?=[^>]*\bid\s*=\s*["']non-claims["'])[^>]*>[\s\S]*?<\/section>/gi, " ");
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
  return [...html.matchAll(/<a\b[^>]*\bhref\s*=\s*["']([^"']+)["']/gi)].map((match) => decodeHtmlEntities(match[1]));
}

function routeTextFields(routeEntry) {
  return [
    routeEntry.title,
    routeEntry.summary,
    ...(Array.isArray(routeEntry.limitations) ? routeEntry.limitations : [])
  ].filter((value) => typeof value === "string");
}

function routeUnboundedTextFields(routeEntry) {
  return [
    routeEntry.title,
    routeEntry.summary,
    ...(Array.isArray(routeEntry.limitations) ? routeEntry.limitations.filter((value) => !isNegatedMetadataBoundary(value)) : [])
  ].filter((value) => typeof value === "string");
}

function isNegatedMetadataBoundary(value) {
  return typeof value === "string" && /\b(?:no|not|does not|do not|cannot|can't|without)\b/i.test(value);
}

function containsForbiddenText(text, ...values) {
  const normalizedText = text.toLowerCase();
  return values.some((value) => value.toLowerCase().includes(normalizedText));
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
