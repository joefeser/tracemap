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

export const incidentCallRoute = "/incident-call/";
export const incidentCallRequiredLinks = [
  "/proof-paths/",
  "/validation/",
  "/docs/",
  "/limitations/",
  "/demo/result/",
  "/use-cases/incident-review/"
];

const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "static dependency evidence",
  "not runtime observability",
  "not operational approval",
  "P1-call orientation and incident review are related, not identical"
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

export async function validateIncidentCallDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "incident-call", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push("Incident call page is missing required public route: /incident-call/");
    errors.push(...localErrors);
    return;
  }

  await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  await validateRoutesIndex({ dist, errors: localErrors });
  await validateIncidentCallPage({ pagePath, errors: localErrors });

  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);

  if (!sitemapUrls.has(`${baseUrl}${incidentCallRoute}`)) {
    errors.push(`Incident call sitemap is missing required route: ${baseUrl}${incidentCallRoute}`);
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
    errors.push(`Incident call could not parse routes-index.json: ${error.message}`);
    return;
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push("Incident call routes-index.json is invalid: expected entries array");
    return;
  }

  const incidentCallEntry = parsed.entries.find((entry) => entry?.path === incidentCallRoute);
  if (!incidentCallEntry) {
    errors.push(`Incident call routes-index.json is missing required route: ${incidentCallRoute}`);
    return;
  }

  const expectedFields = {
    publicClaimLevel: "concept",
    hintCategory: "use-case",
    sourceType: "site-page",
    preferredProofPath: "/proof-paths/"
  };

  for (const [field, expected] of Object.entries(expectedFields)) {
    if (incidentCallEntry[field] !== expected) {
      errors.push(
        `Incident call routes-index.json expected ${field} ${expected}, got ${String(incidentCallEntry[field])}`
      );
    }
  }
}

async function validateIncidentCallPage({ pagePath, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const pageText = normalizeRenderedText(html);

  for (const phrase of requiredText) {
    if (!pageText.includes(phrase)) {
      errors.push(`Incident call page is missing required text: ${phrase}`);
    }
  }

  for (const link of incidentCallRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(`Incident call page is missing required link: ${link}`);
    }
  }

  for (const text of forbiddenText) {
    if (html.includes(text) || decodedHtml.includes(text) || pageText.includes(text)) {
      errors.push(`Incident call page contains forbidden public text: ${text}`);
    }
  }
}

function hasHref(html, href) {
  const escaped = escapeRegExp(href);
  return new RegExp(`<a\\b[^>]*\\bhref\\s*=\\s*["']${escaped}["']`, "i").test(html);
}
