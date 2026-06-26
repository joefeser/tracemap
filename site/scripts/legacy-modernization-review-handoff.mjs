import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import {
  decodeHtmlEntities,
  escapeRegExp,
  fileExists,
  normalizeRenderedText,
  readSitemapLocSet
} from "./validate-utils.mjs";
import { topNavigationLinks } from "./build.mjs";

export const legacyModernizationReviewHandoffRoute = "/legacy-modernization/review-handoff/";
export const legacyModernizationReviewHandoffRequiredLinks = [
  "/legacy-dotnet/evidence/",
  "/legacy-evidence/",
  "/legacy-modernization/evidence-map/",
  "/legacy-data-surface/",
  "/legacy-validation/",
  "/proof-paths/",
  "/review-room/",
  "/review-claim-checklist/",
  "/limitations/",
  "/validation/",
  "/outputs/",
  "/docs/",
  "/owners/follow-up/"
];

const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "Static evidence can start the review; it cannot finish the modernization decision.",
  "The page is a review handoff, not a modernization decision, runtime telemetry report, migration tool, service ownership record, or release approval surface.",
  "TraceMap static evidence",
  "Modernization decisions",
  "Runtime telemetry",
  "Migration tooling",
  "Service ownership",
  "Release approval"
];

const requiredHeaders = [
  "Review question",
  "Static evidence to bring",
  "Required proof field",
  "Limitation to keep attached",
  "Owner to involve",
  "Allowed wording",
  "Stop condition"
];

const requiredRows = new Map([
  ["framework-runtime-age", "Framework/runtime age question"],
  ["route-api", "Route/API question"],
  ["data-surface", "Data surface question"],
  ["package-dependency", "Package/dependency question"],
  ["config-deployment-clue", "Config/deployment clue question"],
  ["validation-reduced-coverage", "Validation/reduced coverage question"],
  ["migration-test-planning", "Migration/test planning question"]
]);

const metadataNonClaimTerms = [
  "runtime behavior",
  "production traffic",
  "endpoint performance",
  "release safety",
  "operational safety",
  "migration success",
  "database execution",
  "AI impact analysis",
  "LLM analysis",
  "complete coverage"
];

const forbiddenClaimPatterns = [
  /\bTraceMap (?:proves|guarantees|validates|approves)\b/i,
  /\b(?:runtime behavior|production traffic|endpoint performance|outage cause) (?:is|are) (?:proven|known|validated|mapped)\b/i,
  /\b(?:release|operational) safety (?:is|are) (?:proven|approved|validated)\b/i,
  /\bmigration success (?:is|are)?\s*(?:proven|validated|guaranteed|approved|safe|successful)\b/i,
  /\bschema compatibility (?:is|are) (?:proven|validated|guaranteed)\b/i,
  /\bdatabase (?:execution|connectivity) (?:is|are) (?:proven|validated|guaranteed)\b/i,
  /\bcomplete coverage (?:is|are)?\s*(?:proven|validated|guaranteed|achieved|available)\b/i,
  /\bAI[- ]powered\b/i,
  /\bLLM[- ]powered\b/i,
  /\buses? embeddings?\b/i,
  /\buses? vector databases?\b/i,
  /\buses? prompt classification\b/i
];

const privateMaterialPatterns = [
  { id: "local-absolute-path", pattern: /(?:^|[\s>"'(=])(?:\/Users\/|\/home\/|\/tmp\/|\/var\/folders\/|\/private\/var\/|[A-Za-z]:[\\/])[^\s<>"')]+/gi },
  { id: "generated-scan-directory", pattern: /(?:^|[\s>"'(=])(?:scan-results|generated-scan|site\/dist|site\/output|dist|output)\/[^\s<>"')]+/gi },
  { id: "private-url", pattern: /\b(?:https?:\/\/(?:localhost|127(?:\.\d{1,3}){3}|10(?:\.\d{1,3}){3}|192\.168(?:\.\d{1,3}){2}|172\.(?:1[6-9]|2\d|3[0-1])(?:\.\d{1,3}){2}|[^/\s<>"']+\.local)(?::\d+)?(?:\/[^\s<>"']*)?|file:\/\/[^\s<>"']*)/gi },
  { id: "raw-remote", pattern: /\b(?:git@[^:\s<>"']+:[^\s<>"']+|ssh:\/\/git@[^/\s<>"']+\/[^\s<>"']+|https:\/\/[^/\s<>"']+\/[^\s<>"']+\/[^\s<>"']+\.git)\b/gi },
  { id: "credential-like-value", pattern: /\b(?:api[_-]?key|access[_-]?token|secret|password|passwd|pwd|client[_-]?secret|connection[_-]?string)\s*(?:=|:)\s*["']?[^"'\s<>{}]+/gi },
  { id: "connection-string", pattern: /\b(?:Server|Data Source|Initial Catalog|Database|User ID|User Id|Uid|Password|Pwd)\s*=\s*[^;\s<>"']+(?:\s*;\s*(?:Server|Data Source|Initial Catalog|Database|User ID|User Id|Uid|Password|Pwd)\s*=\s*[^;\s<>"']+)+/gi }
];

export async function validateLegacyModernizationReviewHandoffDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors
}) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl, localErrors);
  const pagePath = resolve(dist, "legacy-modernization", "review-handoff", "index.html");

  if (!(await fileExists(pagePath))) {
    errors.push(`Legacy modernization review handoff page is missing required public route: ${legacyModernizationReviewHandoffRoute}`);
    return;
  }

  if (cleanBaseUrl) {
    await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  }

  const routeEntry = await readRouteEntry({ dist, errors: localErrors });
  validateRouteEntry(routeEntry, localErrors);
  await validatePage({ pagePath, routeEntry, errors: localErrors });

  errors.push(...localErrors);
}

function normalizeBaseUrl(value, errors) {
  let url;
  try {
    url = new URL(value);
  } catch {
    errors.push(`Legacy modernization review handoff baseUrl must be a valid absolute URL: ${String(value)}`);
    return null;
  }
  return url.origin;
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${legacyModernizationReviewHandoffRoute}`)) {
    errors.push(`Legacy modernization review handoff sitemap is missing required route: ${baseUrl}${legacyModernizationReviewHandoffRoute}`);
  }
}

async function readRouteEntry({ dist, errors }) {
  const routesIndexPath = resolve(dist, "routes-index.json");
  if (!(await fileExists(routesIndexPath))) {
    errors.push("Legacy modernization review handoff routes-index.json is missing.");
    return null;
  }

  try {
    const parsed = JSON.parse(await readFile(routesIndexPath, "utf8"));
    if (!Array.isArray(parsed?.entries)) {
      errors.push("Legacy modernization review handoff routes-index.json is invalid: expected entries array.");
      return null;
    }
    return parsed.entries.find((entry) => entry?.path === legacyModernizationReviewHandoffRoute) ?? null;
  } catch (error) {
    errors.push(`Legacy modernization review handoff could not parse routes-index.json: ${error.message}`);
    return null;
  }
}

function validateRouteEntry(routeEntry, errors) {
  if (!routeEntry) {
    errors.push(`Legacy modernization review handoff routes-index.json is missing required route: ${legacyModernizationReviewHandoffRoute}`);
    return;
  }

  const expected = {
    publicClaimLevel: "concept",
    sourceType: "site-page",
    hintCategory: "use-case",
    preferredProofPath: "/legacy-modernization/evidence-map/"
  };

  for (const [field, value] of Object.entries(expected)) {
    if (routeEntry[field] !== value) {
      errors.push(`Legacy modernization review handoff routes-index.json expected ${field} ${value}, got ${String(routeEntry[field])}`);
    }
  }

  if (!Array.isArray(routeEntry.limitations) || routeEntry.limitations.length < 2) {
    errors.push("Legacy modernization review handoff routes-index.json must include at least two limitations.");
  }

  if (!Array.isArray(routeEntry.nonClaims) || routeEntry.nonClaims.length === 0) {
    errors.push("Legacy modernization review handoff routes-index.json must include nonClaims metadata.");
  } else {
    const nonClaims = routeEntry.nonClaims.join(" ");
    for (const term of metadataNonClaimTerms) {
      if (!nonClaims.includes(term)) {
        errors.push(`Legacy modernization review handoff routes-index.json nonClaims are missing required term: ${term}`);
      }
    }
  }

  validateForbiddenText(JSON.stringify(routeEntry), "routes-index metadata", errors);
}

async function validatePage({ pagePath, errors }) {
  const html = await readFile(pagePath, "utf8");
  const mainHtml = extractMainHtml(html);
  const text = normalizeRenderedText(mainHtml);
  const decodedHtml = decodeHtmlEntities(html);
  const wordCount = countWords(text);

  if (!/<title>Legacy Modernization Review Handoff \| TraceMap<\/title>/i.test(html)) {
    errors.push("Legacy modernization review handoff page is missing the expected title.");
  }

  if (!/\bdata-legacy-modernization-review-handoff\b/.test(html)) {
    errors.push("Legacy modernization review handoff matrix marker is missing.");
  }

  for (const phrase of requiredText) {
    if (!text.includes(phrase)) {
      errors.push(`Legacy modernization review handoff is missing required text: ${phrase}`);
    }
  }

  for (const header of requiredHeaders) {
    if (!text.includes(header)) {
      errors.push(`Legacy modernization review handoff matrix is missing required header: ${header}`);
    }
  }

  for (const [row, label] of requiredRows) {
    const rowMatch = html.match(new RegExp(`<tr\\b[^>]*data-handoff-row=["']${escapeRegExp(row)}["'][^>]*>`, "i"));
    if (!rowMatch) {
      errors.push(`Legacy modernization review handoff matrix is missing required row: ${row}`);
      continue;
    }
    const rowText = normalizeRenderedText(sliceRowHtml(html, rowMatch.index ?? 0));
    if (!rowText.includes(label)) {
      errors.push(`Legacy modernization review handoff row ${row} is missing visible label: ${label}`);
    }
    const cellCount = (sliceRowHtml(html, rowMatch.index ?? 0).match(/<td\b/gi) ?? []).length;
    if (cellCount !== requiredHeaders.length) {
      errors.push(`Legacy modernization review handoff row ${row} expected ${requiredHeaders.length} cells, got ${cellCount}.`);
    }
  }

  for (const href of legacyModernizationReviewHandoffRequiredLinks) {
    if (!new RegExp(`href=["']${escapeRegExp(href)}["']`, "i").test(html)) {
      errors.push(`Legacy modernization review handoff is missing required adjacent link: ${href}`);
    }
  }

  if (topNavigationLinks.some((link) => link.href === legacyModernizationReviewHandoffRoute)) {
    errors.push("Legacy modernization review handoff must not be added to primary navigation without a recorded reason.");
  }

  if (wordCount < 650 || wordCount > 1900) {
    errors.push(`Legacy modernization review handoff word count must be between 650 and 1900 words; got ${wordCount}.`);
  }

  validateForbiddenText(`${decodedHtml}\n${text}`, "page", errors);
}

function validateForbiddenText(value, label, errors) {
  for (const pattern of forbiddenClaimPatterns) {
    if (pattern.test(value)) {
      errors.push(`Legacy modernization review handoff ${label} contains forbidden modernization/runtime claim: ${pattern}`);
    }
  }

  for (const check of privateMaterialPatterns) {
    if (check.pattern.test(value)) {
      errors.push(`Legacy modernization review handoff ${label} contains forbidden private/raw material: redacted ${check.id}`);
    }
  }
}

function extractMainHtml(html) {
  return html.match(/<main\b[^>]*>([\s\S]*?)<\/main>/i)?.[1] ?? html;
}

function sliceRowHtml(html, rowStart) {
  const rest = html.slice(rowStart);
  const end = rest.search(/<\/tr\s*>/i);
  return end === -1 ? rest : rest.slice(0, end + rest.slice(end).match(/^<\/tr\s*>/i)[0].length);
}

function countWords(text) {
  return text.split(/\s+/).filter(Boolean).length;
}
