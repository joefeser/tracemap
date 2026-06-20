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

const boundaryAllowedPrivateTextPatterns = [
  /\braw facts?\b/i,
  /\braw SQLite\b/i,
  /\banalyzer logs?\b/i,
  /\braw source snippets?\b/i,
  /\braw SQL\b/i,
  /\bconfig values?\b/i,
  /\braw remotes?\b/i,
  /\bgenerated scan directories\b/i,
  /\bprivate sample names?\b/i
];

const alwaysForbiddenPrivateTextPatterns = [
  /\/Users\//i,
  /\bfile:\/\//i,
  /\blocalhost\b/i,
  /\bconnection string\b/i,
  /\bpassword\s*=/i,
  /\bapi[_-]?key\b/i,
  /\bsecret\s*=/i,
  /\bsk-[A-Za-z0-9_-]{12,}\b/i
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
  const allAttributeText = collectDecodedAttributeText(html);
  const pageText = normalizeRenderedText(extractMainHtml(html));
  const strippedPageText = normalizeRenderedText(extractMainHtml(strippedHtml));
  const wordCount = countRenderedWords(pageText);
  const routeBoundaryText = collectRouteBoundaryText(routeContext.routeEntry);
  const routeHardPrivateText = collectRouteHardPrivateText(routeContext.routeEntry);
  const positioningText = `${strippedHtml} ${strippedDecodedHtml} ${strippedPageText} ${attributeText} ${metadataText} ${routeBoundaryText}`;
  const boundaryAllowedPrivateText = `${strippedHtml} ${strippedDecodedHtml} ${attributeText} ${metadataText} ${strippedPageText} ${routeBoundaryText}`;
  const alwaysForbiddenPrivateText = `${html} ${decodedHtml} ${allAttributeText} ${metadataText} ${pageText} ${routeBoundaryText} ${routeHardPrivateText}`;

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
    patterns: boundaryAllowedPrivateTextPatterns,
    text: boundaryAllowedPrivateText,
    label: "forbidden private/raw material"
  });
  validatePatterns({
    errors,
    patterns: alwaysForbiddenPrivateTextPatterns,
    text: alwaysForbiddenPrivateText,
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
  const removals = [];
  let from = 0;

  while (from < html.length) {
    const opening = findNextTag(html, "section", from);
    if (!opening) {
      break;
    }

    from = opening.end;
    if (!/\bdata-boundary-region\b/i.test(opening.attributes)) {
      continue;
    }

    const end = findMatchingClosingTag(html, "section", opening.end);
    if (end === -1) {
      continue;
    }

    removals.push([opening.start, end]);
    from = end;
  }

  return removeRanges(html, removals);
}

function collectMetadataText(html) {
  const values = [];
  for (const attributes of findTagAttributes(html, "meta")) {
    const content = getAttribute(attributes, "content");
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
  return decodeHtmlEntities(
    findAllTagAttributes(html)
      .flatMap((attributes) => [...attributes.matchAll(/\s[a-z:-]+\s*=\s*("[^"]*"|'[^']*')/gi)])
      .map((match) => unquoteAttributeValue(match[1]))
      .join(" ")
  );
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
  const tags = [];
  let from = 0;

  while (from < html.length) {
    const tag = findNextTag(html, tagName, from);
    if (!tag) {
      break;
    }

    tags.push(tag.attributes);
    from = tag.end;
  }

  return tags;
}

function findAllTagAttributes(html) {
  const tags = [];
  let from = 0;

  while (from < html.length) {
    const start = html.indexOf("<", from);
    if (start === -1) {
      break;
    }

    const end = findTagEnd(html, start);
    if (end === -1) {
      break;
    }

    const raw = html.slice(start + 1, end);
    if (!raw.startsWith("/") && !raw.startsWith("!") && !raw.startsWith("?")) {
      const name = raw.match(/^([a-z][a-z0-9:-]*)\b/i);
      if (name) {
        tags.push(raw.slice(name[0].length));
      }
    }

    from = end + 1;
  }

  return tags;
}

function findNextTag(html, tagName, from) {
  const pattern = new RegExp(`<${escapeRegExp(tagName)}\\b`, "gi");
  pattern.lastIndex = from;

  while (true) {
    const match = pattern.exec(html);
    if (!match) {
      return null;
    }

    const end = findTagEnd(html, match.index);
    if (end === -1) {
      return null;
    }

    return {
      start: match.index,
      end: end + 1,
      attributes: html.slice(match.index + match[0].length, end)
    };
  }
}

function findMatchingClosingTag(html, tagName, from) {
  const tagPattern = new RegExp(`<\\/?${escapeRegExp(tagName)}\\b`, "gi");
  tagPattern.lastIndex = from;
  let depth = 1;

  while (true) {
    const match = tagPattern.exec(html);
    if (!match) {
      return -1;
    }

    const end = findTagEnd(html, match.index);
    if (end === -1) {
      return -1;
    }

    if (html[match.index + 1] === "/") {
      depth -= 1;
      if (depth === 0) {
        return end + 1;
      }
    } else {
      depth += 1;
    }

    tagPattern.lastIndex = end + 1;
  }
}

function findTagEnd(html, start) {
  let quote = null;

  for (let index = start + 1; index < html.length; index += 1) {
    const char = html[index];
    if (quote) {
      if (char === quote) {
        quote = null;
      }
      continue;
    }

    if (char === "\"" || char === "'") {
      quote = char;
      continue;
    }

    if (char === ">") {
      return index;
    }
  }

  return -1;
}

function removeRanges(value, ranges) {
  let result = "";
  let cursor = 0;

  for (const [start, end] of ranges) {
    result += value.slice(cursor, start);
    cursor = end;
  }

  return result + value.slice(cursor);
}

function collectRouteBoundaryText(routeEntry) {
  if (!routeEntry) {
    return "";
  }

  return [
    routeEntry.title,
    routeEntry.summary,
    ...(Array.isArray(routeEntry.limitations) ? routeEntry.limitations : [])
  ]
    .filter((value) => typeof value === "string")
    .join(" ");
}

function collectRouteHardPrivateText(routeEntry) {
  if (!routeEntry) {
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

function unquoteAttributeValue(value) {
  return value.slice(1, -1);
}

function countRenderedWords(value) {
  return (value.match(/\b[\w'-]+\b/g) ?? []).length;
}
