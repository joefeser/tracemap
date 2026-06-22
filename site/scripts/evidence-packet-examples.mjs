import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import {
  decodeHtmlEntities,
  escapeRegExp,
  fileExists,
  normalizeRenderedText,
  readSitemapLocSet
} from "./validate-utils.mjs";

export const evidencePacketExamplesRoute = "/packets/examples/";
export const evidencePacketExamplesRequiredLinks = [
  "/packets/",
  "/packets/assembly/",
  "/examples/scan-packet/",
  "/demo/result/",
  "/proof-source-catalog/",
  "/review-claim-checklist/"
];
export const evidencePacketExamplesInboundRoutes = ["/packets/", "/packets/assembly/"];

const pageArtifact = "packets/examples/index.html";
const sitemapArtifact = "sitemap.xml";
const routesIndexArtifact = "routes-index.json";

const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "synthetic public-safe example",
  "Paths, spans, commits, extractor versions, and validation notes are placeholders",
  "This gallery shows shapes; adjacent pages keep their own jobs"
];

const requiredCategories = [
  "demo-backed packet",
  "reduced-coverage packet",
  "gap-labeled packet",
  "stop-condition packet"
];

const requiredFields = [
  "claim label",
  "public claim level",
  "proof path",
  "rule ID or family",
  "evidence tier",
  "coverage label",
  "synthetic public-safe path/span",
  "commit or extractor placeholder",
  "limitation",
  "non-claim",
  "next owner",
  "validation evidence"
];

const allowedEvidenceTiers = new Set(["Tier1Semantic", "Tier2Structural", "Tier3SyntaxOrTextual", "Tier4Unknown"]);
const requiredCoverageLabels = ["demo-backed shape", "reduced coverage", "gap-labeled", "stopped"];
const requiredPlaceholders = [
  "examples/public-demo/Controllers/OrdersController.cs:42-58",
  "examples/public-demo/Contracts/OrderDto.cs:12-24",
  "examples/public-demo/Project/Orders.Api.csproj:1-1",
  "examples/public-demo/Review/claim-boundary.md:7-11",
  "commit: demo-sha-placeholder",
  "extractor: tracemap-demo-extractor@x.y.z"
];

const metadataNonClaimTerms = [
  "runtime behavior",
  "production traffic",
  "endpoint performance",
  "outage cause",
  "release approval",
  "release safety",
  "operational safety",
  "complete coverage",
  "AI impact analysis",
  "LLM analysis",
  "autonomous approval",
  "autonomous review",
  "replacement of human review"
];

const forbiddenClaimPatterns = [
  /\b(?:TraceMap\s+)?(?:proves?|proven|proof of)\s+(?:runtime behavior|production traffic|endpoint performance|outage cause|release approval|release safety|operational safety|complete coverage|absence-of-impact)\b/i,
  /\b(?:monitors?|knows?)\s+production traffic\b/i,
  /\bmeasures?\s+endpoint performance\b/i,
  /\bidentifies?\s+outage cause\b/i,
  /\bgrants?\s+release approval\b/i,
  /\bprovides?\s+operational safety\b/i,
  /\bperforms?\s+(?:AI impact analysis|LLM analysis)\b/i,
  /\bconducts?\s+autonomous review\b/i,
  /\breplaces?\s+(?:human review|source review|ownership decisions|telemetry|logs|traces|APM|tests|release controls|incident response|manager judgment|service ownership|database ownership)\b/i,
  /\b(?:route|endpoint|dependency|package|SQL-facing reference|DTO|system|release|team)\s+is\s+(?:impacted|safe|unsafe|approved|blocked|root cause|production proven|validated for release)\b/i
];

const rawMaterialPatterns = [
  /\braw facts?\b/i,
  /\braw SQLite(?: content)?\b/i,
  /\banalyzer logs?\b/i,
  /\bsource snippets?\b/i,
  /\braw source snippets?\b/i,
  /\braw SQL\b/i,
  /\bconfig values?\b/i,
  /\braw remotes?\b/i,
  /\bgenerated scan directories\b/i,
  /\braw command output\b/i,
  /\bhidden validation details\b/i,
  /\bcredential-like values\b/i
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
  /\bsk-[A-Za-z0-9_-]{12,}\b/i,
  /\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b/
];

const blameLanguagePatterns = [
  /\bbad code\b/i,
  /\bvendor fault\b/i,
  /\bconsultant fault\b/i,
  /\bteam fault\b/i,
  /\bmaintainer fault\b/i,
  /\bauthor fault\b/i
];

const sanctionedBoundarySectionPattern =
  /<section\b(?=[^>]*\bdata-packet-example-boundary\b)[^>]*>[\s\S]*?<\/section>/gi;

export async function validateEvidencePacketExamplesDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl, localErrors);
  const pagePath = resolve(dist, "packets", "examples", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence("Evidence packet examples page is missing required public route: /packets/examples/", pageArtifact));
    errors.push(...localErrors);
    return;
  }

  if (cleanBaseUrl) {
    await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  }

  const routeContext = await readRouteContext({ dist, errors: localErrors });
  await validateEvidencePacketExamplesPage({ pagePath, routeContext, errors: localErrors });
  await validateInboundLinks({ dist, errors: localErrors });

  errors.push(...localErrors);
}

function normalizeBaseUrl(value, errors) {
  let url;

  try {
    url = new URL(value);
  } catch {
    errors.push(withEvidence(`Evidence packet examples baseUrl must be a valid absolute URL: ${String(value)}`, "baseUrl input"));
    return null;
  }

  if (url.protocol !== "https:" && url.protocol !== "http:") {
    errors.push(withEvidence(`Evidence packet examples baseUrl must use http or https: ${String(value)}`, "baseUrl input"));
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
  if (!sitemapUrls.has(`${baseUrl}${evidencePacketExamplesRoute}`)) {
    errors.push(withEvidence(`Evidence packet examples sitemap is missing required route: ${baseUrl}${evidencePacketExamplesRoute}`, sitemapArtifact));
  }
}

async function readRouteContext({ dist, errors }) {
  const indexPath = resolve(dist, "routes-index.json");
  const sitemapPath = resolve(dist, "sitemap.xml");
  const routes = new Set();
  const sitemapRoutes = new Set();
  let routeEntry = null;

  if (await fileExists(sitemapPath)) {
    for (const loc of await readSitemapLocSet(sitemapPath)) {
      try {
        sitemapRoutes.add(normalizeRouteHref(new URL(loc).pathname));
      } catch {
        // The aggregate validator reports malformed sitemap URLs separately.
      }
    }
  }

  if (!(await fileExists(indexPath))) {
    return { routeEntry, routes, sitemapRoutes };
  }

  let parsed;
  try {
    parsed = JSON.parse(await readFile(indexPath, "utf8"));
  } catch (error) {
    errors.push(withEvidence(`Evidence packet examples could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return { routeEntry, routes, sitemapRoutes };
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Evidence packet examples routes-index.json is invalid: expected entries array", routesIndexArtifact));
    return { routeEntry, routes, sitemapRoutes };
  }

  for (const entry of parsed.entries) {
    if (typeof entry?.path === "string") {
      routes.add(normalizeRouteHref(entry.path));
    }
  }

  routeEntry = parsed.entries.find((entry) => normalizeRouteHref(entry?.path ?? "") === evidencePacketExamplesRoute) ?? null;
  validateRouteEntry(routeEntry, errors);

  return { routeEntry, routes, sitemapRoutes };
}

function validateRouteEntry(routeEntry, errors) {
  if (!routeEntry) {
    errors.push(withEvidence(`Evidence packet examples routes-index.json is missing required route: ${evidencePacketExamplesRoute}`, routesIndexArtifact));
    return;
  }

  const expectedFields = {
    publicClaimLevel: "concept",
    hintCategory: "use-case",
    sourceType: "site-page",
    preferredProofPath: "/packets/"
  };

  for (const [field, expected] of Object.entries(expectedFields)) {
    if (routeEntry[field] !== expected) {
      errors.push(withEvidence(`Evidence packet examples routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
    }
  }

  if (!Array.isArray(routeEntry.limitations) || routeEntry.limitations.length === 0) {
    errors.push(withEvidence("Evidence packet examples routes-index.json must include limitations metadata.", routesIndexArtifact));
  }

  if (!Array.isArray(routeEntry.nonClaims) || routeEntry.nonClaims.length === 0) {
    errors.push(withEvidence("Evidence packet examples routes-index.json must include nonClaims metadata.", routesIndexArtifact));
    return;
  }

  const nonClaimsText = routeEntry.nonClaims.join(" ").toLowerCase();
  for (const term of metadataNonClaimTerms) {
    if (!nonClaimsText.includes(term.toLowerCase())) {
      errors.push(withEvidence(`Evidence packet examples routes-index.json nonClaims are missing required term: ${term}`, routesIndexArtifact));
    }
  }

  const publicMetadataText = [routeEntry.title, routeEntry.summary, ...(routeEntry.limitations ?? [])].join(" ");
  validateForbiddenClaims({ errors, text: publicMetadataText, label: "metadata", artifact: routesIndexArtifact });
  validateBlameLanguage({ errors, text: publicMetadataText, label: "metadata", artifact: routesIndexArtifact });
}

async function validateEvidencePacketExamplesPage({ pagePath, routeContext, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const mainHtml = extractMainHtml(html);
  const strippedHtml = stripSanctionedBoundaryRegions(html);
  const strippedMainHtml = extractMainHtml(strippedHtml);
  const pageText = normalizeRenderedText(mainHtml);
  const lowerPageText = pageText.toLowerCase();
  const strippedText = normalizeRenderedText(strippedMainHtml);
  const strippedTextTight = normalizeTightHtmlText(strippedMainHtml);
  const metadataText = collectMetadataText(html);
  const attributeText = collectDecodedAttributeText(strippedHtml);
  const allAttributeText = collectDecodedAttributeText(html);
  const allTextTight = normalizeTightHtmlText(html);
  const wordCount = countWords(pageText);

  for (const phrase of requiredText) {
    if (!pageText.includes(phrase)) {
      errors.push(withEvidence(`Evidence packet examples page is missing required text: ${phrase}`, pageArtifact));
    }
  }

  for (const category of requiredCategories) {
    if (!lowerPageText.includes(category)) {
      errors.push(withEvidence(`Evidence packet examples page is missing required category: ${category}`, pageArtifact));
    }
    if (!hasExampleCategory(html, category)) {
      errors.push(withEvidence(`Evidence packet examples page is missing category marker: ${category}`, pageArtifact));
    }
  }

  for (const field of requiredFields) {
    if (!lowerPageText.includes(field.toLowerCase())) {
      errors.push(withEvidence(`Evidence packet examples page is missing required field: ${field}`, pageArtifact));
    }
    if (!hasExampleField(html, field)) {
      errors.push(withEvidence(`Evidence packet examples page is missing field row: ${field}`, pageArtifact));
    }
  }

  for (const label of requiredCoverageLabels) {
    if (!lowerPageText.includes(label)) {
      errors.push(withEvidence(`Evidence packet examples page is missing coverage label: ${label}`, pageArtifact));
    }
  }

  for (const placeholder of requiredPlaceholders) {
    if (!pageText.includes(placeholder)) {
      errors.push(withEvidence(`Evidence packet examples page is missing public-safe placeholder: ${placeholder}`, pageArtifact));
    }
  }

  const tiers = [...pageText.matchAll(/\bTier(?:1Semantic|2Structural|3SyntaxOrTextual|4Unknown)\b/g)].map((match) => match[0]);
  if (tiers.length < 4) {
    errors.push(withEvidence("Evidence packet examples page must include an evidence tier for every example.", pageArtifact));
  }
  for (const tier of tiers) {
    if (!allowedEvidenceTiers.has(tier)) {
      errors.push(withEvidence(`Evidence packet examples page uses unsupported evidence tier: ${tier}`, pageArtifact));
    }
  }

  if (!/blocked:\s+missing public-safe proof trail/i.test(pageText)) {
    errors.push(withEvidence("Evidence packet examples stop-condition packet must include a blocked proof-path marker naming missing public-safe evidence.", pageArtifact));
  }

  if ((pageText.match(/synthetic public-safe example/g) ?? []).length < 4) {
    errors.push(withEvidence("Evidence packet examples page must visibly label non-demo-backed examples as synthetic public-safe examples.", pageArtifact));
  }

  for (const link of evidencePacketExamplesRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Evidence packet examples page is missing required link: ${link}`, pageArtifact));
    }
  }

  validatePageMetadata(html, errors);
  validateInternalRouteLinks(html, routeContext, errors);

  if (wordCount < 450 || wordCount > 1300) {
    errors.push(withEvidence(`Evidence packet examples page word count must be between 450 and 1300 words, got ${wordCount}`, pageArtifact));
  }

  validateForbiddenClaims({
    errors,
    text: `${strippedText} ${strippedTextTight} ${metadataText} ${attributeText}`,
    label: "page copy outside sanctioned boundary regions",
    artifact: pageArtifact
  });
  validateRawMaterial({
    errors,
    text: `${normalizeRenderedText(strippedHtml)} ${normalizeTightHtmlText(strippedHtml)} ${metadataText} ${attributeText}`,
    label: "outside sanctioned boundary regions",
    artifact: pageArtifact
  });
  validateHardPrivateMaterial({
    errors,
    text: `${html} ${decodedHtml} ${pageText} ${allTextTight} ${metadataText} ${allAttributeText} ${JSON.stringify(routeContext.routeEntry ?? {})}`,
    label: "page, attributes, or metadata",
    artifact: pageArtifact
  });
  validateBlameLanguage({
    errors,
    text: `${pageText} ${metadataText} ${allAttributeText}`,
    label: "page, attributes, or metadata",
    artifact: pageArtifact
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
          getAttribute(attributes, "href") === "https://tracemap.tools/packets/examples/"
      ),
      "canonical URL"
    ],
    [hasMeta(metaTags, { property: "og:type", content: "article" }), "Open Graph type"],
    [hasMeta(metaTags, { property: "og:title", content: "non-empty" }), "Open Graph title"],
    [hasMeta(metaTags, { property: "og:description", content: "non-empty" }), "Open Graph description"],
    [
      hasMeta(metaTags, {
        property: "og:url",
        content: "https://tracemap.tools/packets/examples/"
      }),
      "Open Graph URL"
    ]
  ];

  for (const [passed, label] of checks) {
    if (!passed) {
      errors.push(withEvidence(`Evidence packet examples page is missing required metadata: ${label}`, pageArtifact));
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
  if (routes.size === 0 && sitemapRoutes.size === 0) {
    return;
  }

  for (const href of extractHrefs(extractMainHtml(html))) {
    if (!href.startsWith("/") || href.startsWith("//")) {
      continue;
    }

    const route = normalizeRouteHref(href);
    if (!routes.has(route) && !sitemapRoutes.has(route)) {
      errors.push(withEvidence(`Evidence packet examples page links to unresolved internal route: ${route}`, pageArtifact));
    }
  }
}

async function validateInboundLinks({ dist, errors }) {
  const missing = [];

  for (const route of evidencePacketExamplesInboundRoutes) {
    const pagePath = resolve(dist, route.replace(/^\/|\/$/g, ""), "index.html");
    if (!(await fileExists(pagePath))) {
      continue;
    }

    const html = await readFile(pagePath, "utf8");
    if (!hasHref(html, evidencePacketExamplesRoute)) {
      missing.push(route);
    }
  }

  if (missing.length > 0) {
    errors.push(withEvidence(`Evidence packet examples are missing inbound links from live adjacent routes: ${missing.join(", ")}`, "adjacent route HTML"));
  }
}

function validateForbiddenClaims({ errors, text, label, artifact }) {
  for (const pattern of forbiddenClaimPatterns) {
    const flags = pattern.flags.includes("g") ? pattern.flags : `${pattern.flags}g`;
    const globalPattern = new RegExp(pattern.source, flags);
    for (const match of text.matchAll(globalPattern)) {
      if (!hasNegatedContext(text, match.index)) {
        errors.push(withEvidence(`Evidence packet examples contain forbidden public claim in ${label}: ${match[0]}`, artifact));
        break;
      }
    }
  }
}

function validateRawMaterial({ errors, text, label, artifact }) {
  for (const pattern of rawMaterialPatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Evidence packet examples contain forbidden raw/private material ${label}: ${pattern.source}`, artifact));
    }
  }
}

function validateHardPrivateMaterial({ errors, text, label, artifact }) {
  for (const pattern of hardPrivatePatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Evidence packet examples contain hard private material in ${label}: ${pattern.source}`, artifact));
    }
  }
}

function validateBlameLanguage({ errors, text, label, artifact }) {
  for (const pattern of blameLanguagePatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Evidence packet examples contain blame language in ${label}: ${pattern.source}`, artifact));
    }
  }
}

function hasNegatedContext(value, index) {
  const prefix = value.slice(Math.max(0, index - 64), index).toLowerCase();
  return /(?:not|no|without|cannot|can't|does not|do not|never)\s+(?:a\s+)?(?:new\s+)?$/.test(prefix);
}

function hasExampleField(html, field) {
  const escaped = escapeRegExp(field);
  return new RegExp(`<tr\\b(?=[^>]*\\bdata-packet-example-field\\s*=\\s*["']${escaped}["'])`, "i").test(html);
}

function hasExampleCategory(html, category) {
  const escaped = escapeRegExp(category);
  return new RegExp(`\\bdata-example-category\\s*=\\s*["']${escaped}["']`, "i").test(html);
}

function stripSanctionedBoundaryRegions(html) {
  return html.replace(sanctionedBoundarySectionPattern, " ");
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

function normalizeTightHtmlText(html) {
  return normalizeScanText(decodeHtmlEntities(stripTagsTight(html)));
}

function normalizeScanText(value) {
  return String(value)
    .normalize("NFKC")
    .replace(/\p{Cf}/gu, "")
    .replace(/\s+/g, " ")
    .trim();
}

function stripTagsTight(html) {
  let text = "";
  let insideTag = false;
  let quote = "";

  for (let index = 0; index < html.length; index += 1) {
    const char = html[index];

    if (insideTag) {
      if (quote) {
        if (char === quote) {
          quote = "";
        }
        continue;
      }

      if (char === "\"" || char === "'") {
        quote = char;
        continue;
      }

      if (char === ">") {
        insideTag = false;
      }
      continue;
    }

    if (char === "<") {
      insideTag = true;
      continue;
    }

    text += char;
  }

  return text;
}

function findAllTagAttributes(html) {
  return [...html.matchAll(/<([a-z][a-z0-9:-]*)\b([^>]*)>/gi)].map((match) => match[2] ?? "");
}

function findTagAttributes(html, tagName) {
  const escaped = escapeRegExp(tagName);
  return [...html.matchAll(new RegExp(`<${escaped}\\b([^>]*)>`, "gi"))].map((match) => match[1] ?? "");
}

function getAttribute(attributes, name) {
  const escaped = escapeRegExp(name);
  const match = attributes.match(new RegExp(`\\s${escaped}\\s*=\\s*("[^"]*"|'[^']*'|[^\\s>]+)`, "i"));
  if (!match) {
    return null;
  }

  return decodeHtmlEntities(unquoteAttributeValue(match[1]));
}

function unquoteAttributeValue(value) {
  if ((value.startsWith("\"") && value.endsWith("\"")) || (value.startsWith("'") && value.endsWith("'"))) {
    return value.slice(1, -1);
  }

  return value;
}

function extractMainHtml(html) {
  return html.match(/<main\b[^>]*>([\s\S]*?)<\/main>/i)?.[1] ?? html;
}

function extractHrefs(html) {
  return findTagAttributes(html, "a")
    .map((attributes) => getAttribute(attributes, "href"))
    .filter(Boolean);
}

function hasHref(html, expectedHref) {
  const normalizedExpected = normalizeRouteHref(expectedHref);
  return extractHrefs(html).some((href) => normalizeRouteHref(href) === normalizedExpected);
}

function normalizeRouteHref(href) {
  if (typeof href !== "string" || href.trim() === "") {
    return "";
  }

  const cleanHref = href.trim().split("#")[0].split("?")[0];
  if (cleanHref === "") {
    return "";
  }

  if (cleanHref === "/") {
    return "/";
  }

  return `/${cleanHref.replace(/^\/+|\/+$/g, "")}/`;
}

function countWords(text) {
  return (text.match(/\b[\w'-]+\b/g) ?? []).length;
}

function withEvidence(message, artifact) {
  return `${message} Evidence: ${artifact}.`;
}
