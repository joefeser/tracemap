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

export const demoEvidenceTrailRoute = "/demo/evidence-trail/";
export const demoEvidenceTrailRequiredLinks = [
  "/demo/result/",
  "/demo/proof-upgrades/",
  "/demo/proof-assets/",
  "/proof-paths/",
  "/evidence/",
  "/validation/",
  "/limitations/",
  "/packets/"
];

export const demoEvidenceTrailForbiddenPositioning =
  /\b(AI[- ]?powered|AI impact analysis|LLM[- ]?powered|LLM analysis|machine learning impact analysis|artificial intelligence impact analysis|intelligent analysis|smart impact)\b/i;

export const demoEvidenceTrailImpactedPattern = /\bimpacted\b/i;

const pageArtifact = "demo/evidence-trail/index.html";
const sitemapArtifact = "sitemap.xml";
const routesIndexArtifact = "routes-index.json";

const requiredText = [
  "Public claim level: demo",
  "No public conclusion without evidence",
  "What static evidence connects a changed demo surface to a route and downstream surfaces?",
  "This is the same evidence packet made easier to follow, not stronger.",
  "site/src/_data/demo-public-summary.json",
  "public.demo.summary.v1",
  "Tier2Structural",
  "Tier4Unknown",
  "PartialAnalysis",
  "12 changed demo surfaces",
  "14 endpoint findings",
  "12 paths",
  "25 reverse paths",
  "37 path gaps",
  "Package evidence",
  "Configuration evidence",
  "SQL-facing evidence",
  "missing-public-item",
  "runtime proof",
  "production proof",
  "release approval",
  "complete product coverage"
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
  "private sample names",
  "config values"
];

const requiredSurfaceMarkers = [
  { type: "package", gap: "package" },
  { type: "config", gap: "config" },
  { type: "sql-facing", gap: "sql-facing" }
];

export async function validateDemoEvidenceTrailDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeDemoEvidenceTrailBaseUrl(baseUrl, localErrors);
  const pagePath = resolve(dist, "demo", "evidence-trail", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence("Demo evidence trail page is missing required public route: /demo/evidence-trail/", pageArtifact));
    errors.push(...localErrors);
    return;
  }

  if (cleanBaseUrl) {
    await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  }
  await validateRoutesIndex({ dist, errors: localErrors });

  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const pageText = normalizeRenderedText(html);
  const fullText = `${html} ${decodedHtml} ${pageText}`;
  const wordCount = countWords(pageText);

  for (const phrase of requiredText) {
    if (!pageText.includes(phrase)) {
      localErrors.push(withEvidence(`Demo evidence trail page is missing required text: ${phrase}`, pageArtifact));
    }
  }

  for (const link of demoEvidenceTrailRequiredLinks) {
    if (!hasHref(html, link)) {
      localErrors.push(withEvidence(`Demo evidence trail page is missing required link: ${link}`, pageArtifact));
    }
  }

  if (!hasOgTypeArticle(html)) {
    localErrors.push(withEvidence('Demo evidence trail page must include <meta property="og:type" content="article">.', pageArtifact));
  }

  if (wordCount < 450 || wordCount > 1500) {
    localErrors.push(withEvidence(`Demo evidence trail page word count must be between 450 and 1500 words, got ${wordCount}`, pageArtifact));
  }

  if (demoEvidenceTrailForbiddenPositioning.test(fullText)) {
    localErrors.push(withEvidence("Demo evidence trail page contains forbidden AI/LLM positioning.", pageArtifact));
  }

  if (demoEvidenceTrailImpactedPattern.test(pageText)) {
    localErrors.push(withEvidence("Demo evidence trail page contains banned word: impacted.", pageArtifact));
  }

  for (const { type, gap } of requiredSurfaceMarkers) {
    if (!hasDataAttribute(html, "data-trail-surface-type", type)) {
      localErrors.push(withEvidence(`Demo evidence trail page is missing surface marker: ${type}`, pageArtifact));
    }
    if (!hasDataAttribute(html, "data-trail-gap", gap)) {
      localErrors.push(withEvidence(`Demo evidence trail page is missing coverage-gap marker: ${gap}`, pageArtifact));
    }
  }

  for (const text of forbiddenText) {
    if (containsForbiddenText(text, html, decodedHtml, pageText)) {
      localErrors.push(withEvidence(`Demo evidence trail page contains forbidden public text: ${text}`, pageArtifact));
    }
  }

  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${demoEvidenceTrailRoute}`)) {
    errors.push(withEvidence(`Demo evidence trail sitemap is missing required route: ${baseUrl}${demoEvidenceTrailRoute}`, sitemapArtifact));
  }
}

function normalizeDemoEvidenceTrailBaseUrl(value, errors) {
  try {
    return normalizeBaseUrl(new URL(value).origin);
  } catch {
    errors.push(withEvidence(`Demo evidence trail baseUrl must be a valid absolute URL: ${String(value)}`, "baseUrl input"));
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
    errors.push(withEvidence(`Demo evidence trail could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return;
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Demo evidence trail routes-index.json is invalid: expected entries array", routesIndexArtifact));
    return;
  }

  const routeEntry = parsed.entries.find((entry) => entry?.path === demoEvidenceTrailRoute);
  if (!routeEntry) {
    errors.push(withEvidence(`Demo evidence trail routes-index.json is missing required route: ${demoEvidenceTrailRoute}`, routesIndexArtifact));
    return;
  }

  const expectedFields = {
    publicClaimLevel: "demo",
    hintCategory: "demo",
    sourceType: "site-page",
    preferredProofPath: "/demo/proof-upgrades/"
  };

  for (const [field, expected] of Object.entries(expectedFields)) {
    if (routeEntry[field] !== expected) {
      errors.push(withEvidence(`Demo evidence trail routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
    }
  }
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

function hasDataAttribute(html, attribute, value) {
  const escapedAttribute = escapeRegExp(attribute);
  const escapedValue = escapeRegExp(value);
  return new RegExp(`\\b${escapedAttribute}\\s*=\\s*["']${escapedValue}["']`, "i").test(html);
}

function hasOgTypeArticle(html) {
  return /<meta\b(?=[^>]*\bproperty\s*=\s*["']og:type["'])(?=[^>]*\bcontent\s*=\s*["']article["'])[^>]*>/i.test(html);
}

function countWords(value) {
  return (value.match(/\b[\w'-]+\b/g) ?? []).length;
}
