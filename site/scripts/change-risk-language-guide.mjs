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

export const changeRiskLanguageGuideRoute = "/language/change-risk/";
export const changeRiskLanguageGuideRequiredLinks = [
  "/review-claim-checklist/",
  "/questions/objections/",
  "/release-review-boundary/",
  "/static-vs-runtime/",
  "/proof-paths/faq/",
  "/manager-faq/"
];
export const changeRiskLanguageGuideInboundRoutes = [
  "/review-claim-checklist/",
  "/questions/objections/",
  "/manager-faq/"
];

const pageArtifact = "language/change-risk/index.html";
const sitemapArtifact = "sitemap.xml";
const routesIndexArtifact = "routes-index.json";

const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "wording discipline",
  "not established by this scan",
  "TraceMap found static evidence for this review question",
  "owner decision needed"
];

const requiredSections = [
  "why-wording-matters",
  "safe-static-evidence-phrases",
  "unsafe-phrases",
  "evidence-required-wording",
  "reduced-coverage-wording",
  "owner-handoff-wording",
  "stop-conditions",
  "non-claims"
];

const requiredTables = new Set([
  "safe-phrasing",
  "unsafe-blocked-phrasing",
  "needs-review",
  "evidence-shows",
  "coverage-reduced",
  "when-to-stop"
]);

const safePhraseExamples = [
  "static evidence shows",
  "evidence is limited to",
  "coverage is reduced",
  "needs review",
  "owner decision needed"
];

const blockedPhraseExamples = [
  "TraceMap proved impact",
  "Safe to release",
  "No impact",
  "Runtime confirms it",
  "Production is unaffected",
  "Complete coverage",
  "AI analyzed the change",
  "Approved for merge"
];

const metadataNonClaimTerms = [
  "impact proof",
  "absence-of-impact proof",
  "release approval",
  "release safety",
  "operational safety",
  "runtime proof",
  "production traffic proof",
  "endpoint performance proof",
  "complete coverage",
  "AI impact analysis",
  "LLM analysis",
  "replacement of human judgment"
];

const forbiddenPositiveClaimPatterns = [
  /\bTraceMap\s+(?:proved|proves|can prove|has proven)\s+(?:impact|absence of impact|runtime behavior|production behavior|release safety|operational safety|complete coverage)\b/i,
  /\b(?:this|the)\s+(?:scan|guide|page|evidence)\s+(?:proved|proves|can prove|has proven)\s+(?:impact|absence of impact|runtime behavior|production behavior|release safety|operational safety|complete coverage)\b/i,
  /\b(?:safe to release|release safe|approved for merge|approved for release|production is unaffected|runtime confirms|complete coverage|AI analyzed the change)\b/i,
  /\b(?:AI-powered|LLM-powered|embedding-backed|vector database impact analysis|prompt-classified|autonomous approval|autonomous review)\b/i,
  /\breplaces?\s+(?:human judgment|human review|tests|code review|source review|runtime observability|release process)\b/i
];

const hardPrivatePatterns = [
  /\/Users\//i,
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

export async function validateChangeRiskLanguageGuideDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors
}) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "language", "change-risk", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence(`Change-risk language guide is missing required public route: ${changeRiskLanguageGuideRoute}`, pageArtifact));
    errors.push(...localErrors);
    return;
  }

  await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  const routeContext = await readRouteContext({ dist, errors: localErrors });
  await validatePage({ pagePath, routeContext, errors: localErrors });
  await validateInboundLinks({ dist, errors: localErrors });

  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, sitemapArtifact);
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${changeRiskLanguageGuideRoute}`)) {
    errors.push(withEvidence(`Change-risk language guide sitemap is missing required route: ${baseUrl}${changeRiskLanguageGuideRoute}`, sitemapArtifact));
  }
}

async function readRouteContext({ dist, errors }) {
  const routesIndexPath = resolve(dist, routesIndexArtifact);
  const routePaths = new Set();
  let routeEntry = null;

  if (!(await fileExists(routesIndexPath))) {
    return { routeEntry, routePaths };
  }

  let parsed;
  try {
    parsed = JSON.parse(await readFile(routesIndexPath, "utf8"));
  } catch (error) {
    errors.push(withEvidence(`Change-risk language guide could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return { routeEntry, routePaths };
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Change-risk language guide routes-index.json is invalid: expected entries array", routesIndexArtifact));
    return { routeEntry, routePaths };
  }

  for (const entry of parsed.entries) {
    if (typeof entry?.path === "string") {
      routePaths.add(normalizeRouteHref(entry.path));
    }
  }

  routeEntry = parsed.entries.find((entry) => normalizeRouteHref(entry?.path ?? "") === changeRiskLanguageGuideRoute) ?? null;
  validateRouteEntry(routeEntry, errors);

  return { routeEntry, routePaths };
}

function validateRouteEntry(routeEntry, errors) {
  if (!routeEntry) {
    errors.push(withEvidence(`Change-risk language guide routes-index.json is missing required route: ${changeRiskLanguageGuideRoute}`, routesIndexArtifact));
    return;
  }

  const expectedFields = {
    publicClaimLevel: "concept",
    sourceType: "site-page",
    hintCategory: "evidence",
    preferredProofPath: "/proof-paths/"
  };

  for (const [field, expected] of Object.entries(expectedFields)) {
    if (routeEntry[field] !== expected) {
      errors.push(withEvidence(`Change-risk language guide routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
    }
  }

  if (!Array.isArray(routeEntry.limitations) || routeEntry.limitations.length < 2) {
    errors.push(withEvidence("Change-risk language guide routes-index.json must include at least two limitations.", routesIndexArtifact));
  }

  if (!Array.isArray(routeEntry.nonClaims) || routeEntry.nonClaims.length < 2) {
    errors.push(withEvidence("Change-risk language guide routes-index.json must include nonClaims metadata.", routesIndexArtifact));
    return;
  }

  const nonClaimText = routeEntry.nonClaims.join(" ");
  for (const term of metadataNonClaimTerms) {
    if (!nonClaimText.toLowerCase().includes(term.toLowerCase())) {
      errors.push(withEvidence(`Change-risk language guide nonClaims are missing required term: ${term}`, routesIndexArtifact));
    }
  }

  validateForbiddenPositiveClaims(stripSanctionedText(nonClaimText), errors, routesIndexArtifact);
  validateHardPrivateMaterial(nonClaimText, errors, routesIndexArtifact);
}

async function validatePage({ pagePath, routeContext, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const mainHtml = extractElementByTag(html, "main") ?? html;
  const mainText = normalizeRenderedText(mainHtml);
  const lowerMainText = mainText.toLowerCase();
  const wordCount = countWords(mainText);

  for (const phrase of requiredText) {
    if (!lowerMainText.includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Change-risk language guide is missing required text: ${phrase}`, pageArtifact));
    }
  }

  for (const section of requiredSections) {
    if (!hasElementId(html, section)) {
      errors.push(withEvidence(`Change-risk language guide is missing required section: ${section}`, pageArtifact));
    }
  }

  validateMetadata(html, errors);
  validateRequiredLinks(html, routeContext, errors);
  validateTables(html, mainText, errors);

  if (wordCount < 1000 || wordCount > 2400) {
    errors.push(withEvidence(`Change-risk language guide word count must be between 1000 and 2400 words, got ${wordCount}`, pageArtifact));
  }

  validateHardPrivateMaterial(decodedHtml, errors, pageArtifact);
  validateForbiddenPositiveClaims(stripSanctionedHtml(decodedHtml), errors, pageArtifact);
}

function validateMetadata(html, errors) {
  const requiredMetadata = [
    {
      label: "canonical URL",
      pattern: /<link\b(?=[^>]*\brel\s*=\s*["']canonical["'])(?=[^>]*\bhref\s*=\s*["']https:\/\/tracemap\.tools\/language\/change-risk\/["'])[^>]*>/i
    },
    {
      label: "meta description",
      pattern: /<meta\b(?=[^>]*\bname\s*=\s*["']description["'])(?=[^>]*\bcontent\s*=\s*["'][^"']+["'])[^>]*>/i
    },
    {
      label: "Open Graph type",
      pattern: /<meta\b(?=[^>]*\bproperty\s*=\s*["']og:type["'])(?=[^>]*\bcontent\s*=\s*["']article["'])[^>]*>/i
    },
    {
      label: "Open Graph title",
      pattern: /<meta\b(?=[^>]*\bproperty\s*=\s*["']og:title["'])(?=[^>]*\bcontent\s*=\s*["'][^"']*Change-Risk Language Guide[^"']*["'])[^>]*>/i
    },
    {
      label: "Open Graph URL",
      pattern: /<meta\b(?=[^>]*\bproperty\s*=\s*["']og:url["'])(?=[^>]*\bcontent\s*=\s*["']https:\/\/tracemap\.tools\/language\/change-risk\/["'])[^>]*>/i
    }
  ];

  for (const { label, pattern } of requiredMetadata) {
    if (!pattern.test(html)) {
      errors.push(withEvidence(`Change-risk language guide is missing required metadata: ${label}`, pageArtifact));
    }
  }
}

function validateRequiredLinks(html, routeContext, errors) {
  for (const link of changeRiskLanguageGuideRequiredLinks) {
    if (!routeContext.routePaths.has(normalizeRouteHref(link))) {
      errors.push(withEvidence(`Change-risk language guide expected adjacent route is unavailable: ${link}`, routesIndexArtifact));
      continue;
    }

    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Change-risk language guide is missing required adjacent link: ${link}`, pageArtifact));
    }
  }
}

function validateTables(html, mainText, errors) {
  const tables = extractLanguageTables(html);
  const seen = new Set(tables.map((table) => table.name));

  for (const required of requiredTables) {
    if (!seen.has(required)) {
      errors.push(withEvidence(`Change-risk language guide is missing required table: ${required}`, pageArtifact));
    }
  }

  for (const table of tables) {
    const headerText = normalizeRenderedText(table.html).toLowerCase();
    if (!headerText.includes("condition")) {
      errors.push(withEvidence(`Change-risk language guide table ${table.name} is missing condition column text.`, pageArtifact));
    }

    if (!/(allowed wording|blocked wording)/i.test(headerText)) {
      errors.push(withEvidence(`Change-risk language guide table ${table.name} is missing allowed or blocked wording column text.`, pageArtifact));
    }

    if (!/(evidence|boundary reason)/i.test(headerText)) {
      errors.push(withEvidence(`Change-risk language guide table ${table.name} is missing evidence or boundary reason column text.`, pageArtifact));
    }
  }

  for (const phrase of safePhraseExamples) {
    if (!mainText.toLowerCase().includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Change-risk language guide safe phrasing table is missing example: ${phrase}`, pageArtifact));
    }
  }

  for (const phrase of blockedPhraseExamples) {
    const pattern = new RegExp(`<span\\b(?=[^>]*\\bdata-blocked-phrase\\b)[^>]*>\\s*${escapeRegExp(phrase)}\\.?\\s*<\\/span>`, "i");
    if (!pattern.test(html)) {
      errors.push(withEvidence(`Change-risk language guide unsafe table is missing blocked marked phrase: ${phrase}`, pageArtifact));
    }
  }
}

async function validateInboundLinks({ dist, errors }) {
  const missingInbound = [];

  for (const route of changeRiskLanguageGuideInboundRoutes) {
    const routePath = route.replace(/^\/|\/$/g, "");
    const indexPath = resolve(dist, routePath, "index.html");

    if (!(await fileExists(indexPath))) {
      continue;
    }

    const html = await readFile(indexPath, "utf8");
    if (!hasHref(html, changeRiskLanguageGuideRoute)) {
      missingInbound.push(route);
    }
  }

  if (missingInbound.length > 0) {
    errors.push(withEvidence(`Change-risk language guide is missing inbound links from live adjacent routes: ${missingInbound.join(", ")}`, pageArtifact));
  }
}

function stripSanctionedHtml(html) {
  return stripSanctionedText(
    html
      .replace(/<section\b(?=[^>]*\bid\s*=\s*["'](?:unsafe-phrases|stop-conditions|non-claims)["'])[^>]*>[\s\S]*?<\/section>/gi, " ")
      .replace(/<span\b(?=[^>]*\bdata-blocked-phrase\b)[^>]*>[\s\S]*?<\/span>/gi, " ")
  );
}

function stripSanctionedText(text) {
  return String(text)
    .replace(/\b(?:no|not|does not|do not|cannot|must not|without)\s+[^.]*\b(?:impact proof|absence-of-impact proof|release approval|release safety|operational safety|runtime proof|runtime behavior|production traffic|endpoint performance|complete coverage|AI impact analysis|LLM analysis|replacement of human judgment|human judgment)[^.]*\./gi, " ")
    .replace(/\s+/g, " ");
}

function validateForbiddenPositiveClaims(text, errors, artifact) {
  for (const pattern of forbiddenPositiveClaimPatterns) {
    const match = pattern.exec(text);
    if (match) {
      errors.push(withEvidence(`Change-risk language guide contains forbidden public claim: ${match[0]}`, artifact));
      pattern.lastIndex = 0;
    }
  }
}

function validateHardPrivateMaterial(text, errors, artifact) {
  for (const pattern of hardPrivatePatterns) {
    const match = pattern.exec(text);
    if (match) {
      errors.push(withEvidence(`Change-risk language guide contains hard private or credential-like material: ${match[0]}`, artifact));
      pattern.lastIndex = 0;
    }
  }
}

function extractLanguageTables(html) {
  const tables = [];
  const tablePattern = /<table\b([^>]*)>([\s\S]*?)<\/table>/gi;
  let match;

  while ((match = tablePattern.exec(html))) {
    const name = getAttribute(match[1], "data-language-table");
    if (name) {
      tables.push({ name, html: match[0] });
    }
  }

  return tables;
}

function extractElementByTag(html, tag) {
  const pattern = new RegExp(`<${tag}\\b[^>]*>[\\s\\S]*?<\\/${tag}>`, "i");
  return pattern.exec(html)?.[0] ?? null;
}

function hasElementId(html, id) {
  const tagPattern = /<[a-z0-9-]+\b([^>]*)>/gi;
  let match;

  while ((match = tagPattern.exec(html))) {
    if (getAttribute(match[1], "id") === id) {
      return true;
    }
  }

  return false;
}

function hasHref(html, href) {
  return new RegExp(`\\bhref\\s*=\\s*["']${escapeRegExp(href)}["']`, "i").test(html);
}

function getAttribute(attributes, name) {
  const match = new RegExp(`(?:^|\\s)${escapeRegExp(name)}\\s*=\\s*["']([^"']*)["']`, "i").exec(attributes);
  return match ? decodeHtmlEntities(match[1]) : null;
}

function normalizeRouteHref(value) {
  if (typeof value !== "string" || value.length === 0) {
    return "";
  }

  const [path] = value.split("#");
  return path.endsWith("/") ? path : `${path}/`;
}

function countWords(text) {
  return text.split(/\s+/).filter(Boolean).length;
}

function withEvidence(message, artifact) {
  return `${message} [${artifact}]`;
}
