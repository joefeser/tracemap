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

export const reviewClaimChecklistRoute = "/review-claim-checklist/";
export const reviewClaimChecklistRequiredLinks = [
  "/review-room/",
  "/manager-faq/",
  "/proof-paths/",
  "/roadmap/#claim-ledger"
];
export const reviewClaimChecklistInboundRoutes = [
  "/review-room/",
  "/manager-faq/",
  "/proof-paths/",
  "/roadmap/"
];

const checklistFields = new Set([
  "claim statement",
  "public claim level",
  "proof path",
  "rule ID or rule family",
  "evidence tier",
  "coverage label",
  "limitation",
  "non-claims",
  "source branch or main-dev status",
  "owner follow-up",
  "reviewer",
  "review date",
  "decision"
]);
const claimLevels = new Set(["shipped", "demo", "concept", "hidden"]);
const evidenceTiers = new Set(["Tier1Semantic", "Tier2Structural", "Tier3SyntaxOrTextual", "Tier4Unknown"]);
const reviewOutcomes = new Set([
  "repeat with proof",
  "downgrade before repeating",
  "owner follow-up needed",
  "do not repeat",
  "internal only"
]);
const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "claim statement",
  "public claim level",
  "proof path",
  "rule ID or rule family",
  "evidence tier",
  "coverage label",
  "limitation",
  "non-claims",
  "source branch or main-dev status",
  "owner follow-up",
  "reviewer",
  "review date",
  "decision",
  "repeat with proof",
  "downgrade before repeating",
  "owner follow-up needed",
  "do not repeat",
  "internal only",
  "TraceMap does not prove runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, AI impact analysis, LLM analysis, or complete product coverage.",
  "TraceMap does not replace telemetry, logs, traces, tests, source review, ownership decisions, incident response, or release approval."
];
const stopConditions = [
  "missing proof path",
  "private-only artifact",
  "hidden claim detail",
  "unsupported demo claim",
  "forbidden wording"
];
const forbiddenProofPathTargets = [
  "facts.ndjson",
  "index.sqlite",
  "logs/analyzer.log",
  "analyzer.log",
  "scan-manifest.json",
  "report.md",
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
const forbiddenPositioning = /\b(AI[- ]?powered|LLM[- ]?powered|machine learning impact analysis|artificial intelligence impact analysis|embedding-backed|prompt-classified|automated release approval|operational assurance|runtime-safe|release-safe|production-proven)\b/i;
const overclaimPattern = /\b(impacted|safe|unsafe|approved|blocked|root cause|validated for release|production proven|complete coverage|complete product coverage)\b/gi;
const affirmativeProofPattern =
  /\b(?:TraceMap\s+)?(?:proves?|proven|proof of)\s+(?:runtime behavior|production traffic|production behavior|endpoint performance|outage cause|release safety|operational safety|release approval|complete product coverage)\b/gi;
const sanctionedBoundarySectionPattern =
  /<section\b(?=[^>]*\bid\s*=\s*["'](?:non-claims|stop-conditions|private-material)["'])[^>]*>[\s\S]*?<\/section>/gi;

export async function validateReviewClaimChecklistDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "review-claim-checklist", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence("Review claim checklist page is missing required public route: /review-claim-checklist/", "review-claim-checklist/index.html"));
    errors.push(...localErrors);
    return;
  }

  await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  await validateRoutesIndex({ dist, errors: localErrors });
  await validateChecklistPage({ pagePath, errors: localErrors });
  await validateInboundLinks({ dist, errors: localErrors });

  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${reviewClaimChecklistRoute}`)) {
    errors.push(withEvidence(`Review claim checklist sitemap is missing required route: ${baseUrl}${reviewClaimChecklistRoute}`, "sitemap.xml"));
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
    errors.push(withEvidence(`Review claim checklist could not parse routes-index.json: ${error.message}`, "routes-index.json"));
    return;
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Review claim checklist routes-index.json is invalid: expected entries array", "routes-index.json"));
    return;
  }

  const routeEntry = parsed.entries.find((entry) => entry?.path === reviewClaimChecklistRoute);
  if (!routeEntry) {
    errors.push(withEvidence(`Review claim checklist routes-index.json is missing required route: ${reviewClaimChecklistRoute}`, "routes-index.json"));
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
      errors.push(withEvidence(`Review claim checklist routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, "routes-index.json"));
    }
  }
}

async function validateChecklistPage({ pagePath, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const pageText = normalizeRenderedText(html);
  const lowerPageText = pageText.toLowerCase();
  const wordCount = countWords(pageText);

  for (const phrase of requiredText) {
    if (!lowerPageText.includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Review claim checklist page is missing required text: ${phrase}`, "review-claim-checklist/index.html"));
    }
  }

  for (const condition of stopConditions) {
    if (!lowerPageText.includes(condition)) {
      errors.push(withEvidence(`Review claim checklist page is missing stop condition: ${condition}`, "review-claim-checklist/index.html"));
    }
  }

  for (const link of reviewClaimChecklistRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Review claim checklist page is missing required link: ${link}`, "review-claim-checklist/index.html"));
    }
  }

  if (!hasOgTypeArticle(html)) {
    errors.push(withEvidence('Review claim checklist page must include <meta property="og:type" content="article">.', "review-claim-checklist/index.html"));
  }

  if (wordCount < 650 || wordCount > 2200) {
    errors.push(withEvidence(`Review claim checklist page word count must be between 650 and 2200 words, got ${wordCount}`, "review-claim-checklist/index.html"));
  }

  for (const text of forbiddenPrivateText) {
    const lowerText = text.toLowerCase();
    if (decodedHtml.toLowerCase().includes(lowerText) || pageText.toLowerCase().includes(lowerText)) {
      errors.push(withEvidence(`Review claim checklist page contains forbidden private text: ${text}`, "review-claim-checklist/index.html"));
    }
  }

  validateChecklistFieldRows(html, errors);
  validateExampleRows(html, errors);
  validateProofLinks(html, errors);
  validateClaimBoundaryText({ html, pageText, errors });
}

function validateChecklistFieldRows(html, errors) {
  const rows = extractRows(html, "data-checklist-field");
  const seen = new Set();

  if (rows.length === 0) {
    errors.push(withEvidence("Review claim checklist has no data-checklist-field rows.", "review-claim-checklist/index.html"));
    return;
  }

  for (const row of rows) {
    const field = getAttribute(row.attributes, "data-checklist-field");
    if (!checklistFields.has(field)) {
      errors.push(withEvidence(`Review claim checklist row has unexpected field: ${String(field)}`, "review-claim-checklist/index.html"));
    } else {
      seen.add(field);
    }
  }

  for (const field of checklistFields) {
    if (!seen.has(field)) {
      errors.push(withEvidence(`Review claim checklist is missing required field row: ${field}`, "review-claim-checklist/index.html"));
    }
  }
}

function validateExampleRows(html, errors) {
  if (!/\bIllustrative examples\b/i.test(normalizeRenderedText(html))) {
    errors.push(withEvidence("Review claim checklist example rows must be labeled as illustrative.", "review-claim-checklist/index.html"));
  }

  const rows = extractRows(html, "data-example-row");
  if (rows.length === 0) {
    errors.push(withEvidence("Review claim checklist has no illustrative data-example-row entries.", "review-claim-checklist/index.html"));
    return;
  }

  const seenOutcomes = new Set();
  for (const row of rows) {
    const level = getAttribute(row.attributes, "data-public-claim-level");
    const outcome = getAttribute(row.attributes, "data-review-outcome");
    const text = normalizeRenderedText(row.html);

    if (!claimLevels.has(level)) {
      errors.push(withEvidence(`Review claim checklist example row has invalid public claim level: ${String(level)}`, "review-claim-checklist/index.html"));
    }
    if (!reviewOutcomes.has(outcome)) {
      errors.push(withEvidence(`Review claim checklist example row has invalid review outcome: ${String(outcome)}`, "review-claim-checklist/index.html"));
    } else {
      seenOutcomes.add(outcome);
    }
    if (!containsAny(text, evidenceTiers)) {
      errors.push(withEvidence("Review claim checklist example row is missing an allowed evidence tier.", "review-claim-checklist/index.html"));
    }
    if (/\b20\d{2}-\d{2}-\d{2}\b/.test(text)) {
      errors.push(withEvidence("Review claim checklist example row contains a real-looking review date.", "review-claim-checklist/index.html"));
    }
    if (/\b(Joe Feser|assignee|reviewer name|owner name)\b/i.test(text)) {
      errors.push(withEvidence("Review claim checklist example row contains a forbidden reviewer or owner identity placeholder.", "review-claim-checklist/index.html"));
    }
  }

  if (!seenOutcomes.has("repeat with proof") || !seenOutcomes.has("owner follow-up needed")) {
    errors.push(withEvidence("Review claim checklist example rows should demonstrate repeat with proof and owner follow-up needed outcomes.", "review-claim-checklist/index.html"));
  }
}

function validateProofLinks(html, errors) {
  for (const href of extractHrefs(html)) {
    const normalizedHref = href.toLowerCase();
    for (const target of forbiddenProofPathTargets) {
      if (normalizedHref.includes(target.toLowerCase())) {
        errors.push(withEvidence(`Review claim checklist links to forbidden raw proof artifact: ${href}`, "review-claim-checklist/index.html"));
      }
    }
  }
}

function validateClaimBoundaryText({ html, pageText, errors }) {
  const sanctionedText = normalizeRenderedText(extractSanctionedBoundaryHtml(html));
  const reviewText = normalizeRenderedText(stripSanctionedBoundaryHtml(html)).replace(/\bpublic-safe\b/gi, "public evidence");
  const positioningText = `${html} ${pageText}`;

  if (forbiddenPositioning.test(positioningText)) {
    errors.push(withEvidence("Review claim checklist contains forbidden AI, release, or production positioning.", "review-claim-checklist/index.html"));
  }
  if (hasUnsanctionedOverclaim(reviewText)) {
    errors.push(withEvidence("Review claim checklist contains runtime, production, or release overclaim wording outside sanctioned boundary copy.", "review-claim-checklist/index.html"));
  }
  if (hasUnsanctionedProofClaim(reviewText)) {
    errors.push(withEvidence("Review claim checklist contains affirmative runtime, production, or release proof wording outside sanctioned boundary copy.", "review-claim-checklist/index.html"));
  }
  if (!sanctionedText.includes("AI impact analysis") || !sanctionedText.includes("LLM analysis")) {
    errors.push(withEvidence("Review claim checklist sanctioned boundary copy must include AI and LLM non-claims.", "review-claim-checklist/index.html"));
  }
}

function extractSanctionedBoundaryHtml(html) {
  const matches = html.match(sanctionedBoundarySectionPattern);
  return matches?.join(" ") ?? "";
}

function stripSanctionedBoundaryHtml(html) {
  return html.replace(sanctionedBoundarySectionPattern, " ");
}

async function validateInboundLinks({ dist, errors }) {
  const missing = [];

  for (const route of reviewClaimChecklistInboundRoutes) {
    const pagePath = resolve(dist, route.replace(/^\/|\/$/g, ""), "index.html");
    if (!(await fileExists(pagePath))) {
      continue;
    }

    const html = await readFile(pagePath, "utf8");
    if (!hasHref(html, reviewClaimChecklistRoute)) {
      missing.push(route);
    }
  }

  if (missing.length > 0) {
    errors.push(withEvidence(`Review claim checklist is missing inbound links from live adjacent routes: ${missing.join(", ")}`, "adjacent route HTML"));
  }
}

function hasUnsanctionedProofClaim(value) {
  affirmativeProofPattern.lastIndex = 0;
  for (const match of value.matchAll(affirmativeProofPattern)) {
    const prefix = value.slice(Math.max(0, match.index - 32), match.index).toLowerCase();
    if (!/(?:cannot|can't|does not|do not|not|without)\s+$/.test(prefix)) {
      return true;
    }
  }
  return false;
}

function hasUnsanctionedOverclaim(value) {
  overclaimPattern.lastIndex = 0;
  for (const match of value.matchAll(overclaimPattern)) {
    const prefix = value.slice(Math.max(0, match.index - 32), match.index).toLowerCase();
    if (!/(?:cannot|can't|does not|do not|not|no|without)\s+$/.test(prefix)) {
      return true;
    }
  }
  return false;
}

function extractRows(html, marker) {
  const pattern = new RegExp(`<tr\\b(?=[^>]*\\b${escapeRegExp(marker)}\\b)([^>]*)>[\\s\\S]*?<\\/tr>`, "gi");
  return [...html.matchAll(pattern)].map((match) => ({ attributes: match[1], html: match[0] }));
}

function extractHrefs(html) {
  return [...html.matchAll(/<a\b[^>]*\bhref\s*=\s*["']([^"']+)["']/gi)].map((match) => decodeHtmlEntities(match[1]));
}

function getAttribute(attributes, name) {
  const match = attributes.match(new RegExp(`\\b${escapeRegExp(name)}=["']([^"']*)["']`, "i"));
  return match ? decodeHtmlEntities(match[1]) : null;
}

function hasHref(html, href) {
  const escaped = escapeRegExp(href);
  return new RegExp(`<a\\b[^>]*\\bhref\\s*=\\s*["']${escaped}["']`, "i").test(html);
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
