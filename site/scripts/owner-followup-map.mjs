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

export const ownerFollowupMapRoute = "/owners/follow-up/";
export const ownerFollowupMapRequiredLinks = [
  "/team-evidence-handoff/",
  "/incident-evidence-handoff/",
  "/reviewer-quickstart/",
  "/questions/",
  "/questions/objections/",
  "/packets/assembly/",
  "/manager-packet/",
  "/proof-paths/",
  "/limitations/",
  "/validation/"
];

const pageArtifact = "owners/follow-up/index.html";
const sitemapArtifact = "sitemap.xml";
const routesIndexArtifact = "routes-index.json";

const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "Owner categories are routing labels, not proof of real ownership.",
  "Missing evidence creates a labeled follow-up question, not blame and not a clean conclusion."
];

const requiredRows = [
  "code path question",
  "test coverage question",
  "runtime behavior question",
  "data/schema question",
  "config/deployment question",
  "release decision question",
  "architecture decision question",
  "evidence gap question"
];

const requiredFields = [
  "static evidence trigger",
  "what tracemap can show",
  "what tracemap cannot show",
  "next owner",
  "handoff wording",
  "proof path",
  "limitation",
  "stop condition"
];

const allowedOwnerCategories = new Set([
  "code owner",
  "reviewer",
  "test owner",
  "service/runtime owner",
  "database owner",
  "release reviewer",
  "architect",
  "manager",
  "relevant domain owner category"
]);

const metadataNonClaimTerms = [
  "real org ownership",
  "production ownership proof",
  "runtime behavior",
  "production traffic",
  "endpoint performance",
  "release approval",
  "release safety",
  "operational safety",
  "complete coverage",
  "replacement of human judgment",
  "AI impact analysis",
  "LLM analysis"
];

const placeholderTokens = [
  "[static evidence boundary]",
  "[question]",
  "[non-claim]",
  "[owner category]",
  "[proof path]",
  "[limitation]",
  "[stop condition]"
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

const rawDirectTargetPatterns = [
  /\bfacts\.ndjson\b/i,
  /\bindex\.sqlite\b/i,
  /\blogs\/analyzer\.log\b/i,
  /\banalyzer\.log\b/i,
  /\bscan-manifest\.json\b/i,
  /\breport\.md\b/i
];

const blamePatterns = [
  /\bwho broke it\b/i,
  /\bat fault\b/i,
  /\bculprit\b/i,
  /\bbad owner\b/i,
  /\bbad team\b/i,
  /\bguilty\b/i
];

const forbiddenClaimPatterns = [
  /\bTraceMap\b[^.]{0,90}\b(?:proves?|knows?|identifies?|detects?)\s+(?:real organizational ownership|real org ownership|production ownership|runtime behavior|production traffic|endpoint performance|release approval|release safety|operational safety|complete coverage)\b/i,
  /\bTraceMap\b[^.]{0,90}\b(?:approves?|blocks?|certifies?|guarantees?)\s+(?:a\s+)?release\b/i,
  /\b(?:AI-powered|LLM-powered|AI impact analysis engine|LLM impact analysis engine)\b/i,
  /\breplaces?\s+(?:human judgment|owners|reviewers|managers|architects|tests|telemetry|logs|traces|source review|code review|release process)\b/i,
  /\b(?:safe to release|release-safe|operationally safe|production-proven|ownership-proven)\b/i
];

export async function validateOwnerFollowupMapDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors
}) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "owners", "follow-up", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence(`Owner follow-up map is missing required public route: ${ownerFollowupMapRoute}`, pageArtifact));
    errors.push(...localErrors);
    return;
  }

  await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  const routeContext = await readRouteContext({ dist, errors: localErrors });
  await validateOwnerFollowupPage({ pagePath, routeContext, errors: localErrors });

  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, sitemapArtifact);
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${ownerFollowupMapRoute}`)) {
    errors.push(withEvidence(`Owner follow-up map sitemap is missing required route: ${baseUrl}${ownerFollowupMapRoute}`, sitemapArtifact));
  }
}

async function readRouteContext({ dist, errors }) {
  const routesIndexPath = resolve(dist, routesIndexArtifact);
  const routes = new Set();
  let routeEntry = null;

  if (!(await fileExists(routesIndexPath))) {
    return { routeEntry, routes };
  }

  let parsed;
  try {
    parsed = JSON.parse(await readFile(routesIndexPath, "utf8"));
  } catch (error) {
    errors.push(withEvidence(`Owner follow-up map could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return { routeEntry, routes };
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Owner follow-up map routes-index.json is invalid: expected entries array", routesIndexArtifact));
    return { routeEntry, routes };
  }

  for (const entry of parsed.entries) {
    if (typeof entry?.path === "string") {
      routes.add(normalizeRoute(entry.path));
    }
  }

  routeEntry = parsed.entries.find((entry) => normalizeRoute(entry?.path ?? "") === ownerFollowupMapRoute) ?? null;
  validateRouteEntry(routeEntry, errors);

  return { routeEntry, routes };
}

function validateRouteEntry(routeEntry, errors) {
  if (!routeEntry) {
    errors.push(withEvidence(`Owner follow-up map routes-index.json is missing required route: ${ownerFollowupMapRoute}`, routesIndexArtifact));
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
      errors.push(withEvidence(`Owner follow-up map routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
    }
  }

  if (!Array.isArray(routeEntry.limitations) || routeEntry.limitations.length === 0) {
    errors.push(withEvidence("Owner follow-up map routes-index.json must include limitations metadata.", routesIndexArtifact));
  }

  if (!Array.isArray(routeEntry.nonClaims) || routeEntry.nonClaims.length === 0) {
    errors.push(withEvidence("Owner follow-up map routes-index.json must include nonClaims metadata.", routesIndexArtifact));
    return;
  }

  const nonClaimsText = normalizeScanText(routeEntry.nonClaims.join(" "));
  for (const term of metadataNonClaimTerms) {
    if (!nonClaimsText.includes(term.toLowerCase())) {
      errors.push(withEvidence(`Owner follow-up map routes-index.json nonClaims are missing required term: ${term}`, routesIndexArtifact));
    }
  }

  validateForbiddenText({
    errors,
    text: collectRouteMetadataText(routeEntry),
    artifact: routesIndexArtifact
  });
}

async function validateOwnerFollowupPage({ pagePath, routeContext, errors }) {
  const html = await readFile(pagePath, "utf8");
  const mainHtml = extractMainHtml(html);
  const pageText = normalizeRenderedText(mainHtml);
  const decodedText = decodeHtmlEntities(html);
  const metadataText = collectMetadataText(html);
  const attributeText = collectDecodedAttributeText(html);
  const scanText = `${pageText} ${decodedText} ${metadataText} ${attributeText} ${collectRouteMetadataText(routeContext.routeEntry)}`;
  const wordCount = countWords(pageText);

  for (const phrase of requiredText) {
    if (!pageText.includes(phrase)) {
      errors.push(withEvidence(`Owner follow-up map page is missing required text: ${phrase}`, pageArtifact));
    }
  }

  validatePageMetadata(html, errors);
  validateRows(html, errors);
  validateRequiredLinks({ html, routeContext, errors });
  validateAccessibilityShape({ html, errors });

  if (wordCount < 600 || wordCount > 1700) {
    errors.push(withEvidence(`Owner follow-up map word count must be between 600 and 1700 words, got ${wordCount}`, pageArtifact));
  }

  for (const token of placeholderTokens) {
    if (scanText.includes(token)) {
      errors.push(withEvidence(`Owner follow-up map contains unsubstituted handoff placeholder token: ${token}`, pageArtifact));
    }
  }

  validateForbiddenText({ errors, text: scanText, artifact: pageArtifact });
}

function validateRows(html, errors) {
  const rows = extractOwnerRows(html);
  const seenRows = new Set();

  for (const row of rows) {
    const rowName = normalizeScanText(row.name);
    seenRows.add(rowName);
    const fields = extractOwnerFields(row.body);

    for (const field of requiredFields) {
      if (!fields.has(field)) {
        errors.push(withEvidence(`Owner follow-up map ${row.name} is missing required field: ${field}`, pageArtifact));
        continue;
      }

      const text = normalizeRenderedText(fields.get(field));
      if (text.trim() === "") {
        errors.push(withEvidence(`Owner follow-up map ${row.name} has empty field: ${field}`, pageArtifact));
      }
    }

    if (fields.has("next owner")) {
      validateOwnerCategories({ rowName: row.name, text: normalizeRenderedText(fields.get("next owner")), errors });
    }

    if (fields.has("handoff wording")) {
      const handoff = normalizeRenderedText(fields.get("handoff wording"));
      if (!/\bTraceMap can show\b/.test(handoff) || !/\bIt cannot\b/.test(handoff) || !/\bPlease route\b/.test(handoff) || !/\bStop\b/.test(handoff)) {
        errors.push(withEvidence(`Owner follow-up map ${row.name} handoff wording must include can-show, cannot-show, route, and stop wording.`, pageArtifact));
      }
    }

    if (fields.has("proof path") && !/<a\b[^>]*\bhref\s*=/i.test(fields.get("proof path"))) {
      errors.push(withEvidence(`Owner follow-up map ${row.name} proof path field must include at least one link.`, pageArtifact));
    }
  }

  for (const row of requiredRows) {
    if (!seenRows.has(row)) {
      errors.push(withEvidence(`Owner follow-up map is missing required row: ${row}`, pageArtifact));
    }
  }
}

function validateOwnerCategories({ rowName, text, errors }) {
  const categories = text
    .split(/\s*,\s*|\s+or\s+/i)
    .map((value) => value.trim().toLowerCase().replace(/^or\s+/, ""))
    .filter(Boolean);

  for (const category of categories) {
    if (!allowedOwnerCategories.has(category)) {
      errors.push(withEvidence(`Owner follow-up map ${rowName} uses unsupported next owner category: ${category}`, pageArtifact));
    }
  }
}

function validateRequiredLinks({ html, routeContext, errors }) {
  for (const link of ownerFollowupMapRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Owner follow-up map page is missing required link: ${link}`, pageArtifact));
      continue;
    }

    if (routeContext.routes.size > 0 && !routeContext.routes.has(normalizeRoute(link))) {
      errors.push(withEvidence(`Owner follow-up map required link is not present in discovery route index: ${link}`, routesIndexArtifact));
    }
  }
}

function validatePageMetadata(html, errors) {
  const metaTags = findTagAttributes(html, "meta");
  const linkTags = findTagAttributes(html, "link");
  const checks = [
    [/<title>[^<]+<\/title>/i.test(html), "title"],
    [hasMeta(metaTags, { name: "description", content: "non-empty" }), "description"],
    [
      linkTags.some(
        (attributes) =>
          hasRel(attributes, "canonical") &&
          getAttribute(attributes, "href") === "https://tracemap.tools/owners/follow-up/"
      ),
      "canonical URL"
    ],
    [hasMeta(metaTags, { property: "og:type", content: "article" }), "Open Graph type"],
    [hasMeta(metaTags, { property: "og:title", content: "non-empty" }), "Open Graph title"],
    [hasMeta(metaTags, { property: "og:description", content: "non-empty" }), "Open Graph description"],
    [
      hasMeta(metaTags, {
        property: "og:url",
        content: "https://tracemap.tools/owners/follow-up/"
      }),
      "Open Graph URL"
    ]
  ];

  for (const [ok, label] of checks) {
    if (!ok) {
      errors.push(withEvidence(`Owner follow-up map page is missing required metadata: ${label}`, pageArtifact));
    }
  }
}

function validateAccessibilityShape({ html, errors }) {
  const h1Count = [...html.matchAll(/<h1\b/gi)].length;
  if (h1Count !== 1) {
    errors.push(withEvidence(`Owner follow-up map page must have exactly one h1, got ${h1Count}`, pageArtifact));
  }

  if (!/<div\b[^>]*\bdata-owner-followup-map\b/i.test(html)) {
    errors.push(withEvidence("Owner follow-up map is missing the accessible owner map container.", pageArtifact));
  }

  if (!/<dl\b[^>]*\bclass\s*=\s*["'][^"']*\bowner-field-list\b/i.test(html)) {
    errors.push(withEvidence("Owner follow-up map rows must use description lists for field labels and values.", pageArtifact));
  }
}

function validateForbiddenText({ errors, text, artifact }) {
  for (const pattern of hardPrivatePatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Owner follow-up map contains forbidden private or credential-like material: ${pattern.source}`, artifact));
    }
  }

  for (const pattern of rawDirectTargetPatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Owner follow-up map contains forbidden raw artifact target: ${pattern.source}`, artifact));
    }
  }

  for (const pattern of blamePatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Owner follow-up map contains blame-oriented wording: ${pattern.source}`, artifact));
    }
  }

  for (const pattern of forbiddenClaimPatterns) {
    for (const match of matchAllPattern(text, pattern)) {
      if (!isNegatedClaimContext(text, match.index ?? 0, match[0].length)) {
        errors.push(withEvidence(`Owner follow-up map contains forbidden claim wording: ${match[0]}`, artifact));
      }
    }
  }
}

function matchAllPattern(text, pattern) {
  const flags = pattern.flags.includes("g") ? pattern.flags : `${pattern.flags}g`;
  return text.matchAll(new RegExp(pattern.source, flags));
}

function isNegatedClaimContext(text, index, length) {
  const start = Math.max(0, index - 80);
  const end = Math.min(text.length, index + length + 20);
  return /\b(?:no|not|does not|do not|cannot|can't|without|must not|it cannot)\b/i.test(text.slice(start, end));
}

function extractOwnerRows(html) {
  const rows = [];
  const pattern = /<article\b([^>]*)>([\s\S]*?)<\/article>/gi;

  for (const match of html.matchAll(pattern)) {
    const name = getAttribute(match[1], "data-owner-row");
    if (name) {
      rows.push({ name, body: match[2] });
    }
  }

  return rows;
}

function extractOwnerFields(rowHtml) {
  const fields = new Map();
  const pattern = /<dd\b([^>]*)>([\s\S]*?)<\/dd>/gi;

  for (const match of rowHtml.matchAll(pattern)) {
    const field = getAttribute(match[1], "data-owner-field");
    if (field) {
      fields.set(field.toLowerCase(), match[2]);
    }
  }

  return fields;
}

function hasHref(html, href) {
  return new RegExp(`<a\\b[^>]*\\bhref\\s*=\\s*["']${escapeRegExp(href)}["']`, "i").test(html);
}

function extractMainHtml(html) {
  return html.match(/<main\b[^>]*>([\s\S]*?)<\/main>/i)?.[1] ?? "";
}

function collectMetadataText(html) {
  const tags = [...html.matchAll(/<meta\b([^>]*)>/gi)];
  return tags.map((match) => getAttribute(match[1], "content") ?? "").join(" ");
}

function collectDecodedAttributeText(html) {
  return [...html.matchAll(/\s(?:href|content|title|aria-label|alt)\s*=\s*["']([^"']*)["']/gi)]
    .map((match) => decodeHtmlEntities(match[1]))
    .join(" ");
}

function collectRouteMetadataText(routeEntry) {
  if (!routeEntry || typeof routeEntry !== "object") {
    return "";
  }

  return [
    routeEntry.title,
    routeEntry.summary,
    ...(Array.isArray(routeEntry.limitations) ? routeEntry.limitations : []),
    ...(Array.isArray(routeEntry.nonClaims) ? routeEntry.nonClaims : [])
  ]
    .filter((value) => typeof value === "string")
    .join(" ");
}

function findTagAttributes(html, tagName) {
  return [...html.matchAll(new RegExp(`<${tagName}\\b([^>]*)>`, "gi"))].map((match) => match[1]);
}

function hasMeta(metaTags, { name, property, content }) {
  return metaTags.some((attributes) => {
    if (name && getAttribute(attributes, "name") !== name) {
      return false;
    }

    if (property && getAttribute(attributes, "property") !== property) {
      return false;
    }

    const value = getAttribute(attributes, "content");
    return content === "non-empty" ? typeof value === "string" && value.trim() !== "" : value === content;
  });
}

function hasRel(attributes, expectedRel) {
  return (getAttribute(attributes, "rel") ?? "")
    .split(/\s+/)
    .some((rel) => rel.toLowerCase() === expectedRel.toLowerCase());
}

function getAttribute(attributes, name) {
  const match = attributes.match(new RegExp(`\\b${escapeRegExp(name)}\\s*=\\s*["']([^"']*)["']`, "i"));
  return match ? decodeHtmlEntities(match[1]) : null;
}

function normalizeRoute(value) {
  if (typeof value !== "string") {
    return "";
  }

  if (/^https?:\/\//i.test(value)) {
    try {
      return normalizeRoute(new URL(value).pathname);
    } catch {
      return value;
    }
  }

  const path = value.startsWith("/") ? value : `/${value}`;
  return path.endsWith("/") ? path : `${path}/`;
}

function normalizeScanText(value) {
  return normalizeRenderedText(String(value)).toLowerCase();
}

function countWords(value) {
  return (value.match(/\b[\w'-]+\b/g) ?? []).length;
}

function withEvidence(message, artifact) {
  return `${message} Evidence: ${artifact}.`;
}
