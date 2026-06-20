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

export const glossaryRoute = "/glossary/";
export const glossaryRequiredLinks = [
  "/evidence/",
  "/proof-paths/",
  "/proof-source-catalog/",
  "/limitations/",
  "/validation/",
  "/roadmap/",
  "/capabilities/",
  "/manager-brief/",
  "/review-claim-checklist/",
  "/docs/"
];

const pageArtifact = "glossary/index.html";
const sitemapArtifact = "sitemap.xml";
const routesIndexArtifact = "routes-index.json";
const sanctionedSectionIds = ["local-only-artifact-family", "non-claims"];
const wordCountMin = 900;
const wordCountMax = 2200;

const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "engineers, reviewers, managers, architects, and agents",
  "vocabulary map, not a new source of proof",
  "The glossary defines public-safe terminology; it does not certify that every term is fully implemented, complete, or present on every TraceMap surface",
  "The glossary does not expand TraceMap's public claims"
];

export const glossaryRequiredTerms = [
  { term: "rule ID", anchor: "rule-id" },
  { term: "evidence tier", anchor: "evidence-tier" },
  { term: "proof path", anchor: "proof-path" },
  { term: "coverage label", anchor: "coverage-label" },
  { term: "limitation", anchor: "limitation" },
  { term: "analysis gap", anchor: "analysis-gap" },
  { term: "commit/source context", anchor: "commit-source-context" },
  { term: "extractor version", anchor: "extractor-version" },
  { term: "supporting IDs", anchor: "supporting-ids" },
  { term: "public claim level", anchor: "public-claim-level" },
  { term: "local-only artifact family", anchor: "local-only-artifact-family" }
];

const requiredTiers = ["Tier1Semantic", "Tier2Structural", "Tier3SyntaxOrTextual", "Tier4Unknown"];
const requiredNonClaims = [
  "runtime behavior",
  "production traffic",
  "endpoint performance",
  "outage cause",
  "release safety",
  "operational safety",
  "AI impact analysis",
  "LLM analysis",
  "complete product coverage",
  "raw artifact publication"
];
const requiredRawBoundaryTokens = ["facts.ndjson", "index.sqlite", "logs/analyzer.log", ".tracemap/", ".tracemap-demo/"];

const forbiddenAffirmativePositioning = [
  { label: "AI-powered", pattern: /\bAI[- ]?powered\b/i },
  { label: "AI impact analysis", pattern: affirmativeTraceMapPattern("AI impact analysis") },
  { label: "LLM-powered", pattern: /\bLLM[- ]?powered\b/i },
  { label: "LLM analysis", pattern: affirmativeTraceMapPattern("LLM analysis") },
  { label: "machine learning impact analysis", pattern: affirmativeTraceMapPattern("machine learning impact analysis") },
  { label: "artificial intelligence impact analysis", pattern: affirmativeTraceMapPattern("artificial intelligence impact analysis") },
  { label: "intelligent analysis", pattern: /\b(?:TraceMap|glossary)\b[^.]{0,80}\b(?:provides|performs|uses|runs|offers|adds|includes)\b[^.]{0,80}\bintelligent analysis\b/i },
  { label: "smart impact", pattern: /\bsmart impact\b/i },
  { label: "vector database", pattern: affirmativeTraceMapPattern("vector database") },
  { label: "prompt-based classification", pattern: affirmativeTraceMapPattern("prompt-based classification") },
  { label: "powered by embeddings", pattern: /\bpowered by embeddings\b/i },
  { label: "uses embeddings", pattern: /\buses embeddings\b/i },
  { label: "embedding-based analysis", pattern: /\bembedding-based analysis\b/i },
  { label: "vector search-powered", pattern: /\bvector search[- ]powered\b/i },
  { label: "vector search analysis", pattern: affirmativeTraceMapPattern("vector search analysis") },
  { label: "runtime proof", pattern: /\b(?:TraceMap|glossary|static evidence)\b[^.]{0,80}\b(?:proves|certifies|confirms|guarantees)\b[^.]{0,80}\bruntime behavior\b/i },
  { label: "production traffic proof", pattern: /\b(?:proves|certifies|confirms|guarantees)\b[^.]{0,80}\bproduction traffic\b/i },
  { label: "endpoint performance proof", pattern: /\b(?:proves|certifies|confirms|guarantees)\b[^.]{0,80}\bendpoint performance\b/i },
  { label: "outage cause proof", pattern: /\b(?:proves|certifies|confirms|guarantees|diagnoses)\b[^.]{0,80}\boutage cause\b/i },
  { label: "release safety proof", pattern: /\b(?:proves|certifies|confirms|guarantees)\b[^.]{0,80}\brelease safety\b/i },
  { label: "operational safety proof", pattern: /\b(?:proves|certifies|confirms|guarantees)\b[^.]{0,80}\boperational safety\b/i },
  { label: "complete product coverage", pattern: /\b(?:proves|certifies|confirms|guarantees|provides)\b[^.]{0,80}\bcomplete product coverage\b/i },
  { label: "unsupported impacted wording", pattern: /\b(?:endpoint|route|contract|surface|service|package)\b[^.]{0,80}\bimpacted\b/i }
];

const forbiddenPrivateRawPatterns = [
  { label: "/Users/", pattern: /(?:^|[\s"'(=])\/Users\//i },
  { label: "/home/", pattern: /(?:^|[\s"'(=])\/home\//i },
  { label: "~/", pattern: /(?:^|[\s"'(=])~\//i },
  { label: "C:\\", pattern: /[A-Za-z]:\\/ },
  { label: "file://", pattern: /file:\/\//i },
  { label: "localhost", pattern: /\blocalhost\b/i },
  { label: "127.0.0.1", pattern: /\b127\.0\.0\.1\b/ },
  { label: "raw remotes", pattern: /\b(?:git@[^:\s]+:[^\s]+|https:\/\/github\.com\/[^/\s]+\/[^/\s]+\.git|raw remotes?)\b/i },
  { label: "connection string", pattern: /\b(?:ConnectionString|connection string|Server=|User Id=|Password=)\b/i },
  { label: "credential token", pattern: /\b(?:api[_-]?key|access[_-]?token|client[_-]?secret|secret\s*=|password\s*=|sk-[A-Za-z0-9_-]{12,})\b/i },
  { label: "raw SQL", pattern: /\braw SQL\b/i },
  { label: "raw source snippets", pattern: /\braw source snippets?\b/i },
  { label: "config values", pattern: /\bconfig values?\b/i },
  { label: "secrets", pattern: /\bsecrets?\b/i },
  { label: "generated scan directories", pattern: /\bgenerated scan directories\b/i },
  { label: "private sample names", pattern: /\bprivate sample names?\b/i },
  { label: "hidden validation details", pattern: /\bhidden validation details\b/i },
  { label: "facts.ndjson", pattern: /\bfacts\.ndjson\b/i },
  { label: "index.sqlite", pattern: /\bindex\.sqlite\b/i },
  { label: "logs/analyzer.log", pattern: /\blogs\/analyzer\.log\b/i },
  { label: ".tracemap/", pattern: /\.tracemap\//i },
  { label: ".tracemap-demo/", pattern: /\.tracemap-demo\//i }
];

export async function validateGlossaryDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "glossary", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence(`Glossary page is missing required public route: ${glossaryRoute}`, pageArtifact));
    errors.push(...localErrors);
    return;
  }

  await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  await validateRoutesIndex({ dist, errors: localErrors });
  await validateGlossaryPage({ pagePath, errors: localErrors });

  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${glossaryRoute}`)) {
    errors.push(withEvidence(`Glossary sitemap is missing required route: ${baseUrl}${glossaryRoute}`, sitemapArtifact));
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
    errors.push(withEvidence(`Glossary could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return;
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Glossary routes-index.json is invalid: expected entries array", routesIndexArtifact));
    return;
  }

  const routeEntry = parsed.entries.find((entry) => entry?.path === glossaryRoute);
  if (!routeEntry) {
    errors.push(withEvidence(`Glossary routes-index.json is missing required route: ${glossaryRoute}`, routesIndexArtifact));
    return;
  }

  const expectedFields = {
    publicClaimLevel: "concept",
    hintCategory: "evidence",
    sourceType: "site-page",
    preferredProofPath: "/proof-paths/"
  };

  for (const [field, expected] of Object.entries(expectedFields)) {
    if (routeEntry[field] !== expected) {
      errors.push(withEvidence(`Glossary routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
    }
  }

  for (const field of ["title", "summary"]) {
    if (typeof routeEntry[field] !== "string" || routeEntry[field].trim() === "") {
      errors.push(withEvidence(`Glossary routes-index.json is missing non-empty ${field}`, routesIndexArtifact));
    }
  }

  const publicMetadataText = [
    routeEntry.title,
    routeEntry.summary,
    ...(Array.isArray(routeEntry.limitations) ? routeEntry.limitations : [])
  ]
    .filter((value) => typeof value === "string")
    .join(" ");

  for (const { label, pattern } of forbiddenAffirmativePositioning) {
    if (pattern.test(publicMetadataText)) {
      errors.push(withEvidence(`Glossary routes-index.json contains forbidden affirmative positioning: ${label}`, routesIndexArtifact));
    }
  }

  const nonClaimsText = Array.isArray(routeEntry.nonClaims) ? routeEntry.nonClaims.join(" ") : "";
  for (const phrase of requiredNonClaims) {
    if (!nonClaimsText.toLowerCase().includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Glossary routes-index.json nonClaims are missing boundary phrase: ${phrase}`, routesIndexArtifact));
    }
  }
}

async function validateGlossaryPage({ pagePath, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const pageText = normalizeRenderedText(html);
  const decodedText = normalizeRenderedText(decodedHtml);
  const unsanctionedHtml = removeSectionsById(decodedHtml, sanctionedSectionIds);
  const unsanctionedText = normalizeRenderedText(unsanctionedHtml);
  const positioningText = `${unsanctionedHtml} ${unsanctionedText}`;
  const fullText = `${decodedHtml} ${decodedText}`;
  const wordCount = countWords(pageText);

  for (const phrase of requiredText) {
    if (!pageText.toLowerCase().includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Glossary page is missing required text: ${phrase}`, pageArtifact));
    }
  }

  if (!hasElementWithAttributes(html, "link", { rel: "canonical", href: "https://tracemap.tools/glossary/" })) {
    errors.push(withEvidence("Glossary page is missing canonical metadata for https://tracemap.tools/glossary/.", pageArtifact));
  }

  if (!hasElementWithAttributes(html, "meta", { property: "og:title" }, ["content"])) {
    errors.push(withEvidence("Glossary page is missing Open Graph title metadata.", pageArtifact));
  }

  if (!hasElementWithAttributes(html, "meta", { name: "tracemap:public-claim-level", content: "concept" })) {
    errors.push(withEvidence("Glossary page is missing concept page-level metadata.", pageArtifact));
  }

  for (const link of glossaryRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Glossary page is missing required link: ${link}`, pageArtifact));
    }
  }

  for (const { term, anchor } of glossaryRequiredTerms) {
    if (!hasId(html, anchor)) {
      errors.push(withEvidence(`Glossary page is missing required term anchor: ${anchor}`, pageArtifact));
    }

    const entryHtml = extractElementById(html, anchor);
    const entryText = normalizeRenderedText(entryHtml);
    if (!entryHtml || !entryText.toLowerCase().includes(term.toLowerCase())) {
      errors.push(withEvidence(`Glossary page is missing required term entry: ${term}`, pageArtifact));
      continue;
    }

    for (const phrase of ["Definition:", "Public use:", "Limitation:"]) {
      if (!entryText.includes(phrase)) {
        errors.push(withEvidence(`Glossary term ${term} is missing ${phrase}`, pageArtifact));
      }
    }
  }

  for (const tier of requiredTiers) {
    if (!pageText.includes(tier)) {
      errors.push(withEvidence(`Glossary page is missing required evidence tier: ${tier}`, pageArtifact));
    }
  }

  const nonClaimsHtml = extractElementById(html, "non-claims");
  const nonClaimsText = normalizeRenderedText(nonClaimsHtml);
  if (!nonClaimsHtml) {
    errors.push(withEvidence("Glossary page is missing required non-claims section.", pageArtifact));
  } else {
    for (const phrase of [...requiredNonClaims, ...requiredRawBoundaryTokens]) {
      if (!nonClaimsText.toLowerCase().includes(phrase.toLowerCase())) {
        errors.push(withEvidence(`Glossary non-claims section is missing boundary phrase: ${phrase}`, pageArtifact));
      }
    }
  }

  if (wordCount < wordCountMin || wordCount > wordCountMax) {
    errors.push(withEvidence(`Glossary page word count must be between ${wordCountMin} and ${wordCountMax} words, got ${wordCount}`, pageArtifact));
  }

  for (const { label, pattern } of forbiddenAffirmativePositioning) {
    if (pattern.test(positioningText)) {
      errors.push(withEvidence(`Glossary page contains forbidden affirmative positioning outside sanctioned sections: ${label}`, pageArtifact));
    }
  }

  for (const { label, pattern } of forbiddenPrivateRawPatterns) {
    if (pattern.test(unsanctionedHtml) || pattern.test(unsanctionedText)) {
      errors.push(withEvidence(`Glossary page contains forbidden private/raw material outside sanctioned sections: ${label}`, pageArtifact));
    }
  }

  for (const token of requiredRawBoundaryTokens) {
    if (!fullText.toLowerCase().includes(token.toLowerCase())) {
      errors.push(withEvidence(`Glossary page is missing required raw-material boundary token: ${token}`, pageArtifact));
    }
  }
}

function affirmativeTraceMapPattern(phrase) {
  return new RegExp(
    `\\b(?:TraceMap|glossary)\\b[^.]{0,80}\\b(?:provides|performs|uses|runs|offers|adds|includes|is|does)\\b[^.]{0,80}\\b${escapeRegExp(phrase)}\\b`,
    "i"
  );
}

function removeSectionsById(html, ids) {
  let result = html;
  for (const id of ids) {
    const escaped = escapeRegExp(id);
    result = result.replace(new RegExp(`<([a-z][\\w:-]*)\\b(?=[^>]*\\sid\\s*=\\s*["']${escaped}["'])[^>]*>[\\s\\S]*?<\\/\\1>`, "gi"), "");
  }

  return result;
}

function extractElementById(html, id) {
  const escaped = escapeRegExp(id);
  const match = html.match(new RegExp(`<([a-z][\\w:-]*)\\b(?=[^>]*\\sid\\s*=\\s*["']${escaped}["'])[^>]*>[\\s\\S]*?<\\/\\1>`, "i"));
  return match ? match[0] : "";
}

function hasHref(html, href) {
  const escaped = escapeRegExp(href);
  return new RegExp(`<a\\b[^>]*\\shref\\s*=\\s*["']${escaped}["']`, "i").test(html);
}

function hasId(html, id) {
  const escaped = escapeRegExp(id);
  return new RegExp(`\\sid\\s*=\\s*["']${escaped}["']`, "i").test(html);
}

function hasElementWithAttributes(html, tagName, expectedAttributes, requiredAttributes = []) {
  const escapedTagName = escapeRegExp(tagName);
  for (const match of html.matchAll(new RegExp(`<${escapedTagName}\\b([^>]*)>`, "gi"))) {
    const attributes = parseAttributes(match[1]);
    const hasExpectedAttributes = Object.entries(expectedAttributes).every(
      ([name, value]) => attributes.get(name.toLowerCase()) === value
    );
    const hasRequiredAttributes = requiredAttributes.every((name) => {
      const value = attributes.get(name.toLowerCase());
      return typeof value === "string" && value.trim() !== "";
    });

    if (hasExpectedAttributes && hasRequiredAttributes) {
      return true;
    }
  }

  return false;
}

function parseAttributes(value) {
  const attributes = new Map();
  for (const match of value.matchAll(/\b([^\s"'=<>`]+)\s*=\s*(?:"([^"]*)"|'([^']*)')/g)) {
    attributes.set(match[1].toLowerCase(), decodeHtmlEntities(match[2] ?? match[3] ?? ""));
  }

  return attributes;
}

function withEvidence(message, artifact) {
  return `${message} Evidence: ${artifact}.`;
}

function countWords(value) {
  return (value.match(/\b[\w'-]+\b/g) ?? []).length;
}
