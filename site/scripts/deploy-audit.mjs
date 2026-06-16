import { readFile, stat } from "node:fs/promises";
import { resolve } from "node:path";

export const deployAuditRequiredRoutes = [
  "/",
  "/docs/",
  "/validation/",
  "/limitations/",
  "/demo/",
  "/demo/result/",
  "/proof-paths/",
  "/legacy-evidence/",
  "/deploy-audit/"
];

export const deployAuditRequiredFiles = [
  "sitemap.xml",
  "robots.txt",
  "llms.txt",
  "docs-index.json",
  "routes-index.json"
];

const deployAuditDeniedText = [
  "/Users/",
  "C:\\",
  "file://",
  "localhost",
  "127.0.0.1",
  "ConnectionString",
  "connection string",
  "Server=",
  "User Id=",
  "Password=",
  "facts.ndjson",
  "index.sqlite",
  "logs/analyzer.log",
  "analyzer.log",
  "raw SQL",
  "raw source snippets",
  "local output roots"
];

export async function validateDeployAuditDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);

  for (const file of deployAuditRequiredFiles) {
    if (!(await fileExists(resolve(dist, file)))) {
      localErrors.push(`Deploy audit missing required generated file: ${file}`);
    }
  }

  for (const route of deployAuditRequiredRoutes) {
    if (!(await publicPathExists(dist, route))) {
      localErrors.push(`Deploy audit missing required public route: ${route}`);
    }
  }

  await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  await validateRoutesIndex({ dist, errors: localErrors });
  await validateDeployAuditPage({ dist, errors: localErrors });

  errors.push(...localErrors);
  return {
    requiredFileCount: deployAuditRequiredFiles.length,
    requiredRouteCount: deployAuditRequiredRoutes.length
  };
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
  for (const route of deployAuditRequiredRoutes) {
    if (!sitemapUrls.has(`${baseUrl}${route}`)) {
      errors.push(`Deploy audit sitemap is missing required route: ${baseUrl}${route}`);
    }
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
    errors.push(`Deploy audit could not parse routes-index.json: ${error.message}`);
    return;
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) {
    errors.push("Deploy audit routes-index.json is invalid: expected an object");
    return;
  }

  if (!Array.isArray(parsed.entries)) {
    errors.push("Deploy audit routes-index.json is invalid: expected entries array");
    return;
  }

  const paths = new Set(parsed.entries.map((entry) => entry?.path).filter(Boolean));
  for (const route of ["/docs/", "/validation/", "/proof-paths/", "/deploy-audit/"]) {
    if (!paths.has(route)) {
      errors.push(`Deploy audit routes-index.json is missing required route: ${route}`);
    }
  }
}

async function validateDeployAuditPage({ dist, errors }) {
  const pagePath = resolve(dist, "deploy-audit", "index.html");
  if (!(await fileExists(pagePath))) {
    return;
  }

  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const pageText = normalizeRenderedText(html);
  const requiredText = [
    "Public claim level: demo",
    "No public conclusion without evidence",
    "not live AWS state",
    "not runtime behavior proof",
    "not deployment success proof",
    "llms.txt",
    "docs-index.json",
    "routes-index.json",
    "sitemap.xml",
    "robots.txt"
  ];

  for (const phrase of requiredText) {
    if (!pageText.includes(phrase)) {
      errors.push(`Deploy audit page is missing required text: ${phrase}`);
    }
  }

  for (const text of deployAuditDeniedText) {
    if (html.includes(text) || decodedHtml.includes(text) || pageText.includes(text)) {
      errors.push(`Deploy audit page contains forbidden public text: ${text}`);
    }
  }
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

async function publicPathExists(dist, route) {
  const normalized = route.endsWith("/") ? route : `${route}/`;
  return fileExists(resolve(dist, `.${normalized}`, "index.html"));
}

async function fileExists(path) {
  try {
    return (await stat(path)).isFile();
  } catch {
    return false;
  }
}
