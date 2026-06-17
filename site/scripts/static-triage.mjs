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

export const staticTriageRoute = "/static-triage/";
export const staticTriageRequiredLinks = [
  "/proof-paths/",
  "/validation/",
  "/docs/",
  "/limitations/",
  "/demo/result/",
  "/incident-call/"
];

export const forbiddenStaticTriagePositioning =
  /\b(AI[- ]?powered|AI impact analysis|LLM[- ]?powered|LLM analysis|machine learning impact analysis|artificial intelligence impact analysis|intelligent analysis|smart impact)\b/i;

const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "static evidence checklist",
  "evidence tier",
  "handoff questions",
  "Partial static evidence is useful when labeled as partial",
  "Static triage is the engineer checklist and handoff page, distinct from the incident-call orientation page",
  "The checklist is not telemetry, diagnosis, or approval"
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
  "ConnectionString",
  "connection string",
  "Server=",
  "User Id=",
  "Password="
];

export async function validateStaticTriageDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl, localErrors);
  const pagePath = resolve(dist, "static-triage", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push("Static triage page is missing required public route: /static-triage/");
    errors.push(...localErrors);
    return;
  }

  if (cleanBaseUrl) {
    await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  }
  await validateRoutesIndex({ dist, errors: localErrors });
  await validateStaticTriagePage({ pagePath, errors: localErrors });

  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${staticTriageRoute}`)) {
    errors.push(`Static triage sitemap is missing required route: ${baseUrl}${staticTriageRoute}`);
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
    errors.push(`Static triage could not parse routes-index.json: ${error.message}`);
    return;
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push("Static triage routes-index.json is invalid: expected entries array");
    return;
  }

  const routeEntry = parsed.entries.find((entry) => entry?.path === staticTriageRoute);
  if (!routeEntry) {
    errors.push(`Static triage routes-index.json is missing required route: ${staticTriageRoute}`);
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
      errors.push(`Static triage routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`);
    }
  }
}

async function validateStaticTriagePage({ pagePath, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const pageText = normalizeRenderedText(html);
  const positioningText = `${decodedHtml} ${pageText}`;
  const wordCount = countWords(pageText);

  for (const phrase of requiredText) {
    if (!pageText.includes(phrase)) {
      errors.push(`Static triage page is missing required text: ${phrase}`);
    }
  }

  for (const link of staticTriageRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(`Static triage page is missing required link: ${link}`);
    }
  }

  if (wordCount < 400 || wordCount > 1500) {
    errors.push(`Static triage page word count must be between 400 and 1500 words, got ${wordCount}`);
  }

  if (forbiddenStaticTriagePositioning.test(positioningText)) {
    errors.push("Static triage page contains forbidden AI/LLM positioning.");
  }

  for (const text of forbiddenText) {
    if (containsForbiddenText(text, html, decodedHtml, pageText)) {
      errors.push(`Static triage page contains forbidden public text: ${text}`);
    }
  }
}

function containsForbiddenText(text, ...values) {
  const normalizedText = text.toLowerCase();
  return values.some((value) => value.toLowerCase().includes(normalizedText));
}

function hasHref(html, href) {
  const escaped = escapeRegExp(href);
  return new RegExp(`<a\\b[^>]*\\bhref\\s*=\\s*["']${escaped}["']`, "i").test(html);
}

function countWords(value) {
  return (value.match(/\b[\w'-]+\b/g) ?? []).length;
}
