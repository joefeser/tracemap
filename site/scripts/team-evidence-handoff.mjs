import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import {
  decodeHtmlEntities,
  escapeRegExp,
  fileExists,
  normalizeRenderedText,
  readSitemapLocSet
} from "./validate-utils.mjs";

export const teamEvidenceHandoffRoute = "/team-evidence-handoff/";
export const teamEvidenceHandoffRequiredLinks = [
  "/proof-paths/",
  "/packets/",
  "/manager-packet/",
  "/review-room/",
  "/manager-faq/",
  "/proof-source-catalog/",
  "/limitations/",
  "/validation/"
];

const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "teammate",
  "reviewer",
  "manager",
  "agent",
  "summary",
  "proof path",
  "rule ID/rule family",
  "evidence tier",
  "coverage label",
  "limitations",
  "non-claims",
  "local-only artifacts",
  "next owner/action",
  "A handoff is complete only when the summary, proof path, rule ID/rule family, evidence tier, coverage label, limitations, non-claims, local-only artifacts, and next owner/action travel together.",
  "The summary is a bounded statement of what static evidence supports",
  "not private scanner output on the public site",
  "Private repository evidence needs private review before any public-safe summary is written"
];

const receiverPatterns = [
  "Teammate",
  "Reviewer",
  "Manager",
  "Agent"
];

const neighboringRoutes = [
  "/packets/",
  "/manager-packet/",
  "/review-room/",
  "/manager-faq/",
  "/proof-source-catalog/"
];

const metadataNonClaimTerms = [
  "runtime behavior",
  "production traffic",
  "endpoint performance",
  "outage cause",
  "release safety",
  "operational safety",
  "AI impact analysis",
  "LLM analysis",
  "complete product coverage"
];

const forbiddenPositioningPatterns = [
  /\bAI-powered\b/i,
  /\bAI impact analysis\b/i,
  /\bLLM-powered\b/i,
  /\bLLM analysis\b/i,
  /\bmachine learning impact analysis\b/i,
  /\bartificial intelligence impact analysis\b/i,
  /\bintelligent analysis\b/i,
  /\bintelligent impact analysis\b/i,
  /\bsmart impact\b/i
];

const unsupportedOverclaimPatterns = [
  /\bimpacted\b/i,
  /(?<!public-)\bsafe\b/i,
  /\bunsafe\b/i,
  /\bapproved\b/i,
  /\bblocked\b/i,
  /\broot cause\b/i,
  /\bvalidated for release\b/i,
  /\bproduction proven\b/i,
  /\boperational assurance\b/i,
  /\bproduction observability tool\b/i
];

const privateTextPatterns = [
  /\/Users\//i,
  /\bfile:\/\//i,
  /\blocalhost\b/i,
  /\bconnection string\b/i,
  /\bpassword\s*=/i,
  /\bapi[_-]?key\b/i,
  /\bsecret\s*=/i,
  /\bsk-[A-Za-z0-9_-]{12,}\b/i,
  /\braw facts?\b/i,
  /\braw SQLite\b/i,
  /\banalyzer logs?\b/i,
  /\braw source snippets?\b/i,
  /\braw SQL\b/i,
  /\bconfig values?\b/i,
  /\braw remotes?\b/i,
  /\bgenerated scan directories\b/i,
  /\bprivate sample names?\b/i,
  /\bcredential-like values?\b/i,
  /\bprivate URLs?\b/i
];

export async function validateTeamEvidenceHandoffDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors
}) {
  const localErrors = [];
  const cleanBaseUrl = normalizeTeamEvidenceHandoffBaseUrl(baseUrl, localErrors);
  const pagePath = resolve(dist, "team-evidence-handoff", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push("Team evidence handoff page is missing required public route: /team-evidence-handoff/");
    errors.push(...localErrors);
    return;
  }

  if (cleanBaseUrl) {
    await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  }
  const routeContext = await readRouteContext({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  await validateTeamEvidenceHandoffPage({ pagePath, routeContext, errors: localErrors });

  errors.push(...localErrors);
}

function normalizeTeamEvidenceHandoffBaseUrl(value, errors) {
  let url;

  try {
    url = new URL(value);
  } catch {
    errors.push(`Team evidence handoff baseUrl must be a valid absolute URL: ${String(value)}`);
    return null;
  }

  if (url.protocol !== "https:" && url.protocol !== "http:") {
    errors.push(`Team evidence handoff baseUrl must use http or https: ${String(value)}`);
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
  if (!sitemapUrls.has(`${baseUrl}${teamEvidenceHandoffRoute}`)) {
    errors.push(`Team evidence handoff sitemap is missing required route: ${baseUrl}${teamEvidenceHandoffRoute}`);
  }
}

async function readRouteContext({ baseUrl, dist, errors }) {
  const routesIndexPath = resolve(dist, "routes-index.json");
  const sitemapPath = resolve(dist, "sitemap.xml");
  const routes = new Set();
  let routeEntry = null;

  if (await fileExists(routesIndexPath)) {
    try {
      const parsed = JSON.parse(await readFile(routesIndexPath, "utf8"));
      if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
        errors.push("Team evidence handoff routes-index.json is invalid: expected entries array");
      } else {
        for (const entry of parsed.entries) {
          if (typeof entry?.path === "string") {
            routes.add(entry.path);
          }
        }
        routeEntry = parsed.entries.find((entry) => entry?.path === teamEvidenceHandoffRoute) ?? null;
      }
    } catch (error) {
      errors.push(`Team evidence handoff could not parse routes-index.json: ${error.message}`);
    }
  }

  const sitemapRoutes = new Set();
  if (baseUrl && (await fileExists(sitemapPath))) {
    for (const loc of await readSitemapLocSet(sitemapPath)) {
      if (loc.startsWith(baseUrl)) {
        sitemapRoutes.add(new URL(loc).pathname);
      }
    }
  }

  validateRouteEntry(routeEntry, errors);

  return { routeEntry, routes, sitemapRoutes };
}

function validateRouteEntry(routeEntry, errors) {
  if (!routeEntry) {
    errors.push(`Team evidence handoff routes-index.json is missing required route: ${teamEvidenceHandoffRoute}`);
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
      errors.push(`Team evidence handoff routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`);
    }
  }

  if (!Array.isArray(routeEntry.limitations) || routeEntry.limitations.length === 0) {
    errors.push("Team evidence handoff routes-index.json must include limitations metadata.");
  }

  if (!Array.isArray(routeEntry.nonClaims) || routeEntry.nonClaims.length === 0) {
    errors.push("Team evidence handoff routes-index.json must include nonClaims metadata.");
    return;
  }

  const nonClaimsText = routeEntry.nonClaims.join(" ");
  for (const term of metadataNonClaimTerms) {
    if (!nonClaimsText.includes(term)) {
      errors.push(`Team evidence handoff routes-index.json nonClaims are missing required term: ${term}`);
    }
  }
}

async function validateTeamEvidenceHandoffPage({ pagePath, routeContext, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const strippedHtml = stripSanctionedBoundaryRegions(html);
  const strippedDecodedHtml = decodeHtmlEntities(strippedHtml);
  const metadataText = collectMetadataText(html);
  const attributeText = collectDecodedAttributeText(strippedHtml);
  const pageText = normalizeRenderedText(extractMainHtml(html));
  const strippedPageText = normalizeRenderedText(extractMainHtml(strippedHtml));
  const wordCount = countRenderedWords(pageText);
  const positioningText = `${strippedHtml} ${strippedDecodedHtml} ${strippedPageText} ${attributeText} ${metadataText}`;
  const privateText = `${strippedHtml} ${strippedDecodedHtml} ${attributeText} ${metadataText} ${strippedPageText}`;

  for (const phrase of requiredText) {
    if (!pageText.includes(phrase)) {
      errors.push(`Team evidence handoff page is missing required text: ${phrase}`);
    }
  }

  for (const receiver of receiverPatterns) {
    if (!pageText.includes(receiver)) {
      errors.push(`Team evidence handoff page is missing receiver pattern: ${receiver}`);
    }
  }

  for (const route of neighboringRoutes) {
    if (!pageText.includes(route)) {
      errors.push(`Team evidence handoff page is missing neighboring route distinction: ${route}`);
    }
  }

  for (const link of teamEvidenceHandoffRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(`Team evidence handoff page is missing required link: ${link}`);
    }
  }

  validatePageMetadata(html, errors);
  validateInternalRouteLinks(html, routeContext, errors);

  if (wordCount < 400 || wordCount > 1500) {
    errors.push(`Team evidence handoff page word count must be between 400 and 1500 words, got ${wordCount}`);
  }

  validatePatterns({
    errors,
    patterns: forbiddenPositioningPatterns,
    text: positioningText,
    label: "forbidden AI/LLM positioning"
  });
  validatePatterns({
    errors,
    patterns: unsupportedOverclaimPatterns,
    text: positioningText,
    label: "unsupported overclaim wording"
  });
  validatePatterns({
    errors,
    patterns: privateTextPatterns,
    text: privateText,
    label: "forbidden private/raw material"
  });
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
          getAttribute(attributes, "href") === "https://tracemap.tools/team-evidence-handoff/"
      ),
      "canonical URL"
    ],
    [hasMeta(metaTags, { property: "og:type", content: "article" }), "Open Graph type"],
    [hasMeta(metaTags, { property: "og:title", content: "non-empty" }), "Open Graph title"],
    [hasMeta(metaTags, { property: "og:description", content: "non-empty" }), "Open Graph description"],
    [
      hasMeta(metaTags, {
        property: "og:url",
        content: "https://tracemap.tools/team-evidence-handoff/"
      }),
      "Open Graph URL"
    ]
  ];

  for (const [passed, label] of checks) {
    if (!passed) {
      errors.push(`Team evidence handoff page is missing required metadata: ${label}`);
    }
  }
}

function hasMeta(metaTags, expected) {
  return metaTags.some((attributes) => {
    if (expected.name && getAttribute(attributes, "name") !== expected.name) {
      return false;
    }

    if (expected.property && getAttribute(attributes, "property") !== expected.property) {
      return false;
    }

    const content = getAttribute(attributes, "content");
    return expected.content === "non-empty" ? Boolean(content?.trim()) : content === expected.content;
  });
}

function hasRel(attributes, expectedRel) {
  return (getAttribute(attributes, "rel") ?? "")
    .split(/\s+/)
    .some((rel) => rel.toLowerCase() === expectedRel.toLowerCase());
}

function validateInternalRouteLinks(html, { routes, sitemapRoutes }, errors) {
  for (const href of extractHrefs(extractMainHtml(html))) {
    if (!href.startsWith("/") || href.startsWith("//")) {
      continue;
    }

    const route = normalizeRouteHref(href);
    if (!routes.has(route) && !sitemapRoutes.has(route)) {
      errors.push(`Team evidence handoff page links to unresolved internal route: ${route}`);
    }
  }
}

function validatePatterns({ errors, patterns, text, label }) {
  for (const pattern of patterns) {
    if (pattern.test(text)) {
      errors.push(`Team evidence handoff page contains ${label}: ${pattern.source}`);
    }
  }
}

function stripSanctionedBoundaryRegions(html) {
  return html.replace(/<section\b(?=[^>]*\bdata-boundary-region\b)[^>]*>[\s\S]*?<\/section>/gi, "");
}

function collectMetadataText(html) {
  const values = [];
  for (const match of html.matchAll(/<meta\b([^>]*)>/gi)) {
    const content = getAttribute(match[1], "content");
    if (content) {
      values.push(content);
    }
  }

  const title = html.match(/<title>([\s\S]*?)<\/title>/i);
  if (title) {
    values.push(title[1]);
  }

  return decodeHtmlEntities(values.join(" "));
}

function collectDecodedAttributeText(html) {
  return decodeHtmlEntities([...html.matchAll(/\s[a-z:-]+\s*=\s*("[^"]*"|'[^']*')/gi)].map((match) => unquoteAttributeValue(match[1])).join(" "));
}

function extractMainHtml(html) {
  const main = html.match(/<main\b[^>]*>([\s\S]*?)<\/main>/i);
  return main ? main[1] : html;
}

function extractHrefs(html) {
  return findTagAttributes(html, "a")
    .map((attributes) => getAttribute(attributes, "href"))
    .filter((href) => typeof href === "string");
}

function normalizeRouteHref(href) {
  const path = href.split("#", 1)[0].split("?", 1)[0];
  if (path === "") {
    return "/";
  }

  if (/\.[a-z0-9]+$/i.test(path)) {
    return path;
  }

  return path.endsWith("/") ? path : `${path}/`;
}

function hasHref(html, href) {
  return extractHrefs(html).includes(href);
}

function getAttribute(attributes, name) {
  const match = attributes.match(new RegExp(`\\b${escapeRegExp(name)}\\s*=\\s*("[^"]*"|'[^']*')`, "i"));
  return match ? decodeHtmlEntities(unquoteAttributeValue(match[1])) : null;
}

function findTagAttributes(html, tagName) {
  return [...html.matchAll(new RegExp(`<${escapeRegExp(tagName)}\\b([^>]*)>`, "gi"))].map((match) => match[1]);
}

function unquoteAttributeValue(value) {
  return value.slice(1, -1);
}

function countRenderedWords(value) {
  return (value.match(/\b[\w'-]+\b/g) ?? []).length;
}
