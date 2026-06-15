import { readdir, readFile, stat } from "node:fs/promises";
import { dirname, extname, relative, resolve, sep } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

import { buildSite } from "./build.mjs";

const defaultRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const defaultBaseUrl = "https://tracemap.tools";

export async function validateSite(options = {}) {
  const { log = console.log, root = defaultRoot } = options;

  await buildSite({ log, root });
  const result = await validateDist({ root });

  log(
    `Validated ${result.htmlFileCount} HTML files, ${result.internalReferenceCount} internal references, and ${result.sitemapUrlCount} sitemap URLs.`
  );

  return result;
}

export async function validateDist({ baseUrl = defaultBaseUrl, root = defaultRoot } = {}) {
  const dist = resolve(root, "dist");
  const errors = [];
  const files = await collectFiles(dist);
  const htmlFiles = files.filter((file) => extname(file) === ".html");
  const sitemapPath = resolve(dist, "sitemap.xml");
  const robotsPath = resolve(dist, "robots.txt");

  await validateRequiredFile(sitemapPath, "sitemap.xml", errors);
  await validateRequiredFile(robotsPath, "robots.txt", errors);

  const sitemapUrls = await readSitemapUrls(sitemapPath, errors);
  await validateSitemapUrls({ baseUrl, dist, errors, sitemapUrls });

  const internalReferenceCount = await validateHtmlReferences({
    baseUrl,
    dist,
    errors,
    htmlFiles
  });

  await validateRobotsSitemap({ baseUrl, errors, robotsPath });

  if (errors.length > 0) {
    throw new Error(`Site validation failed:\n- ${errors.join("\n- ")}`);
  }

  return {
    htmlFileCount: htmlFiles.length,
    internalReferenceCount,
    sitemapUrlCount: sitemapUrls.length
  };
}

async function collectFiles(directory) {
  const files = [];

  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const path = resolve(directory, entry.name);

    if (entry.isDirectory()) {
      files.push(...(await collectFiles(path)));
      continue;
    }

    if (entry.isFile()) {
      files.push(path);
    }
  }

  return files;
}

async function validateRequiredFile(path, label, errors) {
  if (!(await fileExists(path))) {
    errors.push(`Missing required generated file: ${label}`);
  }
}

async function readSitemapUrls(sitemapPath, errors) {
  if (!(await fileExists(sitemapPath))) {
    return [];
  }

  const sitemap = await readFile(sitemapPath, "utf8");
  const urls = [...sitemap.matchAll(/<loc>([^<]+)<\/loc>/g)].map((match) => decodeXml(match[1]));

  if (urls.length === 0) {
    errors.push("sitemap.xml does not contain any <loc> entries.");
  }

  return urls;
}

async function validateSitemapUrls({ baseUrl, dist, errors, sitemapUrls }) {
  const seen = new Set();

  for (const url of sitemapUrls) {
    if (seen.has(url)) {
      errors.push(`Duplicate sitemap URL: ${url}`);
      continue;
    }

    seen.add(url);

    const path = publicPathFromUrl(url, { baseUrl, source: "sitemap.xml" });
    if (!path) {
      errors.push(`Sitemap URL is not on ${baseUrl}: ${url}`);
      continue;
    }

    if (!(await publicPathExists(dist, path))) {
      errors.push(`Sitemap URL has no generated file: ${url}`);
    }
  }
}

async function validateHtmlReferences({ baseUrl, dist, errors, htmlFiles }) {
  let internalReferenceCount = 0;

  for (const file of htmlFiles) {
    const html = await readFile(file, "utf8");

    for (const reference of extractHtmlReferences(html)) {
      const path = resolveReference(reference, { baseUrl, file, dist });

      if (!path) {
        continue;
      }

      internalReferenceCount += 1;

      if (!(await publicPathExists(dist, path))) {
        errors.push(`${formatDistPath(dist, file)} references missing path: ${reference}`);
      }
    }
  }

  return internalReferenceCount;
}

async function validateRobotsSitemap({ baseUrl, errors, robotsPath }) {
  if (!(await fileExists(robotsPath))) {
    return;
  }

  const robots = await readFile(robotsPath, "utf8");
  const expected = `Sitemap: ${baseUrl}/sitemap.xml`;

  if (!robots.split(/\r?\n/).some((line) => line.trim() === expected)) {
    errors.push(`robots.txt must include "${expected}".`);
  }
}

function extractHtmlReferences(html) {
  return [...html.matchAll(/\b(?:href|src)=["']([^"']+)["']/g)].map((match) => match[1]);
}

function resolveReference(reference, { baseUrl, dist, file }) {
  if (
    reference === "" ||
    reference.startsWith("#") ||
    reference.startsWith("data:") ||
    reference.startsWith("mailto:") ||
    reference.startsWith("tel:") ||
    reference.startsWith("javascript:")
  ) {
    return null;
  }

  if (/^https?:\/\//.test(reference)) {
    return publicPathFromUrl(reference, { baseUrl, source: formatDistPath(dist, file) });
  }

  if (reference.startsWith("//")) {
    return null;
  }

  if (reference.startsWith("/")) {
    return stripQueryAndHash(reference);
  }

  const fileRoute = `/${relative(dist, file).split(sep).join("/")}`;
  const url = new URL(reference, `${defaultBaseUrl}${fileRoute}`);
  return stripQueryAndHash(url.pathname);
}

function publicPathFromUrl(value, { baseUrl }) {
  let url;

  try {
    url = new URL(value);
  } catch {
    return null;
  }

  if (url.origin !== baseUrl) {
    return null;
  }

  return stripQueryAndHash(`${url.pathname}${url.search}${url.hash}`);
}

async function publicPathExists(dist, pathname) {
  const publicPath = stripQueryAndHash(pathname);
  const resolved = resolvePublicPath(dist, publicPath);

  return resolved ? fileExists(resolved) : false;
}

function resolvePublicPath(dist, pathname) {
  let decoded;

  try {
    decoded = decodeURIComponent(pathname);
  } catch {
    return null;
  }

  if (!decoded.startsWith("/")) {
    return null;
  }

  const filePath = decoded.endsWith("/")
    ? resolve(dist, `.${decoded}`, "index.html")
    : resolve(dist, `.${decoded}`);
  const safeRoot = dist.endsWith(sep) ? dist : dist + sep;

  if (filePath !== dist && !filePath.startsWith(safeRoot)) {
    return null;
  }

  return filePath;
}

async function fileExists(path) {
  try {
    const info = await stat(path);
    return info.isFile();
  } catch {
    return false;
  }
}

function stripQueryAndHash(value) {
  return value.split("#", 1)[0].split("?", 1)[0];
}

function decodeXml(value) {
  return value
    .replaceAll("&amp;", "&")
    .replaceAll("&lt;", "<")
    .replaceAll("&gt;", ">")
    .replaceAll("&quot;", '"')
    .replaceAll("&apos;", "'");
}

function formatDistPath(dist, file) {
  return relative(dist, file).split(sep).join("/");
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  await validateSite();
}
