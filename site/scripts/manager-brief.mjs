import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import {
  decodeHtmlEntities,
  escapeRegExp,
  fileExists,
  normalizeRenderedText,
  readSitemapLocSet
} from "./validate-utils.mjs";

export const managerBriefRoute = "/manager-brief/";
export const managerBriefRequiredLinks = ["/proof-paths/", "/validation/", "/limitations/", "/demo/", "/docs/"];

export const forbiddenManagerBriefPositioning =
  /\b(AI[- ]?powered|AI impact analysis|LLM[- ]?powered|LLM analysis|machine learning impact analysis|artificial intelligence impact analysis|intelligent analysis|smart impact)\b/i;

const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "Manual dependency indexing is expensive",
  "deterministic artifacts",
  "Static evidence is useful because its limits stay visible"
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

export async function validateManagerBriefDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeManagerBriefBaseUrl(baseUrl, localErrors);
  const pagePath = resolve(dist, "manager-brief", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push("Manager brief page is missing required public route: /manager-brief/");
    errors.push(...localErrors);
    return;
  }

  if (cleanBaseUrl) {
    await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  }
  await validateRoutesIndex({ dist, errors: localErrors });
  await validateManagerBriefPage({ pagePath, errors: localErrors });

  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${managerBriefRoute}`)) {
    errors.push(`Manager brief sitemap is missing required route: ${baseUrl}${managerBriefRoute}`);
  }
}

function normalizeManagerBriefBaseUrl(value, errors) {
  let url;

  try {
    url = new URL(value);
  } catch {
    errors.push(`Manager brief baseUrl must be a valid absolute URL: ${String(value)}`);
    return null;
  }

  if (url.protocol !== "https:" && url.protocol !== "http:") {
    errors.push(`Manager brief baseUrl must use http or https: ${String(value)}`);
    return null;
  }

  return url.origin;
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
    errors.push(`Manager brief could not parse routes-index.json: ${error.message}`);
    return;
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push("Manager brief routes-index.json is invalid: expected entries array");
    return;
  }

  const routeEntry = parsed.entries.find((entry) => entry?.path === managerBriefRoute);
  if (!routeEntry) {
    errors.push(`Manager brief routes-index.json is missing required route: ${managerBriefRoute}`);
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
      errors.push(`Manager brief routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`);
    }
  }
}

async function validateManagerBriefPage({ pagePath, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const pageText = normalizeRenderedText(html);
  const positioningText = `${decodedHtml} ${pageText}`;
  const wordCount = countWords(pageText);

  for (const phrase of requiredText) {
    if (!pageText.includes(phrase)) {
      errors.push(`Manager brief page is missing required text: ${phrase}`);
    }
  }

  for (const link of managerBriefRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(`Manager brief page is missing required link: ${link}`);
    }
  }

  if (wordCount < 400 || wordCount > 1500) {
    errors.push(`Manager brief page word count must be between 400 and 1500 words, got ${wordCount}`);
  }

  if (forbiddenManagerBriefPositioning.test(positioningText)) {
    errors.push("Manager brief page contains forbidden AI/LLM positioning.");
  }

  for (const text of forbiddenText) {
    if (html.includes(text) || decodedHtml.includes(text) || pageText.includes(text)) {
      errors.push(`Manager brief page contains forbidden public text: ${text}`);
    }
  }
}

function hasHref(html, href) {
  const escaped = escapeRegExp(href);
  return new RegExp(`<a\\b[^>]*\\bhref\\s*=\\s*["']${escaped}["']`, "i").test(html);
}

function countWords(value) {
  return (value.match(/\b[\w'-]+\b/g) ?? []).length;
}
