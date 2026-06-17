import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import {
  decodeHtmlEntities,
  escapeRegExp,
  fileExists,
  normalizeRenderedText,
  readSitemapLocSet
} from "./validate-utils.mjs";

export const reviewRoomRoute = "/review-room/";
export const reviewRoomRequiredLinks = [
  "/proof-paths/",
  "/evidence/",
  "/validation/",
  "/limitations/",
  "/manager-brief/",
  "/manager-packet/",
  "/incident-call/",
  "/use-cases/incident-review/"
];

export const forbiddenReviewRoomPositioning =
  /\b(AI[- ]?powered|AI impact analysis|LLM[- ]?powered|LLM analysis|machine learning impact analysis|artificial intelligence impact analysis|intelligent analysis|smart impact)\b/i;

const reviewRoomPageArtifact = "review-room/index.html";
const sitemapArtifact = "sitemap.xml";
const routesIndexArtifact = "routes-index.json";

const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "claim",
  "proof path",
  "rule ID/evidence tier",
  "coverage label",
  "limitation",
  "owner decision gap",
  "Known evidence is reducer-backed and public-safe; partial evidence is reduced-coverage and labeled; missing evidence is an explicit gap for human review."
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
  "Password=",
  "generated scan directories",
  "private sample names"
];

export async function validateReviewRoomDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeReviewRoomBaseUrl(baseUrl, localErrors);
  const pagePath = resolve(dist, "review-room", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence("Review room page is missing required public route: /review-room/", reviewRoomPageArtifact));
    errors.push(...localErrors);
    return;
  }

  if (cleanBaseUrl) {
    await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  }
  await validateRoutesIndex({ dist, errors: localErrors });
  await validateReviewRoomPage({ pagePath, errors: localErrors });

  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${reviewRoomRoute}`)) {
    errors.push(withEvidence(`Review room sitemap is missing required route: ${baseUrl}${reviewRoomRoute}`, sitemapArtifact));
  }
}

function normalizeReviewRoomBaseUrl(value, errors) {
  let url;

  try {
    url = new URL(value);
  } catch {
    errors.push(withEvidence(`Review room baseUrl must be a valid absolute URL: ${String(value)}`, "baseUrl input"));
    return null;
  }

  if (url.protocol !== "https:" && url.protocol !== "http:") {
    errors.push(withEvidence(`Review room baseUrl must use http or https: ${String(value)}`, "baseUrl input"));
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
    errors.push(withEvidence(`Review room could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return;
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Review room routes-index.json is invalid: expected entries array", routesIndexArtifact));
    return;
  }

  const routeEntry = parsed.entries.find((entry) => entry?.path === reviewRoomRoute);
  if (!routeEntry) {
    errors.push(withEvidence(`Review room routes-index.json is missing required route: ${reviewRoomRoute}`, routesIndexArtifact));
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
      errors.push(withEvidence(`Review room routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
    }
  }
}

async function validateReviewRoomPage({ pagePath, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const pageText = normalizeRenderedText(html);
  const positioningText = `${html} ${decodedHtml} ${pageText}`;
  const wordCount = countWords(pageText);

  for (const phrase of requiredText) {
    if (!pageText.includes(phrase)) {
      errors.push(withEvidence(`Review room page is missing required text: ${phrase}`, reviewRoomPageArtifact));
    }
  }

  for (const link of reviewRoomRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Review room page is missing required link: ${link}`, reviewRoomPageArtifact));
    }
  }

  if (!hasOgTypeArticle(html)) {
    errors.push(withEvidence('Review room page must include <meta property="og:type" content="article">.', reviewRoomPageArtifact));
  }

  if (wordCount < 400 || wordCount > 1500) {
    errors.push(withEvidence(`Review room page word count must be between 400 and 1500 words, got ${wordCount}`, reviewRoomPageArtifact));
  }

  if (forbiddenReviewRoomPositioning.test(positioningText)) {
    errors.push(withEvidence("Review room page contains forbidden AI/LLM positioning.", reviewRoomPageArtifact));
  }

  for (const text of forbiddenText) {
    if (html.includes(text) || decodedHtml.includes(text) || pageText.includes(text)) {
      errors.push(withEvidence(`Review room page contains forbidden public text: ${text}`, reviewRoomPageArtifact));
    }
  }
}

function withEvidence(message, artifact) {
  return `${message} Evidence: ${artifact}.`;
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
