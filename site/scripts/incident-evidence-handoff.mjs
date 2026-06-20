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

export const incidentEvidenceHandoffRoute = "/incident-evidence-handoff/";
export const incidentEvidenceHandoffRequiredLinks = [
  "/proof-paths/",
  "/validation/",
  "/limitations/",
  "/demo/result/",
  "/incident-call/",
  "/static-triage/",
  "/review-room/",
  "/manager-faq/",
  "/packets/",
  "/manager-packet/",
  "/manager-brief/",
  "/use-cases/incident-review/",
  "/docs/"
];

const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "Incident evidence handoff is the packet of static evidence, proof paths, limits, and next owners; it is not runtime proof or incident command.",
  "Static triage frames the question; the incident evidence handoff packet carries the already-framed evidence, proof paths, limits, and next owners into the next conversation.",
  "static evidence",
  "proof path",
  "rule ID/evidence tier",
  "coverage label",
  "limitation",
  "next owner"
];

const ownershipRows = [
  "route existence",
  "DTO shape",
  "package reference",
  "dependency edge",
  "SQL-facing reference",
  "telemetry",
  "logs",
  "traces",
  "APM",
  "release controls",
  "tests",
  "database ownership",
  "service ownership",
  "incident command"
];

const positioningDenylist = [
  "proves runtime behavior",
  "proves production traffic",
  "endpoint performance proof",
  "proves outage cause",
  "proves release safety",
  "proves operational safety",
  "AI-powered",
  "LLM-powered",
  "AI impact analysis engine",
  "LLM impact analysis engine",
  "complete product coverage",
  "production dependency understanding",
  "replaces telemetry",
  "replaces logs",
  "replaces traces",
  "replaces APM",
  "replaces incident command",
  "replaces incident response",
  "replaces ownership",
  "replaces ownership review",
  "replaces tests",
  "replaces release controls",
  "replaces service-owner judgment",
  "replaces database-owner judgment",
  "replaces source review"
];

const privateArtifactDenylist = [
  "raw fact stream",
  "raw SQLite",
  "analyzer log",
  "raw source snippet",
  "raw SQL",
  "raw config value",
  "credential secret",
  "local absolute path",
  "raw remote",
  "generated scan directory",
  "private sample name",
  "connection string",
  "credential"
];

export async function validateIncidentEvidenceHandoffDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors
}) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "incident-evidence-handoff", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push("Incident evidence handoff page is missing required public route: /incident-evidence-handoff/");
    errors.push(...localErrors);
    return;
  }

  await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  const routeContext = await readRouteContext({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  await validateIncidentEvidenceHandoffPage({ pagePath, routeContext, errors: localErrors });

  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${incidentEvidenceHandoffRoute}`)) {
    errors.push(`Incident evidence handoff sitemap is missing required route: ${baseUrl}${incidentEvidenceHandoffRoute}`);
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
        errors.push("Incident evidence handoff routes-index.json is invalid: expected entries array");
      } else {
        for (const entry of parsed.entries) {
          if (typeof entry?.path === "string") {
            routes.add(entry.path);
          }
        }
        routeEntry = parsed.entries.find((entry) => entry?.path === incidentEvidenceHandoffRoute) ?? null;
      }
    } catch (error) {
      errors.push(`Incident evidence handoff could not parse routes-index.json: ${error.message}`);
    }
  }

  const sitemapRoutes = new Set();
  if (await fileExists(sitemapPath)) {
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
    errors.push(`Incident evidence handoff routes-index.json is missing required route: ${incidentEvidenceHandoffRoute}`);
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
        `Incident evidence handoff routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`
      );
    }
  }

  if (!Array.isArray(routeEntry.limitations) || routeEntry.limitations.length === 0) {
    errors.push("Incident evidence handoff routes-index.json must include limitations metadata.");
  }

  if (!Array.isArray(routeEntry.nonClaims) || routeEntry.nonClaims.length === 0) {
    errors.push("Incident evidence handoff routes-index.json must include nonClaims metadata.");
  }
}

async function validateIncidentEvidenceHandoffPage({ pagePath, routeContext, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const metadataText = collectMetadataText(html);
  const attributeText = collectDecodedAttributeText(html);
  const pageText = normalizeRenderedText(extractMainHtml(html));
  const wordCount = countRenderedWords(pageText);
  const positioningText = `${pageText} ${attributeText} ${metadataText}`;
  const privateText = `${html} ${decodedHtml} ${attributeText} ${metadataText} ${pageText}`;

  for (const phrase of requiredText) {
    if (!pageText.includes(phrase)) {
      errors.push(`Incident evidence handoff page is missing required text: ${phrase}`);
    }
  }

  for (const row of ownershipRows) {
    if (!pageText.includes(row)) {
      errors.push(`Incident evidence handoff page is missing required ownership row: ${row}`);
    }
  }

  for (const link of incidentEvidenceHandoffRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(`Incident evidence handoff page is missing required link: ${link}`);
    }
  }

  validatePageMetadata(html, errors);
  validateInternalRouteLinks(html, routeContext, errors);

  // Reproducible word count: only the <main> subtree is normalized as visible body text.
  // This excludes head metadata, the canonical top navigation, and the global footer.
  if (wordCount < 400 || wordCount > 1800) {
    errors.push(`Incident evidence handoff page word count must be between 400 and 1800 words, got ${wordCount}`);
  }

  validateDenylist({
    errors,
    phrases: positioningDenylist,
    text: `${positioningText} ${collectRouteMetadataText(routeContext.routeEntry)}`,
    label: "forbidden runtime/AI positioning"
  });
  validateDenylist({
    errors,
    phrases: privateArtifactDenylist,
    text: `${privateText} ${collectRouteMetadataText(routeContext.routeEntry)}`,
    label: "forbidden private/raw artifact text"
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
          getAttribute(attributes, "href") === "https://tracemap.tools/incident-evidence-handoff/"
      ),
      "canonical URL"
    ],
    [hasMeta(metaTags, { property: "og:type", content: "article" }), "Open Graph type"],
    [hasMeta(metaTags, { property: "og:title", content: "non-empty" }), "Open Graph title"],
    [hasMeta(metaTags, { property: "og:description", content: "non-empty" }), "Open Graph description"],
    [
      hasMeta(metaTags, {
        property: "og:url",
        content: "https://tracemap.tools/incident-evidence-handoff/"
      }),
      "Open Graph URL"
    ]
  ];

  for (const [passed, label] of checks) {
    if (!passed) {
      errors.push(`Incident evidence handoff page is missing required metadata: ${label}`);
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
      errors.push(`Incident evidence handoff page links to unresolved internal route: ${route}`);
    }
  }
}

function validateDenylist({ errors, phrases, text, label }) {
  const haystack = text.toLowerCase();

  for (const phrase of phrases) {
    if (haystack.includes(phrase.toLowerCase())) {
      errors.push(`Incident evidence handoff page contains ${label}: ${phrase}`);
    }
  }
}

function collectRouteMetadataText(routeEntry) {
  if (!routeEntry) {
    return "";
  }

  const values = [];
  collectStringValues(routeEntry, values);
  return values.join(" ");
}

function collectStringValues(value, values) {
  if (typeof value === "string") {
    values.push(value);
    return;
  }

  if (Array.isArray(value)) {
    for (const item of value) {
      collectStringValues(item, values);
    }
    return;
  }

  if (value && typeof value === "object") {
    for (const item of Object.values(value)) {
      collectStringValues(item, values);
    }
  }
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
