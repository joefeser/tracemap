import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import { splitLlmsSections } from "./discovery.mjs";
import {
  decodeHtmlEntities,
  escapeRegExp,
  fileExists,
  normalizeBaseUrl,
  normalizeRenderedText,
  readSitemapLocSet
} from "./validate-utils.mjs";

export const adoptionPlaybookRoute = "/adoption/";
export const adoptionPlaybookRequiredLinks = [
  "/demo/",
  "/demo/result/",
  "/docs/",
  "/validation/",
  "/limitations/",
  "/proof-paths/",
  "/review-room/",
  "/static-triage/"
];

export const adoptionPartialAnalysisSentence =
  "Partial analysis is useful only when it is clearly labeled as partial.";

export const forbiddenAdoptionPlaybookPositioning =
  /\b(AI[- ]?powered|AI impact analysis|LLM[- ]?powered|LLM analysis|machine learning impact analysis|artificial intelligence impact analysis|intelligent analysis|smart impact)\b/i;

const adoptionPageArtifact = "adoption/index.html";
const sitemapArtifact = "sitemap.xml";
const routesIndexArtifact = "routes-index.json";
const llmsArtifact = "llms.txt";

const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "not a product promise or replacement for engineering judgment",
  "start with the public demo",
  "repository owners",
  "runtime owners",
  "test owners",
  "documentation owners",
  "future extractor work",
  adoptionPartialAnalysisSentence,
  "The playbook is not runtime proof or release approval"
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

export async function validateAdoptionPlaybookDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeAdoptionBaseUrl(baseUrl, localErrors);
  const pagePath = resolve(dist, "adoption", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence("Adoption playbook page is missing required public route: /adoption/", adoptionPageArtifact));
    errors.push(...localErrors);
    return;
  }

  if (cleanBaseUrl) {
    await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  }
  await validateRoutesIndex({ dist, errors: localErrors });
  await validateLlmsRouteSection({ dist, errors: localErrors });
  await validateAdoptionPlaybookPage({ pagePath, errors: localErrors });

  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${adoptionPlaybookRoute}`)) {
    errors.push(withEvidence(`Adoption playbook sitemap is missing required route: ${baseUrl}${adoptionPlaybookRoute}`, sitemapArtifact));
  }
}

function normalizeAdoptionBaseUrl(value, errors) {
  try {
    return normalizeBaseUrl(new URL(value).origin);
  } catch {
    errors.push(withEvidence(`Adoption playbook baseUrl must be a valid absolute URL: ${String(value)}`, "baseUrl input"));
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
    errors.push(withEvidence(`Adoption playbook could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return;
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Adoption playbook routes-index.json is invalid: expected entries array", routesIndexArtifact));
    return;
  }

  const routeEntry = parsed.entries.find((entry) => entry?.path === adoptionPlaybookRoute);
  if (!routeEntry) {
    errors.push(withEvidence(`Adoption playbook routes-index.json is missing required route: ${adoptionPlaybookRoute}`, routesIndexArtifact));
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
      errors.push(withEvidence(`Adoption playbook routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
    }
  }
}

async function validateLlmsRouteSection({ dist, errors }) {
  const llmsPath = resolve(dist, "llms.txt");
  if (!(await fileExists(llmsPath))) {
    return;
  }

  const llmsText = await readFile(llmsPath, "utf8");
  const limitationsSection = splitLlmsSections(llmsText).get("Limitations") ?? "";

  if (!limitationsSection.includes("[Adoption Playbook](https://tracemap.tools/adoption/)")) {
    errors.push(withEvidence("Adoption playbook is missing from the llms.txt Limitations route section.", llmsArtifact));
  }

  if (!limitationsSection.includes("Public claim level: concept")) {
    errors.push(withEvidence("Adoption playbook llms.txt route section must preserve concept claim level.", llmsArtifact));
  }
}

async function validateAdoptionPlaybookPage({ pagePath, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const pageText = normalizeRenderedText(html);
  const positioningText = `${html} ${decodedHtml} ${pageText}`;
  const wordCount = countWords(pageText);

  for (const phrase of requiredText) {
    if (!pageText.includes(phrase)) {
      errors.push(withEvidence(`Adoption playbook page is missing required text: ${phrase}`, adoptionPageArtifact));
    }
  }

  for (const link of adoptionPlaybookRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Adoption playbook page is missing required link: ${link}`, adoptionPageArtifact));
    }
  }

  if (!hasOgTypeArticle(html)) {
    errors.push(withEvidence('Adoption playbook page must include <meta property="og:type" content="article">.', adoptionPageArtifact));
  }

  if (wordCount < 500 || wordCount > 1500) {
    errors.push(withEvidence(`Adoption playbook page word count must be between 500 and 1500 words, got ${wordCount}`, adoptionPageArtifact));
  }

  if (forbiddenAdoptionPlaybookPositioning.test(positioningText)) {
    errors.push(withEvidence("Adoption playbook page contains forbidden AI/LLM positioning.", adoptionPageArtifact));
  }

  for (const text of forbiddenText) {
    if (containsForbiddenText(text, html, decodedHtml, pageText)) {
      errors.push(withEvidence(`Adoption playbook page contains forbidden public text: ${text}`, adoptionPageArtifact));
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

function hasOgTypeArticle(html) {
  return /<meta\b(?=[^>]*\bproperty\s*=\s*["']og:type["'])(?=[^>]*\bcontent\s*=\s*["']article["'])[^>]*>/i.test(html);
}

function countWords(value) {
  return (value.match(/\b[\w'-]+\b/g) ?? []).length;
}
