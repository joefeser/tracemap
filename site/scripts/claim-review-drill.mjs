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

export const claimReviewDrillRoute = "/review-claim-checklist/drill/";
export const claimReviewDrillRequiredLinks = [
  "/review-claim-checklist/",
  "/proof-paths/tour/",
  "/proof-paths/faq/",
  "/questions/objections/",
  "/packets/examples/",
  "/language/change-risk/"
];
export const claimReviewDrillInboundRoutes = claimReviewDrillRequiredLinks;

const pageArtifact = "review-claim-checklist/drill/index.html";
const requiredSections = new Set([
  "drill-setup",
  "sample-public-safe-claims",
  "evidence-checklist",
  "answer-key",
  "unsafe-answer-examples",
  "stop-conditions",
  "non-claims"
]);
const requiredScenarios = new Set([
  "supported demo-level claim",
  "concept-only claim",
  "reduced-coverage claim",
  "unsafe runtime claim",
  "unsafe release claim",
  "private-evidence-only claim",
  "missing-proof claim"
]);
const requiredRowFields = new Set([
  "claim text",
  "expected claim level",
  "proof path needed",
  "evidence fields to check",
  "limitation or non-claim",
  "correct outcome",
  "next action"
]);
const requiredEvidenceFields = new Set([
  "proof path",
  "rule ID or rule family",
  "evidence tier",
  "coverage label",
  "limitation",
  "non-claim",
  "source context",
  "public/private status"
]);
const claimLevels = new Set(["shipped", "demo", "concept", "hidden"]);
const evidenceTiers = new Set(["Tier1Semantic", "Tier2Structural", "Tier3SyntaxOrTextual", "Tier4Unknown"]);
const answerOutcomes = new Set([
  "repeat with proof",
  "downgrade before repeating",
  "owner follow-up needed",
  "do not repeat",
  "internal only"
]);
const allowedScenarioOutcomes = new Map([
  ["supported demo-level claim", new Set(["repeat with proof"])],
  ["concept-only claim", new Set(["downgrade before repeating"])],
  ["reduced-coverage claim", new Set(["downgrade before repeating", "owner follow-up needed"])],
  ["unsafe runtime claim", new Set(["do not repeat"])],
  ["unsafe release claim", new Set(["do not repeat"])],
  ["private-evidence-only claim", new Set(["internal only", "owner follow-up needed"])],
  ["missing-proof claim", new Set(["do not repeat", "owner follow-up needed"])]
]);
const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "learning exercise, not an automated grader",
  "proof path",
  "rule ID or rule family",
  "evidence tier",
  "coverage label",
  "limitation",
  "non-claim",
  "source context",
  "public/private status",
  "repeat with proof",
  "downgrade before repeating",
  "owner follow-up needed",
  "do not repeat",
  "internal only"
];
const forbiddenProofPathTargets = [
  "facts.ndjson",
  "index.sqlite",
  "logs/analyzer.log",
  "analyzer.log",
  "scan-manifest.json",
  "raw-sql",
  "connectionstring"
];
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
const blamePattern = /\b(fault|to blame|negligent|careless)\b/i;
const forbiddenPositioning =
  /\b(AI[- ]?powered|LLM[- ]?powered|machine learning impact analysis|artificial intelligence impact analysis|embedding-backed|prompt-classified|automated grading system|automated release approval|operational assurance|runtime-safe|release-safe|production-proven)\b/i;
const affirmativeProofPattern =
  /\b(?:TraceMap\s+)?(?:proves?|proven|proof\s+of)\b(?:(?![.!?]).){0,120}\b(?:runtime behavior|production traffic|production behavior|endpoint performance|endpoint\s+(?:stayed\s+)?fast|outage cause|release safety|release safe|operational safety|operationally safe|release approval|absence of impact|complete coverage|complete product coverage)\b/gi;
const sanctionedBoundarySectionPattern =
  /<section\b(?=[^>]*\bid\s*=\s*["'](?:sample-public-safe-claims|unsafe-answer-examples|stop-conditions|non-claims)["'])[^>]*>[\s\S]*?<\/section>/gi;

export async function validateClaimReviewDrillDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "review-claim-checklist", "drill", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence("Claim review drill page is missing required public route: /review-claim-checklist/drill/", pageArtifact));
    errors.push(...localErrors);
    return;
  }

  await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  await validateRoutesIndex({ dist, errors: localErrors });
  await validateDrillPage({ pagePath, errors: localErrors });
  await validateInboundLinks({ dist, errors: localErrors });

  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${claimReviewDrillRoute}`)) {
    errors.push(withEvidence(`Claim review drill sitemap is missing required route: ${baseUrl}${claimReviewDrillRoute}`, "sitemap.xml"));
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
    errors.push(withEvidence(`Claim review drill could not parse routes-index.json: ${error.message}`, "routes-index.json"));
    return;
  }

  const routeEntry = parsed?.entries?.find((entry) => entry?.path === claimReviewDrillRoute);
  if (!routeEntry) {
    errors.push(withEvidence(`Claim review drill routes-index.json is missing required route: ${claimReviewDrillRoute}`, "routes-index.json"));
    return;
  }

  const expectedFields = {
    publicClaimLevel: "concept",
    hintCategory: "use-case",
    sourceType: "site-page",
    preferredProofPath: "/review-claim-checklist/"
  };

  for (const [field, expected] of Object.entries(expectedFields)) {
    if (routeEntry[field] !== expected) {
      errors.push(withEvidence(`Claim review drill routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, "routes-index.json"));
    }
  }
}

async function validateDrillPage({ pagePath, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const pageText = normalizeRenderedText(html);
  const lowerPageText = pageText.toLowerCase();
  const wordCount = countWords(pageText);

  for (const phrase of requiredText) {
    if (!lowerPageText.includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Claim review drill page is missing required text: ${phrase}`, pageArtifact));
    }
  }

  for (const section of requiredSections) {
    if (!hasSection(html, section)) {
      errors.push(withEvidence(`Claim review drill page is missing required section: ${section}`, pageArtifact));
    }
  }

  for (const link of claimReviewDrillRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Claim review drill page is missing required adjacent link: ${link}`, pageArtifact));
    }
  }

  if (!hasOgTypeArticle(html)) {
    errors.push(withEvidence('Claim review drill page must include <meta property="og:type" content="article">.', pageArtifact));
  }

  if (wordCount < 500 || wordCount > 1800) {
    errors.push(withEvidence(`Claim review drill page word count must be between 500 and 1800 words, got ${wordCount}`, pageArtifact));
  }

  validateRows(html, errors);
  validateAnswerKey(html, errors);
  validateProofLinks(html, errors);
  validateBoundaryText({ decodedHtml, html, pageText, errors });
}

function validateRows(html, errors) {
  const rows = extractRows(html, "data-drill-row");
  const seen = new Set();

  if (rows.length !== requiredScenarios.size) {
    errors.push(withEvidence(`Claim review drill expected ${requiredScenarios.size} drill rows, got ${rows.length}`, pageArtifact));
  }

  for (const row of rows) {
    const scenario = getAttribute(row.attributes, "data-drill-scenario");
    const level = getAttribute(row.attributes, "data-expected-claim-level");
    const outcome = getAttribute(row.attributes, "data-correct-outcome");
    const text = normalizeRenderedText(row.html);

    if (!requiredScenarios.has(scenario)) {
      errors.push(withEvidence(`Claim review drill row has unexpected scenario: ${String(scenario)}`, pageArtifact));
    } else {
      seen.add(scenario);
    }
    if (!claimLevels.has(level)) {
      errors.push(withEvidence(`Claim review drill row has invalid expected claim level: ${String(level)}`, pageArtifact));
    }
    if (!answerOutcomes.has(outcome)) {
      errors.push(withEvidence(`Claim review drill row has invalid correct outcome: ${String(outcome)}`, pageArtifact));
    } else if (!allowedScenarioOutcomes.get(scenario)?.has(outcome)) {
      errors.push(withEvidence(`Claim review drill scenario "${String(scenario)}" has disallowed outcome: ${outcome}`, pageArtifact));
    }

    validateRowFields(row.html, scenario, errors);
    if (!containsAny(text, evidenceTiers)) {
      errors.push(withEvidence(`Claim review drill row "${String(scenario)}" is missing an allowed evidence tier.`, pageArtifact));
    }
  }

  for (const scenario of requiredScenarios) {
    if (!seen.has(scenario)) {
      errors.push(withEvidence(`Claim review drill is missing required scenario: ${scenario}`, pageArtifact));
    }
  }
}

function validateRowFields(rowHtml, scenario, errors) {
  const fields = new Set(extractElementsWithAttribute(rowHtml, "data-row-field").map((element) => getAttribute(element.attributes, "data-row-field")));

  for (const field of requiredRowFields) {
    if (!fields.has(field)) {
      errors.push(withEvidence(`Claim review drill row "${String(scenario)}" is missing required row field: ${field}`, pageArtifact));
    }
  }

  const evidenceCell = extractElementsWithAttribute(rowHtml, "data-row-field").find(
    (element) => getAttribute(element.attributes, "data-row-field") === "evidence fields to check"
  );
  const evidenceFieldElements = evidenceCell ? extractElementsWithAttribute(evidenceCell.html, "data-evidence-field") : [];
  const evidenceFields = new Set(evidenceFieldElements.map((element) => getAttribute(element.attributes, "data-evidence-field")));

  for (const field of requiredEvidenceFields) {
    if (!evidenceFields.has(field)) {
      errors.push(withEvidence(`Claim review drill row "${String(scenario)}" evidence fields do not enumerate: ${field}`, pageArtifact));
    }
  }
}

function validateAnswerKey(html, errors) {
  const rows = extractRows(html, "data-answer-key-row");
  const seen = new Set();

  if (rows.length !== requiredScenarios.size) {
    errors.push(withEvidence(`Claim review drill answer key expected ${requiredScenarios.size} rows, got ${rows.length}`, pageArtifact));
  }

  for (const row of rows) {
    const scenario = getAttribute(row.attributes, "data-answer-scenario");
    const outcome = getAttribute(row.attributes, "data-answer-outcome");

    if (!requiredScenarios.has(scenario)) {
      errors.push(withEvidence(`Claim review drill answer key has unexpected scenario: ${String(scenario)}`, pageArtifact));
    } else {
      seen.add(scenario);
    }
    if (!answerOutcomes.has(outcome)) {
      errors.push(withEvidence(`Claim review drill answer key has invalid outcome: ${String(outcome)}`, pageArtifact));
    } else if (!allowedScenarioOutcomes.get(scenario)?.has(outcome)) {
      errors.push(withEvidence(`Claim review drill answer key scenario "${String(scenario)}" has disallowed outcome: ${outcome}`, pageArtifact));
    }
  }

  for (const scenario of requiredScenarios) {
    if (!seen.has(scenario)) {
      errors.push(withEvidence(`Claim review drill answer key is missing scenario: ${scenario}`, pageArtifact));
    }
  }
}

function validateProofLinks(html, errors) {
  for (const href of extractHrefs(html)) {
    const normalizedHref = href.toLowerCase();
    for (const target of forbiddenProofPathTargets) {
      if (normalizedHref.includes(target.toLowerCase())) {
        errors.push(withEvidence(`Claim review drill links to forbidden raw proof artifact: ${href}`, pageArtifact));
      }
    }
  }
}

function validateBoundaryText({ decodedHtml, html, pageText, errors }) {
  const reviewText = normalizeRenderedText(stripSanctionedBoundaryHtml(html)).replace(/\bpublic-safe\b/gi, "public evidence");
  const positioningText = `${html} ${decodedHtml} ${pageText}`;

  for (const text of forbiddenPrivateText) {
    const lowerText = text.toLowerCase();
    if (decodedHtml.toLowerCase().includes(lowerText) || pageText.toLowerCase().includes(lowerText)) {
      errors.push(withEvidence(`Claim review drill page contains forbidden private text: ${text}`, pageArtifact));
    }
  }

  if (blamePattern.test(positioningText)) {
    errors.push(withEvidence("Claim review drill page contains blame language.", pageArtifact));
  }
  if (forbiddenPositioning.test(positioningText)) {
    errors.push(withEvidence("Claim review drill contains forbidden AI, release, or production positioning.", pageArtifact));
  }
  if (hasUnsanctionedProofClaim(reviewText)) {
    errors.push(withEvidence("Claim review drill contains affirmative runtime, release, safety, or complete-coverage proof wording outside sanctioned rejected-example or boundary copy.", pageArtifact));
  }
}

async function validateInboundLinks({ dist, errors }) {
  const missing = [];

  for (const route of claimReviewDrillInboundRoutes) {
    const pagePath = resolve(dist, route.replace(/^\/|\/$/g, ""), "index.html");
    if (!(await fileExists(pagePath))) {
      continue;
    }

    const html = await readFile(pagePath, "utf8");
    if (!hasHref(html, claimReviewDrillRoute)) {
      missing.push(route);
    }
  }

  if (missing.length > 0) {
    errors.push(withEvidence(`Claim review drill is missing inbound links from live adjacent routes: ${missing.join(", ")}`, "adjacent route HTML"));
  }
}

function hasUnsanctionedProofClaim(value) {
  affirmativeProofPattern.lastIndex = 0;
  for (const match of value.matchAll(affirmativeProofPattern)) {
    const prefix = value.slice(Math.max(0, match.index - 40), match.index).toLowerCase();
    if (!/(?:cannot|can't|does not|do not|not|no|without|neither)(?:\s+\w+){0,4}\s+$/.test(prefix)) {
      return true;
    }
  }
  return false;
}

function stripSanctionedBoundaryHtml(html) {
  return html.replace(sanctionedBoundarySectionPattern, " ");
}

function extractRows(html, marker) {
  const pattern = new RegExp(`<tr\\b(?=[^>]*\\b${escapeRegExp(marker)}\\b)([^>]*)>[\\s\\S]*?<\\/tr>`, "gi");
  return [...html.matchAll(pattern)].map((match) => ({ attributes: match[1], html: match[0] }));
}

function extractElementsWithAttribute(html, marker) {
  const pattern = new RegExp(`<([a-z0-9]+)\\b(?=[^>]*\\b${escapeRegExp(marker)}\\b)([^>]*)>[\\s\\S]*?<\\/\\1>`, "gi");
  return [...html.matchAll(pattern)].map((match) => ({ attributes: match[2], html: match[0], tag: match[1] }));
}

function extractHrefs(html) {
  return [...html.matchAll(/<[a-z][^>]*\s(?:href|src)\s*=\s*["']([^"']+)["'][^>]*>/gi)].map((match) => decodeHtmlEntities(match[1]));
}

function getAttribute(attributes, name) {
  const match = attributes?.match(new RegExp(`\\b${escapeRegExp(name)}=["']([^"']*)["']`, "i"));
  return match ? decodeHtmlEntities(match[1]) : null;
}

function hasHref(html, href) {
  const escaped = escapeRegExp(href);
  return new RegExp(`<a\\b[^>]*\\shref\\s*=\\s*["']${escaped}["']`, "i").test(html);
}

function hasSection(html, id) {
  const escaped = escapeRegExp(id);
  return new RegExp(`<section\\b[^>]*\\bid\\s*=\\s*["']${escaped}["']`, "i").test(html);
}

function hasOgTypeArticle(html) {
  return /<meta\b(?=[^>]*\bproperty\s*=\s*["']og:type["'])(?=[^>]*\bcontent\s*=\s*["']article["'])[^>]*>/i.test(html);
}

function containsAny(value, allowedValues) {
  for (const allowed of allowedValues) {
    if (value.includes(allowed)) {
      return true;
    }
  }
  return false;
}

function countWords(value) {
  return (value.match(/\b[\w'-]+\b/g) ?? []).length;
}

function withEvidence(message, artifact) {
  return `${message} Evidence: ${artifact}.`;
}
