import { readdir, readFile } from "node:fs/promises";
import { relative, resolve, sep } from "node:path";

import {
  decodeHtmlEntities,
  fileExists,
  normalizeBaseUrl,
  normalizeRenderedText,
  readSitemapLocSet
} from "./validate-utils.mjs";

export const siteClaimGuardrailsRoute = "/site-claim-guardrails/";
export const siteClaimGuardrailsRequiredLinks = [
  "/review-claim-checklist/",
  "/proof-source-catalog/",
  "/roadmap/",
  "/limitations/",
  "/questions/objections/",
  "/language/change-risk/"
];

const pageArtifact = "site-claim-guardrails/index.html";
const sitemapArtifact = "sitemap.xml";
const routesIndexArtifact = "routes-index.json";
const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "No claim is upgraded by confidence, seniority, repetition, urgency, roadmap intent, pressure, or appealing phrasing",
  "Private-only evidence can support internal follow-up"
];
const requiredSections = [
  "public-claim-levels",
  "proof-path-requirements",
  "allowed-evidence-references",
  "forbidden-raw-material",
  "non-claim-patterns",
  "downgrade-and-hidden-rules",
  "validation-expectations",
  "review-handoff"
];
const requiredRows = new Set([
  "shipped",
  "demo",
  "concept",
  "hidden",
  "raw artifact reference",
  "dev-only feature",
  "reduced coverage",
  "runtime/release wording",
  "AI/LLM wording",
  "private-only support"
]);
const requiredRowFields = [
  "condition",
  "allowed public wording/action",
  "required proof path",
  "downgrade/hidden trigger",
  "forbidden implication",
  "review handoff"
];
const requiredHandoffStates = [
  "repeat with proof",
  "downgrade before repeating",
  "owner follow-up needed",
  "do not repeat",
  "internal only",
  "hidden"
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
  /\bsk-[A-Za-z0-9_-]{12,}\b/i
];
const forbiddenProofPathTargets = [
  "facts.ndjson",
  "index.sqlite",
  "logs/analyzer.log",
  "analyzer.log",
  "scan-manifest.json",
  "report.md"
];
const forbiddenPositiveClaimPatterns = [
  /\bTraceMap\s+(?:proves?|proved|can prove|has proven)\s+(?:runtime behavior|production traffic|endpoint performance|outage cause|release approval|release safety|operational safety|complete coverage|AI\/LLM impact analysis|AI impact analysis|LLM impact analysis)\b/i,
  /\b(?:runtime-proven|release-safe|operationally safe|production-proven|traffic-proven|approved for release|automated release approval|AI-powered|LLM-powered|embedding-backed|prompt-classified)\b/i,
  /\breplaces?\s+(?:human review|human judgment|tests|code review|release process)\b/i
];
const blameLanguagePattern = /\b(?:blame|fault|negligent|careless|irresponsible|failed team|bad maintainer)\b/i;

export async function validateSiteClaimGuardrailsDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors
}) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "site-claim-guardrails", "index.html");

  await validateGeneratedIndexPages({ dist, errors: localErrors });

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence(`Site claim guardrails page is missing required public route: ${siteClaimGuardrailsRoute}`, pageArtifact));
    errors.push(...localErrors);
    return;
  }

  await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  const routeContext = await readRouteContext({ dist, errors: localErrors });
  await validatePage({ pagePath, routeContext, errors: localErrors });
  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, sitemapArtifact);
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${siteClaimGuardrailsRoute}`)) {
    errors.push(withEvidence(`Site claim guardrails sitemap is missing required route: ${baseUrl}${siteClaimGuardrailsRoute}`, sitemapArtifact));
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
    errors.push(withEvidence(`Site claim guardrails could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return { routeEntry, routePaths };
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Site claim guardrails routes-index.json is invalid: expected entries array", routesIndexArtifact));
    return { routeEntry, routePaths };
  }

  for (const entry of parsed.entries) {
    if (typeof entry?.path === "string") {
      routePaths.add(normalizeRouteHref(entry.path));
    }
  }

  routeEntry = parsed.entries.find((entry) => normalizeRouteHref(entry?.path ?? "") === siteClaimGuardrailsRoute) ?? null;
  validateRouteEntry(routeEntry, errors);

  return { routeEntry, routePaths };
}

function validateRouteEntry(routeEntry, errors) {
  if (!routeEntry) {
    errors.push(withEvidence(`Site claim guardrails routes-index.json is missing required route: ${siteClaimGuardrailsRoute}`, routesIndexArtifact));
    return;
  }

  const expectedFields = {
    publicClaimLevel: "concept",
    sourceType: "site-page",
    hintCategory: "evidence",
    preferredProofPath: "/review-claim-checklist/"
  };

  for (const [field, expected] of Object.entries(expectedFields)) {
    if (routeEntry[field] !== expected) {
      errors.push(withEvidence(`Site claim guardrails routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
    }
  }

  if (!Array.isArray(routeEntry.limitations) || routeEntry.limitations.length < 2) {
    errors.push(withEvidence("Site claim guardrails routes-index.json must include at least two limitations.", routesIndexArtifact));
  }

  if (!Array.isArray(routeEntry.nonClaims) || routeEntry.nonClaims.length < 2) {
    errors.push(withEvidence("Site claim guardrails routes-index.json must include nonClaims metadata.", routesIndexArtifact));
  }

  const routeText = JSON.stringify(routeEntry);
  validatePublicSafetyText(routeText, errors, routesIndexArtifact);
  validateForbiddenPositiveClaims(routeText, errors, routesIndexArtifact);
}

async function validatePage({ pagePath, routeContext, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const pageText = normalizeRenderedText(html);
  const lowerPageText = pageText.toLowerCase();
  const wordCount = countWords(pageText);

  for (const phrase of requiredText) {
    if (!lowerPageText.includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Site claim guardrails page is missing required text: ${phrase}`, pageArtifact));
    }
  }

  validateMetadata({ html, errors });
  validateSections(html, errors);
  validateRows(html, errors);
  validateHandoffStates(pageText, errors);
  validateLinks({ html, routeContext, errors });
  validatePublicSafety({ decodedHtml, html, pageText, errors });
  validatePrimaryNavigation(html, errors);

  if (wordCount < 700 || wordCount > 2200) {
    errors.push(withEvidence(`Site claim guardrails page word count must be between 700 and 2200 words, got ${wordCount}`, pageArtifact));
  }
}

function validateMetadata({ html, errors }) {
  const metadataChecks = [
    [/Site Claim Guardrails \| TraceMap/i, "title"],
    [/content="Concept-level TraceMap site claim guardrails/i, "description"],
    [/rel="canonical" href="https:\/\/tracemap\.tools\/site-claim-guardrails\/"/i, "canonical URL"],
    [/property="og:type" content="article"/i, "Open Graph type"],
    [/property="og:title" content="TraceMap Site Claim Guardrails"/i, "Open Graph title"],
    [/property="og:url" content="https:\/\/tracemap\.tools\/site-claim-guardrails\/"/i, "Open Graph URL"]
  ];

  for (const [pattern, label] of metadataChecks) {
    if (!pattern.test(html)) {
      errors.push(withEvidence(`Site claim guardrails page is missing concept-level metadata: ${label}`, pageArtifact));
    }
  }
}

function validateSections(html, errors) {
  for (const section of requiredSections) {
    if (!hasId(html, section)) {
      errors.push(withEvidence(`Site claim guardrails page is missing required section: ${section}`, pageArtifact));
    }
  }
}

function validateRows(html, errors) {
  const rows = extractRows(html, "data-claim-guardrail-row");
  const seen = new Set();

  if (rows.length === 0) {
    errors.push(withEvidence("Site claim guardrails page has no data-claim-guardrail-row entries.", pageArtifact));
    return;
  }

  for (const row of rows) {
    const id = getAttribute(row.attributes, "data-claim-guardrail-row");
    const fields = extractRowFields(row.html);

    if (!requiredRows.has(id)) {
      errors.push(withEvidence(`Site claim guardrails row has unexpected id: ${String(id)}`, pageArtifact));
    } else {
      seen.add(id);
    }

    for (const field of requiredRowFields) {
      if (!fields[field] || fields[field].trim() === "") {
        errors.push(withEvidence(`Site claim guardrails row ${id ?? "(missing id)"} is missing required field: ${field}`, pageArtifact));
      }
    }

    if (fields["review handoff"] && !requiredHandoffStates.includes(normalizeRenderedText(fields["review handoff"]))) {
      errors.push(withEvidence(`Site claim guardrails row ${id ?? "(missing id)"} has invalid review handoff: ${normalizeRenderedText(fields["review handoff"])}`, pageArtifact));
    }
  }

  for (const row of requiredRows) {
    if (!seen.has(row)) {
      errors.push(withEvidence(`Site claim guardrails page is missing required row: ${row}`, pageArtifact));
    }
  }
}

function validateHandoffStates(pageText, errors) {
  const lowerPageText = pageText.toLowerCase();
  for (const state of requiredHandoffStates) {
    if (!lowerPageText.includes(state)) {
      errors.push(withEvidence(`Site claim guardrails page is missing review handoff state: ${state}`, pageArtifact));
    }
  }
}

function validateLinks({ html, routeContext, errors }) {
  for (const link of siteClaimGuardrailsRequiredLinks) {
    if (!routeContext.routePaths.has(normalizeRouteHref(link))) {
      errors.push(withEvidence(`Site claim guardrails adjacent route is absent from discovery output: ${link}`, routesIndexArtifact));
      continue;
    }

    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Site claim guardrails page is missing adjacent route link: ${link}`, pageArtifact));
    }
  }
}

function validatePublicSafety({ decodedHtml, html, pageText, errors }) {
  const searchText = hardPrivateSearchText({ decodedHtml, html, pageText });
  validateHardPrivateText(searchText, errors, pageArtifact);
  if (blameLanguagePattern.test(searchText)) {
    errors.push(withEvidence("Site claim guardrails page contains blame language.", pageArtifact));
  }

  for (const href of extractHrefs(html)) {
    for (const target of forbiddenProofPathTargets) {
      if (href.toLowerCase().includes(target.toLowerCase())) {
        errors.push(withEvidence(`Site claim guardrails page links to forbidden raw proof artifact: ${href}`, pageArtifact));
      }
    }
  }

  const publicClaimText = normalizeRenderedText(stripAllowedContextHtml(html));
  validateForbiddenPositiveClaims(publicClaimText, errors, pageArtifact);

  if (!/\bdata-claim-guardrails-zone\s*=\s*["'](?:boundary|non-claim|rejected)["']/.test(html)) {
    errors.push(withEvidence("Site claim guardrails page is missing machine-marked boundary or non-claim zones.", pageArtifact));
  }
}

async function validateGeneratedIndexPages({ dist, errors }) {
  const indexPaths = await findGeneratedIndexPages(dist, errors);

  for (const indexPath of indexPaths) {
    const artifact = relative(dist, indexPath).split(sep).join("/") || "index.html";
    if (artifact === pageArtifact) {
      continue;
    }

    let html;
    try {
      html = await readFile(indexPath, "utf8");
    } catch (error) {
      errors.push(withEvidence(`Site claim guardrails could not read generated index page: ${safeErrorCategory(error)}`, artifact));
      continue;
    }

    const decodedHtml = decodeHtmlEntities(html);
    const pageText = normalizeRenderedText(html);
    const searchText = hardPrivateSearchText({ decodedHtml, html, pageText });
    validateHardPrivateText(searchText, errors, artifact);
  }
}

async function findGeneratedIndexPages(root, errors) {
  const indexPaths = [];

  async function visit(directory) {
    let entries;
    try {
      entries = await readdir(directory, { withFileTypes: true });
    } catch (error) {
      const subtree = relative(root, directory).split(sep).join("/");
      const artifact = subtree ? `${subtree}/**/index.html` : "dist/**/index.html";
      errors.push(withEvidence(`Site claim guardrails could not enumerate generated index subtree: ${safeErrorCategory(error)}`, artifact));
      return;
    }

    for (const entry of entries.sort((left, right) => left.name.localeCompare(right.name, "en"))) {
      const entryPath = resolve(directory, entry.name);
      if (entry.isDirectory()) {
        await visit(entryPath);
      } else if (entry.isFile() && entry.name === "index.html") {
        indexPaths.push(entryPath);
      }
    }
  }

  await visit(root);
  return indexPaths;
}

function hardPrivateSearchText({ decodedHtml, html, pageText }) {
  return `${decodedHtml}\n${pageText}\n${collapseTagSplitText(decodedHtml)}\n${collapseTagSplitTextTight(html)}`;
}

function collapseTagSplitTextTight(html) {
  return decodeHtmlEntities(String(html).replace(/<[^>]+>/g, "")).replace(/\s+/g, "");
}

function safeErrorCategory(error) {
  if (typeof error?.code === "string" && /^[A-Z0-9_]+$/.test(error.code)) {
    return error.code;
  }

  return "read-failed";
}

function validateHardPrivateText(text, errors, artifact) {
  for (const pattern of hardPrivatePatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Site claim guardrails page contains hard private or credential-like material: ${pattern}`, artifact));
    }
  }
}

function validatePublicSafetyText(text, errors, artifact) {
  validateHardPrivateText(text, errors, artifact);
  if (blameLanguagePattern.test(text)) {
    errors.push(withEvidence("Site claim guardrails page contains blame language.", artifact));
  }
}

function validateForbiddenPositiveClaims(text, errors, artifact) {
  for (const pattern of forbiddenPositiveClaimPatterns) {
    const match = text.match(pattern);
    if (match) {
      errors.push(withEvidence(`Site claim guardrails page contains forbidden public claim outside marked boundary copy: ${match[0]}`, artifact));
    }
  }
}

function validatePrimaryNavigation(html, errors) {
  const navMatch = html.match(/<nav\b[^>]*class=["'][^"']*\btop-nav\b[^"']*["'][^>]*>([\s\S]*?)<\/nav>/i);
  if (!navMatch) {
    errors.push(withEvidence("Site claim guardrails page is missing primary navigation.", pageArtifact));
    return;
  }

  if (hasHref(navMatch[1], siteClaimGuardrailsRoute)) {
    errors.push(withEvidence("Site claim guardrails page must not be added to primary navigation without an implementation-state note.", pageArtifact));
  }
}

function stripAllowedContextHtml(html) {
  return stripMarkedZones(html, new Set(["non-claim", "rejected"]));
}

function stripMarkedZones(html, allowedZones) {
  const tagPattern = /<\/?([a-z][a-z0-9:-]*)\b[^>]*>/gi;
  let output = "";
  let cursor = 0;
  let skipDepth = 0;

  for (const match of html.matchAll(tagPattern)) {
    const token = match[0];
    const isClosing = /^<\//.test(token);
    const isSelfClosing = /\/>$/.test(token);

    if (skipDepth === 0) {
      output += html.slice(cursor, match.index);
    }

    if (isClosing) {
      if (skipDepth > 0) {
        skipDepth -= 1;
        if (skipDepth === 0) {
          cursor = match.index + token.length;
        }
      } else {
        output += token;
        cursor = match.index + token.length;
      }
      continue;
    }

    if (skipDepth > 0) {
      if (!isSelfClosing) {
        skipDepth += 1;
      }
      continue;
    }

    const zone = getAttribute(token, "data-claim-guardrails-zone");
    if (zone && allowedZones.has(zone)) {
      if (!isSelfClosing) {
        skipDepth = 1;
      }
      cursor = match.index + token.length;
      continue;
    }

    output += token;
    cursor = match.index + token.length;
  }

  if (skipDepth === 0) {
    output += html.slice(cursor);
  }

  return output;
}

function extractRows(html, attributeName) {
  const pattern = /<tr\b([^>]*)>([\s\S]*?)<\/tr>/gi;
  return [...html.matchAll(pattern)]
    .filter((match) => getAttribute(match[1], attributeName) !== null)
    .map((match) => ({ attributes: match[1], html: match[2] }));
}

function extractRowFields(rowHtml) {
  const fields = {};
  const pattern = /<(?:td|th)\b([^>]*)>([\s\S]*?)<\/(?:td|th)>/gi;
  for (const match of rowHtml.matchAll(pattern)) {
    const field = getAttribute(match[1], "data-field");
    if (field) {
      fields[field] = normalizeRenderedText(match[2]);
    }
  }
  return fields;
}

function hasId(html, id) {
  return new RegExp(`<[^>]+\\sid\\s*=\\s*["']${escapeRegExp(id)}["']`, "i").test(html);
}

function hasHref(html, href) {
  return new RegExp(`\\bhref\\s*=\\s*["']${escapeRegExp(href)}["']`, "i").test(html);
}

function extractHrefs(html) {
  return [...html.matchAll(/\bhref\s*=\s*["']([^"']+)["']/gi)].map((match) => decodeHtmlEntities(match[1]));
}

function getAttribute(attributes, name) {
  const match = attributes.match(new RegExp(`\\b${escapeRegExp(name)}\\s*=\\s*["']([^"']*)["']`, "i"));
  return match ? decodeHtmlEntities(match[1]) : null;
}

function collapseTagSplitText(html) {
  return normalizeRenderedText(String(html).replace(/<[^>]+>/g, ""));
}

function normalizeRouteHref(value) {
  if (typeof value !== "string") {
    return "";
  }

  const [pathname] = value.split("#");
  return pathname.endsWith("/") ? pathname : `${pathname}/`;
}

function countWords(text) {
  return text.split(/\s+/).filter(Boolean).length;
}

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function withEvidence(message, artifact) {
  return `${message} (${artifact})`;
}
