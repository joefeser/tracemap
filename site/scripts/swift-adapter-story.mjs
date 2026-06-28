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

export const swiftAdapterStoryRoute = "/swift/story/";
export const swiftAdapterStoryRequiredLinks = [
  "/swift/",
  "/capabilities/",
  "/validation/",
  "/limitations/",
  "/limitations/reduced-coverage/",
  "/static-vs-runtime/",
  "/site-claim-guardrails/",
  "/proof-source-catalog/"
];

const pageArtifact = "swift/story/index.html";
const routesIndexArtifact = "routes-index.json";
const sitemapArtifact = "sitemap.xml";

const requiredText = [
  "Public claim level: shipped",
  "PR #425",
  "e8813daaf763e277e7c5d88a2c0b2ad0b570f25a",
  "Swift repositories deserve the same evidence contract",
  "same cross-language evidence model",
  "static inventory",
  "symbols and calls",
  "surface discovery",
  "storage/data surfaces",
  "evidence safety",
  "rule IDs",
  "evidence tiers",
  "file spans",
  "commit SHA",
  "coverage labels",
  "reduced-coverage",
  "TraceMap core scanning and reduction do not use AI impact analysis, LLM analysis, prompt-based classification, embeddings, or vector database analysis",
  "TraceMap should not describe a Swift surface as affected by a change unless reducer-backed public-safe evidence supports that wording"
];

const expectedRows = ["adapter-landed", "review-use", "claim-safety"];

const hardPrivatePatterns = [
  /\/Users\//i,
  /\/private\//i,
  /\/home\//i,
  /~\//,
  /\bC:\\/i,
  /\bfile:\/\//i,
  /\blocalhost\b/i,
  /\b127\.0\.0\.1\b/i,
  /\bgit@/i,
  /\bConnectionString\b/i,
  /\bconnection string\b/i,
  /\bServer\s*=/i,
  /\bUser Id\s*=/i,
  /\bPassword\s*=/i,
  /\bapi[_-]?key\b/i,
  /\bsecret\s*=/i,
  /\bsk-[A-Za-z0-9_-]{12,}\b/i
];

const forbiddenPositiveClaims = [
  /\bTraceMap\b[^.]{0,120}\b(?:proves|guarantees|certifies|approves|validates)\b[^.]{0,120}\b(?:runtime|build|navigation|production|release|stored values?|query execution|live schema|complete Swift understanding)\b/i,
  /\bSwift v0\b[^.]{0,120}\b(?:proves|guarantees|certifies|approves|validates)\b[^.]{0,120}\b(?:runtime|build|navigation|production|release|stored values?|query execution|live schema|complete Swift understanding)\b/i,
  /\bTraceMap\b[^.]{0,120}\b(?:uses|performs|provides|runs|adds)\b[^.]{0,120}\b(?:AI impact analysis|LLM analysis|prompt-based classification|embedding search|vector database analysis)\b/i,
  /\b(?:safe to release|ready to release|approved to merge|complete Swift analysis)\b/i
];

export async function validateSwiftAdapterStoryDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl, localErrors);
  const pagePath = resolve(dist, "swift", "story", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence(`Swift adapter story is missing required public route: ${swiftAdapterStoryRoute}`, pageArtifact));
    errors.push(...localErrors);
    return;
  }

  if (cleanBaseUrl) {
    await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  }
  await validateRoutesIndex({ dist, errors: localErrors });
  await validatePage({ pagePath, errors: localErrors });

  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${swiftAdapterStoryRoute}`)) {
    errors.push(withEvidence(`Swift adapter story sitemap is missing required route: ${baseUrl}${swiftAdapterStoryRoute}`, sitemapArtifact));
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
    errors.push(withEvidence(`Swift adapter story could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return;
  }

  if (!Array.isArray(parsed?.entries)) {
    errors.push(withEvidence("Swift adapter story routes-index.json is invalid: expected entries array.", routesIndexArtifact));
    return;
  }

  const routeEntry = parsed.entries.find((entry) => entry?.path === swiftAdapterStoryRoute);
  if (!routeEntry) {
    errors.push(withEvidence(`Swift adapter story routes-index.json is missing required route: ${swiftAdapterStoryRoute}`, routesIndexArtifact));
    return;
  }

  const expectedFields = {
    publicClaimLevel: "shipped",
    hintCategory: "evidence",
    sourceType: "site-page",
    preferredProofPath: "/swift/"
  };

  for (const [field, expected] of Object.entries(expectedFields)) {
    if (routeEntry[field] !== expected) {
      errors.push(withEvidence(`Swift adapter story routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
    }
  }

  const metadataText = normalizeRenderedText(
    [
      routeEntry.title,
      routeEntry.summary,
      ...(Array.isArray(routeEntry.limitations) ? routeEntry.limitations : []),
      ...(Array.isArray(routeEntry.nonClaims) ? routeEntry.nonClaims : [])
    ].join(" ")
  );
  const routePolicyText = decodeHtmlEntities(metadataText);

  for (const phrase of ["runtime behavior", "build success", "release safety", "AI impact analysis", "raw source snippets"]) {
    if (!metadataText.toLowerCase().includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Swift adapter story route metadata is missing boundary phrase: ${phrase}`, routesIndexArtifact));
    }
  }

  scanPolicyText({ errors, label: "route metadata", text: routePolicyText, artifact: routesIndexArtifact });
}

async function validatePage({ pagePath, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const pageText = normalizeRenderedText(html);
  const policyText = `${decodedHtml} ${pageText}`;

  if (!/<title>Why Swift Evidence Matters \| TraceMap<\/title>/i.test(html)) {
    errors.push(withEvidence("Swift adapter story page is missing expected title.", pageArtifact));
  }

  if (!/\bog:type["']\s+content=["']article["']/i.test(html) && !/\bog:type["']\s+content\s*=\s*["']article["']/i.test(html)) {
    errors.push(withEvidence("Swift adapter story page must be article metadata.", pageArtifact));
  }

  for (const phrase of requiredText) {
    if (!pageText.toLowerCase().includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Swift adapter story page is missing required text: ${phrase}`, pageArtifact));
    }
  }

  for (const link of swiftAdapterStoryRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Swift adapter story page is missing required link: ${link}`, pageArtifact));
    }
  }

  if (!/\bdata-swift-adapter-story\b/i.test(html)) {
    errors.push(withEvidence("Swift adapter story page is missing the story table marker.", pageArtifact));
  }

  for (const row of expectedRows) {
    if (!new RegExp(`<tr\\b[^>]*data-story-row=["']${escapeRegExp(row)}["'][^>]*>`, "i").test(html)) {
      errors.push(withEvidence(`Swift adapter story page is missing story row: ${row}`, pageArtifact));
    }
  }

  scanPolicyText({ errors, label: "page", text: policyText, artifact: pageArtifact });
}

function scanPolicyText({ errors, label, text, artifact }) {
  for (const pattern of hardPrivatePatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Swift adapter story ${label} contains forbidden private material: ${redactPattern(pattern)}`, artifact));
    }
  }

  for (const pattern of forbiddenPositiveClaims) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Swift adapter story ${label} contains unsupported Swift claim wording: ${pattern}`, artifact));
    }
  }
}

function hasHref(html, href) {
  return new RegExp(`<a\\b(?=[^>]*\\bhref\\s*=\\s*["']${escapeRegExp(href)}["'])[^>]*>`, "i").test(html);
}

function redactPattern(pattern) {
  return `redacted ${pattern.source.slice(0, 24)}...`;
}

function withEvidence(message, artifact) {
  return `${message} Evidence: ${artifact}.`;
}
