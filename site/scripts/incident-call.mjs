import { readFile, stat } from "node:fs/promises";
import { resolve } from "node:path";

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

  const sitemap = await readFile(sitemapPath, "utf8");
  const sitemapUrls = new Set(
    [...sitemap.matchAll(/<loc>\s*([^<]+?)\s*<\/loc>/g)].map((match) => decodeHtmlEntities(match[1].trim()))
  );

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

  const paths = new Set(parsed.entries.map((entry) => entry?.path).filter(Boolean));
  if (!paths.has(incidentCallRoute)) {
    errors.push(`Incident call routes-index.json is missing required route: ${incidentCallRoute}`);
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
  return new RegExp(`<a\\b[^>]*\\bhref=["']${escaped}["']`, "i").test(html);
}

function normalizeBaseUrl(value) {
  return String(value).replace(/\/+$/, "");
}

function normalizeRenderedText(html) {
  return decodeHtmlEntities(html)
    .replace(/<[^>]+>/g, " ")
    .replace(/\s+/g, " ")
    .trim();
}

function decodeHtmlEntities(value) {
  return String(value).replace(/&(#x[0-9a-f]+|#[0-9]+|amp|apos|gt|lt|quot);/gi, (entity, token) => {
    const normalized = token.toLowerCase();
    if (normalized.startsWith("#x")) {
      return decodeCodePoint(Number.parseInt(normalized.slice(2), 16), entity);
    }

    if (normalized.startsWith("#")) {
      return decodeCodePoint(Number.parseInt(normalized.slice(1), 10), entity);
    }

    return (
      {
        amp: "&",
        apos: "'",
        gt: ">",
        lt: "<",
        quot: "\""
      }[normalized] ?? entity
    );
  });
}

function decodeCodePoint(codePoint, fallback) {
  if (!Number.isFinite(codePoint)) {
    return fallback;
  }

  try {
    return String.fromCodePoint(codePoint);
  } catch {
    return fallback;
  }
}

function escapeRegExp(value) {
  return String(value).replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

async function fileExists(path) {
  try {
    return (await stat(path)).isFile();
  } catch {
    return false;
  }
}
