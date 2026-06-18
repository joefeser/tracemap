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

export const managerFaqRoute = "/manager-faq/";
export const managerFaqRequiredLinks = [
  "/manager-brief/",
  "/manager-packet/",
  "/review-room/",
  "/limitations/",
  "/validation/",
  "/proof-paths/"
];

export const managerFaqForbiddenPositioning =
  /\b(AI[- ]?powered|AI impact analysis|LLM[- ]?powered|LLM analysis|machine learning impact analysis|artificial intelligence impact analysis|intelligent analysis|smart impact|automated release approval|operational assurance)\b/i;

export const managerFaqOverclaimPattern =
  /\b(impacted|safe|unsafe|approved|blocked|root cause|production proven|validated for release|approved for release|proven behavior|statically proven|deployment[- ]safe|confirmed[- ]safe)\b/i;

export const managerFaqForbiddenProofClaimPattern =
  /\b(?:TraceMap\s+)?(?:proves?|proven|proof of)\s+(?:runtime behavior|production traffic|production behavior|endpoint performance|outage cause|release safety|operational safety|release approval|complete product coverage)\b/gi;

const managerFaqPageArtifact = "manager-faq/index.html";
const sitemapArtifact = "sitemap.xml";
const routesIndexArtifact = "routes-index.json";

const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "What can TraceMap say from static evidence?",
  "What can it not prove by itself?",
  "Does TraceMap replace telemetry or tests?",
  "What do rule IDs mean for a manager?",
  "What are evidence tiers?",
  "What does partial or reduced coverage mean?",
  "How should managers use TraceMap in review?",
  "How should it support prioritization?",
  "How should it help incident follow-up?",
  "What should be escalated?",
  "Why no model-driven scanner claim?",
  "What is a proof path?"
];

const forbiddenText = [
  "/Users/",
  "C:\\",
  "file://",
  "localhost",
  "127.0.0.1",
  ".tracemap",
  "facts.ndjson",
  "index.sqlite",
  "logs/analyzer.log",
  "analyzer.log",
  "raw SQL",
  "raw source snippets",
  "raw remotes",
  "ConnectionString",
  "connection string",
  "Server=",
  "User Id=",
  "Password=",
  "secrets",
  "generated scan directories",
  "private sample names"
];

export async function validateManagerFaqDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeManagerFaqBaseUrl(baseUrl, localErrors);
  const pagePath = resolve(dist, "manager-faq", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence("Manager FAQ page is missing required public route: /manager-faq/", managerFaqPageArtifact));
    errors.push(...localErrors);
    return;
  }

  if (cleanBaseUrl) {
    await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  }
  await validateRoutesIndex({ dist, errors: localErrors });
  await validateManagerFaqPage({ pagePath, errors: localErrors });

  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${managerFaqRoute}`)) {
    errors.push(withEvidence(`Manager FAQ sitemap is missing required route: ${baseUrl}${managerFaqRoute}`, sitemapArtifact));
  }
}

function normalizeManagerFaqBaseUrl(value, errors) {
  try {
    return normalizeBaseUrl(new URL(value).origin);
  } catch {
    errors.push(withEvidence(`Manager FAQ baseUrl must be a valid absolute URL: ${String(value)}`, "baseUrl input"));
    return null;
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
    errors.push(withEvidence(`Manager FAQ could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return;
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Manager FAQ routes-index.json is invalid: expected entries array", routesIndexArtifact));
    return;
  }

  const routeEntry = parsed.entries.find((entry) => entry?.path === managerFaqRoute);
  if (!routeEntry) {
    errors.push(withEvidence(`Manager FAQ routes-index.json is missing required route: ${managerFaqRoute}`, routesIndexArtifact));
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
      errors.push(withEvidence(`Manager FAQ routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
    }
  }
}

async function validateManagerFaqPage({ pagePath, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const pageText = normalizeRenderedText(html);
  const positioningText = `${html} ${decodedHtml} ${pageText}`;
  const wordCount = countWords(pageText);

  for (const phrase of requiredText) {
    if (!pageText.includes(phrase)) {
      errors.push(withEvidence(`Manager FAQ page is missing required text: ${phrase}`, managerFaqPageArtifact));
    }
  }

  for (const link of managerFaqRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Manager FAQ page is missing required link: ${link}`, managerFaqPageArtifact));
    }
  }

  if (!hasOgTypeArticle(html)) {
    errors.push(withEvidence('Manager FAQ page must include <meta property="og:type" content="article">.', managerFaqPageArtifact));
  }

  if (wordCount < 500 || wordCount > 1500) {
    errors.push(withEvidence(`Manager FAQ page word count must be between 500 and 1500 words, got ${wordCount}`, managerFaqPageArtifact));
  }

  if (managerFaqForbiddenPositioning.test(positioningText)) {
    errors.push(withEvidence("Manager FAQ page contains forbidden AI/LLM positioning.", managerFaqPageArtifact));
  }

  const sanctionedBoundaryText = extractSanctionedBoundaryText(html);
  const overclaimReviewText = normalizeOverclaimText(normalizeRenderedText(html.replace(sanctionedBoundaryText, " ")));
  if (managerFaqOverclaimPattern.test(overclaimReviewText)) {
    errors.push(withEvidence("Manager FAQ page contains runtime, production, or release overclaim wording outside sanctioned boundary copy.", managerFaqPageArtifact));
  }
  if (hasUnsanctionedProofClaim(overclaimReviewText)) {
    errors.push(withEvidence("Manager FAQ page contains affirmative runtime, production, or release proof wording outside sanctioned boundary copy.", managerFaqPageArtifact));
  }

  for (const text of forbiddenText) {
    if (containsForbiddenText(text, html, decodedHtml, pageText)) {
      errors.push(withEvidence(`Manager FAQ page contains forbidden public text: ${text}`, managerFaqPageArtifact));
    }
  }
}

function extractSanctionedBoundaryText(html) {
  const matches = html.match(/<section\b[^>]*class=["'][^"']*\bboundary-section\b[^"']*["'][^>]*>[\s\S]*?<\/section>/gi);
  return matches?.join(" ") ?? "";
}

function normalizeOverclaimText(value) {
  return value.replace(/\bpublic-safe\b/gi, "public evidence");
}

function hasUnsanctionedProofClaim(value) {
  managerFaqForbiddenProofClaimPattern.lastIndex = 0;
  for (const match of value.matchAll(managerFaqForbiddenProofClaimPattern)) {
    const prefix = value.slice(Math.max(0, match.index - 32), match.index).toLowerCase();
    if (!/(?:cannot|can't|does not|do not|not|without)\s+$/.test(prefix)) {
      return true;
    }
  }
  return false;
}

function withEvidence(message, artifact) {
  return `${message} Evidence: ${artifact}.`;
}

function containsForbiddenText(text, ...values) {
  const normalizedText = text.toLowerCase();
  return values.some((value) => value.toLowerCase().includes(normalizedText));
}

function hasHref(html, href) {
  const escaped = escapeRegExp(href);
  return new RegExp(`<a\\b[^>]*\\bhref\\s*=\\s*["']${escaped}["']`, "i").test(html);
}

function hasOgTypeArticle(html) {
  return /<meta\b(?=[^>]*\bproperty\s*=\s*["']og:type["'])(?=[^>]*\bcontent\s*=\s*["']article["'])[^>]*>/i.test(html);
}

function countWords(value) {
  return (value.match(/\b[\w'-]+\b/g) ?? []).length;
}
