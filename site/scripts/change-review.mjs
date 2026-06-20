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

export const changeReviewRoute = "/use-cases/change-review/";
export const changeReviewRequiredLinks = [
  "/proof-paths/",
  "/packets/",
  "/review-room/",
  "/validation/",
  "/limitations/",
  "/use-cases/endpoint-review/",
  "/use-cases/incident-review/",
  "/static-vs-runtime/",
  "/review-claim-checklist/",
  "/use-cases/"
];

const pageArtifact = "use-cases/change-review/index.html";
const sitemapArtifact = "sitemap.xml";
const routesIndexArtifact = "routes-index.json";

const sanctionedSectionIds = [
  "change-review-stop-conditions",
  "change-review-limitations",
  "change-review-non-claims"
];

const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "A change review brief is a bounded static-evidence packet for a PR, release, or change-review conversation.",
  "Engineers",
  "Code reviewers",
  "Architects and managers",
  "Release reviewers and agents",
  "Change Context",
  "Evidence Packet",
  "Review Questions",
  "Stop Conditions",
  "Next Owners",
  "Limitations",
  "Non-Claims",
  "proof path",
  "Rule ID or rule family",
  "Visible static dependency surfaces",
  "Tier1Semantic",
  "Tier2Structural",
  "Tier3SyntaxOrTextual",
  "Tier4Unknown",
  "coverage label",
  "file path and line span",
  "commit SHA",
  "extractor version",
  "The brief does not replace tests, code review, source review, runtime observability, release review, owner confirmation, or human judgment.",
  "A change review brief is not release approval and does not approve a release."
];

const requiredSectionIds = [
  "change-context",
  "evidence-packet",
  "review-questions",
  "change-review-stop-conditions",
  "next-owners",
  "change-review-limitations",
  "change-review-non-claims",
  "adjacent-routes"
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
  "release approval",
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

const forbiddenUnsanctionedPrivateTextPatterns = [
  { label: ".ndjson file reference", pattern: /\b[\w.-]+\.ndjson\b/i },
  { label: ".sqlite file reference", pattern: /\b[\w.-]+\.sqlite\b/i },
  { label: "generated scan directory", pattern: /\b(?:generated scan directories|scan output folders|\.tracemap)\b/i },
  { label: "private sample names", pattern: /\bprivate sample names\b/i },
  { label: "raw facts", pattern: /\braw facts?\b/i },
  { label: "raw SQLite", pattern: /\braw SQLite\b/i },
  { label: "analyzer logs", pattern: /\banalyzer logs?\b/i },
  { label: "raw source snippets", pattern: /\braw source snippets?\b/i },
  { label: "raw SQL", pattern: /\braw SQL\b/i },
  { label: "config values", pattern: /\bconfig values\b/i },
  { label: "secrets", pattern: /\bsecrets\b/i },
  { label: "credentials", pattern: /\bcredentials\b/i },
  { label: "connection strings", pattern: /\bconnection strings?\b/i },
  { label: "raw remotes", pattern: /\braw remotes?\b/i },
  { label: "raw command output", pattern: /\braw command output\b/i },
  { label: "hidden validation details", pattern: /\bhidden validation details\b/i }
];

const forbiddenWholePagePatterns = [
  { label: "/Users/", pattern: /(?:^|[\s"'(=])\/Users\//i },
  { label: "/home/", pattern: /(?:^|[\s"'(=])\/home\//i },
  { label: "C:\\Users\\", pattern: /[A-Za-z]:\\Users\\/i },
  { label: "file://", pattern: /file:\/\//i },
  { label: "localhost", pattern: /\blocalhost\b/i },
  { label: "127.0.0.1", pattern: /\b127\.0\.0\.1\b/ },
  { label: "raw repository remote", pattern: /\b(?:git@[^:\s]+:[^\s]+|https:\/\/github\.com\/[^/\s]+\/[^/\s]+\.git)\b/i },
  { label: "connection string value", pattern: /\b(?:ConnectionString|Server=|User Id=|Password=)\b/i },
  { label: "credential-like value", pattern: /\b(?:api[_-]?key|secret\s*=|password\s*=|sk-[A-Za-z0-9_-]{12,})\b/i },
  { label: "team blame", pattern: /\b(?:team|engineers|developers)\b[^.]{0,80}\b(?:failed|missed|broke|caused)\b/i },
  { label: "vendor blame", pattern: /\b(?:vendor|consultant|contractor)\b[^.]{0,80}\b(?:failed|missed|broke|caused|fault)\b/i },
  { label: "scare framing", pattern: /\b(?:bad code|dangerous code|this change is trash)\b/i }
];

const forbiddenUnsanctionedPositioning = [
  { label: "AI or LLM positioning", pattern: /\b(?:AI[- ]?powered|AI impact analysis|LLM[- ]?powered|LLM analysis|machine learning impact analysis|artificial intelligence impact analysis|prompt-based classification|embedding search|vector database analysis|intelligent analysis|intelligent impact analysis|smart impact)\b/i },
  { label: "unsupported impact wording", pattern: /\bimpacted\b/i },
  { label: "unsupported safety wording", pattern: /(?<!public-)\bsafe\b|\bunsafe\b/i },
  { label: "unsupported approval wording", pattern: /\bapproved\b|\bapproves the release\b|\brelease approval\b/i },
  { label: "unsupported blocking wording", pattern: /\bblocked\b/i },
  { label: "unsupported cause wording", pattern: /\broot cause\b/i },
  { label: "unsupported release wording", pattern: /\bvalidated for release\b|\bproduction proven\b/i },
  { label: "unsupported operational wording", pattern: /\boperational assurance\b|\bproduction observability tool\b/i },
  { label: "unsupported replacement wording", pattern: /\breplaces tests\b|\breplaces code review\b|\breplaces source review\b|\breplaces release review\b/i },
  { label: "runtime overclaim", pattern: /\b(?:proves runtime behavior|proves production traffic|proves endpoint performance|proves outage cause|proves release safety|proves operational safety|complete product coverage)\b/i }
];

export async function validateChangeReviewDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "use-cases", "change-review", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence(`Change review page is missing required public route: ${changeReviewRoute}`, pageArtifact));
    errors.push(...localErrors);
    return;
  }

  if (cleanBaseUrl) {
    await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  }
  await validateRoutesIndex({ dist, errors: localErrors });
  await validateChangeReviewPage({ pagePath, errors: localErrors });

  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${changeReviewRoute}`)) {
    errors.push(withEvidence(`Change review sitemap is missing required route: ${baseUrl}${changeReviewRoute}`, sitemapArtifact));
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
    errors.push(withEvidence(`Change review could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return;
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Change review routes-index.json is invalid: expected entries array", routesIndexArtifact));
    return;
  }

  const routeEntry = parsed.entries.find((entry) => entry?.path === changeReviewRoute);
  if (!routeEntry) {
    errors.push(withEvidence(`Change review routes-index.json is missing required route: ${changeReviewRoute}`, routesIndexArtifact));
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
      errors.push(withEvidence(`Change review routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
    }
  }

  for (const field of ["title", "summary"]) {
    if (typeof routeEntry[field] !== "string" || routeEntry[field].trim() === "") {
      errors.push(withEvidence(`Change review routes-index.json is missing non-empty ${field}`, routesIndexArtifact));
    }
  }

  for (const field of ["limitations", "nonClaims"]) {
    if (!Array.isArray(routeEntry[field]) || routeEntry[field].length === 0 || routeEntry[field].some((value) => typeof value !== "string" || value.trim() === "")) {
      errors.push(withEvidence(`Change review routes-index.json is missing non-empty ${field}`, routesIndexArtifact));
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
      errors.push(withEvidence(`Change review discovery ${field} contains artifact-family text outside nonClaims`, routesIndexArtifact));
    }

    if (/\b(?:available|shipped|released|deployed)\b/i.test(value)) {
      errors.push(withEvidence(`Change review discovery ${field} uses unavailable status wording: ${value}`, routesIndexArtifact));
    }

    if (/\b(?:runtime proof|production traffic proof|endpoint performance proof|release safety proof|operational safety proof|AI impact analysis|LLM analysis)\b/i.test(value)) {
      errors.push(withEvidence(`Change review discovery ${field} overstates the concept boundary: ${value}`, routesIndexArtifact));
    }
  }

  const nonClaimsText = Array.isArray(routeEntry.nonClaims) ? routeEntry.nonClaims.join(" ") : "";
  for (const phrase of requiredNonClaims) {
    if (!nonClaimsText.toLowerCase().includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Change review routes-index.json nonClaims are missing boundary phrase: ${phrase}`, routesIndexArtifact));
    }
  }
}

async function validateChangeReviewPage({ pagePath, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const pageText = normalizeRenderedText(html);
  const decodedText = normalizeRenderedText(decodedHtml);
  const unsanctionedHtml = removeSectionsById(decodedHtml, sanctionedSectionIds);
  const unsanctionedText = normalizeRenderedText(unsanctionedHtml);
  const wordCount = countWords(pageText);

  for (const phrase of requiredText) {
    if (!pageText.includes(phrase)) {
      errors.push(withEvidence(`Change review page is missing required text: ${phrase}`, pageArtifact));
    }
  }

  for (const id of requiredSectionIds) {
    if (!hasId(html, id)) {
      errors.push(withEvidence(`Change review page is missing required section id: ${id}`, pageArtifact));
    }
  }

  for (const link of changeReviewRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Change review page is missing required link: ${link}`, pageArtifact));
    }
  }

  if (!hasOgTypeArticle(html)) {
    errors.push(withEvidence('Change review page must include <meta property="og:type" content="article">.', pageArtifact));
  }

  if (wordCount < 700 || wordCount > 1900) {
    errors.push(withEvidence(`Change review page word count must be between 700 and 1900 words, got ${wordCount}`, pageArtifact));
  }

  for (const name of artifactFamilyNames) {
    if (containsText(name, unsanctionedHtml, unsanctionedText)) {
      errors.push(withEvidence(`Change review page contains artifact-family text outside sanctioned sections: ${name}`, pageArtifact));
    }
  }

  for (const { label, pattern } of forbiddenUnsanctionedPrivateTextPatterns) {
    if (pattern.test(unsanctionedHtml) || pattern.test(unsanctionedText)) {
      errors.push(withEvidence(`Change review page contains forbidden public text outside sanctioned sections: ${label}`, pageArtifact));
    }
  }

  for (const { label, pattern } of forbiddenWholePagePatterns) {
    if (pattern.test(decodedHtml) || pattern.test(decodedText)) {
      errors.push(withEvidence(`Change review page contains forbidden ${label}.`, pageArtifact));
    }
  }

  for (const { label, pattern } of forbiddenUnsanctionedPositioning) {
    if (pattern.test(unsanctionedHtml) || pattern.test(unsanctionedText)) {
      errors.push(withEvidence(`Change review page contains unsupported ${label} outside sanctioned sections.`, pageArtifact));
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

function withEvidence(message, artifact) {
  return `${message} Evidence: ${artifact}.`;
}
