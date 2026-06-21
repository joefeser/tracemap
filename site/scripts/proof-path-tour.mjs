import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import {
  decodeHtmlEntities,
  escapeRegExp,
  fileExists,
  normalizeRenderedText,
  readSitemapLocSet
} from "./validate-utils.mjs";

export const proofPathTourRoute = "/proof-paths/tour/";
export const proofPathTourRequiredLinks = [
  "/proof-paths/",
  "/proof-source-catalog/",
  "/demo/evidence-trail/",
  "/review-room/",
  "/packets/",
  "/packets/assembly/",
  "/validation/",
  "/limitations/",
  "/demo/runbook/",
  "/review-claim-checklist/",
  "/glossary/"
];
export const proofPathTourInboundRoutes = ["/proof-paths/"];

const pageArtifact = "proof-paths/tour/index.html";
const sitemapArtifact = "sitemap.xml";
const routesIndexArtifact = "routes-index.json";

const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "This is a guided explanation",
  "not a proof engine",
  "not a real product claim",
  "Bounded non-claim conclusion"
];

const requiredAnchors = [
  "claim-label",
  "public-claim-level",
  "proof-path",
  "rule-id-family",
  "evidence-tier",
  "coverage-label",
  "commit-source-context",
  "extractor-version",
  "supporting-public-route-artifact",
  "limitation",
  "step-non-claim",
  "next-owner",
  "where-to-stop",
  "non-claims"
];

const requiredStepFields = [
  "claim label",
  "public claim level",
  "proof path",
  "rule ID/family",
  "evidence tier",
  "coverage label",
  "commit SHA",
  "extractor version",
  "supporting public route/artifact",
  "limitation",
  "non-claim",
  "next owner"
];

const requiredEvidenceTiers = ["Tier1Semantic", "Tier2Structural", "Tier3SyntaxOrTextual", "Tier4Unknown"];
const requiredCoverageTerms = ["full", "partial", "reduced", "unknown", "gap-labeled"];

const requiredStopFields = [
  "rule ID/family",
  "evidence tier",
  "coverage label",
  "commit/source context",
  "extractor version",
  "limitation",
  "supporting public route/artifact"
];

const metadataNonClaimTerms = [
  "runtime behavior",
  "production traffic",
  "endpoint performance",
  "outage cause",
  "release safety",
  "operational safety",
  "release approval",
  "complete coverage",
  "AI impact analysis",
  "LLM analysis",
  "embeddings",
  "vector databases",
  "prompt classification",
  "autonomous approval"
];

const rawMaterialPatterns = [
  /\bfacts\.ndjson\b/i,
  /\bindex\.sqlite\b/i,
  /\bscan-manifest\.json\b/i,
  /\breport\.md\b/i,
  /\blogs\/analyzer\.log\b/i,
  /\braw facts?\b/i,
  /\braw SQLite(?: content)?\b/i,
  /\banalyzer logs?\b/i,
  /\braw source snippets?\b/i,
  /\braw SQL\b/i,
  /\bconfig values?\b/i,
  /\bsecrets?\b/i,
  /\blocal paths?\b/i,
  /\braw remotes?\b/i,
  /\bgenerated scan directories\b/i,
  /\bprivate sample names?\b/i,
  /\bhidden validation details\b/i,
  /\braw command output\b/i,
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
  /\bsk-[A-Za-z0-9_-]{12,}\b/i
];

const forbiddenClaimPatterns = [
  /\b(?:TraceMap\s+)?(?:proves?|proven|proof of)\s+(?:runtime behavior|production traffic|endpoint performance|outage cause|release approval|release safety|operational safety|complete coverage|product behavior)\b/i,
  /\b(?:certifies?|guarantees?|verifies?)\s+(?:runtime behavior|production traffic|endpoint performance|release safety|operational safety|complete coverage|product behavior)\b/i,
  /\b(?:monitors?|knows?)\s+production traffic\b/i,
  /\bmeasures?\s+endpoint performance\b/i,
  /\bidentifies?\s+outage cause\b/i,
  /\bgrants?\s+release approval\b/i,
  /\bprovides?\s+operational safety\b/i,
  /\bperforms?\s+(?:AI impact analysis|LLM analysis)\b/i,
  /\buses?\s+(?:embeddings|vector databases|prompt classification)\b/i,
  /\bautonomously\s+approves?\b/i,
  /\breplaces?\s+(?:tests|code review|source review|runtime observability|human judgment|human review|telemetry|logs|traces|APM|release controls)\b/i
];

const sanctionedBoundarySectionPattern =
  /<section\b(?=[^>]*\bdata-tm-boundary\s*=\s*["'][^"']+["'])[^>]*>[\s\S]*?<\/section>/gi;

export async function validateProofPathTourDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl, localErrors);
  const pagePath = resolve(dist, "proof-paths", "tour", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence("Guided proof-path tour page is missing required public route: /proof-paths/tour/", pageArtifact));
    errors.push(...localErrors);
    return;
  }

  if (cleanBaseUrl) {
    await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  }

  const routeContext = await readRouteContext({ dist, errors: localErrors });
  await validateProofPathTourPage({ pagePath, routeContext, errors: localErrors });
  await validateInboundLinks({ dist, errors: localErrors });

  errors.push(...localErrors);
}

function normalizeBaseUrl(value, errors) {
  let url;

  try {
    url = new URL(value);
  } catch {
    errors.push(withEvidence(`Guided proof-path tour baseUrl must be a valid absolute URL: ${String(value)}`, "baseUrl input"));
    return null;
  }

  if (url.protocol !== "https:" && url.protocol !== "http:") {
    errors.push(withEvidence(`Guided proof-path tour baseUrl must use http or https: ${String(value)}`, "baseUrl input"));
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
  if (!sitemapUrls.has(`${baseUrl}${proofPathTourRoute}`)) {
    errors.push(withEvidence(`Guided proof-path tour sitemap is missing required route: ${baseUrl}${proofPathTourRoute}`, sitemapArtifact));
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
    errors.push(withEvidence(`Guided proof-path tour could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return { routeEntry, routes, sitemapRoutes };
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Guided proof-path tour routes-index.json is invalid: expected entries array", routesIndexArtifact));
    return { routeEntry, routes, sitemapRoutes };
  }

  for (const entry of parsed.entries) {
    if (typeof entry?.path === "string") {
      routes.add(normalizeRouteHref(entry.path));
    }
  }

  routeEntry = parsed.entries.find((entry) => normalizeRouteHref(entry?.path ?? "") === proofPathTourRoute) ?? null;
  validateRouteEntry(routeEntry, errors);

  return { routeEntry, routes, sitemapRoutes };
}

function validateRouteEntry(routeEntry, errors) {
  if (!routeEntry) {
    errors.push(withEvidence(`Guided proof-path tour routes-index.json is missing required route: ${proofPathTourRoute}`, routesIndexArtifact));
    return;
  }

  const expectedFields = {
    publicClaimLevel: "concept",
    hintCategory: "evidence",
    sourceType: "site-page",
    preferredProofPath: "/proof-paths/"
  };

  for (const [field, expected] of Object.entries(expectedFields)) {
    if (routeEntry[field] !== expected) {
      errors.push(withEvidence(`Guided proof-path tour routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
    }
  }

  if (!Array.isArray(routeEntry.limitations) || routeEntry.limitations.length === 0) {
    errors.push(withEvidence("Guided proof-path tour routes-index.json must include limitations metadata.", routesIndexArtifact));
  }

  if (!Array.isArray(routeEntry.nonClaims) || routeEntry.nonClaims.length === 0) {
    errors.push(withEvidence("Guided proof-path tour routes-index.json must include nonClaims metadata.", routesIndexArtifact));
    return;
  }

  const normalizedNonClaimsText = normalizeScanText(routeEntry.nonClaims.join(" ")).toLowerCase();
  for (const term of metadataNonClaimTerms) {
    if (!normalizedNonClaimsText.includes(term.toLowerCase())) {
      errors.push(withEvidence(`Guided proof-path tour routes-index.json nonClaims are missing required term: ${term}`, routesIndexArtifact));
    }
  }

  const publicMetadataText = [routeEntry.title, routeEntry.summary, ...(routeEntry.limitations ?? [])].join(" ");
  validateForbiddenClaims({ errors, text: publicMetadataText, label: "metadata" });
}

async function validateProofPathTourPage({ pagePath, routeContext, errors }) {
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
      errors.push(withEvidence(`Guided proof-path tour page is missing required text: ${phrase}`, pageArtifact));
    }
  }

  for (const anchor of requiredAnchors) {
    if (!hasId(html, anchor)) {
      errors.push(withEvidence(`Guided proof-path tour page is missing required anchor: #${anchor}`, pageArtifact));
    }
  }

  for (const field of requiredStepFields) {
    if (!lowerPageText.includes(field.toLowerCase())) {
      errors.push(withEvidence(`Guided proof-path tour page is missing required proof-step field: ${field}`, pageArtifact));
    }
    if (!hasProofStep(html, field)) {
      errors.push(withEvidence(`Guided proof-path tour page is missing proof-step marker: ${field}`, pageArtifact));
    }
  }

  for (const tier of requiredEvidenceTiers) {
    if (!pageText.includes(tier)) {
      errors.push(withEvidence(`Guided proof-path tour page is missing evidence tier: ${tier}`, pageArtifact));
    }
  }

  for (const term of requiredCoverageTerms) {
    if (!lowerPageText.includes(term)) {
      errors.push(withEvidence(`Guided proof-path tour page is missing coverage label term: ${term}`, pageArtifact));
    }
  }

  for (const field of requiredStopFields) {
    if (!lowerPageText.includes(field.toLowerCase())) {
      errors.push(withEvidence(`Guided proof-path tour where-to-stop copy is missing field: ${field}`, pageArtifact));
    }
  }

  for (const link of proofPathTourRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Guided proof-path tour page is missing required link: ${link}`, pageArtifact));
    }
  }

  validateWorkedExample(html, pageText, errors);
  validatePageMetadata(html, errors);
  validateInternalRouteLinks(html, routeContext, errors);

  if (wordCount < 650 || wordCount > 1600) {
    errors.push(withEvidence(`Guided proof-path tour page word count must be between 650 and 1600 words, got ${wordCount}`, pageArtifact));
  }

  validateForbiddenClaims({
    errors,
    text: `${strippedText} ${strippedTextTight} ${metadataText} ${attributeText}`,
    label: "page copy outside sanctioned boundary sections"
  });
  validateRawMaterial({
    errors,
    text: `${normalizeRenderedText(strippedHtml)} ${normalizeTightHtmlText(strippedHtml)} ${metadataText} ${attributeText}`,
    label: "outside sanctioned boundary sections"
  });
  validateHardPrivateMaterial({
    errors,
    text: `${html} ${decodedHtml} ${pageText} ${allTextTight} ${metadataText} ${allAttributeText} ${JSON.stringify(routeContext.routeEntry ?? {})}`,
    label: "page, attributes, or metadata"
  });
}

function validateWorkedExample(html, pageText, errors) {
  const example = html.match(/<section\b(?=[^>]*\bid\s*=\s*["']worked-example["'])([\s\S]*?)<\/section>/i);
  if (!example) {
    errors.push(withEvidence("Guided proof-path tour page is missing worked example section.", pageArtifact));
    return;
  }

  const exampleHtml = example[0];
  const exampleText = normalizeRenderedText(exampleHtml);

  if (!/data-worked-example\s*=\s*["']illustrative-not-real["']/i.test(exampleHtml)) {
    errors.push(withEvidence("Guided proof-path tour worked example must be visibly marked illustrative and not real.", pageArtifact));
  }

  if (!/illustrative/i.test(exampleText) || !/not a real product claim/i.test(exampleText)) {
    errors.push(withEvidence("Guided proof-path tour worked example is missing visible illustrative/not-real wording.", pageArtifact));
  }

  for (const field of [...requiredStepFields, "bounded non-claim conclusion"]) {
    if (!hasExampleField(exampleHtml, field)) {
      errors.push(withEvidence(`Guided proof-path tour worked example is missing required field traversal: ${field}`, pageArtifact));
    }
  }

  if (!pageText.includes("stop before repeating it as a real TraceMap finding")) {
    errors.push(withEvidence("Guided proof-path tour worked example is missing bounded non-claim conclusion.", pageArtifact));
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
          getAttribute(attributes, "href") === "https://tracemap.tools/proof-paths/tour/"
      ),
      "canonical URL"
    ],
    [hasMeta(metaTags, { property: "og:type", content: "article" }), "Open Graph type"],
    [hasMeta(metaTags, { property: "og:title", content: "non-empty" }), "Open Graph title"],
    [hasMeta(metaTags, { property: "og:description", content: "non-empty" }), "Open Graph description"],
    [
      hasMeta(metaTags, {
        property: "og:url",
        content: "https://tracemap.tools/proof-paths/tour/"
      }),
      "Open Graph URL"
    ]
  ];

  for (const [passed, label] of checks) {
    if (!passed) {
      errors.push(withEvidence(`Guided proof-path tour page is missing required metadata: ${label}`, pageArtifact));
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
      errors.push(withEvidence(`Guided proof-path tour page links to unresolved internal route: ${route}`, pageArtifact));
    }
  }
}

async function validateInboundLinks({ dist, errors }) {
  const missing = [];

  for (const route of proofPathTourInboundRoutes) {
    const pagePath = resolve(dist, route.replace(/^\/|\/$/g, ""), "index.html");
    if (!(await fileExists(pagePath))) {
      continue;
    }

    const html = await readFile(pagePath, "utf8");
    if (!hasHref(html, proofPathTourRoute)) {
      missing.push(route);
    }
  }

  if (missing.length > 0) {
    errors.push(withEvidence(`Guided proof-path tour is missing inbound links from live adjacent routes: ${missing.join(", ")}`, "adjacent route HTML"));
  }
}

function validateForbiddenClaims({ errors, text, label }) {
  for (const pattern of forbiddenClaimPatterns) {
    const flags = pattern.flags.includes("g") ? pattern.flags : `${pattern.flags}g`;
    const globalPattern = new RegExp(pattern.source, flags);
    for (const match of text.matchAll(globalPattern)) {
      if (!hasNegatedContext(text, match.index)) {
        errors.push(withEvidence(`Guided proof-path tour contains forbidden public claim in ${label}: ${match[0]}`, pageArtifact));
        break;
      }
    }
  }
}

function validateRawMaterial({ errors, text, label }) {
  for (const pattern of rawMaterialPatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Guided proof-path tour contains forbidden raw/private material ${label}: ${pattern.source}`, pageArtifact));
    }
  }
}

function validateHardPrivateMaterial({ errors, text, label }) {
  for (const pattern of hardPrivatePatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Guided proof-path tour contains hard private material in ${label}: ${pattern.source}`, pageArtifact));
    }
  }
}

function hasNegatedContext(value, index) {
  const prefix = value.slice(Math.max(0, index - 56), index).toLowerCase();
  return /(?:not|no|without|cannot|can't|does not|do not|never)\s+(?:a\s+)?(?:real\s+)?(?:new\s+)?$/.test(prefix);
}

function hasId(html, id) {
  const escaped = escapeRegExp(id);
  return new RegExp(`\\bid\\s*=\\s*["']${escaped}["']`, "i").test(html);
}

function hasProofStep(html, field) {
  const escaped = escapeRegExp(field);
  return new RegExp(`\\bdata-proof-tour-step\\s*=\\s*["']${escaped}["']`, "i").test(html);
}

function hasExampleField(html, field) {
  const escaped = escapeRegExp(field);
  return new RegExp(`\\bdata-proof-tour-example-field\\s*=\\s*["']${escaped}["']`, "i").test(html);
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
      if (html.startsWith("<!--", index)) {
        const end = html.indexOf("-->", index + 4);
        index = end === -1 ? html.length : end + 2;
        continue;
      }

      insideTag = true;
      quote = "";
      continue;
    }

    text += char;
  }

  return text;
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
  const normalizedHref = normalizeRouteHref(href);
  return extractHrefs(html).some((candidate) => normalizeRouteHref(candidate) === normalizedHref);
}

function getAttribute(attributes, name) {
  const match = attributes.match(new RegExp(`(?:^|\\s)${escapeRegExp(name)}\\s*=\\s*("[^"]*"|'[^']*')`, "i"));
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
  const match = pattern.exec(html);
  if (!match) {
    return null;
  }

  const end = findTagEnd(html, match.index);
  if (end === -1) {
    return null;
  }

  return {
    attributes: html.slice(match.index + match[0].length, end),
    end: end + 1,
    start: match.index
  };
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

function unquoteAttributeValue(value) {
  return value.slice(1, -1);
}

function countWords(value) {
  return (value.match(/\b[\w'-]+\b/g) ?? []).length;
}

function withEvidence(message, artifact) {
  return `${message} Evidence: ${artifact}.`;
}
