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

export const endpointReviewRoute = "/use-cases/endpoint-review/";
export const endpointReviewRequiredLinks = [
  "/use-cases/",
  "/evidence/",
  "/proof-paths/",
  "/validation/",
  "/limitations/",
  "/review-room/",
  "/static-triage/",
  "/demo/runbook/"
];

const pageArtifact = "use-cases/endpoint-review/index.html";
const sitemapArtifact = "sitemap.xml";
const routesIndexArtifact = "routes-index.json";

const sanctionedSectionIds = ["artifact-boundary", "claim-safe-language"];

const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "Endpoint review starts with static evidence, not certainty.",
  "endpoint-adjacent static paths",
  "packages",
  "config surfaces",
  "SQL-facing surfaces",
  "coverage labels",
  "limitations",
  "static evidence suggests a review candidate",
  "rule ID <rule-id>, Tier2Structural, partial coverage",
  "gap-labeled packet: review question remains open",
  "deeper code review",
  "targeted tests",
  "telemetry question",
  "owner follow-up"
];

const requiredSectionIds = [
  "evidence-packet",
  "workflow",
  "decisions",
  "concept-example",
  "artifact-boundary",
  "claim-safe-language"
];

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
  "facts.ndjson",
  "index.sqlite",
  "logs/analyzer.log"
];

const artifactFamilyNames = [
  "facts.ndjson",
  "index.sqlite",
  "report.md",
  "scan-manifest.json",
  "logs/analyzer.log",
  "analyzer.log"
];

const privateTextPatterns = [
  { label: "/Users/", pattern: /(?:^|[\s"'(=])\/Users\//i },
  { label: "/home/", pattern: /(?:^|[\s"'(=])\/home\//i },
  { label: "C:\\Users\\", pattern: /[A-Za-z]:\\Users\\/i },
  { label: "file://", pattern: /file:\/\//i },
  { label: "localhost", pattern: /\blocalhost\b/i },
  { label: "127.0.0.1", pattern: /\b127\.0\.0\.1\b/ },
  { label: ".ndjson file reference", pattern: /\b[\w.-]+\.ndjson\b/i },
  { label: ".sqlite file reference", pattern: /\b[\w.-]+\.sqlite\b/i },
  { label: "raw repository remote", pattern: /\b(?:git@[^:\s]+:[^\s]+|https:\/\/github\.com\/[^/\s]+\/[^/\s]+\.git|raw remotes?)\b/i },
  { label: "connection string", pattern: /\b(?:ConnectionString|connection string|Server=|User Id=|Password=)\b/i },
  { label: "generated scan directory", pattern: /\b(?:generated scan directories|scan output folders|\.tracemap)\b/i },
  { label: "private sample names", pattern: /\bprivate sample names\b/i },
  { label: "raw source snippets", pattern: /\braw source snippets?\b/i },
  { label: "raw SQL", pattern: /\braw SQL\b/i },
  { label: "config values", pattern: /\bconfig values\b/i },
  { label: "secrets", pattern: /\bsecrets\b/i },
  { label: "credentials", pattern: /\bcredentials\b/i },
  { label: "table dumps", pattern: /\btable dumps\b/i },
  { label: "database contents", pattern: /\bdatabase contents\b/i }
];

const forbiddenWholePagePatterns = [
  { label: "rejected scare framing", pattern: /\bthis endpoint is trash\b/i },
  { label: "team blame", pattern: /\b(?:team|engineers|developers)\b[^.]{0,80}\b(?:failed|missed|broke|caused)\b/i },
  { label: "vendor blame", pattern: /\b(?:vendor|consultant|contractor)\b[^.]{0,80}\b(?:failed|missed|broke|caused|fault)\b/i }
];

const forbiddenUnsanctionedPositioning = [
  { label: "AI or LLM positioning", pattern: /\b(?:AI[- ]?powered|AI impact analysis|LLM[- ]?powered|LLM analysis|prompt-based classification|embedding search|vector database analysis)\b/i },
  { label: "complete coverage", pattern: /\b(?:complete product coverage|fully covered|full coverage)\b/i },
  { label: "operational conclusion", pattern: /\b(?:certifies operational safety|proves operational safety|proves release safety|approves a release|safe to release|unsafe to release)\b/i },
  { label: "runtime conclusion", pattern: /\b(?:proves runtime behavior|proves production traffic|proves production usage|proves endpoint performance|diagnoses an outage|proves outage cause)\b/i },
  { label: "affirmative endpoint conclusion", pattern: /\b(?:endpoint|Endpoint [A-Z])\b[^.]{0,80}\b(?:is|was|seems|looks|remains)\b[^.]{0,40}\b(?:broken|slow|high[- ]traffic|release[- ]safe|operationally safe|safe to release|unsafe to release)\b/i },
  { label: "affirmative endpoint conclusion", pattern: /\b(?:broken|slow|high[- ]traffic|release[- ]safe|operationally safe)\s+endpoint\b/i },
  { label: "unsupported impact wording", pattern: /\bendpoint\b[^.]{0,80}\b(?:is|was|are|were)\b[^.]{0,40}\bimpacted\b/i }
];

export async function validateEndpointReviewDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "use-cases", "endpoint-review", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence(`Endpoint review page is missing required public route: ${endpointReviewRoute}`, pageArtifact));
    errors.push(...localErrors);
    return;
  }

  if (cleanBaseUrl) {
    await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  }
  await validateRoutesIndex({ dist, errors: localErrors });
  await validateEndpointReviewPage({ pagePath, errors: localErrors });

  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${endpointReviewRoute}`)) {
    errors.push(withEvidence(`Endpoint review sitemap is missing required route: ${baseUrl}${endpointReviewRoute}`, sitemapArtifact));
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
    errors.push(withEvidence(`Endpoint review could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return;
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Endpoint review routes-index.json is invalid: expected entries array", routesIndexArtifact));
    return;
  }

  const routeEntry = parsed.entries.find((entry) => entry?.path === endpointReviewRoute);
  if (!routeEntry) {
    errors.push(withEvidence(`Endpoint review routes-index.json is missing required route: ${endpointReviewRoute}`, routesIndexArtifact));
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
      errors.push(
        withEvidence(`Endpoint review routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact)
      );
    }
  }

  for (const field of ["title", "summary"]) {
    if (typeof routeEntry[field] !== "string" || routeEntry[field].trim() === "") {
      errors.push(withEvidence(`Endpoint review routes-index.json is missing non-empty ${field}`, routesIndexArtifact));
    }
  }

  for (const field of ["limitations", "nonClaims"]) {
    if (!Array.isArray(routeEntry[field]) || routeEntry[field].length === 0 || routeEntry[field].some((value) => typeof value !== "string" || value.trim() === "")) {
      errors.push(withEvidence(`Endpoint review routes-index.json is missing non-empty ${field}`, routesIndexArtifact));
    }
  }

  validateDiscoveryBoundary(routeEntry, errors);
}

function validateDiscoveryBoundary(routeEntry, errors) {
  const publicTextFields = [
    ["title", routeEntry.title],
    ["summary", routeEntry.summary],
    ["preferredProofPath", routeEntry.preferredProofPath],
    ...arrayField("limitations", routeEntry.limitations)
  ];

  for (const [field, value] of publicTextFields) {
    if (typeof value !== "string") {
      continue;
    }

    if (containsArtifactFamily(value)) {
      errors.push(withEvidence(`Endpoint review discovery ${field} contains artifact-family text outside nonClaims`, routesIndexArtifact));
    }

    if (/\b(?:available|shipped|released|deployed)\b/i.test(value)) {
      errors.push(withEvidence(`Endpoint review discovery ${field} uses unavailable status wording: ${value}`, routesIndexArtifact));
    }

    if (/\b(?:runtime proof|production traffic proof|endpoint performance proof|release safety proof|operational safety proof|AI impact analysis|LLM analysis)\b/i.test(value)) {
      errors.push(withEvidence(`Endpoint review discovery ${field} overstates the concept boundary: ${value}`, routesIndexArtifact));
    }
  }

  const nonClaimsText = Array.isArray(routeEntry.nonClaims) ? routeEntry.nonClaims.join(" ") : "";
  for (const phrase of requiredNonClaims) {
    if (!nonClaimsText.toLowerCase().includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Endpoint review routes-index.json nonClaims are missing boundary phrase: ${phrase}`, routesIndexArtifact));
    }
  }
}

async function validateEndpointReviewPage({ pagePath, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const pageText = normalizeRenderedText(html);
  const decodedText = normalizeRenderedText(decodedHtml);
  const unsanctionedHtml = removeSectionsById(decodedHtml, sanctionedSectionIds);
  const unsanctionedText = normalizeRenderedText(unsanctionedHtml);
  const wordCount = countWords(pageText);

  for (const phrase of requiredText) {
    const found = phrase.includes("<rule-id>") ? hasEscapedRuleIdPhrase(html) : pageText.includes(phrase);
    if (!found) {
      errors.push(withEvidence(`Endpoint review page is missing required text: ${phrase}`, pageArtifact));
    }
  }

  for (const id of requiredSectionIds) {
    if (!hasId(html, id)) {
      errors.push(withEvidence(`Endpoint review page is missing required section id: ${id}`, pageArtifact));
    }
  }

  for (const link of endpointReviewRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Endpoint review page is missing required link: ${link}`, pageArtifact));
    }
  }

  if (!hasOgTypeArticle(html)) {
    errors.push(withEvidence('Endpoint review page must include <meta property="og:type" content="article">.', pageArtifact));
  }

  if (wordCount < 700 || wordCount > 1900) {
    errors.push(withEvidence(`Endpoint review page word count must be between 700 and 1900 words, got ${wordCount}`, pageArtifact));
  }

  for (const name of artifactFamilyNames) {
    if (containsText(name, unsanctionedHtml, unsanctionedText)) {
      errors.push(withEvidence(`Endpoint review page contains artifact-family text outside sanctioned sections: ${name}`, pageArtifact));
    }
  }

  for (const { label, pattern } of privateTextPatterns) {
    if (pattern.test(unsanctionedHtml) || pattern.test(unsanctionedText)) {
      errors.push(withEvidence(`Endpoint review page contains forbidden public text outside sanctioned sections: ${label}`, pageArtifact));
    }
  }

  for (const { label, pattern } of forbiddenWholePagePatterns) {
    if (pattern.test(decodedHtml) || pattern.test(decodedText)) {
      errors.push(withEvidence(`Endpoint review page contains forbidden ${label}.`, pageArtifact));
    }
  }

  for (const { label, pattern } of forbiddenUnsanctionedPositioning) {
    if (pattern.test(unsanctionedHtml) || pattern.test(unsanctionedText)) {
      errors.push(withEvidence(`Endpoint review page contains unsupported ${label} outside sanctioned sections.`, pageArtifact));
    }
  }
}

function arrayField(field, values) {
  return Array.isArray(values) ? values.map((value, index) => [`${field}[${index}]`, value]) : [];
}

function containsArtifactFamily(value) {
  return artifactFamilyNames.some((name) => containsText(name, value));
}

function containsText(needle, ...values) {
  const normalizedNeedle = needle.toLowerCase();
  return values.some((value) => String(value).toLowerCase().includes(normalizedNeedle));
}

function removeSectionsById(html, ids) {
  let result = html;
  for (const id of ids) {
    const escaped = escapeRegExp(id);
    result = result.replace(new RegExp(`<([a-z][\\w:-]*)\\b(?=[^>]*\\bid\\s*=\\s*["']${escaped}["'])[^>]*>[\\s\\S]*?<\\/\\1>`, "gi"), "");
  }
  return result;
}

function hasHref(html, href) {
  const escaped = escapeRegExp(href);
  return new RegExp(`<a\\b[^>]*\\bhref\\s*=\\s*["']${escaped}["']`, "i").test(html);
}

function hasId(html, id) {
  const escaped = escapeRegExp(id);
  return new RegExp(`\\bid\\s*=\\s*["']${escaped}["']`, "i").test(html);
}

function hasOgTypeArticle(html) {
  return /<meta\b(?=[^>]*\bproperty\s*=\s*["']og:type["'])(?=[^>]*\bcontent\s*=\s*["']article["'])[^>]*>/i.test(html);
}

function countWords(value) {
  return (value.match(/\b[\w'-]+\b/g) ?? []).length;
}

function hasEscapedRuleIdPhrase(html) {
  const escapedLt = "(?:&lt;|&#0*60;|&#x0*3c;)";
  const escapedGt = "(?:&gt;|&#0*62;|&#x0*3e;)";
  return new RegExp(
    `rule\\s+ID\\s+${escapedLt}rule-id${escapedGt}\\s*,\\s*Tier2Structural,\\s*partial\\s+coverage`,
    "i"
  ).test(html);
}

function withEvidence(message, artifact) {
  return `${message} Evidence: ${artifact}.`;
}
