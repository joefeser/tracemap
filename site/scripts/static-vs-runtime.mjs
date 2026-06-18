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

export const staticVsRuntimeRoute = "/static-vs-runtime/";
export const staticVsRuntimeRequiredLinks = [
  "/docs/",
  "/validation/",
  "/limitations/",
  "/outputs/",
  "/proof-paths/",
  "/capabilities/",
  "/demo/",
  "/demo/result/",
  "/static-triage/",
  "/incident-call/",
  "/use-cases/incident-review/"
];

export const forbiddenStaticVsRuntimePositioning =
  /\b(AI[- ]?powered|LLM[- ]?powered|machine learning impact analysis|artificial intelligence impact analysis|intelligent analysis|smart impact)\b/i;

const forbiddenOperationalPositioning =
  /\bTraceMap\b[^.]{0,80}\b(?:ships|collects|ingests|provides|runs|includes|offers|has)\b[^.]{0,80}\b(?:runtime agent|telemetry ingestion|live dashboard|incident automation)\b/i;

const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "deterministic static repository evidence",
  "runtime observability remains the source",
  "Static evidence question",
  "TraceMap evidence shape",
  "Runtime question",
  "Runtime system owner",
  "Before runtime review",
  "During handoff",
  "After runtime review",
  "TraceMap does not prove runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, incident root cause, service ownership, production dependency understanding, test sufficiency, or complete product coverage",
  "TraceMap does not replace logs, traces, APM, telemetry, incident dashboards, production metrics, tests, service-owner review, incident response, release approval, governance, or human judgment",
  "TraceMap does not perform AI impact analysis, LLM analysis, prompt-based classification, embedding search, or vector database analysis",
  "TraceMap should not use impact wording for a surface unless reducer-backed public-safe evidence supports that wording"
];

const requiredAnchors = [
  "static-questions",
  "runtime-questions",
  "handoff-workflow",
  "proof-paths",
  "limitations",
  "non-claims"
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
  "ConnectionString",
  "connection string",
  "Server=",
  "User Id=",
  "Password="
];

export async function validateStaticVsRuntimeDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl, localErrors);
  const pagePath = resolve(dist, "static-vs-runtime", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push("Static vs runtime page is missing required public route: /static-vs-runtime/");
    errors.push(...localErrors);
    return;
  }

  if (cleanBaseUrl) {
    await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  }
  await validateRoutesIndex({ dist, errors: localErrors });
  await validateStaticVsRuntimePage({ pagePath, errors: localErrors });

  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${staticVsRuntimeRoute}`)) {
    errors.push(`Static vs runtime sitemap is missing required route: ${baseUrl}${staticVsRuntimeRoute}`);
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
    errors.push(`Static vs runtime could not parse routes-index.json: ${error.message}`);
    return;
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push("Static vs runtime routes-index.json is invalid: expected entries array");
    return;
  }

  const routeEntry = parsed.entries.find((entry) => entry?.path === staticVsRuntimeRoute);
  if (!routeEntry) {
    errors.push(`Static vs runtime routes-index.json is missing required route: ${staticVsRuntimeRoute}`);
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
      errors.push(`Static vs runtime routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`);
    }
  }

  const routeText = routeTextFields(routeEntry).join(" ");
  if (/runtime proof|production traffic proof|endpoint performance proof|release safety proof|operational safety proof/i.test(routeText)) {
    errors.push("Static vs runtime routes-index.json inflates concept metadata into runtime or operational proof.");
  }

  const nonClaimsText = Array.isArray(routeEntry.nonClaims) ? routeEntry.nonClaims.join(" ") : "";
  for (const phrase of [
    "No runtime behavior",
    "production traffic",
    "endpoint performance",
    "outage cause",
    "release safety",
    "operational safety",
    "AI impact analysis",
    "LLM analysis",
    "complete product coverage",
    "incident root cause",
    "service ownership",
    "test sufficiency"
  ]) {
    if (!nonClaimsText.includes(phrase)) {
      errors.push(`Static vs runtime routes-index.json nonClaims are missing boundary phrase: ${phrase}`);
    }
  }
}

async function validateStaticVsRuntimePage({ pagePath, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const pageText = normalizeRenderedText(html);
  const positioningText = `${decodedHtml} ${pageText}`;
  const wordCount = countWords(pageText);

  for (const phrase of requiredText) {
    if (!pageText.toLowerCase().includes(phrase.toLowerCase())) {
      errors.push(`Static vs runtime page is missing required text: ${phrase}`);
    }
  }

  for (const anchor of requiredAnchors) {
    if (!hasId(html, anchor)) {
      errors.push(`Static vs runtime page is missing required anchor: ${anchor}`);
    }
  }

  for (const link of staticVsRuntimeRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(`Static vs runtime page is missing required link: ${link}`);
    }
  }

  if (!/<table\b[\s\S]*?<th\b[^>]*scope=["']col["'][\s\S]*?Static evidence question[\s\S]*?<\/table>/i.test(html)) {
    errors.push("Static vs runtime page is missing an accessible comparison table with column headers.");
  }

  if (wordCount < 650 || wordCount > 1900) {
    errors.push(`Static vs runtime page word count must be between 650 and 1900 words, got ${wordCount}`);
  }

  if (forbiddenStaticVsRuntimePositioning.test(positioningText)) {
    errors.push("Static vs runtime page contains forbidden runtime or AI/LLM positioning.");
  }

  if (forbiddenOperationalPositioning.test(positioningText)) {
    errors.push("Static vs runtime page contains forbidden runtime or AI/LLM positioning.");
  }

  if (/\b(?:TraceMap|static evidence)\b[^.]{0,80}\b(?:confirms|certifies|guarantees|proves|replaces)\b/i.test(pageText)) {
    errors.push("Static vs runtime page contains unsupported proof or replacement wording.");
  }

  if (/\b(?:surface|endpoint|route|contract|package|service)\b[^.]{0,80}\bimpacted\b/i.test(pageText)) {
    errors.push("Static vs runtime page contains unsupported impacted wording.");
  }

  for (const text of forbiddenText) {
    if (containsForbiddenText(text, html, decodedHtml, pageText)) {
      errors.push(`Static vs runtime page contains forbidden public text: ${text}`);
    }
  }
}

function routeTextFields(routeEntry) {
  return [
    routeEntry.title,
    routeEntry.summary,
    ...(Array.isArray(routeEntry.limitations) ? routeEntry.limitations : [])
  ].filter((value) => typeof value === "string");
}


function containsForbiddenText(text, ...values) {
  const normalizedText = text.toLowerCase();
  return values.some((value) => value.toLowerCase().includes(normalizedText));
}

function hasHref(html, href) {
  const escaped = escapeRegExp(href);
  return new RegExp(`<a\\b[^>]*\\bhref\\s*=\\s*["']${escaped}["']`, "i").test(html);
}

function hasId(html, id) {
  const escaped = escapeRegExp(id);
  return new RegExp(`\\bid\\s*=\\s*["']${escaped}["']`, "i").test(html);
}

function countWords(value) {
  return (value.match(/\b[\w'-]+\b/g) ?? []).length;
}
